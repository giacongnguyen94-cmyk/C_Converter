# Phát hiện 04 — CorelDRAW 2026: ảnh, đối tượng phức tạp, mọi bản đích

Ngày 2026-11-19. Máy có CorelDRAW 2019 và 2026 (`CorelDRAW.Application.27`).

## Bối cảnh
Người dùng báo: thử trên dự án thật (máy khác), file phức tạp hoặc có ảnh TIF/PSD/bitmap thì chương trình báo lỗi.
File thật không lấy được, nên dựng mẫu bằng CorelDRAW 2026 phủ càng nhiều trường hợp càng tốt, cho Corel tự
"Save As" xuống **mọi** bản 2025 → X7 làm đáp án, rồi sửa C_Converter cho khớp.

## Bộ mẫu (131 thiết kế × 11 phiên bản)
| Thư mục | Công cụ | Nội dung |
|---|---|---|
| `samples/bmp_all` (48) | `tools/harness/make-bitmap-samples.ps1` | TIF (thường/LZW/ZIP/CMYK/xám/đen trắng/alpha/18 MB), PSD (nhiều lớp/1 lớp/CMYK/xám — `make_psd.py`), PNG alpha/16-bit, JPG RGB/CMYK, BMP, GIF, ảnh liên kết ngoài, xoay/nghiêng/co, PowerClip, trong suốt, bóng đổ, Invert/BCI/HSL/Color balance/Posterize/Gamma, đổi chế độ màu, resample, crop, inflate, trace, chuyển vector → bitmap… |
| `samples/gen27_all` (42) | `make-samples.ps1 -ProgId …27` | 42 mẫu đồ họa cũ, tạo lại bằng 2026 |
| `samples/cplx` (25) | `make-complex-samples.ps1` | bảng, đường nối, chữ theo đường, phối cảnh, xoắn, zipper, guides, trong suốt gradient trên ảnh, PowerClip lồng, nhóm lồng 6 cấp, lớp trang chủ, trang khác khổ, lens, contour/envelope/extrude chữ, 1500 đối tượng + 6 ảnh, symbol… |
| `samples/x26` (16) | `tools/harness/CorelGen` (C#, interop có kiểu bằng TlbImp) | đường kích thước, blend (xoay, theo đường), màu pha PANTONE/HKS, overprint, khung văn bản nối qua trang, 8 kiểu viền, weld/trim/intersect/combine, chữ định dạng phong phú; 7 tài liệu "thật": poster, brochure 4 trang, bao bì, bộ logo, ghép 19 ảnh, infographic, tài liệu tổng hợp 6 trang |

## Lỗi tìm thấy và đã sửa
| # | Lỗi | Nguyên nhân | Sửa |
|---|---|---|---|
| 1 | File 2025/2026 bị **từ chối** ("báo lỗi") khi có ảnh liên kết, bảng, đường kích thước… | Nguồn > 2024 chạy chế độ nghiêm ngặt: gặp bản ghi chưa có trong mẫu là dừng | 2025 (`T`, 2600) và 2026 (`U`, 2700) thành phiên bản chính thức; chế độ nghiêm ngặt chỉ còn cho > 2026 |
| 2 | **Bóng đổ trên ảnh bị xóa** | Nhận "bóng trong" bằng byte 0x18 = 4 — thực ra là kiểu làm mờ Gaussian | Nhận bằng byte 0x73 = 1 (6/6 bóng trong, 0/50 bóng đổ) |
| 3 | Chế độ nghiêm ngặt bỏ khóa `painter.*` của style tài liệu | Lược đồ 2019 rút từ file 2019 cũ | Corel 2026 Save As 2019 giữ nguyên các khóa này → không lọc nữa |
| 4 | Không đọc được file đang mở trong CorelDRAW | Mở file không cho chia sẻ ghi | `FileShare.ReadWrite \| Delete` |
| 5 | Kéo file hỏng/lạ vào có thể gây lỗi giao diện | `Probe` chỉ bắt một số loại lỗi | Bắt mọi lỗi, ghi "File hỏng hoặc lạ" |
| 6 | Bản ghi bị thay hai lần ghi đè nhau (X7: bóng đổ mất bước 2200 → 1300) | `RootDat.Read` đọc dữ liệu gốc thay vì nội dung đã thay | `Read` trả nội dung đã thay; tra chỉ số bằng bảng băm (file 13 000 bản ghi: < 1 s) |

## Quy tắc mới (đối chiếu file CorelDRAW 2026 tự Save As)
| Quy tắc | Áp dụng | Bằng chứng |
|---|---|---|
| Đối tượng thiếu style (trang, nhóm, nhóm hiệu ứng, **đối tượng** — vd chữ trong ô bảng; không phải layer) → thêm `{}`, xóa bit 0x40 `flgs` trang | **mọi** đích thấp hơn nguồn (trước chỉ ≤ 2019) | mọi mẫu, mọi đích |
| Bóng trong: **giữ** khi đích 2020–2025 | Corel giữ nguyên | T32, M07 |
| Thụt bullet 2019 = thụt 2020+ − độ rộng ký tự bullet × cỡ font bullet (đọc bảng `cmap`/`hmtx` của font đã cài); bullet "theo font chữ" → chép font/cỡ/độ đậm chữ vào `bulletFont`; khối `paragraph` rỗng cũng thêm `bulletFirstCharIndent` 48676 | đích ≤ 2019 | Arial 12pt 48676, Times đậm 28pt 28911, AIGDT 47999 (C_Converter 47994: lệch 0,5 µm do làm tròn) |
| Bóng đổ làm mờ Gaussian (0x18 = 4) chỉ có từ X8 → đổi sang "Average" (3) | đích X7 | Corel raster hóa 5/5 tài liệu có bóng Gaussian khi lưu X7 |
| Tô texture (fild kiểu 11, dạng 1300 lồng nhau): thêm 2 byte 0 cuối bản ghi, hai trường độ dài +2 | đích 2017 · X8 · X7 | trùng từng byte với Corel 2026 Save As; thiếu thì CorelDRAW 2026 không mở được |
| Bản ghi bảng/kích thước/nối (`npps`, `ppid`, `ppdt`), mẫu tô bitmap (`bmpf`, `bpft`), ảnh liên kết (`url`, `elbl`, `elbs`), đối tượng loại 7/22/38/39 | mọi đích — chép nguyên | Corel 2026 ghi y nguyên vào 2019 và X7 |

## Đối chiếu từng byte (1310 ca = 131 thiết kế × 10 đích)
`tools/analysis/byte_targets.py`: 1029/1310 ca trùng mọi bản ghi (bỏ qua GUID `spid` và ảnh raster hóa lại).
Phần còn lại đều đã phân loại:
- **Nhiễu do Corel**: chỉ số bảng tô/viền (đối số 0x0A/0x14 của loda, `stlt`) được đánh số lại mỗi lần lưu — các file
  Corel tự lưu ở 2025/2024/2022 cũng lệch nhau đúng như vậy;
- **Hình học dẫn xuất**: phần tử con của đường kích thước/đường nối được Corel dựng lại khi lưu (C_Converter giữ nguyên;
  Corel dựng lại khi mở);
- **Giới hạn đã biết** (có cảnh báo): bóng trong khi đích ≤ 2019, font biến thể, bóng Gaussian ở X7 (Corel raster hóa;
  C_Converter giữ vector, đổi kiểu làm mờ).

Corel 2019 mở file X7/2018 (kể cả file chính Corel 2026 lưu) lệch vị trí ~0,07 mm — đặc điểm của Corel 2019 khi đọc
bản cũ, không phải lỗi chuyển đổi.

## Mở lại bằng CorelDRAW (1310 ca, so cấu trúc + ảnh từng trang với file Corel tự lưu cùng bản đích)
`tools/harness/all_targets_cases.py` → `verify-2019.ps1` → `tools/analysis/compare_many.py`.

| Đích | Mở bằng | Ca | Mở được | Giống hệt file Corel tự lưu |
|---|---|---|---|---|
| 2019 · X7 | CorelDRAW 2019 | 262 | 262 | 252 |
| 2025 · 2024 · 2022 · 2021 · 2020 · 2018 · 2017 · X8 | CorelDRAW 2026 | 1048 | 1048 ¹ | 1036 |

¹ Lần chạy đầu 2 file tô texture (T30 → 2017, X8) không mở được trong CorelDRAW 2026 → tìm ra quy tắc tô texture
2017 trở xuống (thêm 2 byte, xem bảng quy tắc), sửa và mở lại được cả 6 file T30 (2017/X8/X7, nguồn 2024 và 2026).

Các ca còn khác đều là giới hạn đã biết (có cảnh báo trong nhật ký): bóng trong khi đích ≤ 2019 (T32, M07 trang 4),
font biến thể (T37), bóng Gaussian ở X7 (M01, M02, M05, M07 — Corel raster hóa, C_Converter giữ bóng vector),
1 điểm ảnh (X09) và chữ viền C23 (vài chục điểm ảnh, Corel vẽ không ổn định giữa các lần).
