using System.Buffers.Binary;
using System.IO.Compression;

namespace CConvert.Core;

/// <summary>Đọc nhanh thông tin file .cdr (không chuyển đổi).</summary>
public static class CdrInfo
{
    /// <summary>Phiên bản CorelDRAW của file, hoặc mô tả lý do không đọc được.</summary>
    public static (CorelVersion? Version, string Description) Probe(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> sig = stackalloc byte[4];
            if (fs.Read(sig) < 4) return (null, "File rỗng");
            fs.Position = 0;
            if (sig[0] == 'R' && sig[1] == 'I' && sig[2] == 'F' && sig[3] == 'F')
                return (null, "CorelDRAW X3 trở về trước (chưa hỗ trợ)");
            if (sig[0] != 'P' || sig[1] != 'K')
                return (null, "Không phải file CorelDRAW");

            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var rootEntry = zip.GetEntry("content/root.dat");
            if (rootEntry is null)
                return (null, "CorelDRAW X4–X6 (chưa hỗ trợ)");
            byte[] root;
            using (var s = rootEntry.Open())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                root = ms.ToArray();
            }
            var rd = new RootDat(root);
            if (rd.Find("vrsn") is { Inline: true, Length: 2 } v)
            {
                ushort vrsn = BinaryPrimitives.ReadUInt16LittleEndian(root.AsSpan(v.PointerOffset + 8));
                var ver = CorelVersion.Recognize(vrsn);
                return (ver, ver?.ToString() ?? $"Phiên bản lạ (vrsn {vrsn})");
            }
            return (null, "Không tìm thấy số phiên bản");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return (null, "Lỗi đọc file: " + ex.Message);
        }
    }

    /// <summary>Đường dẫn chưa tồn tại: thêm " (1)", " (2)"… nếu cần. Không bao giờ trả về file đã có.</summary>
    public static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path)!, name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
        for (int i = 1; ; i++)
        {
            string p = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(p)) return p;
        }
    }

    /// <summary>Tên file đầu ra: &lt;tên&gt;_&lt;phiên bản&gt;.cdr trong thư mục chỉ định (mặc định cạnh file gốc).</summary>
    public static string OutputPathFor(string input, CorelVersion target, string? outDir)
    {
        string dir = outDir ?? Path.GetDirectoryName(Path.GetFullPath(input))!;
        return UniquePath(Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(input)}_{target.Name}.cdr"));
    }
}
