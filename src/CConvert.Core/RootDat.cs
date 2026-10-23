using System.Buffers.Binary;
using System.Text;

namespace CConvert.Core;

/// <summary>
/// content/root.dat: RIFF "CDR?" mà mỗi chunk lá là con trỏ 16 byte
/// (u32 fileIndex | 0xFFFFFFFF = inline, u32 length, u32 offset hoặc ≤8 byte inline, u32 0).
/// </summary>
public sealed class RootDat
{
    /// <param name="Path">Đường dẫn LIST, vd "/page/gobj/layr/obj/lgob/loda".</param>
    /// <param name="Parent">Số thứ tự của LIST cha (để nhóm các chunk cùng một đối tượng).</param>
    public sealed record Leaf(string FourCC, string Path, int Parent, int PointerOffset, uint FileIndex, uint Length, uint DataOffset)
    {
        public bool Inline => FileIndex == 0xFFFFFFFF;
    }

    public byte[] Bytes { get; }
    public List<Leaf> Leaves { get; } = new();
    public List<string> ListTypes { get; } = new();

    public RootDat(byte[] bytes)
    {
        Bytes = bytes;
        if (bytes.Length < 12 || Ascii(0) != "RIFF" || Ascii(8, 3) != "CDR")
            throw new InvalidDataException("content/root.dat không phải RIFF 'CDR?'.");
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        if (8L + size != bytes.Length)
            throw new InvalidDataException($"Kích thước RIFF ({size}) không khớp độ dài root.dat ({bytes.Length}).");
        Walk(12, bytes.Length, "");
    }

    public char FormChar
    {
        get => (char)Bytes[11];
        set => Bytes[11] = (byte)value;
    }

    public Leaf? Find(string fourcc) => Leaves.FirstOrDefault(l => l.FourCC == fourcc);

    /// <summary>Đọc nội dung chunk lá; dataFiles theo thứ tự trong dataFileList.</summary>
    public byte[] Read(Leaf leaf, IReadOnlyList<byte[]> dataFiles)
    {
        if (leaf.Inline)
            return Bytes.AsSpan(leaf.PointerOffset + 8, (int)leaf.Length).ToArray();
        byte[] file = dataFiles[(int)leaf.FileIndex];
        return file.AsSpan((int)leaf.DataOffset, (int)leaf.Length).ToArray();
    }

    /// <summary>
    /// Tạo root.dat mới không còn các chunk được chọn: <paramref name="removeList"/> nhận chỉ số trong
    /// <see cref="Lists"/>, <paramref name="removeLeaf"/> nhận chỉ số trong <see cref="Leaves"/>.
    /// Kích thước các LIST cha và RIFF được tính lại. Dữ liệu trong file .dat không bị xóa (chỉ không còn được trỏ tới).
    /// Phải gọi trước mọi <see cref="Replace"/> vì chỉ số chunk thay đổi.
    /// </summary>
    public RootDat Without(Func<int, bool> removeList, Func<int, bool> removeLeaf)
    {
        if (_replacements.Count > 0)
            throw new InvalidOperationException("Xóa chunk phải làm trước khi thay nội dung.");
        int listIndex = 0, leafIndex = 0;
        using var ms = new MemoryStream(Bytes.Length);
        ms.Write(Bytes, 0, 12);
        Copy(12, Bytes.Length);
        byte[] result = ms.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
        return new RootDat(result);

        // Duyệt theo đúng thứ tự Walk() để chỉ số LIST/lá khớp với Lists/Leaves.
        void Copy(int pos, int end)
        {
            while (pos + 8 <= end)
            {
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan(pos + 4));
                int ds = pos + 8, next = ds + (int)size + (int)(size & 1);
                if (Ascii(pos) == "LIST")
                {
                    int me = listIndex++;
                    if (removeList(me))
                    {
                        SkipLists(ds + 4, ds + (int)size);
                    }
                    else
                    {
                        long header = ms.Position;
                        ms.Write(Bytes, pos, 12);
                        Copy(ds + 4, ds + (int)size);
                        long after = ms.Position;
                        uint newSize = (uint)(after - header - 8);
                        ms.Position = header + 4;
                        Span<byte> b = stackalloc byte[4];
                        BinaryPrimitives.WriteUInt32LittleEndian(b, newSize);
                        ms.Write(b);
                        ms.Position = after;
                        if ((newSize & 1) == 1) ms.WriteByte(0);
                    }
                }
                else if (!removeLeaf(leafIndex++))
                {
                    ms.Write(Bytes, pos, next - pos);
                }
                pos = next;
            }
        }

        // Bỏ qua cả cây con: vẫn phải đếm LIST/lá bên trong để giữ đúng chỉ số.
        void SkipLists(int pos, int end)
        {
            while (pos + 8 <= end)
            {
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan(pos + 4));
                int ds = pos + 8;
                if (Ascii(pos) == "LIST") { listIndex++; SkipLists(ds + 4, ds + (int)size); }
                else leafIndex++;
                pos = ds + (int)size + (int)(size & 1);
            }
        }
    }

    private readonly Dictionary<int, byte[]> _replacements = new();

    /// <summary>Đặt nội dung mới cho chunk lá (được áp dụng khi gọi <see cref="Rebuild"/>).</summary>
    public void Replace(Leaf leaf, byte[] content)
    {
        int i = Leaves.IndexOf(leaf);
        if (i < 0) throw new ArgumentException("Chunk không thuộc root.dat này.", nameof(leaf));
        if (leaf.Inline && content.Length > 8)
            throw new InvalidOperationException($"Chunk inline {leaf.FourCC} không thể dài hơn 8 byte.");
        _replacements[i] = content;
    }

    public bool HasReplacements => _replacements.Count > 0;

    /// <summary>
    /// Dựng lại các file dữ liệu theo nội dung đã thay và cập nhật mọi con trỏ trong root.dat.
    /// Byte không thuộc chunk nào (khoảng trống) được giữ nguyên vị trí tương đối.
    /// Từ chối nếu hai con trỏ chồng lên nhau (không biết cách dịch an toàn).
    /// </summary>
    public List<byte[]> Rebuild(IReadOnlyList<byte[]> dataFiles)
    {
        var result = new List<byte[]>(dataFiles.Count);
        for (int f = 0; f < dataFiles.Count; f++)
        {
            byte[] src = dataFiles[f];
            var refs = Leaves.Select((l, i) => (Leaf: l, Index: i))
                .Where(x => !x.Leaf.Inline && x.Leaf.FileIndex == f)
                .OrderBy(x => x.Leaf.DataOffset)
                .ToList();
            if (!refs.Any(x => _replacements.ContainsKey(x.Index)))
            {
                result.Add(src);
                continue;
            }

            using var ms = new MemoryStream(src.Length + 1024);
            long cursor = 0;
            foreach (var (leaf, index) in refs)
            {
                if (leaf.DataOffset < cursor)
                    throw new InvalidDataException($"Hai chunk chồng lên nhau trong file dữ liệu #{f} tại 0x{leaf.DataOffset:X}.");
                ms.Write(src, (int)cursor, (int)(leaf.DataOffset - cursor));
                byte[] content = _replacements.TryGetValue(index, out var r) ? r : src.AsSpan((int)leaf.DataOffset, (int)leaf.Length).ToArray();
                var p = Bytes.AsSpan(leaf.PointerOffset);
                BinaryPrimitives.WriteUInt32LittleEndian(p[4..], (uint)content.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(p[8..], (uint)ms.Position);
                ms.Write(content);
                cursor = leaf.DataOffset + leaf.Length;
            }
            ms.Write(src, (int)cursor, (int)(src.Length - cursor));
            result.Add(ms.ToArray());
        }

        foreach (var (index, content) in _replacements)
        {
            var leaf = Leaves[index];
            if (!leaf.Inline) continue;
            var p = Bytes.AsSpan(leaf.PointerOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(p[4..], (uint)content.Length);
            p.Slice(8, 8).Clear();
            content.CopyTo(p[8..]);
        }
        _replacements.Clear();
        return result;
    }

    /// <summary>Ghi đè dữ liệu inline (không đổi độ dài).</summary>
    public void WriteInline(Leaf leaf, ReadOnlySpan<byte> data)
    {
        if (!leaf.Inline || data.Length != leaf.Length)
            throw new InvalidOperationException($"Chunk {leaf.FourCC} không phải inline {data.Length} byte.");
        data.CopyTo(Bytes.AsSpan(leaf.PointerOffset + 8));
    }

    /// <summary>Các LIST theo thứ tự gặp: kiểu và chỉ số LIST cha (-1 = gốc).</summary>
    public List<(string Type, int Parent)> Lists { get; } = new();

    private void Walk(int pos, int end, string prefix, int parent = -1)
    {
        while (pos + 8 <= end)
        {
            string cc = Ascii(pos);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan(pos + 4));
            int ds = pos + 8;
            if (ds + size > end)
                throw new InvalidDataException($"Chunk '{cc}' tại 0x{pos:X} vượt biên.");
            if (cc == "LIST")
            {
                string type = Ascii(ds).TrimEnd();
                ListTypes.Add(type);
                Lists.Add((type, parent));
                Walk(ds + 4, ds + (int)size, prefix + "/" + type, Lists.Count - 1);
            }
            else
            {
                if (size != 16)
                    throw new InvalidDataException($"Chunk lá '{cc}' tại 0x{pos:X} dài {size}, không phải con trỏ 16 byte.");
                var s = Bytes.AsSpan(ds);
                Leaves.Add(new Leaf(cc, prefix + "/" + cc, parent, ds,
                    BinaryPrimitives.ReadUInt32LittleEndian(s),
                    BinaryPrimitives.ReadUInt32LittleEndian(s[4..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(s[8..])));
            }
            pos = ds + (int)size + (int)(size & 1);
        }
    }

    private string Ascii(int pos, int len = 4) => Encoding.Latin1.GetString(Bytes, pos, len);
}
