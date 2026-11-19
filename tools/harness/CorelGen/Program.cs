// Bộ tạo mẫu phức tạp (chỉ dùng khi phát triển, KHÔNG phải phụ thuộc của C_Converter).
// Điều khiển CorelDRAW 2026 qua COM (interop có kiểu từ VGCoreAuto.tlb: lib/Interop.VGCore.dll, tạo bằng TlbImp),
// vẽ từng mẫu rồi "Save As" ra mọi phiên bản: <tên>_27.cdr (2026 = bản gốc) … <tên>_17.cdr (X7 = đáp án).
//
//   dotnet run -c Release -- <thư mục ra> [tên mẫu …]
using System.Runtime.InteropServices;
using VGCore;
using CorelGen;


string outDir = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("..", "..", "..", "samples", "x26"));
Directory.CreateDirectory(outDir);
var only = args.Skip(1).ToHashSet();
var gen = new Samples(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "samples", "bmp", "assets")));
int[] versions = [27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17];
foreach (var (name, build) in gen.All())
{
    if (only.Count > 0 && !only.Contains(name)) continue;
    Document doc = null;
    try
    {
        doc = gen.App.CreateDocument();
        doc.Unit = cdrUnit.cdrMillimeter;
        doc.ActivePage.SetSize(210, 297);
        gen.Failed.Clear();
        build(doc, doc.ActiveLayer);
        foreach (int v in versions)
        {
            string f = Path.Combine(outDir, $"{name}_{v}.cdr");
            File.Delete(f);
            var o = gen.App.CreateStructSaveAsOptions();
            o.Version = (cdrFileVersion)v;
            doc.SaveAs(f, o);
        }
        Console.WriteLine($"OK    {name}" + (gen.Failed.Count > 0 ? "   (bỏ qua: " + string.Join("; ", gen.Failed) + ")" : ""));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"LỖI  {name}: {ex.Message}");
    }
    finally
    {
        try { doc?.Close(); } catch { }
    }
}
