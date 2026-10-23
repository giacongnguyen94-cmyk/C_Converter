# C_Converter — Hạ phiên bản file CorelDRAW

Chuyển file `.cdr` từ **CorelDRAW 2020, 2021, 2022, 2023, 2024** xuống **CorelDRAW 2019**, không cần cài CorelDRAW
(không dùng COM, không gọi chương trình của Corel). **2025, 2026: chế độ thử nghiệm** (xem bên dưới).

Đây là hạ phiên bản thật (ghi lại các bản ghi theo định dạng 2019), không phải sửa số phiên bản trong header,
và cũng không phải chuyển sang định dạng khác.

## Dùng
**Giao diện:** chạy `dist/C_Converter.exe` (một file duy nhất, không cần cài .NET).
1. Kéo thả file `.cdr` hoặc cả thư mục vào cửa sổ (hoặc bấm *Thêm file… / Thêm thư mục…*).
2. Chọn nơi lưu: cạnh file gốc (đuôi `_2019`) hoặc một thư mục riêng.
3. Bấm **Bắt đầu**. Mỗi file có trạng thái và nhật ký riêng; *Lưu log…* để xuất toàn bộ.

**Dòng lệnh:**
```
dotnet src/CConvert.Cli/bin/Release/net8.0/CConvert.Cli.dll <file.cdr> [...] [--out <thư mục>]
```

Cam kết an toàn:
- **Không bao giờ ghi đè** file gốc hay file đã tồn tại (tự thêm ` (1)`, ` (2)`…); ghi qua file tạm rồi mới đổi tên.
- Một file lỗi không làm dừng cả lô.
- File đã là 2019 trở xuống, hoặc không phải CorelDRAW, được bỏ qua kèm lý do.

## Mức độ tin cậy
Đã kiểm thử bằng CorelDRAW 2019 thật (mở file, so cấu trúc và so ảnh từng điểm với file do Corel tự "Save As 2019"):
45 mẫu bao phủ hình cơ bản, màu RGB/CMYK, nét/mũi tên, nhóm, layer, nhiều trang, text tiếng Việt, paragraph,
bullet, bitmap, gradient, PowerClip, lens, transparency, đổ bóng, contour, envelope, extrude, symbol, texture…
cùng 5 file thực tế. Chi tiết: [docs/PhatHien-02-loda.md](docs/PhatHien-02-loda.md).

Gặp đối tượng/chunk chưa có trong mẫu đối chiếu, C_Converter vẫn chép nguyên nhưng **ghi cảnh báo cụ thể** vào nhật ký.

Giới hạn đã biết (đều có cảnh báo):
- **Bóng trong** (inner shadow, 2023+): 2019 không có → C_Converter gỡ hiệu ứng, giữ đối tượng.
  Muốn giữ bóng: *Convert to Bitmap* trong 2024 trước khi hạ.
- **Font biến thể** (vd Bahnschrift): 2019 vẽ bằng kiểu mặc định của font → nên *Convert to Curves* trước.
- **Bullet** dùng font khác bullet mặc định: khoảng cách chữ–bullet có thể lệch vài mm.
- Chưa hạ xuống bản thấp hơn 2019 (X7, X8, 2017, 2018), chưa đọc nguồn X6 trở về trước.

## Chế độ thử nghiệm: nguồn 2025 / 2026
Chưa có mẫu 2025/2026 để kiểm chứng, nên file mới hơn 2024 được xử lý bằng suy luận:
- Nhận theo **cấu trúc** (ZIP + `root.dat` + `fver`/`vrsn` + `mcfg` dạng 2100 như 2019–2024), không theo bảng tên.
  Cấu trúc khác → từ chối.
- Dùng chung các quy tắc 2020–2024, cộng **lọc JSON theo lược đồ 2019** (`Schema2019.g.cs`, rút từ 54 file 2019 thật
  bằng `tools/analysis/json2019.py`): khóa không có trong 2019 bị bỏ và liệt kê trong nhật ký.
- Gặp thuộc tính đối tượng, dạng text, dạng hiệu ứng hoặc chunk **chưa từng thấy** → **báo lỗi, không ghi file**
  (thay vì tạo file có thể làm CorelDRAW 2019 crash).
- Kết quả gắn nhãn **"Xong (thử nghiệm)"** — nên mở kiểm tra bằng CorelDRAW 2019 trước khi giao.

Đã kiểm tra: logic thử nghiệm chạy trên 42 mẫu 2024 cho kết quả giống hệt logic đã kiểm chứng (lược đồ 2019 không
bỏ nhầm khóa nào); file "2025/2026" mô phỏng (`tools/analysis/simulate_newer.py`) mở được bằng CorelDRAW 2019 và
hiển thị giống hệt bản Corel tự hạ. Khi có CorelDRAW 2025 (bản dùng thử cũng được), chạy bộ kiểm thử
`tools/harness` với `CorelDRAW.Application.26` để chuyển sang hỗ trợ chính thức.

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
dotnet test tests/CConvert.Tests                                   # 167 test
powershell -File tools/harness/run-regression.ps1 -Regenerate   # cần CorelDRAW 2024 + 2019
dotnet publish src/CConvert.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```
