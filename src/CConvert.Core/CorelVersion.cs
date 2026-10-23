namespace CConvert.Core;

/// <summary>
/// Thông tin nhận dạng một phiên bản CorelDRAW trong file .cdr dạng ZIP.
/// Mọi giá trị dưới đây đọc từ file mẫu do CorelDRAW 2024 "Save As" ra (docs/PhatHien-01-T00-T01.md).
/// </summary>
/// <param name="Vrsn">Giá trị chunk vrsn, vd 2100.</param>
/// <param name="FormChar">Ký tự thứ 4 của form type RIFF "CDR?".</param>
/// <param name="FverMajor">Byte 6 của fver.</param>
/// <param name="FverMinor">Byte 5 của fver (bản cập nhật, vd 2023 = 3).</param>
/// <param name="DocFormatterId">inTxt:DocFormatterId trong META-INF/metadata.xml.</param>
public sealed record CorelVersion(string Name, ushort Vrsn, char FormChar, byte FverMajor, byte FverMinor, int DocFormatterId)
{
    /// <summary>Phiên bản mới hơn 2024 chưa có mẫu để kiểm chứng (chế độ thử nghiệm).</summary>
    public bool IsExperimental { get; init; }

    /// <summary>
    /// Nguồn mới hơn 2024 (vrsn &gt; 2500). Tên sản phẩm là ngoại suy từ quy luật 17→X7 … 25→2024
    /// (GIẢ ĐỊNH: 26 = 2025, 27 = 2026), chỉ dùng để hiển thị.
    /// </summary>
    public static CorelVersion? Experimental(ushort vrsn)
    {
        if (vrsn <= V2024.Vrsn || vrsn >= 4000) return null;
        int major = vrsn / 100;
        string name = major is >= 26 and <= 40 ? $"{major + 1999}?" : $"vrsn {vrsn}";
        return new CorelVersion(name, vrsn, '?', (byte)major, (byte)(vrsn % 100 / 10), 0) { IsExperimental = true };
    }

    /// <summary>Phiên bản đã kiểm chứng, hoặc phiên bản thử nghiệm nếu mới hơn 2024.</summary>
    public static CorelVersion? Recognize(ushort vrsn) => FromVrsn(vrsn) ?? Experimental(vrsn);

    public static readonly CorelVersion X7 = new("X7", 1700, 'H', 0x11, 0, 1700);
    public static readonly CorelVersion X8 = new("X8", 1800, 'J', 0x12, 0, 1800);
    public static readonly CorelVersion V2017 = new("2017", 1900, 'K', 0x13, 0, 1800);
    public static readonly CorelVersion V2018 = new("2018", 2000, 'L', 0x14, 0, 1800);
    public static readonly CorelVersion V2019 = new("2019", 2100, 'M', 0x15, 0, 1700);
    public static readonly CorelVersion V2020 = new("2020", 2200, 'N', 0x16, 0, 1800);
    public static readonly CorelVersion V2021 = new("2021", 2300, 'O', 0x17, 0, 1800);
    public static readonly CorelVersion V2022 = new("2022", 2400, 'P', 0x18, 0, 1800);
    public static readonly CorelVersion V2023 = new("2023", 2430, 'Q', 0x18, 3, 1800);
    public static readonly CorelVersion V2024 = new("2024", 2500, 'R', 0x19, 0, 1800);

    public static IReadOnlyList<CorelVersion> All { get; } =
        [X7, X8, V2017, V2018, V2019, V2020, V2021, V2022, V2023, V2024];

    public static CorelVersion? FromVrsn(ushort vrsn) => All.FirstOrDefault(v => v.Vrsn == vrsn);

    /// <summary>8 byte nội dung chunk fver: u16 vrsn, u16 1, u8 0, u8 minor, u8 major, u8 0.</summary>
    public byte[] FverBytes() =>
        [(byte)Vrsn, (byte)(Vrsn >> 8), 1, 0, 0, FverMinor, FverMajor, 0];

    public override string ToString() => IsExperimental
        ? $"CorelDRAW {Name} ({Vrsn}, thử nghiệm)"
        : $"CorelDRAW {Name} ({Vrsn})";
}
