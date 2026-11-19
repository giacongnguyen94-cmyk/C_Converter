# Bộ kiểm thử đối tượng phức tạp (chỉ dùng khi phát triển): điều khiển CorelDRAW 2026 qua COM,
# tạo bảng, đường kích thước, đường nối, chữ theo đường, blend, phối cảnh, biến dạng, màu pha, lớp trang chủ…
# rồi lưu ra <tên>_<v>.cdr cho mọi phiên bản trong $Versions (27 = 2026 là bản gốc; các bản khác = đáp án).
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\..\samples\cplx'),
    [string]$ProgId = 'CorelDRAW.Application.27',
    [int[]]$Versions = @(27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17),
    [string[]]$Only
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$AssetDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..\samples\bmp\assets')).Path
$app = New-Object -ComObject $ProgId
$app.Visible = $true
function Rgb($r, $g, $b) { $app.CreateRGBColor($r, $g, $b) }
function Imp($L, $file) {
    $L.Import((Join-Path $AssetDir $file), 0, $app.CreateStructImportOptions())
    $app.ActiveSelectionRange.Item(1)
}
function Box($L, $x, $y, $w, $h, $r, $g, $b) { $s = $L.CreateRectangle2($x, $y, $w, $h); $s.Fill.UniformColor.RGBAssign($r, $g, $b); $s }

$samples = [ordered]@{
    'C01_table'        = { param($d, $L) $L.CreateCustomShape('Table', 10, 150, 200, 250, 4, 3) | Out-Null }
    'C02_dim_linear'   = { param($d, $L) Box $L 20 20 100 50 0 150 255 | Out-Null; $L.CreateLinearDimension(0, $d.CreateFreeSnapPoint(20, 20), $d.CreateFreeSnapPoint(120, 20), $true, 70, 10) | Out-Null }
    'C03_dim_angular'  = { param($d, $L) $L.CreateAngularDimension($d.CreateFreeSnapPoint(50, 50), $d.CreateFreeSnapPoint(150, 50), $d.CreateFreeSnapPoint(120, 120), 120, 80) | Out-Null }
    'C04_connector'    = { param($d, $L) $a = Box $L 10 10 40 30 255 0 0; $b = Box $L 140 150 40 30 0 0 255; $L.CreateConnector($a.SnapPoints.Item(1), $b.SnapPoints.Item(1)) | Out-Null }
    'C05_connector_ra' = { param($d, $L) $a = Box $L 10 10 40 30 255 0 0; $b = Box $L 140 150 40 30 0 0 255; $L.CreateRightAngleConnector($a.SnapPoints.Item(1), $b.SnapPoints.Item(1)) | Out-Null }
    'C06_text_on_path' = { param($d, $L) $c = $L.CreateEllipse2(105, 150, 60, 60); $t = $L.CreateArtisticText(50, 150, 'Chữ chạy theo đường tròn — Việt Nam'); $t.Text.Story.Size = 20; $t.Text.FitToPath($c) | Out-Null }
    'C07_blend'        = { param($d, $L) $a = Box $L 10 10 30 30 255 0 0; $b = $L.CreateEllipse2(170, 200, 20, 20); $b.Fill.UniformColor.RGBAssign(0, 0, 255); $a.CreateBlend($b) | Out-Null }
    'C08_perspective'  = { param($d, $L) $a = Box $L 40 40 120 80 0 180 90; $a.CreatePerspective(400, 80, 100, 600) | Out-Null }
    'C09_twister'      = { param($d, $L) $a = $L.CreatePolygon(30, 250, 180, 100, 7); $a.Fill.UniformColor.RGBAssign(200, 0, 150); $a.CreateTwisterDistortion(105, 175, 270) | Out-Null }
    'C10_zipper'       = { param($d, $L) $a = Box $L 30 30 150 100 255 150 0; $a.CreateZipperDistortion(105, 80, 20, 10) | Out-Null }
    'C11_hatch'        = { param($d, $L) $a = Box $L 20 20 150 100 255 255 255; $a.Fill.ApplyHatchFill('Basic', 'Cross Hatch') | Out-Null }
    'C12_spot'         = { param($d, $L) $a = Box $L 20 20 100 60 0 0 0; $a.Fill.UniformColor = $app.CreateSpotColorByName('PANTONE+ Solid Coated', 'PANTONE 185 C') }
    'C13_color_style'  = { param($d, $L) $a = Box $L 20 20 100 60 0 120 200; $d.ColorStyles.CreateFromShape($a, $true, $true, $true) | Out-Null }
    'C14_guides'       = { param($d, $L) Box $L 20 20 100 60 0 120 200 | Out-Null; $g = $d.MasterPage.GuidesLayer; $g.CreateGuide(0, 100, 210, 100) | Out-Null; $g.CreateGuideAngle(105, 150, 45) | Out-Null }
    'C15_fountain_transp' = { param($d, $L) Box $L 10 10 190 100 255 0 0 | Out-Null; $b = Imp $L 'photo.jpg'; $b.SetSize(150, 100); $b.Transparency.ApplyFountainTransparency(0, 100, 1, 90) | Out-Null }
    'C16_linked_frames' = { param($d, $L) $a = $L.CreateParagraphText(10, 280, 100, 200, ('Đoạn văn bản dài được nối qua hai khung. ' * 20)); $b = $L.CreateParagraphText(110, 280, 200, 200, ''); $a.Text.Frame.LinkTo($b.Text.Frame) | Out-Null }
    'C17_text_styles'  = { param($d, $L) $t = $L.CreateArtisticText(10, 200, 'Đậm Nghiêng Gạch chân Times'); $t.Text.Story.Size = 30; $s = $t.Text.Story; $s.Characters.Item(1).Bold = $true; $s.Words.Item(2).Italic = $true; $s.Words.Item(3).Underline = 1; $s.Words.Item(5).Font = 'Times New Roman' }
    'C18_powerclip_nested' = { param($d, $L) $b = Imp $L 'rgb.tif'; $c1 = $L.CreateEllipse2(60, 60, 50, 40); $b.AddToPowerClip($c1) | Out-Null; $c2 = Box $L 20 30 120 80 255 255 0; $c1.AddToPowerClip($c2) | Out-Null }
    'C19_group_nested' = { param($d, $L) $s = Box $L 10 10 20 20 255 0 0; for ($i = 1; $i -le 6; $i++) { $n = Box $L (10 + $i * 25) (10 + $i * 25) 20 20 0 ($i * 40) 255; $r = $app.CreateShapeRange(); $r.Add($s); $r.Add($n); $s = $r.Group() } }
    'C20_master_layer' = { param($d, $L) $m = $d.MasterPage.CreateLayer('Lớp trang chủ'); Box $m 0 280 210 17 30 30 30 | Out-Null; $d.AddPages(2) | Out-Null; Box $L 20 20 50 50 255 0 0 | Out-Null }
    'C21_pages_mixed'  = { param($d, $L) $d.AddPages(3) | Out-Null; $d.Pages.Item(2).Orientation = 1; $d.Pages.Item(3).SetSize(100, 100); $d.Pages.Item(4).SetSize(420, 297); foreach ($i in 1..4) { Box $d.Pages.Item($i).ActiveLayer 5 5 30 30 ($i * 50) 0 0 | Out-Null } }
    'C22_lens_types'   = { param($d, $L) $b = Imp $L 'photo.jpg'; $b.SetSize(200, 150); $x = 10; foreach ($t in 1, 2, 3, 4, 5, 6, 7) { $e = $L.CreateEllipse2($x + 12, 130, 12, 12); try { $e.CreateLens($t, 50, (Rgb 255 0 0), (Rgb 0 0 0), 0) | Out-Null } catch {}; $x += 27 } }
    'C23_contour_text' = { param($d, $L) $t = $L.CreateArtisticText(20, 150, 'VIỀN'); $t.Text.Story.Size = 72; $t.CreateContour(1, 1.5, 3, 0, (Rgb 0 0 0), (Rgb 255 255 0), (Rgb 255 0 0), 0, 0, 0, 0, 15) | Out-Null }
    'C24_envelope_text' = { param($d, $L) $t = $L.CreateArtisticText(20, 150, 'BAO CHỮ'); $t.Text.Story.Size = 60; $t.CreateEnvelope(2, 0, $false) | Out-Null }
    'C25_extrude_text' = { param($d, $L) $t = $L.CreateArtisticText(20, 150, 'NỔI 3D'); $t.Text.Story.Size = 60; $t.CreateExtrude(1, 0, 150, 200, 20, 0, (Rgb 200 0 0), (Rgb 0 0 0), 0, 0, (Rgb 0 0 0), $false) | Out-Null }
    'C26_bitmap_shadow_clip' = { param($d, $L) $b = Imp $L 'layers.psd'; $c = $L.CreateEllipse2(60, 60, 50, 40); $b.AddToPowerClip($c) | Out-Null; $c.CreateDropShadow(1, 60, 10, 3, -3, (Rgb 0 0 0), 0, 0, 0, 0, 0, 3) | Out-Null }
    'C27_outline_styles' = { param($d, $L) $y = 280; foreach ($w in 0.2, 1, 3, 6) { $s = $L.CreateLineSegment(10, $y, 200, $y - 20); $s.Outline.Width = $w; $s.Outline.Color.CMYKAssign(0, 100, 0, 0); $s.Outline.Style = $app.OutlineStyles.Item([int]$w + 2); $s.Outline.StartArrow = $app.ArrowHeads.Item(2); $s.Outline.EndArrow = $app.ArrowHeads.Item(7); $y -= 40 }; $c = $L.CreateEllipse2(105, 60, 40, 30); $c.Outline.Width = 2; $c.Outline.NibStretch = 30; $c.Outline.NibAngle = 45 }
    'C28_freehand_curves' = { param($d, $L) $rnd = New-Object Random 7; $c = $d.CreateCurve(); $p = $c.CreateSubPath(10, 10); for ($i = 0; $i -lt 60; $i++) { $p.AppendCurveSegment($rnd.Next(0, 210), $rnd.Next(0, 297)) | Out-Null }; $s = $L.CreateCurve($c); $s.Outline.Width = 0.5 }
    'C29_combine_weld' = { param($d, $L) $a = $L.CreateEllipse2(80, 150, 50, 50); $b = Box $L 80 100 100 100 0 0 0; $w = $a.Weld($b); $w.Fill.UniformColor.RGBAssign(0, 160, 160); $c = $L.CreateEllipse2(60, 60, 30, 30); $e = $L.CreateEllipse2(80, 60, 30, 30); $c.Combine() | Out-Null; $e.Trim($c) | Out-Null }
    'C30_cmyk_rich'    = { param($d, $L) $x = 5; foreach ($c in @(0, 0, 0, 100), @(60, 40, 40, 100), @(100, 0, 0, 0), @(0, 100, 100, 0), @(30, 0, 100, 10)) { $s = $L.CreateRectangle2($x, 50, 38, 38); $s.Fill.UniformColor.CMYKAssign($c[0], $c[1], $c[2], $c[3]); $s.Outline.SetNoOutline(); $s.OverprintFill = $true; $x += 40 } }
    'C31_paragraph_columns' = { param($d, $L) $t = $L.CreateParagraphText(10, 280, 200, 100, ('Văn bản chia cột, căn đều hai bên, có thụt dòng. ' * 25)); $t.Text.Frame.SetColumns(3, $true, 60, 5); $t.Text.Story.Alignment = 4; $t.Text.Story.FirstLineIndent = 5 }
    'C32_dropcap_bullets' = { param($d, $L) $t = $L.CreateParagraphText(10, 280, 200, 150, "Chữ hoa đầu đoạn rất lớn ở dòng đầu.`rMục một`rMục hai"); $t.Text.Story.Paragraphs.Item(1).ApplyDropCapEffect(3, 0) | Out-Null }
    'C33_huge'         = { param($d, $L) $rnd = New-Object Random 3; for ($i = 0; $i -lt 1500; $i++) { $s = $L.CreateEllipse2($rnd.Next(0, 210), $rnd.Next(0, 297), $rnd.Next(1, 6), $rnd.Next(1, 6)); $s.Fill.UniformColor.RGBAssign($rnd.Next(256), $rnd.Next(256), $rnd.Next(256)) }; foreach ($f in 'rgb.tif', 'cmyk.tif', 'layers.psd', 'rgba.png', 'photo.jpg', 'big.tif') { $b = Imp $L $f; $b.SetSize(40, 30); $b.Move($rnd.Next(0, 150), $rnd.Next(0, 250)) } }
    'C34_symbols_many' = { param($d, $L) $a = $L.CreateEllipse2(20, 20, 10, 10); $a.Fill.UniformColor.RGBAssign(255, 0, 0); $s = $a.ConvertToSymbol('ky_hieu'); for ($i = 1; $i -le 10; $i++) { $s.Duplicate($i * 15, $i * 20) | Out-Null } }
    'C35_qr'           = { param($d, $L) $L.CreateCustomShape('QRCode', 20, 20, 100, 100) | Out-Null }
}

foreach ($name in $samples.Keys) {
    if ($Only -and $name -notin $Only) { continue }
    $line = ''
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
        $line = "OK    $name"
    } catch {
        $line = "LỖI  $name : $($_.Exception.Message)"
    }
    try { $doc.Close() } catch {}
    Write-Host $line
}
