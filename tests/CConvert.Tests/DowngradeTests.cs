using System.IO.Compression;
using CConvert.Core;

namespace CConvert.Tests;

/// <summary>
/// So kết quả hạ 2024 → 2019 với file CorelDRAW 2024 tự "Save As 2019" (samples/gen/*_21.cdr).
/// Các mẫu dưới đây đã được xác nhận trùng từng byte ở toàn bộ phần content/ (root.dat + data/*.dat).
/// Những mẫu còn khác đều khác ở chỗ vô hại (GUID tự sinh, số thứ tự 0xA, txtj do 2019 tự tính lại)
/// hoặc là giới hạn đã biết — xem docs/PhatHien-02-loda.md.
/// </summary>
public class DowngradeTests
{
    public static IEnumerable<object[]> ExactContent() =>
        new[]
        {
            "T00_empty", "T01_rect", "T02_rect_moved", "T03_rect_cmyk", "T04_two_rects", "T05_ellipse", "T06_line",
            "T07_curve", "T08_outline", "T09_roundrect", "T10_rotated", "T12_layers", "T13_pages", "T14b_text_curves",
            "T17_linear", "T18_radial", "T21_lens", "T22_transparency", "T23_shadow", "T23b_shadow_soft",
            "T23c_shadow_persp", "T24_contour", "T25_polygon", "T26_envelope", "T27_extrude", "T28_pushpull",
            "T30_texture", "T35_spiral", "T38_many", "T39_bitmap_effect",
        }
        .Where(n => File.Exists(Path.Combine(SampleFiles.Root, n + "_25.cdr")))
        .Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(ExactContent))]
    public void Content_matches_CorelDRAW_save_as_2019(string name)
    {
        string output = Path.Combine(Path.GetTempPath(), $"cconvert_test_{Guid.NewGuid():N}.cdr");
        try
        {
            var result = Downgrader.Run(Path.Combine(SampleFiles.Root, name + "_25.cdr"), output, CorelVersion.V2019);
            Assert.True(result.Success, string.Join("\n", result.Log));

            using var ours = ZipFile.OpenRead(output);
            using var corel = ZipFile.OpenRead(Path.Combine(SampleFiles.Root, name + "_21.cdr"));
            var names = corel.Entries.Where(e => e.FullName.StartsWith("content/")).Select(e => e.FullName).ToList();
            Assert.Equal(names, ours.Entries.Where(e => e.FullName.StartsWith("content/")).Select(e => e.FullName));
            foreach (string n in names)
                Assert.True(Read(ours.GetEntry(n)!).SequenceEqual(Read(corel.GetEntry(n)!)), $"{name}: {n} khác Corel");
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void Never_overwrites_existing_output()
    {
        string input = Path.Combine(SampleFiles.Root, "T01_rect_25.cdr");
        if (!File.Exists(input)) return;
        string output = Path.Combine(Path.GetTempPath(), $"cconvert_test_{Guid.NewGuid():N}.cdr");
        File.WriteAllText(output, "giữ nguyên");
        try
        {
            var result = Downgrader.Run(input, output, CorelVersion.V2019);
            Assert.False(result.Success);
            Assert.Equal("giữ nguyên", File.ReadAllText(output));
            Assert.NotEqual(output, CdrInfo.UniquePath(output));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void Garbage_input_fails_cleanly()
    {
        string input = Path.Combine(Path.GetTempPath(), $"cconvert_bad_{Guid.NewGuid():N}.cdr");
        string output = input + ".out.cdr";
        File.WriteAllBytes(input, [0x50, 0x4B, 3, 4, 1, 2, 3]);
        try
        {
            var result = Downgrader.Run(input, output, CorelVersion.V2019);
            Assert.False(result.Success);
            Assert.False(File.Exists(output));
        }
        finally
        {
            File.Delete(input);
        }
    }

    [Theory]
    [InlineData("9f000000", "78010000")] // 'Ÿ' — mẫu thật
    [InlineData("95000000", "22200000")] // '•'
    [InlineData("22200000", null)]       // đã là Unicode
    [InlineData("41000000", null)]
    public void Bullet_char_maps_cp1252_to_unicode(string input, string? expected) =>
        Assert.Equal(expected, CorelJson.MapBulletChar(input));

    private static byte[] Read(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
