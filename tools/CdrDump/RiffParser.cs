using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace CdrDump;

/// <summary>Một nút trong cây chunk RIFF.</summary>
public sealed class Chunk
{
    public required string FourCC { get; init; }
    public string? ListType { get; init; }
    /// <summary>Offset của header chunk, tính trong bộ đệm <see cref="Source"/>.</summary>
    public long Offset { get; init; }
    public uint Size { get; init; }
    /// <summary>"file", "zip:&lt;entry&gt;", hoặc "cmpr@0x..." cho dữ liệu đã giải nén.</summary>
    public required string Source { get; init; }
    public List<Chunk> Children { get; } = new();
    public List<string> Notes { get; } = new();
    public byte[] Preview { get; init; } = Array.Empty<byte>();
    /// <summary>Nội dung thật của chunk lá (sau khi đi theo con trỏ, nếu có).</summary>
    public byte[] Content { get; init; } = Array.Empty<byte>();

    public string Label => ListType is null ? FourCC : $"{FourCC} '{ListType}'";
}

/// <summary>
/// Bộ đọc cây RIFF của CorelDRAW. Chỉ hiểu ở mức cấu trúc (chunk/list/cmpr),
/// KHÔNG diễn giải nội dung đối tượng. Những chỗ dựa trên giả định được đánh dấu "GIẢ ĐỊNH".
/// </summary>
public sealed class RiffParser
{
    private readonly int _previewBytes;
    private readonly IReadOnlyList<(string Name, byte[] Data)>? _dataFiles;

    /// <param name="dataFiles">
    /// Các file trong content/dataFileList.dat (định dạng ZIP có root.dat). Khi có, mỗi chunk lá 16 byte
    /// trong root.dat là con trỏ (đã kiểm chứng trên mẫu X7–2023):
    ///   u32 fileIndex (0-based theo dataFileList; 0xFFFFFFFF = dữ liệu nằm ngay trong con trỏ),
    ///   u32 length, u32 offset (hoặc tối đa 8 byte dữ liệu inline), u32 (luôn 0 trên mẫu hiện có).
    /// </param>
    public RiffParser(int previewBytes, IReadOnlyList<(string Name, byte[] Data)>? dataFiles = null)
    {
        _previewBytes = previewBytes;
        _dataFiles = dataFiles;
    }

    /// <summary>Đọc nội dung thật của chunk lá (đi theo con trỏ nếu có).</summary>
    private (byte[] Data, string? Note) ResolveLeaf(byte[] buf, int dataStart, int dataEnd)
    {
        byte[] raw = buf.AsSpan(dataStart, dataEnd - dataStart).ToArray();
        if (_dataFiles is null || raw.Length != 16)
            return (raw, null);

        uint index = BinaryPrimitives.ReadUInt32LittleEndian(raw);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4));
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8));
        uint extra = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12));
        string extraNote = extra == 0 ? "" : $" (u32 cuối = 0x{extra:X8}, khác 0!)";

        if (index == 0xFFFFFFFF)
        {
            if (length > 8)
                return (raw, $"CẢNH BÁO: con trỏ inline nhưng length={length} > 8");
            return (raw.AsSpan(8, (int)length).ToArray(), $"→ inline {length} byte");
        }
        if (index >= _dataFiles.Count)
            return (raw, $"CẢNH BÁO: fileIndex {index} vượt dataFileList ({_dataFiles.Count} file)");

        var (name, data) = _dataFiles[(int)index];
        if ((long)offset + length > data.Length)
            return (raw, $"CẢNH BÁO: {name}[0x{offset:X}+{length}] vượt kích thước file ({data.Length})");
        return (data.AsSpan((int)offset, (int)length).ToArray(), $"→ {name} @0x{offset:X} len={length}{extraNote}");
    }

    public Chunk ParseRoot(byte[] buf, string source)
    {
        if (buf.Length < 12 || Ascii(buf, 0, 4) != "RIFF")
            throw new InvalidDataException("Không phải RIFF (thiếu chữ ký 'RIFF').");

        uint size = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(4));
        string form = Ascii(buf, 8, 4);
        var root = new Chunk { FourCC = "RIFF", ListType = form, Offset = 0, Size = size, Source = source };

        if (!form.StartsWith("CDR", StringComparison.Ordinal))
            root.Notes.Add($"Form type '{form}' không bắt đầu bằng 'CDR' — có thể không phải file CorelDRAW.");
        else
            root.Notes.Add($"Ký tự phiên bản trong form type: '{form[3]}' (bảng tra ký tự→phiên bản là GIẢ ĐỊNH, ưu tiên giá trị chunk 'vrsn').");

        long end = 8L + size;
        if (end > buf.Length)
        {
            root.Notes.Add($"CẢNH BÁO: kích thước RIFF khai báo ({size}) vượt quá độ dài dữ liệu ({buf.Length - 8}).");
            end = buf.Length;
        }
        else if (end < buf.Length)
        {
            root.Notes.Add($"Có {buf.Length - end} byte nằm sau chunk RIFF gốc.");
        }

        ParseChildren(buf, 12, (int)end, root, source, blockSizes: null);
        return root;
    }

    private void ParseChildren(byte[] buf, int start, int end, Chunk parent, string source, uint[]? blockSizes)
    {
        int pos = start;
        while (pos + 8 <= end)
        {
            string fourcc = Ascii(buf, pos, 4);
            if (!IsPlausibleFourCC(fourcc))
            {
                parent.Notes.Add($"Dừng tại 0x{pos:X8}: '{Escape(buf, pos, 4)}' không giống FourCC ({end - pos} byte còn lại chưa phân tích).");
                return;
            }

            uint rawSize = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(pos + 4));
            uint size = rawSize;
            var notes = new List<string>();

            // GIẢ ĐỊNH (theo cách libcdr xử lý cmpr): trong luồng đã giải nén, trường "size"
            // là CHỈ SỐ vào bảng kích thước khối, không phải kích thước thật.
            if (blockSizes is not null)
            {
                if (rawSize < blockSizes.Length)
                {
                    size = blockSizes[rawSize];
                    notes.Add($"size là chỉ số #{rawSize} trong bảng khối → {size}");
                }
                else
                {
                    notes.Add($"CẢNH BÁO: chỉ số khối {rawSize} vượt bảng ({blockSizes.Length} phần tử); dùng như kích thước thật.");
                }
            }

            int dataStart = pos + 8;
            long dataEndL = (long)dataStart + size;
            bool overflow = dataEndL > end;
            int dataEnd = overflow ? end : (int)dataEndL;
            if (overflow)
                notes.Add($"CẢNH BÁO: chunk khai báo {size} byte nhưng vượt biên chunk cha ({end - dataStart} byte khả dụng).");

            bool isList = fourcc is "LIST" or "RIFF";
            string? listType = isList && dataEnd - dataStart >= 4 ? Ascii(buf, dataStart, 4) : null;

            byte[] content = Array.Empty<byte>();
            if (!isList)
            {
                (content, string? resolveNote) = ResolveLeaf(buf, dataStart, dataEnd);
                if (resolveNote is not null) notes.Add(resolveNote);
            }

            var chunk = new Chunk
            {
                FourCC = fourcc,
                ListType = listType,
                Offset = pos,
                Size = size,
                Source = source,
                Content = content,
                Preview = content.AsSpan(0, Math.Min(_previewBytes, content.Length)).ToArray(),
            };
            chunk.Notes.AddRange(notes);
            parent.Children.Add(chunk);

            if (isList)
                ParseChildren(buf, dataStart + 4, dataEnd, chunk, source, blockSizes);
            else if (fourcc == "cmpr")
                ParseCmpr(buf, dataStart, dataEnd, chunk);
            else if (fourcc == "vrsn" && content.Length >= 2)
                chunk.Notes.Add(DescribeVersion(BinaryPrimitives.ReadUInt16LittleEndian(content)));

            if (overflow)
                return;

            pos = dataEnd;
            // Đệm RIFF chuẩn: chunk có kích thước lẻ được đệm 1 byte. Không chắc CDR luôn tuân thủ,
            // nên chỉ nhảy qua byte đệm khi vị trí sau đệm trông giống FourCC hợp lệ.
            if ((size & 1) == 1 && (pos + 1 == end
                || (pos + 1 + 4 <= end && !IsPlausibleFourCC(Ascii(buf, pos, 4)) && IsPlausibleFourCC(Ascii(buf, pos + 1, 4)))))
            {
                pos++;
            }
        }

        if (pos < end)
            parent.Notes.Add($"{end - pos} byte thừa ở cuối (0x{pos:X8}).");
    }

    /// <summary>
    /// Chunk 'cmpr' (CDR khoảng v8–X3). Bố cục dưới đây là GIẢ ĐỊNH theo đọc mã libcdr, cần kiểm chứng bằng file mẫu:
    ///   u32 compressedSize, u32 uncompressedSize, u32 blockSizesCompressedSize, u32 blockSizesUncompressedSize,
    ///   "CPng" u16 u16, [zlib dữ liệu chunk], "CPng" u16 u16, [zlib bảng kích thước khối (mảng u32)].
    /// </summary>
    private void ParseCmpr(byte[] buf, int start, int end, Chunk chunk)
    {
        if (end - start < 24)
        {
            chunk.Notes.Add("cmpr quá ngắn để có header.");
            return;
        }

        var s = buf.AsSpan(start);
        uint compressed = BinaryPrimitives.ReadUInt32LittleEndian(s);
        uint uncompressed = BinaryPrimitives.ReadUInt32LittleEndian(s[4..]);
        uint bsCompressed = BinaryPrimitives.ReadUInt32LittleEndian(s[8..]);
        uint bsUncompressed = BinaryPrimitives.ReadUInt32LittleEndian(s[12..]);
        chunk.Notes.Add($"cmpr header: compressed={compressed}, uncompressed={uncompressed}, blockSizesCompressed={bsCompressed}, blockSizesUncompressed={bsUncompressed}");

        int sig1 = IndexOf(buf, "CPng"u8, start, end);
        if (sig1 < 0)
        {
            chunk.Notes.Add("Không thấy chữ ký 'CPng' — bố cục cmpr khác giả định.");
            return;
        }
        if (sig1 != start + 16)
            chunk.Notes.Add($"'CPng' đầu tiên ở +{sig1 - start}, không phải +16 như giả định.");

        byte[]? data = TryInflate(buf, sig1 + 8, end, out string how1);
        if (data is null)
        {
            chunk.Notes.Add($"Giải nén dữ liệu thất bại: {how1}");
            return;
        }
        chunk.Notes.Add($"Giải nén dữ liệu: {data.Length} byte ({how1}){(data.Length == uncompressed ? "" : $" — KHÁC uncompressed={uncompressed}")}");

        // Vị trí khối thứ hai: thử theo compressedSize trước, nếu không khớp thì tìm 'CPng' tiếp theo.
        int sig2 = sig1 + (int)Math.Min(compressed, (uint)(end - sig1));
        if (sig2 + 4 > end || Ascii(buf, sig2, 4) != "CPng")
            sig2 = IndexOf(buf, "CPng"u8, sig1 + 8, end);

        uint[]? blockSizes = null;
        if (sig2 >= 0)
        {
            byte[]? bs = TryInflate(buf, sig2 + 8, end, out string how2);
            if (bs is not null)
            {
                blockSizes = new uint[bs.Length / 4];
                for (int i = 0; i < blockSizes.Length; i++)
                    blockSizes[i] = BinaryPrimitives.ReadUInt32LittleEndian(bs.AsSpan(i * 4));
                chunk.Notes.Add($"Bảng kích thước khối: {blockSizes.Length} phần tử ({how2}).");
            }
            else
            {
                chunk.Notes.Add($"Giải nén bảng kích thước khối thất bại: {how2}");
            }
        }
        else
        {
            chunk.Notes.Add("Không tìm thấy khối 'CPng' thứ hai (bảng kích thước khối).");
        }

        string src = $"cmpr@0x{chunk.Offset:X8}";
        ParseChildren(data, 0, data.Length, chunk, src, blockSizes);
    }

    private static byte[]? TryInflate(byte[] buf, int start, int end, out string how)
    {
        if (start >= end)
        {
            how = "không còn dữ liệu";
            return null;
        }
        foreach (bool zlib in new[] { true, false })
        {
            try
            {
                using var input = new MemoryStream(buf, start, end - start, writable: false);
                using Stream z = zlib ? new ZLibStream(input, CompressionMode.Decompress) : new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                z.CopyTo(output);
                if (output.Length > 0)
                {
                    how = zlib ? "zlib" : "deflate thô";
                    return output.ToArray();
                }
            }
            catch (InvalidDataException)
            {
            }
        }
        how = "không phải zlib/deflate hợp lệ";
        return null;
    }

    /// <summary>vrsn = phiên bản*100 + bản cập nhật (vd 1700, 2430). 17→2024 đã kiểm chứng bằng mẫu.</summary>
    public static string DescribeVersion(ushort v)
    {
        int major = v / 100;
        string name = (major, v % 100) switch
        {
            (24, 30) => "CorelDRAW 2023",
            (24, _) => "CorelDRAW 2022",
            (25, _) => "CorelDRAW 2024",
            _ => major switch
        {
            >= 1 and <= 12 => $"CorelDRAW {major}",
            13 => "CorelDRAW X3",
            14 => "CorelDRAW X4",
            15 => "CorelDRAW X5",
            16 => "CorelDRAW X6",
            17 => "CorelDRAW X7",
            18 => "CorelDRAW X8",
            19 => "CorelDRAW 2017",
            20 => "CorelDRAW 2018",
            21 => "CorelDRAW 2019",
            22 => "CorelDRAW 2020",
            23 => "CorelDRAW 2021",
            _ => "không rõ",
        },
        };
        return $"vrsn = {v} → {name}";
    }

    public static string Ascii(byte[] buf, int pos, int len) => Encoding.Latin1.GetString(buf, pos, len);

    public static string Escape(byte[] buf, int pos, int len)
    {
        var sb = new StringBuilder();
        for (int i = pos; i < Math.Min(buf.Length, pos + len); i++)
            sb.Append(buf[i] is >= 0x20 and < 0x7F ? (char)buf[i] : '.');
        return sb.ToString();
    }

    private static bool IsPlausibleFourCC(string s)
    {
        foreach (char c in s)
            if (c is < ' ' or > '~')
                return false;
        return s.Trim().Length > 0;
    }

    private static int IndexOf(byte[] buf, ReadOnlySpan<byte> needle, int start, int end)
    {
        if (start >= end) return -1;
        int i = buf.AsSpan(start, end - start).IndexOf(needle);
        return i < 0 ? -1 : start + i;
    }
}
