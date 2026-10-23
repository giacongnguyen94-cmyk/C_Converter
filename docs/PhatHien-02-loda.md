# Phát hiện 02 — Quy tắc hạ 2020–2024 → 2019 (bản ghi đối tượng, text, hiệu ứng)

Nguồn dữ liệu: 45 mẫu do `tools/harness/make-samples.ps1` vẽ trong CorelDRAW 2024 (v25.0.0.230) và lưu ra 2 bản
(`_25` gốc, `_21` = Corel tự Save As 2019), cộng 5 file thật. Kiểm chứng bằng CorelDRAW 2019 (v21.2.0.706) thật qua
`tools/harness/verify-2019.ps1`.

Mọi điều dưới đây **đã kiểm chứng**, trừ chỗ ghi [GĐ].

## 1. Bản ghi đối tượng `loda`
```
u32 length, u32 n, u32 offOffsets (=0x14), u32 offTypes (=0x14+4(n+1)), u32 objType,
u32 offsets[n+1]          (offset cuối = length)
u32 types[n]              (tăng dần)
dữ liệu                   offsets[i] thuộc về types[n-1-i] → dữ liệu xếp theo kiểu GIẢM dần
```
Đọc/ghi lại trùng từng byte trên mọi bản ghi của mọi mẫu (test `Loda_roundtrips_byte_for_byte`).

objType gặp: 0 nhóm/trang/layer, 1 chữ nhật, 2 ellipse, 3 đường/curve, 4 artistic text, 5 bitmap, 6 paragraph text,
8 extrude, 11/12/17 bản ghi layer, 15 contour, 20 polygon/star, 23 đổ bóng.

Đối số thường gặp: 0x0A số thứ tự [GĐ], 0x14 tham chiếu fill, 0x1E hình học, 0x64 liên kết, **0xC9 style JSON**
(`u32 1, u32 độ dài, JSON gọn`), 0xBB8 dữ liệu hiệu ứng.

## 2. Quy tắc chuyển
| # | Quy tắc | Bằng chứng |
|---|---|---|
| 1 | `loda` trong **page / grp / lnkg** thiếu style 0xC9 → thêm `{}`. Layer (`layr`) thì **không**. | 48 bản ghi; thống kê theo loại LIST chứa |
| 2 | Bản ghi trang được thêm style → xóa bit 0x40 ở byte đầu `flgs` của trang | mọi mẫu do COM tạo |
| 3 | JSON style (trong `loda`, `txsm`, `styles/document.cdss`): bỏ `font_axes`; trong `paragraph` bỏ `levels`, `bulletFormat`, `bulletInheritFont`, `bulletNumberStyle`, `bulletFirstCharIndentIncludesBullet`, `numberInheritFont` | T14, T15, T36, T40 |
| 4 | `bulletChar` mã 0x80–0x9F (byte CP1252) → Unicode (`9f000000` → `78010000` 'Ÿ') | file thật R02, R05 |
| 5 | Đổ bóng (objType 23, đối số 0xBB8 dài 127, header u32 2200): header → 1300, xóa 4 byte tại 0x7B rồi 4 byte tại 0x1D (vị trí theo bản 2200) | 4 biến thể: phẳng, mờ, phối cảnh, bóng chữ |
| 6 | **Bóng trong** (0xBB8 @0x18 = 4, có từ 2023): 2019 không có, mở là **crash**. Gỡ nhóm `lnkg`, mục tương ứng trong `lnkt` (bỏ cả `lnkt` nếu rỗng) và `spnd` không còn dùng | T32: 3/3 lần mở OK, ảnh trùng bản gốc 2024 |
| 7 | `txsm` (text) dạng 14 → 13: u32 @0x14 = 13; JSON (u32 độ dài + JSON thụt lề 2 cách) theo quy tắc 3, `paragraph` thiếu `bulletFirstCharIndent` → thêm `"48676"`; bullet mặc định ('•' Arial 12pt, `63500`) → `48676`; bỏ cấu trúc danh sách cuối bản ghi (`u32 n, n×(u32 c, c×u32)`, sau chuỗi text + byte 0) | T14, T23d, T40 trùng từng byte với Corel |

Bảng liên kết hiệu ứng `lnkt` (trong `doc`): `u32 length, u32 n, u32 0x0C, u32 offsets[n+1]`, mục 32 byte:
`u32 loại (0xE0 bóng, 0x80 contour), u32 spnd nguồn (nhóm lnkg), u32 spnd đích (đối tượng), …`.

## 3. Khác biệt vô hại (không chuyển)
- `spid` (GUID) của nhóm trong PowerClip/symbol: Corel sinh mới mỗi lần lưu.
- Đối số 0x0A đổi 1↔2 (đánh số lại).
- `txtj`: số liệu dàn trang chữ, 2019 tự tính lại.
- `metadata.xml`: Corel 2019 sắp xếp lại phần tử / namespace.
- `embed/*` (font nhúng), `previews/*.png`.

## 4. Giới hạn đã biết (đều có cảnh báo trong log)
| Trường hợp | C_Converter | Corel 2024 khi Save As 2019 |
|---|---|---|
| Bóng trong (2023+) | gỡ hiệu ứng, giữ đối tượng | raster hóa bóng thành bitmap |
| Font biến thể (vd Bahnschrift) | giữ chữ, cảnh báo; 2019 vẽ bằng kiểu mặc định → khác dáng | chuyển chữ thành curves |
| Bullet font khác Arial-mặc-định | giữ khoảng thụt 2024, cảnh báo; chữ có thể lệch ~1,5 mm so với bullet | tính lại theo độ rộng glyph (công thức chưa xác định: AIGDT không khớp phép đo hmtx) |

## 5. Kết quả hồi quy (2026-10-22)
- 41/41 file C_Converter tạo ra mở được bằng CorelDRAW 2019 (T00 trang trống không xuất được PNG ở cả hai phía).
- 39/42 giống hệt về cấu trúc + ảnh với file Corel tự hạ; 3 còn lại là mục 4.
- 30/42 trùng từng byte với file Corel tự hạ (bỏ qua GUID/bộ đếm lưu).
- 5/5 file thật: mở được và giống hệt về cấu trúc + ảnh.

## 6. Chế độ thử nghiệm cho nguồn 2025/2026 (chưa có mẫu thật)
- Nhận diện: `vrsn` > 2500, cấu trúc giống 2019–2024 (container.xml → `content/root.dat`, `fver` inline với u16 = vrsn,
  `mcfg` dạng 2100). Tên hiển thị "2025?"/"2026?" là ngoại suy (26 → 2025, 27 → 2026) [GĐ].
- Áp dụng mọi quy tắc mục 2, cộng lọc JSON theo lược đồ 2019 (`src/CConvert.Core/Schema2019.g.cs`, sinh bằng
  `tools/analysis/json2019.py` từ 54 file vrsn 2100). Object "động" (`style`, `levels`, `color styles`, …) không lọc khóa con.
- Dừng, không ghi file khi gặp: kiểu đối số `loda` ngoài 27 kiểu đã biết; hiệu ứng 0xBB8 ngoài
  {(23,2200,127), (23,1300,119), (15,1300,185)}; `txsm` khác dạng 13/14; chunk/LIST/loại đối tượng chưa biết.
- Không dùng trường @0x18 của `trfd` làm dấu phiên bản: giá trị thay đổi theo đối tượng (0x6CC, 0xBC, 0x1A7, 0x514).
- Kiểm chứng gián tiếp (2026-10-22): logic thử nghiệm = logic đã kiểm chứng trên 42 mẫu 2024; 5 file mô phỏng
  2600/2700 mở được bằng CorelDRAW 2019 thật, ảnh trùng 100% với bản Corel tự hạ.
