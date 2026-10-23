# Phát hiện từ mẫu T00 (trang trống) và T01 (1 hình chữ nhật đỏ)

Mẫu: `File mau/T00_*.cdr`, `File mau/T01_*.cdr`, các phiên bản 17, 18, 19, 20, 21, 22, 23, 24, 24.3, 25.
Công cụ: `tools/CdrDump` (cây chunk), `tools/analysis/cdrtree.py` (so sánh nội dung chunk).

Mọi điều dưới đây **đã kiểm chứng trên 20 file mẫu này**, trừ chỗ ghi [GĐ]. Mẫu chỉ có 1 đối tượng nên kết luận *chưa* áp dụng được cho text, bitmap, hiệu ứng.

## 1. Container (17 → 25 giống nhau)
- ZIP, mục đầu là `mimetype` không nén = `application/x-vnd.corel.zcf.draw.document+zip` (kiểu ODF).
- `META-INF/container.xml` trỏ tới `content/root.dat`.
- `content/root.dat` là RIFF `CDR?`; ký tự thứ 4 đổi theo phiên bản (xem bảng mục 3).
- `content/dataFileList.dat`: danh sách tên file cách nhau bằng `\n`: `data1.dat`, `masterPage.dat`, `page1.dat`.
- Kèm: `previews/*.png`, `color/*.xml`, profile ICC (T01 nhúng `uswebcoatedswop.icc` 557 KB — vì vậy file nặng 400 KB), `styles/document.cdss` (JSON), `META-INF/metadata.xml`, `textinfo.xml`, `links.xml`.

## 2. Chunk lá trong root.dat là con trỏ 16 byte
```
u32 fileIndex   // chỉ số trong dataFileList (0-based); 0xFFFFFFFF = dữ liệu inline
u32 length
u32 offset      // hoặc: tối đa 8 byte dữ liệu inline (byte 8..15)
u32 reserved    // = 0 trên mọi mẫu
```
Đã kiểm chứng: offset+length của các chunk liên tiếp khớp đúng kích thước từng file `.dat`.
Cấu trúc: `doc ` → `data1.dat`; trang master (`page[0]`) → `masterPage.dat`; trang 1 → `page1.dat`.

## 3. Phiên bản
| Sản phẩm | Mẫu | `vrsn` | form type | `fver` (8 byte) | `DocFormatterId` |
|---|---|---|---|---|---|
| X7 | 17 | 1700 | `CDRH` | `A4 06 01 00 00 00 11 00` | 1700 |
| X8 | 18 | 1800 | `CDRJ` | `08 07 01 00 00 00 12 00` | 1800 |
| 2017 | 19 | 1900 | `CDRK` | `6C 07 01 00 00 00 13 00` | 1800 |
| 2018 | 20 | 2000 | `CDRL` | `D0 07 01 00 00 00 14 00` | 1800 |
| **2019** | 21 | 2100 | `CDRM` | `34 08 01 00 00 00 15 00` | **1700** |
| 2020 | 22 | 2200 | `CDRN` | `98 08 01 00 00 00 16 00` | 1800 |
| 2021 | 23 | 2300 | `CDRO` | `FC 08 01 00 00 00 17 00` | 1800 |
| 2022 | 24 | 2400 | `CDRP` | `60 09 01 00 00 00 18 00` | 1800 |
| 2023 | 24.3 | 2430 | `CDRQ` | `7E 09 01 00 00 03 18 00` | 1800 |
| 2024 | 25 | 2500 | `CDRR` | `C4 09 01 00 00 00 19 00` | 1800 |

Tên sản phẩm do người dùng xác nhận (máy có 2024 và 2019; mẫu tạo bằng 2024 rồi Save As).
`fver` = `u16 vrsn, u16 1, u8 0, u8 bản cập nhật, u8 phiên bản chính, u8 0` [tên trường GĐ].
`metadata.xml`: `CoreVersion` = vrsn; `Revision` là bộ đếm số lần lưu (không phụ thuộc phiên bản).

## 4. Khác biệt giữa các phiên bản — rất ít
Cây chunk **giống hệt nhau** 17→25. Nội dung chunk trùng từng byte, trừ:

| Thành phần | Khác biệt |
|---|---|
| `fver`, `vrsn` | số phiên bản |
| `doc/mcfg` | 2 dạng: 17–20 (8323 byte, header `u16=1300`) và 21–25 (7944 byte, header `u16=2100`). Bản 21+ bỏ 8 byte tại 0x365 và 371 byte tại 0x37A (khối có chuỗi UTF‑16 `CorelDefaultID_10900` …). Header: `u16 phiên bản bản ghi, u16 0, u32 độ dài còn lại`. |
| `trfd` | 17: trường @0x18 = `0x0514` (1300), @0x1C = 0; 18+: `0x06CC` (1740), @0x1C = `0x38` [ý nghĩa GĐ] |
| `metadata.xml` | `CoreVersion`, `Revision`, `DocFormatterId` |
| `document.cdss` | `version`; GUID; từ 22 (2020) thêm khóa `"font_axes"` → 17 KB thành 25 KB |
| `mcfg` của 24.3 | khác 24 ở vài byte (GĐ: cài đặt máy, không phải định dạng) |

**Hệ quả:** trong dải X7–2023, với tài liệu đơn giản, hạ phiên bản = viết lại có chủ đích một số bản ghi (`fver`, `vrsn`, `mcfg`, `trfd`, `metadata.xml`, `document.cdss`) — đúng thứ Corel tự làm khi Save As, chứ **không phải** chỉ sửa số phiên bản. Hướng đi: **bộ chuyển đổi theo từng loại chunk** (chunk nào không đổi thì chép nguyên, chunk nào có quy tắc thì chuyển), thay vì giải mã toàn bộ rồi ghi lại từ đầu. Hướng này dễ hơn nhiều so với kiến trúc Reader → Model → Writer ban đầu cho dải này; kiến trúc đầy đủ vẫn cần cho phương án dự phòng và bản cũ hơn X7.

## 5. Đối tượng hình chữ nhật (page[1]/…/layr[3]/obj[0])
- Đơn vị: **0,1 µm** (254 000 đơn vị/inch). `bbox` = (-1479500, -1047000, -479500, -1547000) → 100 × 50 mm ✓.
- Gốc toạ độ / chiều trục Y: [GĐ] — cần T02 (dời 1 mm) để xác định.
- `loda` (558 byte), kiểu bản ghi = 1 (rectangle, khớp cách gọi của libcdr). Header: `u32 length, u32 numArgs=6, u32 offsetBảngOffset=0x14, u32 offsetBảngKiểu=0x30, u32 chunkType=1`; kiểu đối số: 0x0A, 0x14, 0x1E, 0x64, 0xC9, 0x9C41.
- Có **chuỗi JSON** trong `loda`: `{"fill":{"primaryColor":"RGB255,USER,255,0,0,100,…"},"outline":{…"width":"0"},"transparency":{}}`.
- Hình học: chữ nhật kích thước gốc (double) 6 705 941 × −4 582 … nhân với ma trận `trfd` (6 double tại @0x28: a=0.14912, b=0, tx=−1479500, c=0, d=0.10912, ty=−1047000) → đúng 100 × 50 mm. Nghĩa là hình được vẽ rồi co giãn, Corel lưu kích thước gốc + ma trận.
- `fild` (121 byte) và `outl` (237 byte) trong `doc ` chứa số `0x0960` = 2400 **ngay cả trong file X7** → bản ghi này Corel chép nguyên khi hạ phiên bản (số này không phải phiên bản tài liệu).

## 6. Câu hỏi còn mở
1. File X7 do Corel 2022 hạ xuống có thật sự mở được trên X7 thật không (kể cả chuỗi JSON trong `loda`)? → cần thử.
2. Ý nghĩa của khối 379 byte trong `mcfg` mà bản 21+ bỏ đi; khi hạ 21+ → ≤20 có thể chép từ khuôn T00 của bản đích.
3. Mẫu tạo *gốc* trên X7 có khác mẫu do 2022 hạ xuống không?

## 7. Hạ 2020–2024 → 2019: đã có bản thử nghiệm (src/CConvert.Core)
So 2024 với 2019 trên T00/T01: cây chunk giống hệt; `mcfg`, `trfd` và mọi chunk khác **trùng từng byte**. Khác biệt cần chuyển:
1. `root.dat`: ký tự form type → `M`; `fver`, `vrsn` → 2100.
2. `metadata.xml`: `CoreVersion` → 2100, `DocFormatterId` → 1700.
3. `document.cdss`: `version` → 2100; bỏ mọi khóa `font_axes`; trong object `paragraph` bỏ `levels`, `bulletFormat`, `bulletInheritFont`, `bulletNumberStyle`, `bulletFirstCharIndentIncludesBullet`, `numberInheritFont`. Quy tắc giống nhau cho mọi nguồn 2020–2024. Corel viết object rỗng là `{` + xuống dòng + `}`.

Kết quả: chuyển T00/T01 từ 2020, 2021, 2022, 2023, 2024 → 2019 cho ra file **trùng từng byte** với file 2019 do Corel lưu (bỏ qua `Revision` và GUID ngẫu nhiên của palette) ở 9/10 trường hợp. Trường hợp còn lại (T00_24.3) khác vì chính file nguồn đã khác các bản T00 khác (`mcfg`, palette) — không phải lỗi chuyển đổi.

Chunk/LIST/kiểu `loda` chưa có trong mẫu → bộ chuyển đổi cảnh báo rõ trong log thay vì im lặng chép qua.
