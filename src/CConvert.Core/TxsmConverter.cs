using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CConvert.Core;

/// <summary>
/// Chuyển chunk text 'txsm' từ dạng 14 (CorelDRAW 2020+) về dạng 13 (2019).
/// Quy tắc rút ra từ so sánh với file Corel tự Save As (docs/PhatHien-02-loda.md):
///  - u32 tại 0x14: 14 → 13.
///  - Các khối JSON style (u32 độ dài + JSON có thụt lề): bỏ khóa 2020+, cập nhật độ dài.
///    Khối "paragraph" thiếu bulletFirstCharIndent được thêm giá trị mặc định 2019.
///  - Cuối bản ghi (sau chuỗi text + byte 0): bản 14 có thêm cấu trúc danh sách
///    u32 n, n × (u32 c, c × u32) — bản 13 không có → bỏ.
/// </summary>
public static class TxsmConverter
{
    // Giá trị Corel 2019 ghi khi style không có (bullet mặc định '•' Arial 12pt).
    private const string DefaultBulletFirstCharIndent = "48676";

    /// <param name="problem">Gọi khi gặp dạng text chưa biết (chế độ thử nghiệm biến thành lỗi).</param>
    /// <param name="extraStrip">Lọc JSON bổ sung (chế độ thử nghiệm: theo lược đồ 2019); trả về số khóa đã bỏ.</param>
    public static byte[]? Convert(byte[] d, Action<string> warn, Action<string>? problem = null, Func<JsonNode, int>? extraStrip = null)
    {
        problem ??= warn;
        if (d.Length < 0x18) return null;
        uint ver = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(0x14));
        if (ver == 13) return null;
        if (ver != 14)
        {
            problem($"Text dạng {ver} chưa gặp trong mẫu — chép nguyên.");
            return null;
        }

        int tail = FindListTail(d);
        if (tail < 0)
        {
            problem("Không xác định được cấu trúc cuối của khối text — chép nguyên (CorelDRAW 2019 vẫn mở được dạng này trong thử nghiệm).");
            return null;
        }

        using var ms = new MemoryStream(d.Length);
        int pos = 0;
        foreach (var (start, length) in FindJsonBlocks(d, tail))
        {
            ms.Write(d, pos, start - 4 - pos);
            byte[] block = d.AsSpan(start, length).ToArray();
            byte[] converted = ConvertJson(block, warn, extraStrip) ?? block;
            Span<byte> len = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(len, (uint)converted.Length);
            ms.Write(len);
            ms.Write(converted);
            pos = start + length;
        }
        ms.Write(d, pos, tail - pos);

        byte[] result = ms.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x14), 13);
        return result;
    }

    /// <summary>Vị trí bắt đầu cấu trúc danh sách ở cuối (ngay sau "chuỗi text + 0x00"), hoặc -1.</summary>
    private static int FindListTail(byte[] d)
    {
        // Thử từ cuối về: chuỗi text = u32 L + L byte, theo sau 1 byte 0, rồi cấu trúc danh sách kéo đến hết bản ghi.
        for (int p = d.Length - 9; p >= 0x18; p--)
        {
            uint l = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(p));
            long q = p + 4L + l;
            if (q + 5 > d.Length || d[q] != 0) continue;
            if (ListEndsExactly(d, (int)q + 1))
                return (int)q + 1;
        }
        return -1;
    }

    private static bool ListEndsExactly(byte[] d, int pos)
    {
        if (pos + 4 > d.Length) return false;
        uint n = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(pos));
        if (n > 10000) return false;
        pos += 4;
        for (uint i = 0; i < n; i++)
        {
            if (pos + 4 > d.Length) return false;
            uint c = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(pos));
            if (c > 100000) return false;
            pos += 4 + (int)(4 * c);
        }
        return pos == d.Length;
    }

    /// <summary>Các khối JSON: u32 độ dài ngay trước '{', JSON hợp lệ dài đúng độ dài đó.</summary>
    private static IEnumerable<(int Start, int Length)> FindJsonBlocks(byte[] d, int end)
    {
        int i = 4;
        while (i < end)
        {
            if (d[i] == (byte)'{')
            {
                uint l = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(i - 4));
                if (l >= 2 && i + l <= end && d[i + l - 1] == (byte)'}' && IsJson(d.AsSpan(i, (int)l)))
                {
                    yield return (i, (int)l);
                    i += (int)l;
                    continue;
                }
            }
            i++;
        }
    }

    private static bool IsJson(ReadOnlySpan<byte> s)
    {
        try
        {
            var reader = new Utf8JsonReader(s);
            while (reader.Read()) { }
            return reader.BytesConsumed == s.Length;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Str(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static byte[]? ConvertJson(byte[] block, Action<string> warn, Func<JsonNode, int>? extraStrip)
    {
        var node = JsonNode.Parse(block);
        if (node is not JsonObject root) return null;
        int changes = CorelJson.StripPost2019Keys(root, warn);
        if (extraStrip is not null) changes += extraStrip(root);

        if (root["paragraph"] is JsonObject para)
        {
            // 2020+ đo khoảng thụt dòng đầu tính cả bullet; 2019 đo sau bullet. Với bullet mặc định
            // ('•' Arial 12pt, thụt 6,35 mm) giá trị 2019 đã biết chính xác; trường hợp khác cần đo font.
            bool known = false;
            if (Str(para, "bulletFirstCharIndent") == "63500" && Str(para, "bulletChar") == "22200000"
                && para["bulletFont"] is JsonObject bf && Str(bf, "font") == "Arial" && Str(bf, "size") == "42333")
            {
                para["bulletFirstCharIndent"] = DefaultBulletFirstCharIndent;
                known = true;
                changes++;
            }
            if (Str(para, "bulletOn") == "1" && !known)
                warn("Văn bản có danh sách bullet: CorelDRAW 2019 tính khoảng cách chữ–bullet khác 2020+, chữ có thể lệch vài mm so với bản gốc.");
            if (para.Count > 0 && !para.ContainsKey("bulletFirstCharIndent"))
            {
                CorelJson.InsertSorted(para, "bulletFirstCharIndent", JsonValue.Create(DefaultBulletFirstCharIndent)!);
                changes++;
            }
        }
        if (changes == 0) return null;
        string newline = Encoding.UTF8.GetString(block).Contains("\r\n") ? "\r\n" : "\n";
        return CorelJson.Utf8(CorelJson.ToIndented(root, newline));
    }
}
