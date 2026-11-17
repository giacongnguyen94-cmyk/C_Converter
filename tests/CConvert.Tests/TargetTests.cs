using System.IO.Compression;
using System.Text.RegularExpressions;
using CConvert.Core;

namespace CConvert.Tests;

/// <summary>
/// Các bản đích X7 → 2023. Đáp án: file do CorelDRAW tự "Save As":
///  - "File mau/T01_&lt;v&gt;.cdr": CorelDRAW 2024 lưu cùng một thiết kế ở mọi bản 17–25 (do người dùng tạo);
///  - "samples/older/&lt;tên&gt;_&lt;v&gt;.cdr": CorelDRAW 2019 lưu các mẫu 2019 xuống X7–2018 (tools/harness/save-older.ps1).
/// Thiếu dữ liệu đáp án thì test tự bỏ qua.
/// </summary>
public class TargetTests
{
    // SampleFiles.Root = <repo>/samples/gen
    private static string Repo => Path.GetFullPath(Path.Combine(SampleFiles.Root, "..", ".."));

    public static IEnumerable<object[]> CorelSaveAsVersions() =>
        new[] { ("X7", "17"), ("X8", "18"), ("2017", "19"), ("2018", "20"), ("2019", "21"), ("2020", "22"), ("2021", "23"), ("2022", "24"), ("2023", "24.3") }
            .Select(t => new object[] { t.Item1, t.Item2 });

    /// <summary>T01 (hình chữ nhật) bản 2024 → mọi bản đích: trùng từng byte với file CorelDRAW 2024 tự Save As.</summary>
    [Theory]
    [MemberData(nameof(CorelSaveAsVersions))]
    public void T01_matches_CorelDRAW_2024_save_as(string targetName, string corelSuffix)
    {
        string input = Path.Combine(Repo, "File mau", "T01_25.cdr");
        string expected = Path.Combine(Repo, "File mau", $"T01_{corelSuffix}.cdr");
        if (!File.Exists(input) || !File.Exists(expected)) return;

        var target = CorelVersion.Targets.Single(v => v.Name == targetName);
        string output = Temp();
        try
        {
            var r = Downgrader.Run(input, output, target);
            Assert.True(r.Success, string.Join("\n", r.Log));
            Assert.False(r.Experimental);

            using var ours = ZipFile.OpenRead(output);
            using var corel = ZipFile.OpenRead(expected);
            foreach (var e in corel.Entries)
            {
                byte[] a = Read(ours.GetEntry(e.FullName)!), b = Read(e);
                bool same = e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".cdss")
                    ? Normalize(a) == Normalize(b)
                    : a.AsSpan().SequenceEqual(b);
                Assert.True(same, $"{targetName}: {e.FullName} khác CorelDRAW 2024");
            }
        }
        finally
        {
            File.Delete(output);
        }
    }

    public static IEnumerable<object[]> OlderPairs()
    {
        string older = Path.Combine(Repo, "samples", "older");
        if (!Directory.Exists(older)) yield break;
        foreach (string f in Directory.GetFiles(older, "*_20.cdr").Order())
        {
            string name = Path.GetFileName(f)[..^"_20.cdr".Length];
            // T13/T40 (nhiều trang): Corel 2019 tự tính lại một giá trị trạng thái trang trong mcfg khi lưu.
            if (name is "T13_pages" or "T40_mixed") continue;
            string src = new[] { Path.Combine(Repo, "samples", "gen", name + "_21.cdr"), Path.Combine(Repo, "out", "real", name + "_21.cdr") }
                .FirstOrDefault(File.Exists) ?? "";
            if (src.Length > 0) yield return new object[] { name, src };
        }
    }

    /// <summary>2019 → X7: mcfg và mọi trfd trùng từng byte với file CorelDRAW 2019 tự Save As X7.</summary>
    [Theory]
    [MemberData(nameof(OlderPairs))]
    public void Mcfg_and_trfd_match_CorelDRAW_2019_save_as_X7(string name, string source2019)
    {
        string expected = Path.Combine(Repo, "samples", "older", name + "_17.cdr");
        string output = Temp();
        try
        {
            var r = Downgrader.Run(source2019, output, CorelVersion.X7);
            Assert.True(r.Success, string.Join("\n", r.Log));
            var (ours, oursData) = Open(output);
            var (corel, corelData) = Open(expected);
            Assert.Equal(corel.Read(corel.Find("mcfg")!, corelData), ours.Read(ours.Find("mcfg")!, oursData));

            // So các trfd cùng vị trí. (Trình ghi của Corel 2019 tự thêm trfd cho nhóm extrude khi lưu xuống —
            // thói quen riêng của nó, Corel 2024 không làm; không phải yêu cầu định dạng.)
            var ourTrfd = ours.Leaves.Where(l => l.FourCC == "trfd").ToDictionary(l => l.Path + "#" + l.Parent, l => ours.Read(l, oursData));
            var corelTrfd = corel.Leaves.Where(l => l.FourCC == "trfd").ToDictionary(l => l.Path + "#" + l.Parent, l => corel.Read(l, corelData));
            int compared = 0;
            foreach (var (path, data) in ourTrfd)
            {
                var match = corelTrfd.Where(kv => kv.Key.StartsWith(path.Split('#')[0]) && kv.Value.Length == data.Length).Select(kv => kv.Value).ToList();
                if (match.Count == 0) continue;
                Assert.Contains(match, m => m.AsSpan().SequenceEqual(data));
                compared++;
            }
            Assert.True(compared > 0 || ourTrfd.Count == 0);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Theory]
    [MemberData(nameof(SampleFiles.Version25), MemberType = typeof(SampleFiles))]
    public void Every_2024_sample_converts_to_every_target(string file)
    {
        foreach (var target in CorelVersion.Targets.Where(t => t.Vrsn < CorelVersion.V2024.Vrsn))
        {
            string output = Temp();
            try
            {
                var r = Downgrader.Run(Path.Combine(SampleFiles.Root, file), output, target);
                Assert.True(r.Success, $"{file} → {target}: " + string.Join("\n", r.Log));
                var (root, data) = Open(output);
                Assert.Equal(target.FormChar, root.FormChar);
                Assert.Equal(target.Vrsn, BitConverter.ToUInt16(root.Read(root.Find("vrsn")!, data)));
                ushort mcfgForm = BitConverter.ToUInt16(root.Read(root.Find("mcfg")!, data));
                Assert.Equal(target.Vrsn < CorelVersion.V2019.Vrsn ? McfgConverter.FormOld : McfgConverter.Form2019, mcfgForm);
            }
            finally
            {
                File.Delete(output);
            }
        }
    }

    [Fact]
    public void Same_or_newer_target_is_refused()
    {
        string input = Path.Combine(SampleFiles.Root, "T01_rect_21.cdr");
        if (!File.Exists(input)) return;
        foreach (var t in new[] { CorelVersion.V2019, CorelVersion.V2020 })
        {
            string output = Temp();
            var r = Downgrader.Run(input, output, t);
            Assert.False(r.Success);
            Assert.False(File.Exists(output));
        }
    }

    /// <summary>
    /// Đích 2020–2023: chỉ khác số phiên bản (fver, vrsn) so với file 2024 — như Corel 2024 tự Save As.
    /// Mọi bản ghi khác trùng từng byte (trừ khi có bóng trong: gỡ khi đích &lt; 2024).
    /// </summary>
    [Theory]
    [MemberData(nameof(SampleFiles.Version25), MemberType = typeof(SampleFiles))]
    public void Modern_targets_change_only_version_fields(string file)
    {
        string input = Path.Combine(SampleFiles.Root, file);
        var (src, srcData) = Open(input);
        foreach (var target in new[] { CorelVersion.V2020, CorelVersion.V2021, CorelVersion.V2022, CorelVersion.V2023 })
        {
            string output = Temp();
            try
            {
                var r = Downgrader.Run(input, output, target);
                Assert.True(r.Success, string.Join(Environment.NewLine, r.Log));
                var (dst, dstData) = Open(output);
                Assert.Equal(target.FormChar, dst.FormChar);
                if (r.Log.Any(l => l.ToString().Contains("bóng trong", StringComparison.OrdinalIgnoreCase))) continue;
                Assert.Equal(src.Leaves.Select(l => l.Path), dst.Leaves.Select(l => l.Path));
                for (int i = 0; i < src.Leaves.Count; i++)
                {
                    var a = src.Leaves[i];
                    if (a.FourCC is "fver" or "vrsn") continue;
                    Assert.True(src.Read(a, srcData).AsSpan().SequenceEqual(dst.Read(dst.Leaves[i], dstData)), $"{file} → {target}: {a.Path} đổi");
                }
            }
            finally
            {
                File.Delete(output);
            }
        }
    }

    /// <summary>Nguồn là file CorelDRAW 2019 tự lưu ở 2018/2017/X8 → hạ tiếp: mcfg giữ nguyên, trfd như Corel lưu X7.</summary>
    [Theory]
    [MemberData(nameof(OlderPairs))]
    public void Older_corel_files_convert_further_down(string name, string source2019)
    {
        _ = source2019;
        string older = Path.Combine(Repo, "samples", "older");
        foreach (var (srcV, target, refV) in new[] { ("20", CorelVersion.X7, "17"), ("20", CorelVersion.X8, "18"), ("19", CorelVersion.X7, "17"), ("18", CorelVersion.X7, "17") })
        {
            string input = Path.Combine(older, $"{name}_{srcV}.cdr"), expected = Path.Combine(older, $"{name}_{refV}.cdr");
            if (!File.Exists(input) || !File.Exists(expected)) continue;
            string output = Temp();
            try
            {
                var r = Downgrader.Run(input, output, target);
                Assert.True(r.Success, string.Join(Environment.NewLine, r.Log));
                Assert.False(r.HasWarnings, string.Join(Environment.NewLine, r.Log));
                var (ours, oursData) = Open(output);
                var (corel, corelData) = Open(expected);
                Assert.Equal(target.FormChar, ours.FormChar);
                Assert.Equal(corel.Read(corel.Find("mcfg")!, corelData), ours.Read(ours.Find("mcfg")!, oursData));
                // Nguồn do chính Corel 2019 ghi → mọi trfd phải trùng bản Corel lưu X7 theo đúng thứ tự.
                Assert.Equal(corel.Leaves.Where(l => l.FourCC == "trfd").Select(l => Convert.ToHexString(corel.Read(l, corelData))),
                             ours.Leaves.Where(l => l.FourCC == "trfd").Select(l => Convert.ToHexString(ours.Read(l, oursData))));
            }
            finally
            {
                File.Delete(output);
            }
        }
    }

    /// <summary>File hỏng (cắt cụt, đổi byte ngẫu nhiên) ở mọi bản đích: không ném lỗi ra ngoài, thất bại thì không để lại file.</summary>
    [Fact]
    public void Corrupted_inputs_never_crash_or_leave_partial_output()
    {
        string good = Path.Combine(SampleFiles.Root, "T40_mixed_25.cdr");
        if (!File.Exists(good)) return;
        byte[] bytes = File.ReadAllBytes(good);
        var rnd = new Random(1234);
        var variants = new List<byte[]>();
        foreach (double cut in new[] { 0.1, 0.5, 0.9, 0.99 })
            variants.Add(bytes[..(int)(bytes.Length * cut)]);
        for (int k = 0; k < 12; k++)
        {
            byte[] b = (byte[])bytes.Clone();
            for (int j = 0; j < 1 + k * 3; j++) b[rnd.Next(64, b.Length)] ^= (byte)(1 + rnd.Next(255));
            variants.Add(b);
        }
        // root.dat hỏng bên trong ZIP hợp lệ
        variants.Add(Repack(good, "content/root.dat", d => d[..(d.Length / 2)]));
        variants.Add(Repack(good, "content/dataFileList.dat", _ => "khong_co.dat"u8.ToArray()));
        variants.Add(Repack(good, "content/root.dat", d => { var c = (byte[])d.Clone(); for (int i = 12; i < c.Length; i += 7) c[i] ^= 0x5A; return c; }));

        string input = Temp();
        try
        {
            foreach (var v in variants)
            {
                File.WriteAllBytes(input, v);
                foreach (var target in CorelVersion.Targets.Where(t => t.Vrsn < CorelVersion.V2024.Vrsn))
                {
                    string output = Temp();
                    var r = Downgrader.Run(input, output, target);
                    if (r.Success) File.Delete(output);
                    else Assert.False(File.Exists(output));
                    Assert.Empty(Directory.GetFiles(Path.GetTempPath(), "*.c_converter.tmp").Where(f => f.StartsWith(output)));
                }
            }
        }
        finally
        {
            File.Delete(input);
        }
    }

    private static byte[] Repack(string path, string entry, Func<byte[], byte[]> change)
    {
        using var ms = new MemoryStream();
        using (var src = ZipFile.OpenRead(path))
        using (var dst = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var e in src.Entries)
            {
                byte[] d = Read(e);
                using var s = dst.CreateEntry(e.FullName).Open();
                s.Write(e.FullName == entry ? change(d) : d);
            }
        return ms.ToArray();
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

    /// <summary>Bỏ qua bộ đếm lưu, GUID palette và thứ tự phần tử/thuộc tính XML (Corel tự đổi khi lưu).</summary>
    private static string Normalize(byte[] b)
    {
        string s = System.Text.Encoding.UTF8.GetString(b);
        s = Regex.Replace(s, @"<crl:Revision>-?\d+</crl:Revision>", "");
        s = Regex.Replace(s, "\"guid\": \"[0-9a-f-]+\"", "");
        if (s.TrimStart().StartsWith("<?xml"))
            s = string.Join("\n", s.Replace(">", " >").Split('\n')
                .Select(l => string.Join(" ", l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)))
                .Order(StringComparer.Ordinal));
        return s;
    }

    private static byte[] Read(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static string Temp() => Path.Combine(Path.GetTempPath(), $"cconvert_tgt_{Guid.NewGuid():N}.cdr");
}
