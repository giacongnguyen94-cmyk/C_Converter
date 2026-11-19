# C_Converter — Hạ phiên bản file CorelDRAW

*Downgrade CorelDraw Version*

Hạ phiên bản file `.cdr` xuống **CorelDRAW X7, X8, 2017, 2018, 2019, 2020 – 2025**, không cần
cài CorelDRAW (không dùng COM, không gọi chương trình của Corel). Nhận nguồn **X8 → 2026**.

| Bản đích | Nguồn nhận | Kiểm chứng |
|---|---|---|
| X7 · X8 · 2017 · 2018 · 2019 | X8 → 2026 | Đối chiếu file CorelDRAW 2019 và 2026 tự Save As; mở lại bằng CorelDRAW 2019 / 2026 |
| 2020 · 2021 · 2022 · 2023 · 2024 · 2025 | bản mới hơn đích | Đối chiếu file CorelDRAW 2026 (và 2024) tự Save As; mở lại bằng CorelDRAW 2026 |

Bộ mẫu do CorelDRAW 2026 tạo gồm 131 thiết kế (ảnh TIF/PSD/PNG/JPG/BMP/GIF, ảnh liên kết, hiệu ứng bitmap,
bảng, đường kích thước, đường nối, blend, phối cảnh, màu pha, khung chữ nối trang, tài liệu nhiều trang…), mỗi thiết kế
có file Corel tự lưu ở cả 11 phiên bản 2026 → X7. Chi tiết: [docs/PhatHien-04-corel2026.md](docs/PhatHien-04-corel2026.md).

Đây là hạ phiên bản thật (ghi lại các bản ghi theo định dạng 2019), không phải sửa số phiên bản trong header,
và cũng không phải chuyển sang định dạng khác.

## Dùng
**Giao diện:** chạy `dist/C_Converter.exe` (một file duy nhất, không cần cài .NET).
1. Kéo thả file `.cdr` hoặc cả thư mục vào cửa sổ (hoặc bấm *Thêm file… / Thêm thư mục…*).
2. Chọn **phiên bản đích** và nơi lưu: cạnh file gốc (đuôi `_<đích>`, vd `_X7`) hoặc một thư mục riêng.
3. Bấm **Bắt đầu**. Mỗi file có trạng thái và nhật ký riêng; *Lưu log…* để xuất toàn bộ.

**Dòng lệnh:**
```
dotnet src/CConvert.Cli/bin/Release/net8.0/CConvert.Cli.dll <file.cdr> [...] [--to X7|X8|2017|2018|2019|2020|2021|2022|2023|2024|2025|2026] [--out <thư mục>]
```

Cam kết an toàn:
- **Không bao giờ ghi đè** file gốc hay file đã tồn tại (tự thêm ` (1)`, ` (2)`…); ghi qua file tạm rồi mới đổi tên.
- Một file lỗi không làm dừng cả lô.
- File đã ở bản đích hoặc thấp hơn, hoặc không phải CorelDRAW, được bỏ qua kèm lý do.

## Mức độ tin cậy
Đã kiểm thử bằng CorelDRAW 2019 thật (mở file, so cấu trúc và so ảnh từng điểm với file do Corel tự "Save As 2019"):
45 mẫu bao phủ hình cơ bản, màu RGB/CMYK, nét/mũi tên, nhóm, layer, nhiều trang, text tiếng Việt, paragraph,
bullet, bitmap, gradient, PowerClip, lens, transparency, đổ bóng, contour, envelope, extrude, symbol, texture…
cùng 5 file thực tế. Chi tiết: [docs/PhatHien-02-loda.md](docs/PhatHien-02-loda.md).
Bản đích X7–2018: [docs/PhatHien-03-dich-cu.md](docs/PhatHien-03-dich-cu.md) — 188 file đáp án do CorelDRAW 2019 tự lưu.

Gặp đối tượng/chunk chưa có trong mẫu đối chiếu, C_Converter vẫn chép nguyên nhưng **ghi cảnh báo cụ thể** vào nhật ký.

Giới hạn đã biết (đều có cảnh báo):
- **Bóng trong** (inner shadow) khi đích 2019 trở xuống: không có trong các bản đó → C_Converter gỡ hiệu ứng, giữ đối
  tượng (CorelDRAW tự raster hóa bóng). Muốn giữ bóng: *Convert to Bitmap* trước khi hạ. Đích 2020–2025 giữ nguyên.
- **Bóng đổ làm mờ kiểu Gaussian** khi đích X7: X7 chưa có kiểu này → đổi sang kiểu "Average" (Corel raster hóa).
- **Font biến thể** (vd Bahnschrift) khi đích 2019 trở xuống: vẽ bằng kiểu mặc định của font → nên *Convert to Curves* trước.
- **Bullet** dùng font không có trên máy chạy C_Converter: khoảng cách chữ–bullet có thể lệch vài mm
  (font có trên máy thì tính đúng như Corel, sai lệch dưới 1 µm).
- Chưa hỗ trợ X6 trở về trước (cả nguồn lẫn đích).
- Không có CorelDRAW X7–2018 để mở thử trực tiếp: bằng chứng là trùng file Corel tự lưu và mở được bằng 2019/2026.

## Nguồn mới hơn 2026
File mới hơn 2026 (vrsn &gt; 2700) được xử lý bằng suy luận: nhận theo cấu trúc (ZIP + `root.dat` + `fver`/`vrsn` +
`mcfg` dạng 2100), dùng chung quy tắc 2020–2026, lọc JSON theo lược đồ 2019, và **không ghi file** nếu gặp bản ghi
chưa từng thấy. Nên mở kiểm tra trước khi giao.

## Cấu trúc mã
| Thư mục | Nội dung |
|---|---|
| `src/CConvert.Core` | Lõi: đọc/ghi ZIP, `root.dat` (RIFF con trỏ), `loda`, `txsm`, `lnkt`, quy tắc chuyển (`Downgrader`) |
| `src/CConvert.App` | Giao diện WPF tiếng Việt |
| `src/CConvert.Cli` | Dòng lệnh |
| `tests/CConvert.Tests` | Test (cần `samples/gen`, tạo bằng bộ kiểm thử bên dưới) |
| `tools/CdrDump` | Công cụ dump cây chunk của mọi file `.cdr` |
| `tools/analysis` | Script Python so sánh nhị phân / so ảnh |
| `tools/harness` | **Chỉ cho phát triển**: điều khiển CorelDRAW 2024/2019 qua COM để tạo mẫu và kiểm chứng |
| `docs/` | Khảo sát định dạng và các quy tắc đã khám phá |

## Phát triển
```
dotnet test tests/CConvert.Tests                                   # 462 test
powershell -File tools/harness/save-older.ps1 -Files samples/gen/*_21.cdr -OutDir samples/older   # cần CorelDRAW 2019
python tools/harness/many_cases.py <CConvert.Cli.exe> out/many                  # 990 ca kiểm chứng, rồi verify-2019.ps1 + tools/analysis/compare_many.py
powershell -File tools/harness/make-bitmap-samples.ps1 -OutDir samples/bmp_all -Versions 27,26,25,24,23,22,21,20,19,18,17   # cần CorelDRAW 2026
dotnet run -c Release --project tools/harness/CorelGen -- samples/x26   # tài liệu phức tạp (cần CorelDRAW 2026; lib/Interop.VGCore.dll tạo bằng TlbImp từ VGCoreAuto.tlb)
python tools/harness/all_targets_cases.py <CConvert.Cli.exe> out/at samples/bmp_all samples/gen27_all samples/cplx samples/x26   # mọi bản đích, rồi verify-2019.ps1 (-ProgId …21 / …27)
python tools/analysis/byte_targets.py <CConvert.Cli.exe> samples/x26    # so từng byte với file Corel 2026 tự Save As
powershell -File tools/harness/run-regression.ps1 -Regenerate   # cần CorelDRAW 2024 + 2019
dotnet publish src/CConvert.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```
