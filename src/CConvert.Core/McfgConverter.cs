namespace CConvert.Core;

/// <summary>
/// Chuyển bản ghi cấu hình tài liệu 'mcfg' dạng 2100 (CorelDRAW 2019+, 7944 byte) về dạng 1300 (X7–2018, 8323 byte).
/// Quy tắc (trùng từng byte 188/196 cặp do CorelDRAW 2019 và 2024 tự "Save As"; 8 cặp còn lại chỉ khác một giá trị
/// trạng thái tài liệu Corel tự tính lại khi lưu):
///   header u16 1300, u16 0, u32 độ dài còn lại;
///   chèn tại 0x365 (theo bản 2100): hai lần u32 đọc tại 0x347;
///   chèn tại 0x372: khối 371 byte cố định (giống nhau ở mọi tài liệu, có chuỗi "CorelDefaultID_10900").
/// </summary>
public static class McfgConverter
{
    public const ushort Form2019 = 2100;
    public const ushort FormOld = 1300;
    private const int Length2019 = 7944;

    public static byte[]? ToOldForm(byte[] m)
    {
        if (m.Length != Length2019 || BitConverter.ToUInt16(m, 0) != Form2019)
            return null;
        using var ms = new MemoryStream(8323);
        ms.Write(new byte[8]);
        ms.Write(m, 8, 0x365 - 8);
        ms.Write(m, 0x347, 4);
        ms.Write(m, 0x347, 4);
        ms.Write(m, 0x365, 0x372 - 0x365);
        ms.Write(Block371);
        ms.Write(m, 0x372, m.Length - 0x372);
        byte[] r = ms.ToArray();
        BitConverter.TryWriteBytes(r.AsSpan(0), FormOld);
        BitConverter.TryWriteBytes(r.AsSpan(4), (uint)(r.Length - 8));
        return r;
    }

    private static readonly byte[] Block371 = Convert.FromHexString(
        "00000001000000000000000000000000000000010C0000000500050000000000C4000000071000000000000000000000" +
        "0000000000000000000000000000010C00000005000500000000002000C4000710000000000000000000000000000000" +
        "000000000000000000010C0000000500050000000000C400C40007100000000000000000000000000000000000000000" +
        "0000000043006F00720065006C00440065006600610075006C007400490044005F003100300039003000300000000000" +
        "00000000000043006F00720065006C00440065006600610075006C007400490044005F00310030003900300030000000" +
        "000000000000000043006F00720065006C00440065006600610075006C007400490044005F0031003000390030003000" +
        "000000000000000000000000000003000000010000000100000001000000000000000100000000000000000000000000" +
        "0000000000000000000002000000000000000400000064000000000000000000000001");
}
