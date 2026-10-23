# Hồi quy đầy đủ (chỉ dùng khi phát triển, cần CorelDRAW 2024 + 2019 trên máy):
#   1. (tùy chọn) tạo lại mẫu bằng CorelDRAW 2024          -Regenerate
#   2. hạ mọi mẫu *_25.cdr xuống 2019 bằng C_Converter
#   3. so từng byte với file Corel tự Save As 2019 (*_21.cdr)
#   4. mở cả hai bằng CorelDRAW 2019 thật, so cấu trúc + ảnh
param(
    [string]$Samples = (Join-Path $PSScriptRoot '..\..\samples\gen'),
    [string]$Out = (Join-Path $PSScriptRoot '..\..\out\regression'),
    [switch]$Regenerate
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$env:PYTHONIOENCODING = 'utf-8'

if ($Regenerate) { & (Join-Path $PSScriptRoot 'make-samples.ps1') -OutDir $Samples }
$Samples = (Resolve-Path $Samples).Path
Remove-Item -Recurse -Force $Out -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$Out\converted", "$Out\verify" | Out-Null

dotnet build "$repo\src\CConvert.Cli" -c Release | Select-String ' error |Error\(s\)'
$cli = "$repo\src\CConvert.Cli\bin\Release\net8.0\CConvert.Cli.dll"
dotnet $cli (Get-ChildItem "$Samples\*_25.cdr").FullName --out "$Out\converted" | Out-File "$Out\convert.log" -Encoding utf8
Select-String -Path "$Out\convert.log" -Pattern 'THẤT BẠI|\[LỖI\]' | ForEach-Object { $_.Line }

Write-Host "`n== So từng byte với CorelDRAW (Save As 2019)"
python "$repo\tools\analysis\compare.py" $Samples "$Out\converted" | Select-Object -Last 1

Write-Host "`n== Mở bằng CorelDRAW 2019"
$files = @(Get-ChildItem "$Out\converted\*_2019.cdr") + @(Get-ChildItem "$Samples\*_21.cdr")
& (Join-Path $PSScriptRoot 'verify-2019.ps1') -Files ($files.FullName) -OutDir "$Out\verify" | Where-Object { $_ -notmatch 'mở OK' }
python "$repo\tools\analysis\compare_render.py" "$Out\verify" | Where-Object { $_ -notmatch '^GIỐNG' }
