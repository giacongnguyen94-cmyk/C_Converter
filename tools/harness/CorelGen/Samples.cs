using VGCore;

namespace CorelGen;

/// <summary>Các mẫu. Mỗi bước nhỏ chạy trong Step(): bước nào CorelDRAW không làm được thì bỏ qua và ghi lại.</summary>
sealed class Samples(string assets)
{
    public Application App { get; } = (Application)Activator.CreateInstance(Type.GetTypeFromProgID("CorelDRAW.Application.27")!)!;
    public List<string> Failed { get; } = [];
    readonly Random rnd = new(2026);

    void Step(string label, Action a)
    {
        try { a(); }
        catch (Exception ex) { Failed.Add($"{label}: {(ex.InnerException ?? ex).Message.Split('\n')[0]}"); }
    }

    // PANTONE+ Solid Coated-V5 (định danh lấy từ PaletteManager của máy này).
    Color Spot(string name, int tint = 100) => App.CreateSpotColorByName("6e75244b-d853-4d2e-bea2-a5da3f195d08", name, tint);

    Color Rgb(int r, int g, int b) => App.CreateRGBColor(r, g, b);
    Color Cmyk(int c, int m, int y, int k) => App.CreateCMYKColor(c, m, y, k);

    Shape Box(Layer l, double x, double y, double w, double h, Color c)
    {
        var s = l.CreateRectangle2(x, y, w, h);
        s.Fill.UniformColor = c;
        return s;
    }

    Shape Img(Layer l, string file, double x, double y, double w, double h)
    {
        var o = App.CreateStructImportOptions();
        l.Import(Path.Combine(assets, file), cdrFilter.cdrAutoSense, o);
        var s = App.ActiveSelectionRange[1];
        s.SetSize(w, h);
        s.SetPosition(x, y + h);
        return s;
    }

    Shape Text(Layer l, double x, double y, string t, double size, string font = "Arial")
    {
        var s = l.CreateArtisticText(x, y, t);
        s.Text.Story.Font = font;
        s.Text.Story.Size = (float)size;
        return s;
    }

    Shape Para(Layer l, double x1, double y1, double x2, double y2, string t) => l.CreateParagraphText(x1, y1, x2, y2, t);

    const string Lorem = "CorelDRAW tiếng Việt: Đường phố Hà Nội, ăâđêôơư ẮẰẲẴẶ. Văn bản đoạn có nhiều dòng để kiểm tra dàn trang, căn lề và ngắt dòng. ";

    public IEnumerable<(string, Action<Document, Layer>)> All()
    {
        // ---- Đối tượng đơn lẻ trước đây chưa tạo được bằng PowerShell ----
        yield return ("X01_dimensions", (d, l) =>
        {
            Box(l, 20, 150, 120, 60, Rgb(0, 150, 255));
            Step("ngang", () => l.CreateLinearDimension(cdrLinearDimensionType.cdrDimensionHorizontal, d.CreateFreeSnapPoint(20, 150), d.CreateFreeSnapPoint(140, 150), true, 80, 135));
            Step("dọc", () => l.CreateLinearDimension(cdrLinearDimensionType.cdrDimensionVertical, d.CreateFreeSnapPoint(140, 150), d.CreateFreeSnapPoint(140, 210), true, 160, 180));
            Step("xiên", () => l.CreateLinearDimension(cdrLinearDimensionType.cdrDimensionSlanted, d.CreateFreeSnapPoint(20, 210), d.CreateFreeSnapPoint(140, 150), true, 60, 230, Precision: 1));
            Step("góc", () => l.CreateAngularDimension(d.CreateFreeSnapPoint(60, 60), d.CreateFreeSnapPoint(150, 60), d.CreateFreeSnapPoint(120, 120), 120, 90));
        });
        yield return ("X02_blends", (d, l) =>
        {
            Step("blend", () => { var a = Box(l, 10, 10, 30, 30, Rgb(255, 0, 0)); var b = l.CreateEllipse2(180, 60, 15, 15); b.Fill.UniformColor = Rgb(0, 0, 255); a.CreateBlend(b, 12); });
            Step("blend xoay", () => { var a = l.CreatePolygon(10, 150, 40, 120, 5); a.Fill.UniformColor = Rgb(255, 200, 0); var b = l.CreatePolygon(150, 150, 180, 120, 8); b.Fill.UniformColor = Rgb(0, 160, 0); a.CreateBlend(b, 8, cdrFountainFillBlendType.cdrRainbowCWFountainFillBlend, Angle: 90); });
            Step("blend theo đường", () => { var p = l.CreateCurveSegment2(10, 200, 60, 290, 150, 180, 200, 280); var a = l.CreateEllipse2(10, 200, 5, 5); var b = l.CreateEllipse2(200, 280, 10, 10); b.Fill.UniformColor = Rgb(200, 0, 200); a.CreateBlend(b, 15, Path: p); });
        });
        yield return ("X03_hatch_pattern_fills", (d, l) =>
        {
            int i = 0;
            foreach (var h in new object[] { 0, 1, 2, 3 })
                Step($"hatch {h}", () => { var s = Box(l, 10 + (i++ % 2) * 100, 200 - (i / 3) * 90, 90, 80, Rgb(255, 255, 255)); s.Fill.ApplyHatchFill("", h); });
            Step("pattern 2 màu", () => { var s = Box(l, 10, 10, 90, 80, Rgb(255, 255, 255)); s.Fill.ApplyPatternFill(cdrPatternFillType.cdrTwoColorPattern, "", 3, Rgb(200, 0, 0), Rgb(255, 255, 200)); });
            Step("pattern bitmap", () => { var s = Box(l, 110, 10, 90, 80, Rgb(255, 255, 255)); s.Fill.ApplyPatternFill(cdrPatternFillType.cdrBitmapPattern, Path.Combine(assets, "photo.jpg")); });
        });
        yield return ("X04_spot_overprint", (d, l) =>
        {
            int k = 0;
            foreach (var col in new[] { "PANTONE 185 C", "PANTONE 300 C", "PANTONE 7621 C", "PANTONE 368 C" })
                Step($"spot {col}", () => { var s = Box(l, 10 + k * 45, 200, 40, 40, Rgb(0, 0, 0)); k++; s.Fill.UniformColor = Spot(col, 100 - k * 15); });
            Step("HKS", () => { var s = Box(l, 10, 150, 40, 40, Rgb(0, 0, 0)); s.Fill.UniformColor = App.CreateSpotColorByName("01d65145-c60e-4426-8a71-c353dffa3bb2", "HKS 13 N"); });
            Step("registration", () => { var s = Box(l, 10, 100, 40, 40, Rgb(0, 0, 0)); s.Fill.UniformColor = App.CreateRegistrationColor(); });
            Step("overprint", () => { var s = Box(l, 60, 100, 40, 40, Cmyk(0, 0, 0, 100)); s.OverprintFill = true; s.Outline.Width = 2; s.OverprintOutline = true; });
            Step("rich black", () => Box(l, 110, 100, 40, 40, Cmyk(60, 40, 40, 100)));
        });
        yield return ("X05_linked_frames", (d, l) =>
        {
            var a = Para(l, 10, 287, 100, 150, string.Concat(Enumerable.Repeat(Lorem, 12)));
            var b = Para(l, 110, 287, 200, 150, "");
            Step("nối khung", () => a.Text.Frame.LinkTo(b));
            Step("khung trang 2", () => { d.AddPages(1); var c = Para(d.Pages[2].ActiveLayer, 10, 287, 200, 150, ""); b.Text.Frame.LinkTo(c); });
        });
        yield return ("X06_outlines", (d, l) =>
        {
            double y = 280;
            for (int i = 1; i <= 8; i++)
            {
                int n = i;
                Step($"viền {n}", () =>
                {
                    var s = l.CreateCurveSegment2(10, y, 60, y + 10, 150, y - 15, 200, y);
                    s.Outline.SetProperties(n * 0.6, App.OutlineStyles[n], Cmyk(n * 10, 100 - n * 10, 0, 0), App.ArrowHeads[n], App.ArrowHeads[n + 5],
                        cdrTriState.cdrTrue, cdrTriState.cdrTrue, (cdrOutlineLineCaps)(n % 3), (cdrOutlineLineJoin)(n % 3), n * 15, 20 + n * 10);
                });
                y -= 30;
            }
            Step("viền sau tô", () => { var e = l.CreateEllipse2(105, 30, 50, 20); e.Fill.UniformColor = Rgb(255, 255, 0); e.Outline.SetProperties(4, Color: Rgb(255, 0, 0), BehindFill: cdrTriState.cdrTrue); });
        });
        yield return ("X07_shaping", (d, l) =>
        {
            Step("weld", () => { var a = l.CreateEllipse2(50, 230, 30, 30); var b = Box(l, 50, 200, 60, 60, Rgb(0, 0, 0)); var w = a.Weld(b); w.Fill.UniformColor = Rgb(0, 160, 160); });
            Step("trim", () => { var a = l.CreateEllipse2(150, 230, 30, 30); a.Fill.UniformColor = Rgb(255, 0, 0); var b = Box(l, 150, 200, 50, 50, Rgb(0, 0, 0)); b.Trim(a); });
            Step("intersect", () => { var a = l.CreateEllipse2(50, 100, 35, 35); var b = l.CreateEllipse2(80, 100, 35, 35); var i = a.Intersect(b); i.Fill.UniformColor = Rgb(255, 128, 0); });
            Step("combine", () => { var a = l.CreateEllipse2(150, 100, 35, 35); var b = l.CreateEllipse2(150, 100, 15, 15); var r = App.CreateShapeRange(); r.Add(a); r.Add(b); var c = r.Combine(); c.Fill.UniformColor = Rgb(100, 0, 200); });
            Step("chữ → curves", () => { var t = Text(l, 20, 20, "CURVES", 48); t.ConvertToCurves(); });
        });
        yield return ("X08_color_styles", (d, l) =>
        {
            var a = Box(l, 20, 200, 80, 50, Rgb(0, 120, 200));
            var b = Box(l, 110, 200, 80, 50, Cmyk(0, 60, 100, 0));
            Step("color style", () => { var r = App.CreateShapeRange(); r.Add(a); r.Add(b); ((dynamic)d).ColorStyles.CreateStylesFromShapes(r); });
            Step("object style", () => d.StyleSheet.CreateStyleFromShape(a, "fill", "Kiểu tô xanh"));
        });
        yield return ("X09_text_rich", (d, l) =>
        {
            var t = Para(l, 10, 287, 200, 120, "Tiêu đề lớn\rĐoạn một có chữ đậm, nghiêng, gạch chân, gạch ngang, chỉ số trên và chỉ số dưới.\r" + Lorem + "\r\tCột 1\tCột 2\tCột 3\rDòng cuối căn phải.");
            var s = t.Text.Story;
            Step("cỡ/kiểu", () => { var p = s.Paragraphs[1]; p.Size = 28; p.Bold = true; p.Font = "Times New Roman"; p.Fill.UniformColor = Rgb(200, 0, 0); });
            Step("đậm/nghiêng", () => { var w = s.Paragraphs[2].Words; w[4].Bold = true; w[5].Italic = true; });
            Step("gạch", () => { var w = s.Paragraphs[2].Words; w[7].Underline = cdrFontLine.cdrSingleThinFontLine; w[9].Strikethru = cdrFontLine.cdrSingleThinFontLine; });
            Step("chỉ số", () => { var w = s.Paragraphs[2].Words; w[13].Position = cdrFontPosition.cdrSuperscriptFontPosition; w[17].Position = cdrFontPosition.cdrSubscriptFontPosition; });
            Step("màu ký tự", () => { var c = s.Paragraphs[2].Characters; for (int i = 1; i <= Math.Min(30, c.Count); i++) c[i].Fill.UniformColor = Rgb(i * 8, 0, 255 - i * 8); });
            Step("giãn", () => { s.Paragraphs[3].CharSpacing = 20; s.Paragraphs[3].LineSpacing = 150; s.Paragraphs[3].Alignment = cdrAlignment.cdrFullJustifyAlignment; s.Paragraphs[3].FirstLineIndent = 8; });
            Step("căn phải", () => s.Paragraphs[5].Alignment = cdrAlignment.cdrRightAlignment);
            Step("chữ dọc", () => { var v = Text(l, 180, 100, "Chữ xoay", 24); v.Rotate(90); });
            Step("font khác", () => { foreach (var (f, y) in new[] { ("Segoe UI", 90.0), ("Tahoma", 75.0), ("Courier New", 60.0), ("Georgia", 45.0) }) Text(l, 10, y, $"Font {f}: Tiếng Việt", 14, f); });
        });

        // ---- Tài liệu "thật": trộn nhiều tính năng trong một file ----
        yield return ("M01_poster", (d, l) =>
        {
            Step("nền ảnh", () => Img(l, "cmyk.tif", 0, 0, 210, 297));
            Step("lớp phủ", () => { var o = Box(l, 0, 0, 210, 120, Rgb(0, 0, 0)); o.Transparency.ApplyFountainTransparency(0, 100, cdrFountainFillType.cdrLinearFountainFill, 90); });
            Step("tiêu đề", () => { var t = Text(l, 15, 240, "LỄ HỘI MÙA HÈ 2026", 40); t.Text.Story.Bold = true; t.Fill.UniformColor = Rgb(255, 220, 0); t.CreateContour(cdrContourDirection.cdrContourOutside, 1.2, 1, cdrFountainFillBlendType.cdrDirectFountainFillBlend, Rgb(0, 0, 0), Rgb(200, 0, 0), Rgb(200, 0, 0)); });
            Step("phụ đề bóng", () => { var t = Text(l, 15, 220, "Âm nhạc · Ẩm thực · Nghệ thuật", 20); t.Fill.UniformColor = Rgb(255, 255, 255); t.CreateDropShadow(cdrDropShadowType.cdrDropShadowFlat, 70, 6, 1, -1, Rgb(0, 0, 0)); });
            Step("ảnh psd trong khung", () => { var p = Img(l, "layers.psd", 30, 110, 150, 100); var c = l.CreateEllipse2(105, 160, 70, 45); p.AddToPowerClip(c); c.Outline.SetProperties(2, Color: Rgb(255, 255, 255)); c.CreateDropShadow(cdrDropShadowType.cdrDropShadowFlat, 60, 15, 3, -3, Rgb(0, 0, 0)); });
            Step("logo png", () => { var g = Img(l, "rgba.png", 160, 260, 40, 30); g.CreateDropShadow(cdrDropShadowType.cdrDropShadowFlat, 50, 10, 2, -2); });
            Step("đoạn văn", () => { var p = Para(l, 15, 100, 195, 30, string.Concat(Enumerable.Repeat(Lorem, 3))); p.Fill.UniformColor = Rgb(255, 255, 255); p.Text.Frame.SetColumns(2, true, 85, 10); });
            Step("nút bo góc", () => { var b = l.CreateRectangle2(60, 12, 90, 14, 7, 7, 7, 7); b.Fill.ApplyFountainFill(Rgb(255, 100, 0), Rgb(255, 0, 100), cdrFountainFillType.cdrLinearFountainFill, 0); var t = Text(l, 72, 16, "MUA VÉ NGAY", 16); t.Fill.UniformColor = Rgb(255, 255, 255); });
            Step("lớp 2 trang trí", () => { var l2 = d.ActivePage.CreateLayer("Trang trí"); for (int i = 0; i < 40; i++) { var s = l2.CreatePolygon(rnd.Next(0, 200), rnd.Next(120, 297), rnd.Next(0, 200) + 6, rnd.Next(120, 297) - 6, 5, 1, 1, true); s.Fill.UniformColor = Rgb(255, rnd.Next(150, 255), 0); s.Transparency.ApplyUniformTransparency(rnd.Next(20, 70)); } });
        });
        yield return ("M02_brochure", (d, l) =>
        {
            d.AddPages(3);
            string[] imgs = ["rgb.tif", "photo.jpg", "layers.psd", "cmyk.jpg"];
            Shape prev = null;
            for (int i = 1; i <= 4; i++)
            {
                int n = i;
                var pl = d.Pages[n].ActiveLayer;
                Step($"tr{n} header", () => { var h = Box(pl, 0, 267, 210, 30, Cmyk(100, 40, 0, 0)); var t = Text(pl, 10, 277, $"Trang {n} — Giới thiệu công ty", 18); t.Fill.UniformColor = Rgb(255, 255, 255); });
                Step($"tr{n} ảnh", () => { var p = Img(pl, imgs[n - 1], 10, 160, 190, 100); if (n % 2 == 0) { var c = pl.CreateRectangle2(10, 160, 190, 100, 15, 15, 15, 15); p.AddToPowerClip(c); } else p.CreateDropShadow(cdrDropShadowType.cdrDropShadowFlat, 50, 8, 2, -2); });
                Step($"tr{n} văn bản", () => { var f = Para(pl, 10, 150, 200, 25, n == 1 ? string.Concat(Enumerable.Repeat(Lorem, 25)) : ""); f.Text.Frame.SetColumns(2, true, 90, 10); if (prev != null) prev.Text.Frame.LinkTo(f); prev = f; });
                Step($"tr{n} số trang", () => Text(pl, 100, 8, n.ToString(), 10));
            }
            Step("master", () => { var m = d.MasterPage.CreateLayer("Chân trang"); var s = Box(m, 0, 0, 210, 5, Cmyk(0, 0, 0, 30)); });
            Step("guides", () => { var g = d.MasterPage.GuidesLayer; g.CreateGuide(10, 0, 10, 297); g.CreateGuide(200, 0, 200, 297); g.CreateGuide(0, 150, 210, 150); });
        });
        yield return ("M03_packaging", (d, l) =>
        {
            d.ActivePage.SetSize(420, 297);
            Step("mặt hộp", () => { foreach (var (x, w) in new[] { (20.0, 100.0), (120.0, 60.0), (180.0, 100.0), (280.0, 60.0) }) { var s = Box(l, x, 80, w, 140, Cmyk(0, 20, 100, 0)); s.Outline.SetProperties(0.3, App.OutlineStyles[3], Cmyk(0, 100, 0, 0)); } });
            Step("nắp", () => { var s = l.CreatePolygon(20, 240, 120, 220, 4); s.Fill.UniformColor = Cmyk(0, 10, 60, 0); });
            Step("chữ 3D", () => { var t = Text(l, 30, 180, "TRÀ SEN", 36); t.Text.Story.Bold = true; t.CreateExtrude(cdrExtrudeType.cdrExtrudeSmallBack, cdrExtrudeVPType.cdrVPLockedToShape, 150, 120, 15, cdrExtrudeShading.cdrExtrudeColorShading, Rgb(150, 0, 0)); });
            Step("phối cảnh ảnh", () => { var p = Img(l, "photo.jpg", 185, 110, 90, 70); p.ConvertToBitmapEx(cdrImageType.cdrRGBColorImage, false, true, 150); var r = App.ActiveSelectionRange[1]; r.CreatePerspective(600, 200, 230, 900); });
            Step("biến dạng", () => { var s = l.CreatePolygon(285, 200, 335, 150, 12, 1, 1, true); s.Fill.UniformColor = Rgb(0, 150, 50); s.CreateTwisterDistortion(310, 175, 200); });
            Step("envelope", () => { var t = Text(l, 290, 110, "HỮU CƠ", 24); t.CreateEnvelope(3, cdrEnvelopeMode.cdrEnvelopePutty, false); });
            Step("mã vạch giả", () => { for (int i = 0; i < 40; i++) l.CreateRectangle2(125 + i * 1.2, 90, rnd.Next(1, 3) * 0.3, 25).Fill.UniformColor = Rgb(0, 0, 0); });
            Step("spot", () => { var s = Box(l, 125, 200, 50, 15, Rgb(0, 0, 0)); s.Fill.UniformColor = Spot("PANTONE 7621 C"); });
            Step("đường cắt", () => { var c = l.CreateRectangle2(15, 75, 330, 170); c.Fill.ApplyNoFill(); c.Outline.SetProperties(0.25, App.OutlineStyles[4], Cmyk(0, 100, 100, 0)); c.OverprintOutline = true; });
        });
        yield return ("M04_logo_set", (d, l) =>
        {
            Step("vòng chữ", () => { var c = l.CreateEllipse2(60, 230, 45, 45); c.Outline.SetProperties(3, Color: Cmyk(100, 0, 0, 0)); var t = Text(l, 20, 230, "CÔNG TY CỔ PHẦN ĐẠI DƯƠNG XANH", 12); t.Text.FitToPath(c); });
            Step("biểu tượng weld", () => { var a = l.CreatePolygon(130, 260, 190, 200, 6, 1, 1, true); var b = l.CreateEllipse2(160, 230, 18, 18); var w = a.Weld(b); w.Fill.ApplyFountainFill(Cmyk(100, 0, 0, 0), Cmyk(100, 100, 0, 0), cdrFountainFillType.cdrRadialFountainFill, 0); });
            Step("chữ curves + contour", () => { var t = Text(l, 20, 140, "OCEAN", 60); t.Text.Story.Bold = true; t.ConvertToCurves(); });
            Step("contour", () => { var s = App.ActiveDocument.ActiveLayer.Shapes[1]; s.CreateContour(cdrContourDirection.cdrContourInside, 1, 3, cdrFountainFillBlendType.cdrDirectFountainFillBlend, Rgb(0, 0, 0), Rgb(0, 100, 255), Rgb(255, 255, 255)); });
            Step("lens", () => { var e = l.CreateEllipse2(150, 150, 25, 25); e.CreateLens(cdrLensType.cdrLensMagnify, 2, Rgb(0, 0, 0), Rgb(0, 0, 0)); });
            Step("biến thể màu", () => { for (int i = 0; i < 4; i++) { var s = l.CreateEllipse2(30 + i * 50, 50, 20, 20); s.Fill.ApplyFountainFill(Rgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)), Rgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)), (cdrFountainFillType)(1 + i % 4), i * 30); } });
            Step("symbol", () => { var s = l.CreateEllipse2(20, 20, 6, 6); s.Fill.UniformColor = Rgb(255, 0, 0); var sym = s.ConvertToSymbol("cham_do"); for (int i = 1; i < 10; i++) sym.Duplicate(i * 18, 0); });
        });
        yield return ("M05_photo_collage", (d, l) =>
        {
            string[] files = ["rgb.tif", "rgb_lzw.tif", "rgb_zip.tif", "cmyk.tif", "gray.tif", "bw.tif", "rgba.tif", "layers.psd", "single.psd", "cmyk.psd", "gray.psd", "rgba.png", "gray16.png", "photo.jpg", "cmyk.jpg", "rgb.bmp", "pal16.bmp", "pal.gif", "rgb_300dpi.tif"];
            int i = 0;
            foreach (var f in files)
            {
                int n = i++;
                Step(f, () =>
                {
                    var s = Img(l, f, 5 + n % 4 * 51, 230 - n / 4 * 55, 48, 36);
                    switch (n % 7)
                    {
                        case 0: s.Rotate(rnd.Next(-20, 20)); break;
                        case 1: s.CreateDropShadow(cdrDropShadowType.cdrDropShadowFlat, 60, 8, 2, -2); break;
                        case 2: s.Transparency.ApplyUniformTransparency(35); break;
                        case 3: { var c = l.CreateEllipse2(5 + n % 4 * 51 + 24, 230 - n / 4 * 55 + 18, 22, 16); s.AddToPowerClip(c); break; }
                        case 4: s.ApplyEffectBCI(15, 10, 5); break;
                        case 5: s.Bitmap.ConvertTo(cdrImageType.cdrGrayscaleImage); break;
                        case 6: s.Skew(12, 0); break;
                    }
                });
            }
            Step("khung chữ", () => { var t = Text(l, 10, 10, "Bộ sưu tập ảnh — 19 định dạng", 18); t.Fill.UniformColor = Rgb(80, 80, 80); });
            Step("nhóm ảnh", () => { var r = App.CreateShapeRange(); for (int k = 1; k <= Math.Min(4, l.Shapes.Count); k++) r.Add(l.Shapes[k]); r.Group(); });
        });
        yield return ("M06_infographic", (d, l) =>
        {
            Step("bảng", () => l.CreateCustomShape("Table", [10.0, 180.0, 200.0, 260.0, 4, 5]));
            Step("điền bảng", () => { var t = l.Shapes[1].Custom; });
            Step("biểu đồ cột", () => { for (int i = 0; i < 8; i++) { var h = rnd.Next(20, 80); var b = Box(l, 15 + i * 22, 60, 16, h, Cmyk(i * 12, 80 - i * 8, 0, 0)); Text(l, 15 + i * 22, 62 + h, h.ToString(), 9); } });
            Step("trục", () => { l.CreateLineSegment(10, 60, 200, 60).Outline.SetProperties(0.8, EndArrow: App.ArrowHeads[3]); l.CreateLineSegment(10, 60, 10, 160).Outline.SetProperties(0.8, EndArrow: App.ArrowHeads[3]); });
            Step("đường nối", () => { var a = l.CreateEllipse2(40, 30, 12, 12); var b = l.CreateEllipse2(170, 30, 12, 12); a.Fill.UniformColor = Rgb(255, 0, 0); b.Fill.UniformColor = Rgb(0, 0, 255); l.CreateConnector(a.SnapPoints[1], b.SnapPoints[1]); var c = l.CreateEllipse2(105, 10, 8, 8); l.CreateRightAngleConnector(a.SnapPoints[2], c.SnapPoints[1]); });
            Step("kích thước", () => l.CreateLinearDimension(cdrLinearDimensionType.cdrDimensionHorizontal, d.CreateFreeSnapPoint(15, 60), d.CreateFreeSnapPoint(185, 60), true, 100, 50));
            Step("chú thích", () => { foreach (var (x, y, s) in new[] { (20.0, 170.0, "Doanh thu"), (90.0, 170.0, "Chi phí"), (160.0, 170.0, "Lợi nhuận") }) { var t = Text(l, x, y, s, 11); t.Text.Story.Italic = true; } });
            Step("biểu đồ tròn", () => { for (int i = 0; i < 6; i++) { var e = l.CreateEllipse2(170, 120, 25, 25); e.Ellipse.Type = cdrEllipseType.cdrPie; e.Ellipse.StartAngle = i * 60; e.Ellipse.EndAngle = i * 60 + 60; e.Fill.UniformColor = Rgb(40 * i, 200 - 30 * i, 120); } });
        });
        yield return ("M07_everything", (d, l) =>
        {
            d.AddPages(5);
            var p = Enumerable.Range(1, 6).Select(i => d.Pages[i].ActiveLayer).ToArray();
            // Trang 1: đồ họa vector
            Step("1 gradient", () => { var s = Box(p[0], 0, 0, 210, 297, Rgb(0, 0, 0)); s.Fill.ApplyFountainFill(Rgb(10, 20, 60), Rgb(60, 0, 80), cdrFountainFillType.cdrRadialFountainFill, 0); });
            Step("1 sao", () => { for (int i = 0; i < 60; i++) { var s = p[0].CreatePolygon(rnd.Next(0, 205), rnd.Next(0, 292), 0, 0, 5, 1, 1, true); s.SetSize(rnd.Next(2, 8), rnd.Next(2, 8)); s.Fill.UniformColor = Rgb(255, 255, rnd.Next(150, 255)); s.Outline.SetNoOutline(); } });
            Step("1 xoắn ốc", () => p[0].CreateSpiral(50, 250, 160, 140, 6, cdrSpiralType.cdrLogarithmic, 50));
            Step("1 nhóm lồng", () => { var g = Box(p[0], 10, 10, 10, 10, Rgb(255, 0, 0)); for (int i = 1; i < 6; i++) { var n = p[0].CreateEllipse2(15 + i * 12, 15 + i * 8, 5, 5); var r = App.CreateShapeRange(); r.Add(g); r.Add(n); g = r.Group(); } });
            // Trang 2: ảnh + hiệu ứng
            Step("2 ảnh nền", () => { var b = Img(p[1], "big.tif", 0, 100, 210, 140); b.Transparency.ApplyFountainTransparency(0, 80, cdrFountainFillType.cdrLinearFountainFill, 0); });
            Step("2 psd bóng", () => { var b = Img(p[1], "layers.psd", 20, 20, 80, 60); b.CreateDropShadow(cdrDropShadowType.cdrDropShadowFlat, 70, 12, 4, -4); });
            Step("2 lens", () => { var e = p[1].CreateEllipse2(150, 170, 30, 30); e.CreateLens(cdrLensType.cdrLensFishEye, 80, Rgb(0, 0, 0), Rgb(0, 0, 0)); });
            Step("2 to bitmap", () => { var t = Text(p[1], 110, 30, "RASTER", 30); t.ConvertToBitmapEx(cdrImageType.cdrCMYKColorImage, false, true, 200); });
            // Trang 3: chữ
            Step("3 đoạn", () => { var f = Para(p[2], 10, 287, 200, 100, string.Concat(Enumerable.Repeat(Lorem, 20))); f.Text.Frame.SetColumns(3, true, 60, 5); f.Text.Story.Alignment = cdrAlignment.cdrFullJustifyAlignment; });
            Step("3 bullet", () => { var b = Para(p[2], 10, 95, 200, 40, "Điểm thứ nhất\rĐiểm thứ hai\rĐiểm thứ ba"); b.Text.Story.ApplyBulletEffect(); });
            Step("3 dropcap", () => { var b = Para(p[2], 10, 38, 200, 10, Lorem + Lorem); b.Text.Story.Paragraphs[1].ApplyDropCapEffect(3); });
            Step("3 theo đường", () => { var c = p[2].CreateCurveSegment2(10, 70, 60, 120, 150, 50, 200, 90); var t = Text(p[2], 10, 70, "Chữ uốn lượn theo đường cong mềm mại", 16); t.Text.FitToPath(c); });
            // Trang 4: hiệu ứng vector
            Step("4 blend", () => { var a = p[3].CreateEllipse2(20, 260, 8, 8); a.Fill.UniformColor = Rgb(255, 0, 0); var b = p[3].CreateEllipse2(190, 260, 15, 15); b.Fill.UniformColor = Rgb(0, 0, 255); a.CreateBlend(b, 20); });
            Step("4 contour", () => { var s = p[3].CreateEllipse2(50, 200, 25, 20); s.Fill.UniformColor = Rgb(255, 150, 0); s.CreateContour(cdrContourDirection.cdrContourOutside, 2, 4, cdrFountainFillBlendType.cdrDirectFountainFillBlend, Rgb(0, 0, 0), Rgb(255, 255, 0), Rgb(200, 0, 0)); });
            Step("4 extrude", () => { var s = Box(p[3], 120, 180, 50, 40, Rgb(0, 150, 200)); s.CreateExtrude(cdrExtrudeType.cdrExtrudeSmallBack, cdrExtrudeVPType.cdrVPLockedToShape, 200, 250, 25, cdrExtrudeShading.cdrExtrudeColorShading, Rgb(0, 150, 200)); });
            Step("4 envelope", () => { var s = Box(p[3], 20, 100, 80, 50, Rgb(100, 200, 0)); s.CreateEnvelope(1, cdrEnvelopeMode.cdrEnvelopeHorizontal, false); });
            Step("4 push pull", () => { var s = p[3].CreatePolygon(120, 150, 180, 90, 10); s.Fill.UniformColor = Rgb(200, 0, 200); s.CreatePushPullDistortion(150, 120, -40); });
            Step("4 zipper", () => { var s = p[3].CreateEllipse2(55, 50, 30, 30); s.Fill.UniformColor = Rgb(0, 200, 150); s.CreateZipperDistortion(55, 50, 15, 8); });
            Step("4 bóng phối cảnh", () => { var s = p[3].CreatePolygon(130, 70, 180, 20, 5, 1, 1, true); s.Fill.UniformColor = Rgb(255, 220, 0); s.CreateDropShadow(cdrDropShadowType.cdrDropShadowBottom, 50, 10, 0, 0, Rgb(0, 0, 0), PerspectiveAngle: 45, PerspectiveStretch: 1.5); });
            Step("4 bóng trong", () => { var s = Box(p[3], 80, 20, 40, 30, Rgb(255, 255, 255)); s.CreateInnerShadow(60, 8, 2, -2, Rgb(0, 0, 0)); });
            // Trang 5: kỹ thuật
            Step("5 bảng", () => p[4].CreateCustomShape("Table", [10.0, 200.0, 200.0, 280.0, 5, 4]));
            Step("5 kích thước", () => { Box(p[4], 30, 80, 150, 80, Rgb(230, 230, 230)); p[4].CreateLinearDimension(cdrLinearDimensionType.cdrDimensionHorizontal, d.CreateFreeSnapPoint(30, 80), d.CreateFreeSnapPoint(180, 80), true, 105, 65); p[4].CreateLinearDimension(cdrLinearDimensionType.cdrDimensionVertical, d.CreateFreeSnapPoint(180, 80), d.CreateFreeSnapPoint(180, 160), true, 195, 120); });
            Step("5 nối", () => { var a = p[4].CreateEllipse2(40, 40, 8, 8); var b = p[4].CreateEllipse2(170, 40, 8, 8); p[4].CreateConnector(a.SnapPoints[1], b.SnapPoints[1]); });
            Step("5 hatch", () => { var s = Box(p[4], 100, 20, 60, 40, Rgb(255, 255, 255)); s.Fill.ApplyHatchFill("", 1); });
            // Trang 6: màu in
            Step("6 spot", () => { var s = Box(p[5], 10, 200, 60, 60, Rgb(0, 0, 0)); s.Fill.UniformColor = Spot("PANTONE 185 C"); });
            Step("6 cmyk overprint", () => { var s = Box(p[5], 80, 200, 60, 60, Cmyk(0, 0, 0, 100)); s.OverprintFill = true; });
            Step("6 lab/gray", () => { var s = Box(p[5], 150, 200, 50, 60, App.CreateLabColor(50, 40, -30)); var g = Box(p[5], 10, 120, 60, 60, App.CreateGrayColor(128)); });
            Step("6 lớp in ẩn", () => { var hid = d.Pages[6].CreateLayer("Ghi chú (không in)"); hid.Printable = false; hid.Visible = false; Text(hid, 20, 40, "Ghi chú nội bộ", 14); });
            Step("6 lớp khóa", () => { var lk = d.Pages[6].CreateLayer("Khóa"); Box(lk, 80, 120, 60, 60, Rgb(0, 200, 0)); lk.Editable = false; });
            Step("master", () => { var m = d.MasterPage.CreateLayer("Logo mọi trang"); Text(m, 170, 5, "© 2026", 8); });
        });
    }
}
