using System.Buffers.Binary;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CConvert.Core;

public enum LogLevel { Info, Warning, Error }

public sealed record LogEntry(LogLevel Level, string Message)
{
    public override string ToString() => $"[{Level switch { LogLevel.Info => "INFO", LogLevel.Warning => "CẢNH BÁO", _ => "LỖI" }}] {Message}";
}

public sealed class DowngradeResult
{
    public List<LogEntry> Log { get; } = new();
    public bool Success => Log.All(l => l.Level != LogLevel.Error);
    public bool HasWarnings => Log.Any(l => l.Level == LogLevel.Warning);

    /// <summary>Nguồn mới hơn 2024, chuyển bằng chế độ thử nghiệm (suy luận, chưa kiểm chứng bằng mẫu).</summary>
    public bool Experimental { get; internal set; }

    /// <summary>Chế độ nghiêm: điều chưa từng gặp trong mẫu là lỗi (không ghi file) thay vì cảnh báo.</summary>
    internal bool Strict { get; set; }

    /// <summary>Khóa JSON lạ đã bỏ trong chế độ thử nghiệm.</summary>
    internal SortedSet<string> UnknownKeys { get; } = new(StringComparer.Ordinal);

    internal void Info(string m) => Log.Add(new(LogLevel.Info, m));
    internal void Warn(string m) => Log.Add(new(LogLevel.Warning, m));
    internal void Error(string m) => Log.Add(new(LogLevel.Error, m));

    /// <summary>Điều chưa có trong mẫu đối chiếu: lỗi ở chế độ nghiêm, cảnh báo ở chế độ thường.</summary>
    internal void Problem(string m)
    {
        if (Strict) Error(m + " (chế độ thử nghiệm không ghi file khi gặp điều chưa biết)");
        else Warn(m);
    }
}

public sealed record DowngradeOptions
{
    /// <summary>Chỉ dùng cho kiểm thử: xử lý cả nguồn đã kiểm chứng bằng logic thử nghiệm.</summary>
    public bool ForceExperimental { get; init; }
}

/// <summary>
/// Hạ phiên bản .cdr dạng ZIP (root.dat) từ 2020–2024 xuống 2019.
///
/// Đây KHÔNG phải sửa số phiên bản: mỗi thành phần khác nhau giữa các phiên bản được chuyển theo
/// quy tắc rút ra từ việc so sánh với file do chính CorelDRAW "Save As" (docs/PhatHien-01-T00-T01.md).
/// Phạm vi kiểm chứng hiện tại rất hẹp (trang trống + hình chữ nhật), nên mọi chunk/kiểu đối tượng
/// chưa từng gặp trong mẫu đều bị cảnh báo thay vì im lặng chép qua.
/// </summary>
public static class Downgrader
{
    // Chunk lá, LIST và kiểu bản ghi loda đã gặp trong mẫu và đã xác nhận giống hệt nhau giữa 2019 và 2020–2024.
    private static readonly HashSet<string> VerifiedLeaves =
    [
        "fver", "vrsn", "mcfg", "pref", "ptrt", "fild", "outl", "stlt", "flgs", "spid", "bbox", "obbx", "loda", "usdn",
        "ftil", "trfd", "ttil", "arrx", "font", "txsm", "txtj", "spnd", "bmp ", "digk", "lnkt", "envd", "sblk", "symi", "syms",
        "udfe", "udta", "udid", // user data (Object Data) — kiểm chứng bằng file thật
    ];
    private static readonly HashSet<string> VerifiedLists =
    [
        "doc", "filt", "filc", "otlt", "page", "gobj", "layr", "lgob", "obj", "trfl", "grp", "clpt", "lnkg", "arrt", "fntt",
        "bmpt", "symb", "syse", "syst", "sytr", "udaf", "udat", "udte",
    ];
    // 0 nhóm/trang/layer, 1 chữ nhật, 2 ellipse, 3 đường/curve, 4 artistic text, 5 bitmap, 6 paragraph text,
    // 8 extrude, 11/12/17 bản ghi layer, 15 contour, 20 polygon/star, 23 đổ bóng.
    private static readonly HashSet<uint> VerifiedLodaTypes = [0, 1, 2, 3, 4, 5, 6, 8, 11, 12, 15, 17, 20, 23];


    public static DowngradeResult Run(string inputPath, string outputPath, CorelVersion target, DowngradeOptions? options = null)
    {
        var r = new DowngradeResult();
        options ??= new DowngradeOptions();
        try
        {
            if (target != CorelVersion.V2019)
            {
                r.Error($"Chưa hỗ trợ phiên bản đích {target}. Hiện chỉ hỗ trợ {CorelVersion.V2019}.");
                return r;
            }

            var pkg = CdrPackage.Load(inputPath);
            var mime = pkg.Find("mimetype");
            if (mime is null || Encoding.ASCII.GetString(mime.Data) != CdrPackage.MimeType)
            {
                r.Error("Không phải file CorelDRAW dạng ZIP (mimetype không khớp). Bản X6 trở về trước chưa được hỗ trợ.");
                return r;
            }

            var rootEntry = pkg.Get("content/root.dat");
            var root = new RootDat(rootEntry.Data);
            var dataEntries = Encoding.UTF8.GetString(pkg.Get("content/dataFileList.dat").Data)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => pkg.Get("content/data/" + n))
                .ToList();
            var dataFiles = dataEntries.Select(e => e.Data).ToList();

            ushort vrsn = BinaryPrimitives.ReadUInt16LittleEndian(
                root.Read(root.Find("vrsn") ?? throw new InvalidDataException("Thiếu chunk vrsn."), dataFiles));
            var source = CorelVersion.Recognize(vrsn);
            if (source is null)
            {
                r.Error($"Phiên bản nguồn không nhận ra (vrsn={vrsn}).");
                return r;
            }
            r.Info($"Nguồn: {source}; đích: {target}.");
            if (source.IsExperimental || options.ForceExperimental)
            {
                r.Experimental = r.Strict = true;
                r.Warn($"Nguồn {source} chưa có mẫu để kiểm chứng — CHẾ ĐỘ THỬ NGHIỆM: dùng chung quy tắc của 2020–2024, "
                     + "lọc JSON theo lược đồ 2019, và dừng (không ghi file) nếu gặp bản ghi chưa từng thấy. "
                     + "Hãy mở kiểm tra bằng CorelDRAW 2019 trước khi dùng.");
                if (!CheckExperimentalStructure(pkg, root, dataFiles, vrsn, r))
                    return r;
            }
            if (source.Vrsn <= target.Vrsn)
            {
                r.Error($"File đã ở {source}, không cần hạ xuống {target}.");
                return r;
            }
            if (source.Vrsn < CorelVersion.V2020.Vrsn)
            {
                r.Error($"Chưa hỗ trợ nguồn {source}.");
                return r;
            }

            Audit(root, dataFiles, r);
            root = RemoveInnerShadows(root, dataFiles, r);
            ConvertObjects(root, dataFiles, r);
            PatchRoot(root, dataFiles, target, r);
            if (root.HasReplacements)
            {
                var rebuilt = root.Rebuild(dataFiles);
                for (int i = 0; i < dataEntries.Count; i++)
                    dataEntries[i].Data = rebuilt[i];
            }
            rootEntry.Data = root.Bytes;
            PatchMetadata(pkg.Get("META-INF/metadata.xml"), target, r);
            PatchStyles(pkg.Get("styles/document.cdss"), target, r);
            if (r.UnknownKeys.Count > 0)
                r.Warn($"Thử nghiệm: đã bỏ {r.UnknownKeys.Count} loại khóa JSON không có trong CorelDRAW 2019: "
                     + string.Join(", ", r.UnknownKeys.Take(20)) + (r.UnknownKeys.Count > 20 ? ", …" : "")
                     + ". Thuộc tính tương ứng (nếu có dùng) sẽ mất trong bản 2019.");

            if (!r.Success)
                return r;
            pkg.Save(outputPath);
            r.Info($"Đã ghi {outputPath}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            r.Error(ex.Message);
        }
        catch (Exception ex)
        {
            // Dữ liệu ngoài dự kiến (vd chỉ số vượt biên) — báo lỗi cho file này, không làm dừng cả lô.
            r.Error($"Lỗi không mong đợi ({ex.GetType().Name}): {ex.Message}");
        }
        return r;
    }

    /// <summary>
    /// Nguồn mới hơn 2024: chỉ tiếp tục khi file có đúng cấu trúc đã thấy ở 2019–2024
    /// (ZIP + root.dat con trỏ + dataFileList + document.cdss + metadata.xml, fver/vrsn inline, mcfg dạng 2100).
    /// Khác cấu trúc nghĩa là Corel đã đổi định dạng — suy luận không còn giá trị.
    /// </summary>
    private static bool CheckExperimentalStructure(CdrPackage pkg, RootDat root, List<byte[]> dataFiles, ushort vrsn, DowngradeResult r)
    {
        var missing = new[] { "styles/document.cdss", "META-INF/metadata.xml", "META-INF/container.xml" }
            .Where(n => pkg.Find(n) is null).ToList();
        if (missing.Count > 0)
            r.Error("Cấu trúc file khác 2019–2024 (thiếu " + string.Join(", ", missing) + ") — không thể suy luận.");

        if (pkg.Find("META-INF/container.xml") is { } container
            && !Encoding.UTF8.GetString(container.Data).Contains("full-path=\"content/root.dat\""))
            r.Error("container.xml không trỏ tới content/root.dat như 2019–2024 — định dạng đã đổi.");

        if (root.Find("fver") is not { Inline: true, Length: 8 } fver
            || BinaryPrimitives.ReadUInt16LittleEndian(root.Read(fver, dataFiles)) != vrsn)
            r.Error("Chunk fver khác dạng 2019–2024 — định dạng đã đổi.");

        if (root.Find("mcfg") is { } mcfg && BinaryPrimitives.ReadUInt16LittleEndian(root.Read(mcfg, dataFiles)) != 2100)
            r.Error("Cấu hình tài liệu (mcfg) có phiên bản bản ghi mới — chưa biết cách chuyển.");

        // Đối số loda chưa từng gặp = thuộc tính đối tượng mới của bản mới hơn.
        var unknownArgs = root.Leaves.Where(l => l.FourCC == "loda")
            .Select(l => LodaRecord.TryParse(root.Read(l, dataFiles), out var rec) ? rec : null)
            .Where(rec => rec is not null)
            .SelectMany(rec => rec!.Args.Keys)
            .Where(k => !KnownLodaArgs.Contains(k))
            .Distinct().ToList();
        if (unknownArgs.Count > 0)
            r.Error("Đối tượng có thuộc tính chưa từng gặp (loại đối số " + string.Join(", ", unknownArgs.Select(k => $"0x{k:X}"))
                    + ") — có thể là tính năng mới của bản nguồn, chưa biết cách chuyển.");
        return r.Success;
    }

    // Kiểu đối số loda đã gặp trong mọi mẫu 2020–2024 và file thật (tools/analysis: thống kê 67 file).
    private static readonly HashSet<uint> KnownLodaArgs =
    [
        0x0A, 0x14, 0x1E, 0x64, 0xC9, 0x3E8, 0x7D0, 0xBB8, 0xDAC, 0x1F40, 0x1F45, 0x2AF8, 0x2EE0, 0x36B1, 0x3E81, 0x3E8F,
        0x4651, 0x4ABA, 0x4B01, 0x9C40, 0x9C41, 0x9C42, 0x9C68, 0x9C72, 0x9C90, 0x9CEA, 0x9D08,
    ];

    // Các dạng bản ghi hiệu ứng 0xBB8 đã biết: (loại đối tượng, phiên bản bản ghi, độ dài).
    private static readonly HashSet<(uint Obj, uint Version, int Length)> KnownEffectRecords =
    [
        (23, 2200, 127), // đổ bóng 2020+ (chuyển sang 1300)
        (23, 1300, 119), // đổ bóng dạng 2019
        (15, 1300, 185), // contour
    ];

    /// <summary>
    /// Bóng trong (inner shadow, có từ 2023) không tồn tại trong 2019 và làm 2019 crash khi mở.
    /// Corel 2024 tự raster hóa khi hạ; ở đây đơn giản hóa bằng cách gỡ nhóm hiệu ứng (lnkg),
    /// mục liên kết tương ứng trong 'lnkt' và 'spnd' không còn được dùng — đối tượng gốc giữ nguyên.
    /// </summary>
    private static RootDat RemoveInnerShadows(RootDat root, List<byte[]> dataFiles, DowngradeResult r)
    {
        var groups = new HashSet<int>();
        var linkIds = new HashSet<uint>();
        for (int i = 0; i < root.Lists.Count; i++)
        {
            if (root.Lists[i].Type != "lnkg") continue;
            var loda = root.Leaves.FirstOrDefault(l => l.FourCC == "loda" && l.Parent >= 0
                && root.Lists[l.Parent].Type == "lgob" && root.Lists[l.Parent].Parent == i);
            if (loda is null || !LodaRecord.TryParse(root.Read(loda, dataFiles), out var rec) || rec is null) continue;
            if (rec.ObjType != 23 || !rec.Args.TryGetValue(0xBB8, out byte[]? s) || s.Length < 0x1C
                || BinaryPrimitives.ReadUInt32LittleEndian(s.AsSpan(0x18)) != 4)
                continue;
            groups.Add(i);
            var spnd = root.Leaves.FirstOrDefault(l => l.FourCC == "spnd" && l.Parent == i);
            if (spnd is not null && root.Read(spnd, dataFiles) is { Length: 4 } id)
                linkIds.Add(BinaryPrimitives.ReadUInt32LittleEndian(id));
        }
        if (groups.Count == 0) return root;

        // Bỏ mục liên kết xuất phát từ các nhóm đã gỡ; nút đích không còn liên kết nào thì bỏ 'spnd' của nó.
        var lnkt = root.Find("lnkt");
        LinkTable? table = null;
        var orphanIds = new HashSet<uint>();
        if (lnkt is not null && LinkTable.TryParse(root.Read(lnkt, dataFiles), out table) && table is not null)
        {
            var removed = table.Entries.Where(e => linkIds.Contains(LinkTable.From(e))).ToList();
            table.Entries.RemoveAll(removed.Contains);
            foreach (uint to in removed.Select(LinkTable.To))
                if (!table.Entries.Any(e => LinkTable.From(e) == to || LinkTable.To(e) == to))
                    orphanIds.Add(to);
        }
        else if (lnkt is not null)
        {
            r.Error("Không đọc được bảng liên kết hiệu ứng (lnkt) — không thể gỡ bóng trong an toàn.");
            return root;
        }

        var leafIndex = root.Leaves.Select((l, idx) => (l, idx)).ToDictionary(x => x.l, x => x.idx);
        var dropLeaves = new HashSet<int>();
        foreach (var l in root.Leaves.Where(l => l.FourCC == "spnd"))
            if (root.Read(l, dataFiles) is { Length: 4 } v && orphanIds.Contains(BinaryPrimitives.ReadUInt32LittleEndian(v)))
                dropLeaves.Add(leafIndex[l]);
        if (lnkt is not null && table!.Entries.Count == 0)
            dropLeaves.Add(leafIndex[lnkt]);

        var result = root.Without(groups.Contains, dropLeaves.Contains);
        if (table is { Entries.Count: > 0 } && result.Find("lnkt") is { } newLnkt)
            result.Replace(newLnkt, table.ToBytes());
        r.Warn($"{groups.Count} bóng trong (inner shadow — tính năng 2023+) không có trong CorelDRAW 2019: đã gỡ hiệu ứng, đối tượng chính giữ nguyên. Nếu cần giữ bóng, hãy Convert to Bitmap trong 2024 trước khi hạ.");
        return result;
    }

    private static void Audit(RootDat root, List<byte[]> dataFiles, DowngradeResult r)
    {
        foreach (string t in root.ListTypes.Distinct().Where(t => !VerifiedLists.Contains(t)))
            r.Problem($"Nhóm chunk LIST '{t}' chưa có trong mẫu đối chiếu — chép nguyên, CorelDRAW 2019 có thể đọc sai.");

        foreach (var g in root.Leaves.Where(l => !VerifiedLeaves.Contains(l.FourCC)).GroupBy(l => l.FourCC))
            r.Problem($"Chunk '{g.Key}' (x{g.Count()}) chưa có trong mẫu đối chiếu — chép nguyên, CorelDRAW 2019 có thể đọc sai.");

        var unknownLoda = root.Leaves.Where(l => l.FourCC == "loda")
            .Select(l => root.Read(l, dataFiles))
            .Where(d => d.Length >= 20)
            .Select(d => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(16)))
            .Where(t => !VerifiedLodaTypes.Contains(t))
            .GroupBy(t => t);
        foreach (var g in unknownLoda)
            r.Problem($"Đối tượng loại {g.Key} (x{g.Count()}) chưa có trong mẫu đối chiếu — chép nguyên.");

        // Font biến thể (OpenType 'fvar'): 2019 không hỗ trợ trục biến thể. Corel 2024 tự chuyển chữ thành curves
        // khi hạ; ta giữ chữ nhưng cảnh báo (hiển thị trong 2019 có thể khác: độ đậm/độ rộng mặc định).
        var fonts = root.Leaves.Where(l => l.FourCC == "txsm")
            .SelectMany(l => Regex.Matches(Encoding.UTF8.GetString(root.Read(l, dataFiles)), "\"font\": \"([^\"]+)\"").Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (string font in fonts)
            if (FontCatalog.IsVariable(font) == true)
                r.Warn($"Văn bản dùng font biến thể '{font}' — CorelDRAW 2019 không hỗ trợ, chữ có thể hiển thị khác bản gốc. Nên Convert to Curves trong 2024 trước khi hạ.");

        // mcfg: bản ghi dạng 2019+ có header u16 = 2100 (2018 trở xuống là 1300 và dài hơn 379 byte).
        if (root.Find("mcfg") is { } mcfg)
        {
            ushort recVer = BinaryPrimitives.ReadUInt16LittleEndian(root.Read(mcfg, dataFiles));
            if (recVer != 2100)
                r.Warn($"mcfg có phiên bản bản ghi {recVer}, khác 2100 như mọi mẫu 2019–2024.");
        }
    }

    /// <summary>
    /// Chuyển các bản ghi đối tượng (loda) theo quy tắc rút ra từ so sánh với file Corel tự Save As 2019
    /// (docs/PhatHien-02-loda.md):
    ///  1. 2019 luôn có đối số style (0xC9); 2020+ bỏ qua khi rỗng → thêm "{}".
    ///     Với bản ghi của trang, đồng thời xóa bit 0x40 ở byte đầu của 'flgs' trang.
    ///  2. JSON style: bỏ các khóa chỉ có từ 2020 (font_axes, bullet nhiều cấp).
    ///  3. Đổ bóng (loại 23), đối số 0xBB8: bản ghi phiên bản 2200 → 1300 (bỏ 2 trường u32).
    /// </summary>
    private static void ConvertObjects(RootDat root, List<byte[]> dataFiles, DowngradeResult r)
    {
        int addedStyle = 0, strippedStyle = 0, shadows = 0;
        foreach (var leaf in root.Leaves.Where(l => l.FourCC == "loda").ToList())
        {
            byte[] raw = root.Read(leaf, dataFiles);
            if (!LodaRecord.TryParse(raw, out var rec) || rec is null)
            {
                r.Warn($"Không đọc được bản ghi đối tượng tại {leaf.Path} — chép nguyên.");
                continue;
            }

            bool changed = false;
            bool hasStyle = rec.Args.TryGetValue(LodaRecord.StyleArg, out byte[]? style);
            if (!hasStyle && ContainersNeedingStyle.Contains(ContainerOf(root, leaf)))
            {
                rec.Args[LodaRecord.StyleArg] = LodaRecord.MakeStyle("{}");
                addedStyle++;
                changed = true;
                ClearPageFlag(root, leaf, dataFiles);
            }
            else if (hasStyle && LodaRecord.TryReadStyle(style!, out string json) && json.Length > 2)
            {
                var node = JsonNode.Parse(json);
                if (StripJson(node, r) > 0)
                {
                    rec.Args[LodaRecord.StyleArg] = LodaRecord.MakeStyle(CorelJson.ToCompact(node!));
                    strippedStyle++;
                    changed = true;
                }
            }

            if (r.Strict && rec.Args.TryGetValue(0xBB8, out byte[]? effect) && effect.Length >= 4
                && !KnownEffectRecords.Contains((rec.ObjType, BinaryPrimitives.ReadUInt32LittleEndian(effect), effect.Length)))
                r.Error($"Hiệu ứng (đối tượng loại {rec.ObjType}) có bản ghi dạng {BinaryPrimitives.ReadUInt32LittleEndian(effect)}/{effect.Length} byte chưa từng gặp — chưa biết cách chuyển.");

            if (rec.ObjType == 23 && rec.Args.TryGetValue(0xBB8, out byte[]? shadow)
                && BinaryPrimitives.ReadUInt32LittleEndian(shadow) == 2200)
            {
                if (shadow.Length == 127)
                {
                    var s = new List<byte>(shadow);
                    s.RemoveRange(0x7B, 4);
                    s.RemoveRange(0x1D, 4);
                    byte[] converted = s.ToArray();
                    BinaryPrimitives.WriteUInt32LittleEndian(converted, 1300);
                    rec.Args[0xBB8] = converted;
                    shadows++;
                    changed = true;
                }
                else
                {
                    r.Error($"Đổ bóng có bản ghi dài {shadow.Length} byte (mẫu chỉ có 127) — chưa biết cách chuyển, CorelDRAW 2019 sẽ không mở được file.");
                }
            }

            if (changed)
                root.Replace(leaf, rec.ToBytes());
        }
        int texts = 0;
        foreach (var leaf in root.Leaves.Where(l => l.FourCC == "txsm").ToList())
        {
            var converted = TxsmConverter.Convert(root.Read(leaf, dataFiles), r.Warn, r.Problem,
                r.Strict ? node => CorelJson.StripUnknownKeys(node, r.UnknownKeys) : null);
            if (converted is null) continue;
            root.Replace(leaf, converted);
            texts++;
        }
        if (texts > 0)
            r.Info($"Text: chuyển {texts} khối text sang dạng 2019.");
        var parts = new List<string>();
        if (addedStyle > 0) parts.Add($"thêm style rỗng cho {addedStyle} bản ghi");
        if (strippedStyle > 0) parts.Add($"bỏ khóa 2020+ khỏi style của {strippedStyle} đối tượng");
        if (shadows > 0) parts.Add($"chuyển {shadows} đổ bóng");
        if (parts.Count > 0)
            r.Info("Đối tượng: " + string.Join(", ", parts) + ".");
    }

    /// <summary>Quy tắc 2020–2024 đã kiểm chứng; ở chế độ thử nghiệm thêm lọc theo lược đồ 2019.</summary>
    private static int StripJson(JsonNode? node, DowngradeResult r)
    {
        int n = CorelJson.StripPost2019Keys(node, r.Warn);
        if (r.Strict) n += CorelJson.StripUnknownKeys(node, r.UnknownKeys);
        return n;
    }

    // Loại LIST chứa (cha của lgob) mà Corel 2019 thêm style rỗng khi thiếu. Layer ('layr') thì không.
    private static readonly HashSet<string> ContainersNeedingStyle = ["page", "grp", "lnkg"];

    /// <summary>Loại LIST chứa đối tượng của một chunk trong lgob (vd 'page', 'obj', 'layr').</summary>
    private static string ContainerOf(RootDat root, RootDat.Leaf leaf)
    {
        if (leaf.Parent < 0 || root.Lists[leaf.Parent].Type != "lgob") return "";
        int c = root.Lists[leaf.Parent].Parent;
        return c < 0 ? "" : root.Lists[c].Type;
    }

    /// <summary>Bản ghi trang (page/lgob/loda) vừa được thêm style → xóa bit 0x40 trong flgs của trang.</summary>
    private static void ClearPageFlag(RootDat root, RootDat.Leaf loda, List<byte[]> dataFiles)
    {
        if (loda.Parent < 0) return;
        int page = root.Lists[loda.Parent].Parent;
        if (root.Lists[loda.Parent].Type != "lgob" || page < 0 || root.Lists[page].Type != "page") return;
        var flgs = root.Leaves.FirstOrDefault(l => l.FourCC == "flgs" && l.Parent == page);
        if (flgs is null) return;
        byte[] f = root.Read(flgs, dataFiles);
        if (f.Length == 4 && (f[0] & 0x40) != 0)
        {
            f[0] &= 0xBF;
            root.Replace(flgs, f);
        }
    }


    private static void PatchRoot(RootDat root, List<byte[]> dataFiles, CorelVersion target, DowngradeResult r)
    {
        root.FormChar = target.FormChar;

        var fver = root.Find("fver");
        var vrsn = root.Find("vrsn");
        if (fver is not { Inline: true, Length: 8 } || vrsn is not { Inline: true, Length: 2 })
        {
            r.Error("fver/vrsn không ở dạng inline như mẫu.");
            return;
        }
        root.WriteInline(fver, target.FverBytes());
        Span<byte> v = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(v, target.Vrsn);
        root.WriteInline(vrsn, v);
        r.Info($"root.dat: form type 'CDR{target.FormChar}', fver/vrsn = {target.Vrsn}.");
    }

    private static void PatchMetadata(CdrPackage.Entry entry, CorelVersion target, DowngradeResult r)
    {
        string xml = Encoding.UTF8.GetString(entry.Data);
        xml = ReplaceOnce(xml, @"(?<=<cdr:CoreVersion>)\d+(?=</cdr:CoreVersion>)", target.Vrsn.ToString(), "CoreVersion", r);
        xml = ReplaceOnce(xml, @"(?<=<inTxt:DocFormatterId>)\d+(?=</inTxt:DocFormatterId>)", target.DocFormatterId.ToString(), "DocFormatterId", r);
        entry.Data = Encoding.UTF8.GetBytes(xml);
    }

    private static string ReplaceOnce(string text, string pattern, string value, string what, DowngradeResult r)
    {
        int n = Regex.Matches(text, pattern).Count;
        if (n != 1)
            r.Warn($"metadata.xml: tìm thấy {n} lần '{what}' (mong đợi 1).");
        return Regex.Replace(text, pattern, value);
    }

    private static void PatchStyles(CdrPackage.Entry entry, CorelVersion target, DowngradeResult r)
    {
        string text = Encoding.UTF8.GetString(entry.Data);
        var doc = JsonNode.Parse(text)?.AsObject() ?? throw new InvalidDataException("document.cdss rỗng.");
        doc["version"] = (int)target.Vrsn;

        int removed = StripJson(doc, r);

        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        string json = CorelJson.ToIndented(doc, newline);
        if (text.EndsWith(newline) && !json.EndsWith(newline))
            json += newline;
        entry.Data = Encoding.UTF8.GetBytes(json);
        r.Info($"document.cdss: version = {target.Vrsn}, bỏ {removed} khóa chỉ có từ 2020 (font_axes, bullet).");
    }
}
