using System.Text;
using CConvert.Core;

// C_Converter (dòng lệnh)
//   CConvert.Cli <file.cdr> [...] [--to X7|X8|2017|2018|2019|2020|2021|2022|2023|2024] [--out <thư mục>]
// Mặc định hạ xuống CorelDRAW 2019. Không ghi đè file gốc: kết quả là <tên>_<đích>.cdr.

Console.OutputEncoding = Encoding.UTF8;

var inputs = new List<string>();
string? outDir = null;
var target = CorelVersion.V2019;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out": outDir = args[++i]; break;
        case "--to":
            string name = args[++i];
            var t = CorelVersion.Targets.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            if (t is null)
            {
                Console.Error.WriteLine($"Không biết phiên bản đích '{name}'. Hỗ trợ: {string.Join(", ", CorelVersion.Targets.Select(v => v.Name))}");
                return 2;
            }
            target = t;
            break;
        default: inputs.Add(args[i]); break;
    }
}
if (inputs.Count == 0)
{
    Console.Error.WriteLine("Cách dùng: CConvert.Cli <file.cdr> [...] [--to X7|X8|2017|2018|2019|2020|2021|2022|2023|2024] [--out <thư mục>]");
    return 2;
}

if (outDir is not null) Directory.CreateDirectory(outDir);
int failed = 0;
foreach (string input in inputs)
{
    string output = CdrInfo.OutputPathFor(input, target, outDir);
    Console.WriteLine($"=== {input}");
    var result = Downgrader.Run(input, output, target);
    foreach (var entry in result.Log)
        Console.WriteLine("  " + entry);
    Console.WriteLine(result.Success ? (result.HasWarnings ? "  → XONG (có cảnh báo)" : "  → XONG") : "  → THẤT BẠI");
    if (!result.Success) failed++;
}
return failed == 0 ? 0 : 1;
