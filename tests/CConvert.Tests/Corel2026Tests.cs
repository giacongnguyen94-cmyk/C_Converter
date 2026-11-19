using System.Buffers.Binary;
using System.Security.Cryptography;
using CConvert.Core;

namespace CConvert.Tests;

/// <summary>
/// Đáp án từ CorelDRAW 2026: mỗi mẫu có &lt;tên&gt;_27.cdr (2026) và file Corel tự Save As &lt;tên&gt;_&lt;v&gt;.cdr cho mọi bản
/// 26 (2025) … 17 (X7) — tools/harness/make-bitmap-samples.ps1, make-samples.ps1, make-complex-samples.ps1, CorelGen.
/// Thiếu dữ liệu thì test tự bỏ qua.
/// </summary>
public class Corel2026Tests
{
    private static string Repo => Path.GetFullPath(Path.Combine(SampleFiles.Root, "..", ".."));
    private static readonly string[] Folders = ["bmp_all", "gen27_all", "cplx", "x26"];
    private static readonly (string Name, string Suffix)[] Targets =
        [("2025", "26"), ("2024", "25"), ("2022", "24"), ("2021", "23"), ("2020", "22"), ("2019", "21"), ("2018", "20"), ("2017", "19"), ("X8", "18"), ("X7", "17")];

    // Corel tự sinh/đánh số lại khi lưu (GUID spid, ảnh raster hóa lại, chỉ số bảng tô trong loda/stlt…) hoặc
    // dựng lại hình học dẫn xuất (đường kích thước, đường nối); và các giới hạn đã biết (bóng trong, font biến thể,
    // bullet font ký hiệu, bóng Gaussian ở X7 — Corel raster hóa). Xem docs/PhatHien-04-corel2026.md.
    private static readonly HashSet<string> NoisyOrLimited =
    [
        "B09_psd_layers", "B37_many", "B48_trace", "B51_text_on_bmp", "C04_connector", "C05_connector_ra", "C14_guides",
        "C17_text_styles", "C32_dropcap_bullets", "C33_huge", "C34_symbols_many", "M01_poster", "M02_brochure", "M04_logo_set",
        "M05_photo_collage", "M06_infographic", "M07_everything", "T14_text", "T16_bitmap", "T32_inner_shadow",
        "T36_bullets", "T37_varfont", "X01_dimensions", "X07_shaping",
    ];

    public static IEnumerable<object[]> Designs()
    {
        foreach (string folder in Folders)
        {
            string d = Path.Combine(Repo, "samples", folder);
            if (!Directory.Exists(d)) continue;
            foreach (string f in Directory.GetFiles(d, "*_27.cdr").Order())
            {
                string design = Path.GetFileName(f)[..^"_27.cdr".Length];
                if (!NoisyOrLimited.Contains(design)) yield return new object[] { folder, design };
            }
        }
    }

    /// <summary>Hạ 2026 xuống mọi bản đích: mọi bản ghi trùng từng byte với file CorelDRAW 2026 tự Save As (bỏ qua GUID).</summary>
    [Theory]
    [MemberData(nameof(Designs))]
    public void Matches_CorelDRAW_2026_save_as_for_every_target(string folder, string design)
    {
        string dir = Path.Combine(Repo, "samples", folder), input = Path.Combine(dir, design + "_27.cdr");
        var failures = new List<string>();
        foreach (var (name, suffix) in Targets)
        {
            string expected = Path.Combine(dir, $"{design}_{suffix}.cdr");
            if (!File.Exists(expected)) continue;
            string output = Temp();
            try
            {
                var r = Downgrader.Run(input, output, CorelVersion.Targets.Single(t => t.Name == name));
                Assert.True(r.Success, string.Join(Environment.NewLine, r.Log));
                var ours = Leaves(output);
                var corel = Leaves(expected);
                var onlyOurs = ours.Except(corel).Where(k => !k.StartsWith("spid") && !k.StartsWith("bmp ")).ToList();
                var onlyCorel = corel.Except(ours).Where(k => !k.StartsWith("spid") && !k.StartsWith("bmp ")).ToList();
                if (onlyOurs.Count + onlyCorel.Count > 0)
                    failures.Add($"{name}: C_Converter {string.Join(",", onlyOurs.Select(k => k[..4]))} / Corel {string.Join(",", onlyCorel.Select(k => k[..4]))}");
            }
            finally
            {
                File.Delete(output);
            }
        }
        Assert.True(failures.Count == 0, design + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>Bóng đổ trên ảnh (làm mờ Gaussian) được giữ khi hạ xuống 2019, không bị nhận nhầm là bóng trong.</summary>
    [Fact]
    public void Drop_shadows_on_bitmaps_are_kept()
    {
        string input = Path.Combine(Repo, "samples", "x26", "M05_photo_collage_27.cdr");
        string expected = Path.Combine(Repo, "samples", "x26", "M05_photo_collage_21.cdr");
        if (!File.Exists(input) || !File.Exists(expected)) return;
        string output = Temp();
        try
        {
            var r = Downgrader.Run(input, output, CorelVersion.V2019);
            Assert.True(r.Success);
            Assert.DoesNotContain(r.Log, l => l.Message.Contains("bóng trong"));
            Assert.Equal(Count(expected, "lnkg"), Count(output, "lnkg"));
        }
        finally
        {
            File.Delete(output);
        }
    }

    /// <summary>X7 không có kiểu làm mờ Gaussian: đổi sang "Average" (3), bản ghi bóng vẫn ở dạng 1300.</summary>
    [Fact]
    public void Gaussian_shadow_feather_is_mapped_for_X7()
    {
        string input = Path.Combine(Repo, "samples", "x26", "M01_poster_27.cdr");
        if (!File.Exists(input)) return;
        string output = Temp();
        try
        {
            var r = Downgrader.Run(input, output, CorelVersion.X7);
            Assert.True(r.Success);
            Assert.Contains(r.Log, l => l.Message.Contains("Gaussian"));
            var (root, data) = Open(output);
            int shadows = 0;
            foreach (var l in root.Leaves.Where(l => l.FourCC == "loda"))
                if (LodaRecord.TryParse(root.Read(l, data), out var rec) && rec!.ObjType == 23
                    && rec.Args.TryGetValue(0xBB8, out var s) && s.Length >= 0x1C)
                {
                    Assert.Equal(1300u, BinaryPrimitives.ReadUInt32LittleEndian(s));
                    Assert.NotEqual(4, s[0x18]);
                    shadows++;
                }
            Assert.True(shadows > 0);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void Versions_2025_and_2026_are_recognized_from_real_files()
    {
        foreach (var (file, v) in new[] { ("B01_tif_26.cdr", CorelVersion.V2025), ("B01_tif_27.cdr", CorelVersion.V2026) })
        {
            string f = Path.Combine(Repo, "samples", "bmp_all", file);
            if (!File.Exists(f)) continue;
            Assert.Equal(v, CdrInfo.Probe(f).Version);
        }
    }

    private static (RootDat, List<byte[]>) Open(string path)
    {
        var pkg = CdrPackage.Load(path);
        var root = new RootDat(pkg.Get("content/root.dat").Data);
        var data = System.Text.Encoding.UTF8.GetString(pkg.Get("content/dataFileList.dat").Data)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => pkg.Get("content/data/" + n).Data).ToList();
        return (root, data);
    }

    private static int Count(string path, string listType) => Open(path).Item1.Lists.Count(l => l.Type == listType);

    private static List<string> Leaves(string path)
    {
        var (root, data) = Open(path);
        return root.Leaves.Where(l => l.FourCC is not ("fver" or "vrsn"))
            .Select(l => l.FourCC + Convert.ToHexString(MD5.HashData(root.Read(l, data)))).Order().ToList();
    }

    private static string Temp() => Path.Combine(Path.GetTempPath(), $"cconvert_26_{Guid.NewGuid():N}.cdr");
}
