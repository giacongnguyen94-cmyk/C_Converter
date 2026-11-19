using System.Buffers.Binary;
using Microsoft.Win32;

namespace CConvert.Core;

/// <summary>Tra cứu font đã cài trên Windows (chỉ để cảnh báo, không bắt buộc).</summary>
public static class FontCatalog
{
    private static readonly Lazy<Dictionary<string, string>> Files = new(LoadRegistry);

    /// <summary>Font là font biến thể (OpenType có bảng 'fvar')? null nếu không tìm thấy font.</summary>
    public static bool? IsVariable(string family)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (!Files.Value.TryGetValue(family, out string? path)) return null;
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> head = stackalloc byte[12];
            if (fs.Read(head) != 12) return null;
            ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(head[4..]);
            if (head[0] == 't' && head[1] == 't' && head[2] == 'c' && head[3] == 'f')
                return null; // bộ sưu tập TTC: bỏ qua cho đơn giản
            var dir = new byte[16 * numTables];
            if (fs.Read(dir) != dir.Length) return null;
            for (int i = 0; i < numTables; i++)
                if (dir[16 * i] == 'f' && dir[16 * i + 1] == 'v' && dir[16 * i + 2] == 'a' && dir[16 * i + 3] == 'r')
                    return true;
            return false;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Độ rộng (advance) của ký tự theo em (vd bullet U+2022 Arial = 717/2048), đọc bảng cmap/hmtx của file font đã cài.
    /// Font ký hiệu (cmap 3,0) thử thêm mã U+F000 + mã. null nếu không tìm thấy font/ký tự.
    /// </summary>
    public static double? AdvanceEm(string family, bool bold, bool italic, int codePoint)
    {
        if (!OperatingSystem.IsWindows()) return null;
        string style = (bold, italic) switch { (true, true) => " Bold Italic", (true, false) => " Bold", (false, true) => " Italic", _ => "" };
        if (!Files.Value.TryGetValue(family + style, out string? path) && !Files.Value.TryGetValue(family, out path))
            return null;
        try
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 12 || (b[0] == 't' && b[1] == 't' && b[2] == 'c' && b[3] == 'f')) return null;
            var tables = new Dictionary<string, int>();
            int n = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(4));
            for (int i = 0; i < n; i++)
                tables[System.Text.Encoding.ASCII.GetString(b, 12 + 16 * i, 4)] = (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(12 + 16 * i + 8));
            if (!tables.TryGetValue("head", out int head) || !tables.TryGetValue("hhea", out int hhea)
                || !tables.TryGetValue("hmtx", out int hmtx) || !tables.TryGetValue("cmap", out int cmap)) return null;
            int upm = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(head + 18));
            int numH = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(hhea + 34));
            int gid = Glyph(b, cmap, codePoint);
            if (gid <= 0 && codePoint < 0x100) gid = Glyph(b, cmap, 0xF000 | codePoint);
            if (gid <= 0 || upm == 0 || numH == 0) return null;
            int adv = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(hmtx + 4 * Math.Min(gid, numH - 1)));
            return (double)adv / upm;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Tra glyph trong bảng cmap định dạng 4 (BMP).</summary>
    private static int Glyph(byte[] b, int cmap, int cp)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(cmap + 2));
        for (int i = 0; i < count; i++)
        {
            int sub = cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(cmap + 4 + 8 * i + 4));
            if (BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(sub)) != 4) continue;
            int segX2 = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(sub + 6)), seg = segX2 / 2;
            int ends = sub + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int k = 0; k < seg; k++)
            {
                int end = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(ends + 2 * k));
                if (cp > end) continue;
                int start = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(starts + 2 * k));
                if (cp < start) break;
                short delta = BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(deltas + 2 * k));
                int ro = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(ranges + 2 * k));
                if (ro == 0) return (cp + delta) & 0xFFFF;
                int g = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(ranges + 2 * k + ro + 2 * (cp - start)));
                return g == 0 ? 0 : (g + delta) & 0xFFFF;
            }
        }
        return 0;
    }

    private static Dictionary<string, string> LoadRegistry()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows()) return map;
        string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
            if (key is null) continue;
            foreach (string name in key.GetValueNames())
            {
                if (key.GetValue(name) is not string file) continue;
                string path = Path.IsPathRooted(file) ? file : Path.Combine(fontsDir, file);
                // "Arial Bold (TrueType)" → "Arial Bold"; "Bahnschrift (TrueType)" → "Bahnschrift"
                string family = name.Split(" (")[0].Trim();
                map.TryAdd(family, path);
            }
        }
        return map;
    }
}
