# C_Converter — Giai đoạn 0: Khảo sát định dạng .cdr

Ký hiệu độ tin cậy:
- **[Chắc]**: được nhiều nguồn độc lập xác nhận (libcdr, Inkscape/LibreOffice import, mở file ZIP thấy ngay).
- **[libcdr]**: suy ra từ cách libcdr đọc file. Đúng cho mục đích *đọc*, nhưng chưa chắc đủ cho mục đích *ghi*.
- **[GĐ]**: giả định, cần kiểm chứng bằng file mẫu.

---

## 1. Các nhóm phiên bản

| Nhóm | Phiên bản (ước lượng) | Container | Ghi chú |
|---|---|---|---|
| A | CorelDRAW 1–2 | Không phải RIFF (chữ ký khác) [GĐ] | Bỏ qua, không có nhu cầu thực tế. |
| B | 3 → X3 | RIFF trần: `RIFF <u32 size> "CDR?"` [Chắc] | Ký tự thứ 4 của form type đổi theo phiên bản [Chắc]; bảng tra ký tự→phiên bản [GĐ]. Từ khoảng v8 có chunk `cmpr` chứa dữ liệu nén zlib [libcdr]; mốc phiên bản bắt đầu dùng `cmpr` [GĐ]. |
| C | X4 → hiện tại | File ZIP [Chắc], bên trong có luồng RIFF chính | Tên luồng theo libcdr: `content/riffData.cdr`, hoặc `content/root.dat` + `content/dataFileList.dat` + các `content/data/*.dat` [libcdr]; phiên bản nào dùng kiểu nào [GĐ]. Ngoài ra có `META-INF/`, metadata XML, ảnh thumbnail [GĐ về tên chính xác]. |

Hệ quả quan trọng: **phần XML trong ZIP chủ yếu là metadata, không phải bản vẽ**. Hình học vẫn nằm trong RIFF nhị phân ở mọi phiên bản từ 3 trở lên [libcdr]. Vì vậy "hạ phiên bản" từ 2020 xuống X7 vẫn là bài toán ghi RIFF nhị phân, không phải chỉnh XML.

### Chunk thường gặp [libcdr — tên chunk; ý nghĩa từng trường bên trong cần kiểm chứng]
- `vrsn`: số phiên bản dạng u16, ví dụ 1700 [libcdr]; ánh xạ /100 → tên sản phẩm (17 = X7…) [GĐ].
- `LIST doc `: dữ liệu cấp tài liệu — `mcfg` (cấu hình trang), bảng màu/fill/outline/style dùng chung, font (`font`), bitmap (`bmp `), mũi tên (`arrw`)…
- `LIST page` → `LIST layr` → `LIST obj ` / `LIST grp `: cây trang/layer/đối tượng/nhóm.
- Trong đối tượng: `loda` (dữ liệu hình: rect/ellipse/path/text/bitmap…), `trfd` (ma trận biến đổi), `lgob`, `flgs`, `bbox`, `usdn`…
- `fild`/`outl`: định nghĩa fill/outline, đối tượng tham chiếu bằng ID [libcdr]. Từ khoảng X6 cách lưu style thay đổi (`stlt`) [GĐ].
- `txsm`/`stlt`: text và style; định dạng text thay đổi mạnh qua các phiên bản (Unicode từ khoảng X4) [GĐ].
- `DISP`: ảnh xem trước [libcdr].
- Đơn vị toạ độ và độ dài trường số khác nhau giữa bản cũ/mới (libcdr có nhánh riêng cho từng mốc phiên bản) [libcdr; giá trị cụ thể GĐ].

## 2. Khả thi — nói thẳng

| Hạng mục | Đánh giá |
|---|---|
| Đọc cấu trúc chunk (cây RIFF, ZIP, cmpr) | **Khả thi cao.** Đã có công cụ `cdrdump`. |
| Đọc hình học cơ bản, màu fill/outline | **Khả thi.** libcdr làm được; Inkscape/LibreOffice dùng nó để import. |
| Đọc PowerClip, blend, mesh, lens, contour, effect | **Khó/không đầy đủ.** libcdr bỏ qua hoặc làm sai nhiều thứ trong nhóm này. |
| **Ghi .cdr** | **Rủi ro lớn nhất của dự án.** Theo hiểu biết của tôi chưa có writer mã nguồn mở nào. libcdr chỉ cho biết trường nào cần *để vẽ*; nó không cho biết trường nào CorelDRAW *đòi hỏi khi mở* (ID chéo, style mặc định, `mcfg`, bảng font, các chunk mà libcdr bỏ qua). Phải tự reverse engineer bằng so sánh nhị phân và thử-sai trên CorelDRAW thật. |
| Hạ phiên bản không mất mát | **Không khả thi tổng quát.** Kể cả CorelDRAW tự "Save As" phiên bản cũ cũng mất tính năng. Mục tiêu đúng là: giữ đúng hình ảnh, đơn giản hóa phần không ghi được, và báo cáo rõ. |

### Chiến lược writer đề xuất: "ghi dựa trên khuôn" (template-based)
1. Với mỗi phiên bản đích, lấy một file *trống* do chính CorelDRAW phiên bản đó lưu làm khuôn.
2. Giữ nguyên byte-for-byte mọi chunk chưa hiểu (`doc `, `mcfg`, style mặc định…).
3. Chỉ chèn/thay cây `page/layr/obj` và các bảng fill/outline mà ta đã hiểu qua so sánh nhị phân.
4. Tính lại mọi trường kích thước (RIFF/LIST, cmpr, ZIP).

Cách này giảm mạnh lượng thứ phải hiểu trước khi có file đầu tiên CorelDRAW mở được. Khuôn là file do người dùng tự tạo bằng bản Corel của họ, không phân phối kèm phần mềm (cần cân nhắc thêm nếu muốn đóng gói sẵn).

### Chọn phiên bản đích đầu tiên
Về định dạng, RIFF cũ (nhóm B) đơn giản hơn. Về nhu cầu thực tế, nhiều khả năng cần X7/X6/X5 (nhóm C). Đề xuất: **chọn theo nhu cầu thực tế**, vì vỏ ZIP chỉ là lớp bọc; phần khó (RIFF bên trong) là như nhau. Câu hỏi này đang chờ trả lời (mục 6).

### Phương án dự phòng (chuyển đổi định dạng, không phải hạ phiên bản)
- **PDF / AI (dạng PDF)**: hợp với CorelDRAW X3 trở lên [GĐ về chất lượng import từng bản].
- **EPS / AI cũ (dạng PostScript)**: hợp với bản rất cũ.
- **SVG**: import kém trên Corel đời cũ — chỉ dùng như lựa chọn cuối.
- CMX (định dạng trao đổi của Corel) có thể là ứng viên tốt vì mọi bản Corel đều mở được, nhưng tài liệu công khai không đầy đủ [GĐ] — để sau.

## 3. Rủi ro

1. CorelDRAW mở file sai cách "im lặng" (hiển thị thiếu, crash, hoặc báo file hỏng không nói lý do) → vòng lặp thử-sai chậm. Giảm thiểu: thay đổi từng chunk một, có kịch bản test cố định.
2. Trường phụ thuộc chéo (ID fill/outline/style/font, bảng tham chiếu) — sai một ID có thể hỏng cả file.
3. Có thể tồn tại checksum/hash ở đâu đó [GĐ — chưa thấy bằng chứng; so sánh mẫu sẽ cho biết].
4. Text: font, encoding, kerning, paragraph — rất tốn công. Vì vậy kế hoạch chuyển text → curves là đúng; nhưng để chuyển thành curves cần **font có trên máy chạy C_Converter** và engine dựng glyph (ví dụ qua DirectWrite/FreeType).
5. Bitmap: định dạng lưu, nén, palette, alpha khác nhau giữa các bản.
6. Màu: CMYK/Pantone/ICC — ghi sai profile dễ lệch màu khi in.
7. Mỗi bản phát hành phụ (SP/hotfix) có thể khác nhỏ trong định dạng [GĐ]. Cần ghi lại build chính xác của mẫu.
8. Pháp lý (không phải tư vấn pháp lý): reverse engineering phục vụ tương thích thường được chấp nhận ở nhiều nơi, nhưng EULA CorelDRAW có thể cấm reverse engineering *phần mềm*. Ta chỉ phân tích *file dữ liệu*, không phân tích mã CorelDRAW. libcdr theo giấy phép MPL-2.0: dùng lại mã thì các file đó phải giữ MPL; chỉ đọc để tham khảo thì không ràng buộc.

## 4. Công cụ đối chiếu (chỉ dùng khi phát triển, không phải phụ thuộc của C_Converter)
- **Inkscape** (import .cdr qua libcdr): render tham chiếu để so với preview của ta.
- **libcdr `cdr2raw` / `cdr2xhtml`**: in luồng lệnh vẽ mà libcdr hiểu — rất hữu ích để đối chiếu Reader. Có thể cài qua MSYS2 [GĐ về tên gói].
- **CorelDRAW trên máy test**: nguồn sự thật duy nhất cho Writer.

## 5. File mẫu cần chuẩn bị

**Nguyên tắc vàng:** tạo mỗi thiết kế bằng bản CorelDRAW mới nhất bạn có, sau đó dùng chính CorelDRAW *File → Save As → chọn Version* để lưu ra từng phiên bản thấp hơn. Bộ file này vừa là dữ liệu RE, vừa là **đáp án**: file do Corel tự hạ phiên bản cho biết đầu ra đúng trông thế nào.

Đặt tên: `Txx_<mô tả>_<phiên bản>.cdr`, ví dụ `T01_rect_X7.cdr`. Ghi lại build chính xác (Help → About) vào `samples/VERSIONS.txt`.

| Mã | Nội dung | Mục đích |
|---|---|---|
| T00 | Trang A4 trống, không đối tượng | Khuôn gốc cho writer |
| T01 | 1 hình chữ nhật 100×50 mm tại (10,10) mm, fill đỏ RGB(255,0,0), không outline | Đối tượng tối thiểu |
| T02 | Như T01, dời sang phải đúng 1 mm | Tìm trường toạ độ + đơn vị |
| T03 | Như T01, fill CMYK(0,100,100,0) | Mã hóa màu CMYK |
| T04 | Hai hình chữ nhật giống T01 cạnh nhau | ID, cấu trúc lặp |
| T05 | Ellipse 50×30 mm | |
| T06 | Đường thẳng 2 điểm, outline 1 mm đen | Outline |
| T07 | Đường cong Bézier 4 node (vẽ bằng Bézier tool) | Path |
| T08 | Như T06 nhưng outline nét đứt, màu xanh, có mũi tên | Outline nâng cao |
| T09 | Hình chữ nhật bo góc | Thuộc tính rect |
| T10 | Hình chữ nhật xoay 30° | Ma trận biến đổi |
| T11 | Group gồm T01 + T05 | Nhóm |
| T12 | 2 layer, mỗi layer 1 đối tượng | Layer |
| T13 | 2 trang, kích thước khác nhau (A4 dọc, A5 ngang) | Nhiều trang |
| T14 | Artistic text "Xin chào ĐƯỜNG" font Arial 24pt + một bản đã Convert to Curves | Text + tiếng Việt |
| T15 | Paragraph text vài dòng | Text khung |
| T16 | Bitmap: 1 PNG nhỏ có nền trong suốt + 1 JPG | Bitmap |
| T17 | Linear gradient đỏ→xanh | Gradient |
| T18 | Radial gradient | Gradient |
| T19 | PowerClip | Tính năng không hỗ trợ |
| T20 | Blend giữa 2 hình | Tính năng không hỗ trợ |
| T21 | Mesh fill | Tính năng không hỗ trợ |
| T22 | Transparency / lens | Tính năng không hỗ trợ |
| T99 | 3–5 file thật từ công việc (được phép chia sẻ) | Kiểm thử thực tế |

Mỗi thiết kế lưu ở: bản mới nhất bạn có + mọi bản đích muốn hỗ trợ (ví dụ 2020, X8, X7, X6, X5, X4, X3). Nếu một bản lưu có tùy chọn (nén, nhúng font, lưu kèm CMX…), giữ mặc định và ghi lại.

## 6. Tiêu chí hoàn thành Giai đoạn 0
- [x] `cdrdump` đọc được ZIP, RIFF, `cmpr` (đã chạy trên file giả lập; **chưa** chạy trên file .cdr thật).
- [ ] `cdrdump` chạy trên toàn bộ file mẫu không dừng ở "không giống FourCC"; nếu dừng → sửa giả định về `cmpr`/đệm.
- [ ] Bảng thực nghiệm: phiên bản ↔ container ↔ ký tự form type ↔ giá trị `vrsn` ↔ có `cmpr` hay không ↔ tên luồng RIFF trong ZIP.
- [ ] So sánh T01/T02/T03/T04 ở một phiên bản: xác định được chunk/offset chứa toạ độ, màu, ID.

## Cách dùng cdrdump
```
dotnet run --project tools/CdrDump -c Release -- samples/T01_rect_X7.cdr --stats
dotnet run --project tools/CdrDump -c Release -- samples/*.cdr --stats --out dumps
```
Sau đó so sánh `dumps/T01_rect_X7.cdr.tree.txt` với `dumps/T01_rect_2020.cdr.tree.txt` bằng công cụ diff bất kỳ.
