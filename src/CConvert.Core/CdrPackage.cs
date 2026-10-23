using System.IO.Compression;

namespace CConvert.Core;

/// <summary>File .cdr dạng ZIP, giữ nguyên thứ tự mục và cách nén như CorelDRAW ghi.</summary>
public sealed class CdrPackage
{
    public const string MimeType = "application/x-vnd.corel.zcf.draw.document+zip";

    public sealed class Entry
    {
        public required string Name { get; init; }
        public required byte[] Data { get; set; }
        public bool Stored { get; init; }
        public DateTimeOffset LastWrite { get; init; }
    }

    public List<Entry> Entries { get; } = new();

    public Entry? Find(string name) =>
        Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    public Entry Get(string name) =>
        Find(name) ?? throw new InvalidDataException($"File .cdr thiếu mục '{name}'.");

    public static CdrPackage Load(string path)
    {
        var pkg = new CdrPackage();
        using var zip = ZipFile.OpenRead(path);
        foreach (var e in zip.Entries)
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            pkg.Entries.Add(new Entry
            {
                Name = e.FullName,
                Data = ms.ToArray(),
                Stored = e.CompressedLength == e.Length && e.FullName == "mimetype",
                LastWrite = e.LastWriteTime,
            });
        }
        return pkg;
    }

    /// <summary>
    /// Ghi ra file mới. Không bao giờ ghi đè file đã tồn tại; ghi vào file tạm rồi đổi tên
    /// để không để lại file dở dang nếu có lỗi giữa chừng.
    /// </summary>
    public void Save(string path)
    {
        if (File.Exists(path))
            throw new IOException($"File đích đã tồn tại: {path}");
        string temp = path + ".c_converter.tmp";
        try
        {
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var e in Entries)
                {
                    // Giống ODF: "mimetype" phải là mục đầu tiên và không nén.
                    var level = e.Name == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
                    var ze = zip.CreateEntry(e.Name, level);
                    ze.LastWriteTime = e.LastWrite;
                    using var s = ze.Open();
                    s.Write(e.Data);
                }
            }
            File.Move(temp, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
