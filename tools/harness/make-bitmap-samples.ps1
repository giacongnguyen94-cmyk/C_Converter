# Bộ kiểm thử ảnh (chỉ dùng khi phát triển): điều khiển CorelDRAW 2026 qua COM, đặt ảnh TIF/PSD/PNG/JPG/BMP/GIF
# và các xử lý bitmap vào tài liệu, rồi lưu ra <tên>_27.cdr (2026), <tên>_25.cdr (2024), <tên>_21.cdr (2019 = đáp án).
# Ảnh nguồn: samples/bmp/assets (Pillow + tools/harness/make_psd.py).
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\..\samples\bmp'),
    [string]$ProgId = 'CorelDRAW.Application.27',
    [int[]]$Versions = @(27, 25, 21),
    [string[]]$Only
)
$ErrorActionPreference = 'Stop'
$OutDir = (Resolve-Path $OutDir).Path
$AssetDir = Join-Path $OutDir 'assets'
$app = New-Object -ComObject $ProgId
$app.Visible = $true
function Rgb($r, $g, $b) { $app.CreateRGBColor($r, $g, $b) }
function Imp($L, $file, [bool]$link = $false) {
    $o = $app.CreateStructImportOptions()
    if ($link) { $o.LinkBitmapExternally = $true }
    $L.Import((Join-Path $AssetDir $file), 0, $o)
    $s = $app.ActiveSelectionRange
    if ($s.Count -gt 0) { return $s.Item(1) } else { return $L.Shapes.Item(1) }
}

$samples = [ordered]@{
    'B01_tif'         = { param($d, $L) Imp $L 'rgb.tif' | Out-Null }
    'B02_tif_lzw'     = { param($d, $L) Imp $L 'rgb_lzw.tif' | Out-Null }
    'B03_tif_zip'     = { param($d, $L) Imp $L 'rgb_zip.tif' | Out-Null }
    'B04_tif_cmyk'    = { param($d, $L) Imp $L 'cmyk.tif' | Out-Null }
    'B05_tif_gray'    = { param($d, $L) Imp $L 'gray.tif' | Out-Null }
    'B06_tif_bw'      = { param($d, $L) Imp $L 'bw.tif' | Out-Null }
    'B07_tif_alpha'   = { param($d, $L) Imp $L 'rgba.tif' | Out-Null }
    'B08_tif_big'     = { param($d, $L) Imp $L 'big.tif' | Out-Null }
    'B09_psd_layers'  = { param($d, $L) Imp $L 'layers.psd' | Out-Null }
    'B10_psd_single'  = { param($d, $L) Imp $L 'single.psd' | Out-Null }
    'B11_psd_cmyk'    = { param($d, $L) Imp $L 'cmyk.psd' | Out-Null }
    'B12_psd_gray'    = { param($d, $L) Imp $L 'gray.psd' | Out-Null }
    'B13_png_alpha'   = { param($d, $L) Imp $L 'rgba.png' | Out-Null }
    'B14_png_16bit'   = { param($d, $L) Imp $L 'gray16.png' | Out-Null }
    'B15_jpg'         = { param($d, $L) Imp $L 'photo.jpg' | Out-Null }
    'B16_jpg_cmyk'    = { param($d, $L) Imp $L 'cmyk.jpg' | Out-Null }
    'B17_bmp'         = { param($d, $L) Imp $L 'rgb.bmp' | Out-Null }
    'B18_bmp_pal'     = { param($d, $L) Imp $L 'pal16.bmp' | Out-Null }
    'B19_gif'         = { param($d, $L) Imp $L 'pal.gif' | Out-Null }
    'B20_linked'      = { param($d, $L) Imp $L 'rgb.tif' $true | Out-Null }
    'B21_rotated'     = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.Rotate(25); $b.Skew(10, 0) }
    'B22_scaled'      = { param($d, $L) $b = Imp $L 'rgb.tif'; $b.SetSize(150, 40) }
    'B23_powerclip'   = { param($d, $L) $b = Imp $L 'rgb.tif'; $c = $L.CreateEllipse2(60, 40, 40, 30); $b.AddToPowerClip($c) | Out-Null }
    'B24_transp'      = { param($d, $L) $r = $L.CreateRectangle2(0, 0, 150, 100); $r.Fill.UniformColor.RGBAssign(255, 0, 0); $b = Imp $L 'photo.jpg'; $b.Transparency.ApplyUniformTransparency(50) | Out-Null }
    'B25_shadow'      = { param($d, $L) $b = Imp $L 'rgba.png'; $b.CreateDropShadow(1, 60, 10, 3, -3, (Rgb 0 0 0), 0, 0, 0, 0, 0, 3) | Out-Null }
    'B26_invert'      = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.ApplyEffectInvert() }
    'B28_to_gray'     = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.Bitmap.ConvertTo(2) | Out-Null }
    'B29_to_cmyk'     = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.Bitmap.ConvertTo(5) | Out-Null }
    'B30_to_bw'       = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.Bitmap.ConvertTo(0) | Out-Null }
    'B31_resample'    = { param($d, $L) $b = Imp $L 'rgb.tif'; $b.Bitmap.Resample(160, 120, $true, 150, 150) }
    'B32_crop'        = { param($d, $L) $b = Imp $L 'rgb.tif'; $b.Bitmap.Crop() }
    'B33_vector_to_bmp' = { param($d, $L) $a = $L.CreateEllipse2(80, 80, 40, 30); $a.Fill.ApplyFountainFill((Rgb 255 0 0), (Rgb 0 0 255), 1, 45) | Out-Null; $a.ConvertToBitmapEx(5, $false, $true, 300, 1, $true, $false, 95) | Out-Null }
    'B34_group_to_bmp' = { param($d, $L) $a = $L.CreateRectangle2(10, 10, 60, 40); $a.Fill.UniformColor.RGBAssign(0, 200, 0); $t = $L.CreateArtisticText(15, 30, 'Chữ'); $r = $app.CreateShapeRange(); $r.Add($a); $r.Add($t); $r.Group().ConvertToBitmapEx(4, $false, $true, 200, 1, $true, $false, 95) | Out-Null }
    'B35_bitmap_fill' = { param($d, $L) $a = $L.CreateRectangle2(10, 10, 150, 100); $a.Fill.ApplyPatternFill(3, [string](Join-Path $AssetDir 'photo.jpg'), 0, (Rgb 0 0 0), (Rgb 255 255 255), $false) | Out-Null }
    'B36_lens_over'   = { param($d, $L) Imp $L 'photo.jpg' | Out-Null; $c = $L.CreateEllipse2(40, 30, 25, 25); $c.CreateLens(2, 50, (Rgb 255 0 0), (Rgb 0 0 0), 0) | Out-Null }
    'B37_many'        = { param($d, $L) $i = 0; foreach ($f in 'rgb.tif', 'cmyk.tif', 'layers.psd', 'rgba.png', 'photo.jpg', 'rgb.bmp', 'pal.gif', 'gray.tif') { $b = Imp $L $f; $i++; $b.Move($i * 8, $i * 15) } }
    'B38_dup'         = { param($d, $L) $b = Imp $L 'rgb.tif'; for ($i = 1; $i -le 5; $i++) { $b.Duplicate($i * 20, $i * 20) | Out-Null } }
    'B40_bci'         = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.ApplyEffectBCI(20, -10, 15) }
    'B41_hsl'         = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.ApplyEffectHSL(30, 20, 0) }
    'B42_balance'     = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.ApplyEffectColorBalance(20, -20, 10, $true, $true, $true, $false) }
    'B43_posterize'   = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.ApplyEffectPosterize(4) }
    'B44_gamma'       = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.ApplyEffectGamma(1.8) }
    'B45_duotone'     = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.Bitmap.ConvertTo(2) | Out-Null; $b.Bitmap.ConvertToDuotone((New-Object -ComObject CorelDRAW.Duotone -ErrorAction SilentlyContinue)) | Out-Null }
    'B46_paletted'    = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.Bitmap.ConvertToPaletted(0, 0, 50, 16) | Out-Null }
    'B47_inflate'     = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.Bitmap.Inflate(20, 20) }
    'B48_trace'       = { param($d, $L) $b = Imp $L 'rgb.tif'; $t = $b.Bitmap.Trace(1); $t.Finish() | Out-Null }
    'B49_effects_stack' = { param($d, $L) $b = Imp $L 'layers.psd'; $b.ApplyEffectInvert(); $b.ApplyEffectBCI(10, 10, 10); $b.ApplyEffectPosterize(6) }
    'B50_vector_invert' = { param($d, $L) $a = $L.CreateRectangle2(10, 10, 100, 60); $a.Fill.UniformColor.RGBAssign(0, 120, 255); $a.ApplyEffectInvert() }
    'B51_text_on_bmp' = { param($d, $L) $b = Imp $L 'cmyk.tif'; $b.SetSize(180, 120); $t = $L.CreateArtisticText(20, 60, 'Chữ trên ảnh'); $t.Text.Story.Size = 40; $t.Transparency.ApplyUniformTransparency(40) | Out-Null }
    'B39_complex'     = { param($d, $L)
        $g = $L.CreateRectangle2(0, 200, 210, 97); $g.Fill.ApplyFountainFill((Rgb 255 240 200), (Rgb 255 160 0), 1, 90) | Out-Null
        $t = $L.CreateArtisticText(20, 260, 'CÔNG TY TNHH ĐẠI PHÁT'); $t.Text.Story.Font = 'Arial'; $t.Text.Story.Size = 28; $t.CreateDropShadow(1, 40, 3, 2, -2, (Rgb 0 0 0), 0, 0, 0, 0, 0, 3) | Out-Null
        $p = Imp $L 'layers.psd'; $p.Move(10, 100)
        $b = Imp $L 'cmyk.tif'; $c = $L.CreateEllipse2(150, 120, 40, 40); $b.AddToPowerClip($c) | Out-Null
        $j = Imp $L 'photo.jpg'; $j.Rotate(-15); $j.Transparency.ApplyUniformTransparency(30) | Out-Null
        $L2 = $d.ActivePage.CreateLayer('Lớp ảnh 2'); $q = Imp $L2 'rgba.png'; $q.CreateDropShadow(1, 60, 10, 3, -3, (Rgb 0 0 0), 0, 0, 0, 0, 0, 3) | Out-Null
        $L2.CreateParagraphText(20, 60, 190, 10, "Đoạn văn bản tiếng Việt: ăâđêôơư.`rDòng 2.") | Out-Null
        $d.AddPages(1) | Out-Null; $p2 = $d.Pages.Item(2); $p2.Activate(); $x = Imp $p2.ActiveLayer 'big.tif'; $x.SetSize(180, 120)
        $y = Imp $p2.ActiveLayer 'gray.psd'; $y.ApplyEffectInvert(); $z = Imp $p2.ActiveLayer 'photo.jpg'; $z.ApplyEffectBCI(20, 20, 10) }
}

$results = @()
foreach ($name in $samples.Keys) {
    if ($Only -and $name -notin $Only) { continue }
    try {
        $doc = $app.CreateDocument()
        $doc.Unit = 3
        $doc.ActivePage.SetSize(210, 297)
        & $samples[$name] $doc $doc.ActiveLayer
        foreach ($v in $Versions) {
            $f = Join-Path $OutDir "${name}_$v.cdr"
            Remove-Item $f -ErrorAction SilentlyContinue
            $o = $app.CreateStructSaveAsOptions(); $o.Version = $v; $doc.SaveAs($f, $o)
        }
        $doc.Close()
        $results += "OK    $name"
    } catch {
        $results += "LỖI  $name : $($_.Exception.Message)"
        try { $doc.Close() } catch {}
    }
    Write-Host $results[-1]
}
