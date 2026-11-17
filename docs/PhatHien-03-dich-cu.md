# Phát hiện 03 — Bản đích X7, X8, 2017, 2018 (và 2020–2023)

Ngày 2026-11-17. Trên máy chỉ còn CorelDRAW 2019 (bản 2024 đã gỡ).

## Nguồn đáp án
| Nguồn | Nội dung |
|---|---|
| CorelDRAW 2019 tự Save As | 47 thiết kế 2019 (42 mẫu + 5 file thật) lưu xuống 17/18/19/20 = 188 file (`tools/harness/save-older.ps1` → `samples/older`) |
| CorelDRAW 2024 tự Save As | T00/T01 của người dùng ở mọi bản 17–25 (`File mau/`) |

## Hai cách ghi hợp lệ
Corel 2019 khi lưu xuống ≤ 2018 **ghi lại toàn bộ theo kiểu của nó**: bỏ mọi `spid` (GUID), gộp/đánh số lại
bảng outline/fill (`outl`, `fild` → dạng 1300), thêm đối số `loda` 0x2EE0 = 0, đổi thứ tự khóa JSON, bỏ khối `export`
của `document.cdss`, tự thêm `trfd` cho nhóm extrude…

Corel 2024 khi lưu xuống X7–2018 thì **tối thiểu**: giữ nguyên mọi bản ghi (kể cả `spid`, `fild` dạng 2400), chỉ đổi
những thứ dưới đây. C_Converter theo cách tối thiểu này (nguồn của ta là file 2020+ giống Corel 2024).

Corel 2019 lưu xuống X7, X8, 2017, 2018 với **cùng một định dạng** — chỉ khác số phiên bản, riêng X7 thêm `trfd`.

## Quy tắc 2019 → X7 / X8 / 2017 / 2018
| Thành phần | Quy tắc | Kiểm chứng |
|---|---|---|
| `root.dat`, metadata, cdss | ký tự form `H/J/K/L`, `fver`, `vrsn`, `CoreVersion`; `DocFormatterId` = **1700** (mọi bản X7–2019; 2020+ là 1800); cdss `version` | T01: trùng từng byte với Corel 2024 |
| `mcfg` | dạng 2100 (7944 byte) → 1300 (8323 byte): header u16 1300 + u32 độ dài; chèn tại 0x365 hai lần u32 đọc ở 0x347; chèn tại 0x372 khối 371 byte cố định (`McfgConverter`) | 188/196 cặp trùng từng byte (cả Corel 2019 lẫn 2024); 8 cặp còn lại (tài liệu nhiều trang) chỉ khác 1 giá trị trạng thái trang Corel tự tính lại |
| `trfd` (chỉ X7) | đọc bảng offset (`u32 len, u32 n, u32 off, …, offsets[n+1]`); mỗi mục mang dấu 0x6CC (1740) → 0x514 (1300), u32 kế tiếp → 0 | 1000/1000 mục trùng từng byte |
| `txsm` (text) | không đổi — X7–2019 đều dạng 13 | 66/66 khối |

Chuỗi đầy đủ cho nguồn 2020–2024: bước 2020+ → 2019 (docs/PhatHien-02-loda.md) rồi bước trên.
Nguồn X8–2019 cũng được nhận khi đích thấp hơn.

## Bản đích 2020–2023 (thử nghiệm)
Corel 2024 lưu T00/T01 xuống 2020/2021/2022/2023: chỉ khác số phiên bản (`fver`, `vrsn`, ký tự form,
`CoreVersion`, cdss `version`), `DocFormatterId` 1800, `mcfg` giữ dạng 2100. C_Converter trùng từng byte trên T01.
Chỉ có 2 mẫu đơn giản → gắn nhãn thử nghiệm. Bóng trong được gỡ khi đích < 2024 (thận trọng: chưa rõ bản nào bắt đầu có).

Ghi chú: `File mau/T00_24.cdr` thực ra là dạng 2023 (`vrsn` 2430) — tên file bị nhầm.

## Giới hạn kiểm chứng
Không có CorelDRAW X7/X8/2017/2018 để mở thử. Bằng chứng gồm: (1) trùng từng byte với cách Corel 2024 tự hạ trên
T01; (2) các quy tắc định dạng (`mcfg`, `trfd`) trùng với cả Corel 2019 và 2024 trên 188 file; (3) mọi file kết quả
mở được bằng CorelDRAW 2019 và hiển thị giống file Corel tự lưu (mục kết quả bên dưới).

## Kết quả kiểm chứng (2026-11-17)
47 thiết kế bản 2024 (42 mẫu + 5 file thật) → X7, X8, 2017, 2018 bằng C_Converter (188 file), mở bằng CorelDRAW 2019
cùng 188 file CorelDRAW 2019 tự Save As (`tools/analysis/compare_targets.py`):

| Đích | Mở được | Giống hệt bản Corel tự lưu (cấu trúc + ảnh) |
|---|---|---|
| X7 | 47/47 | 44/47 |
| X8 | 47/47 | 44/47 |
| 2017 | 47/47 | 44/47 |
| 2018 | 47/47 | 44/47 |

3 mẫu khác ở mọi đích đều là giới hạn đã biết của bước 2020+ → 2019 (có cảnh báo trong nhật ký): bóng trong (Corel
raster hóa, C_Converter gỡ), font biến thể (Corel chuyển curves), bullet font AIGDT. T00 (trang trống) không xuất được
PNG ở cả hai phía. Test tự động: 264/264.

## Kiểm chứng nhiều trường hợp (2026-11-17, lần 2)
`tools/harness/many_cases.py` dùng C_Converter (CLI) tạo 990 ca, mở tất cả bằng CorelDRAW 2019 (`verify-2019.ps1`)
cùng 245 file đáp án CorelDRAW tự Save As, so bằng `tools/analysis/compare_many.py`:

| Nhóm | Đường chuyển | Ca | Mở được | Giống hệt bản Corel tự lưu |
|---|---|---|---|---|
| A | nguồn 2024 → X7 · X8 · 2017 · 2018 · 2019 | 235 | 235 | 220 |
| B | nguồn 2019 → X7 · X8 · 2017 · 2018 | 188 | 188 | 188 |
| C | nguồn 2018 · 2017 · X8 → bản thấp hơn | 282 | 282 | 282 |
| D | chuỗi 2024 → 2020/2021/2022/2023 → 2019, và 2024 → 2022 → X7 | 235 | 235 | 220 |
| E | T00/T01 của người dùng, nguồn 2020 – 2024 → X7 … 2019 | 50 | 50 | 50 |
| | **Tổng** | **990** | **990** | **960** |

30 ca khác đều thuộc 3 thiết kế giới hạn đã biết (mỗi thiết kế 10 ca, có cảnh báo trong nhật ký): T32 bóng trong,
T36 bullet font AIGDT, T37 font biến thể. Nhóm D cho thấy đích 2020–2023 không làm hỏng gì (đi qua chúng rồi hạ tiếp
cho kết quả giống hệt đường thẳng). Test tự động thêm: đích 2020–2023 chỉ đổi `fver`/`vrsn`; nguồn 2018/2017/X8 hạ
tiếp trùng file Corel; 19 kiểu file hỏng × 9 đích không treo, không để lại file dở. Tổng 352/352.
Nhãn "thử nghiệm" đã bỏ khỏi giao diện; nguồn 2025/2026 vẫn kiểm tra nghiêm ngặt (gặp bản ghi lạ thì không ghi file).
