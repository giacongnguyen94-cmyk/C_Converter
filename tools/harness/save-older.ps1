# Bộ kiểm thử (chỉ dùng khi phát triển, KHÔNG phải phụ thuộc của C_Converter):
# dùng CorelDRAW 2019 mở từng file 2019 (<tên>_21.cdr) và "Save As" xuống các bản cũ hơn
# → <tên>_20.cdr (2018), _19 (2017), _18 (X8), _17 (X7). Đây là đáp án cho bước 2019 → bản đích.
param(
    [Parameter(Mandatory)][string[]]$Files,
    [Parameter(Mandatory)][string]$OutDir,
    [int[]]$Versions = @(20, 19, 18, 17)
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

function New-Corel {
    Get-Process CorelDRW -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*Suite 2019*' -and $_.MainWindowHandle -eq 0 } | Stop-Process -Force
    Start-Sleep -Seconds 2
    New-Object -ComObject CorelDRAW.Application.21
}
$app = New-Corel

foreach ($f in $Files) {
    $full = (Resolve-Path $f).Path
    $base = [IO.Path]::GetFileNameWithoutExtension($full) -replace '_21$', ''
    foreach ($v in $Versions) {
        $dst = Join-Path $OutDir "${base}_$v.cdr"
        if (Test-Path $dst) { continue }
        try {
            $doc = $app.OpenDocument($full, 0)
            $o = $app.CreateStructSaveAsOptions(); $o.Version = $v
            $doc.SaveAs($dst, $o)
            $doc.Close()
            Write-Host "OK   ${base}_$v"
        } catch {
            Write-Host "LỖI  ${base}_$v : $($_.Exception.Message)"
            try { $doc.Close() } catch {}
            $app = New-Corel
        }
    }
}
