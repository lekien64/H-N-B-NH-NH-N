# Sổ hẹn CT

Ứng dụng WPF quản lý lịch hẹn bệnh nhân, sử dụng .NET Framework 4.8, SQLite và CefSharp 109.1.110.

## v0.4 Hybrid Browser
- Sidebar tự thu gọn/mở rộng khi rê chuột, ba nút điều hướng dạng biểu tượng.
- Trình duyệt tab 1 hỗ trợ nhiều tab riêng biệt.
- Thanh địa chỉ trống khi khởi động, hỗ trợ Enter và gợi ý lịch sử URL.
- Có nút zoom +/-, đưa zoom về 100% ở tab 1 và tab 2.
- Dòng lịch đã trả được hiển thị gạch ngang và nhãn “Đã trả hẹn”.
- Có lưu thông tin đăng nhập theo host bằng Windows DPAPI CurrentUser; thao tác lưu/tự điền do người dùng chủ động, không tự submit.

## Build
Mở `PatientAppointmentManager.csproj` bằng Visual Studio có hỗ trợ .NET Framework 4.8 hoặc chạy workflow GitHub Actions `Build So hen CT v0.4`.

## Lưu ý
Giữ nguyên CefSharp 109 để duy trì khả năng chạy Windows 7 SP1. Bản mã nguồn này đã kiểm tra cấu trúc XML và đối chiếu event handler, nhưng chưa được biên dịch trong môi trường hiện tại. Hãy build và thử trên Windows trước khi thay bản ổn định.


## v0.5.0 - Quét và đối chiếu danh sách chỉ định

- Thêm nút Quét DS trong Tab 2.
- Đọc bảng HTML có cột Số phiếu và Mã BN; dùng Số phiếu làm Mã chỉ định (ExamCode) theo xác nhận của người dùng.
- Đối chiếu theo cặp Mã BN + Số phiếu với lịch đang ở trạng thái Đã hẹn trong SQLite.
- Hiển thị thông tin chỉ định và lịch đã lưu trong cửa sổ kết quả.
- Cho phép trả hẹn mềm lịch cũ sau khi xác nhận; không xóa dữ liệu.
- Nút Giữ lịch không thay đổi lịch hiện có.
- Không tự động tạo lịch mới từ dữ liệu quét.

Lưu ý: bộ quét hiện đọc bảng HTML đang hiển thị trong trang hiện tại. Cần kiểm thử trên website bệnh viện thực tế để xác nhận selector và định dạng cột trước khi sử dụng chính thức.


## v0.5.1 - Tạo lịch từ kết quả quét

- Bổ sung tuổi và trạng thái tiêm nhận diện từ tên dịch vụ vào kết quả quét.
- Dòng chưa có lịch có thể mở cửa sổ tạo lịch, chỉ chọn ngày và buổi hẹn.
- Tự điền mã BN, tên bệnh nhân, khoa/phòng, số phiếu (Mã chỉ định), dịch vụ và trạng thái tiêm.
- Chặn tạo lịch nếu thiếu dữ liệu bắt buộc, không phân loại được tiêm, hoặc lịch đã xuất hiện trong lúc thao tác.
- Bổ sung cột Ngày tạo lịch trong danh sách Tab 3 từ trường CreatedAt hiện có.
- Sửa kiểm tra số dòng quét dùng ArrayList.Count.

Lưu ý kiểm thử: phân loại Có tiêm/Không tiêm hiện dựa trên từ khóa trong tên dịch vụ. Nếu tên dịch vụ không thể hiện rõ, ứng dụng yêu cầu kiểm tra thay vì tự đoán. Cần kiểm thử trên website thực tế và build GitHub Actions trước khi dùng chính thức.


## v0.5.2 - Fix build reference

- Thêm tham chiếu tường minh `System.Web` bên cạnh `System.Web.Extensions` để WPF MarkupCompilePass1/MSBuild resolve được assembly phụ thuộc.
- Giữ nguyên .NET Framework 4.8 và CefSharp 109.1.110 để không làm mất mục tiêu Windows 7.
- Cảnh báo NU1903 của CefSharp vẫn cần được đánh giá riêng; không nâng phiên bản tự động vì có thể mất tương thích Windows 7.


## v0.5.3 - Sửa bộ đọc DOM bảng chỉ định

- Không còn phụ thuộc riêng vào `thead th`; lấy tiêu đề từ `thead`, hàng đầu tiên hoặc ARIA columnheader.
- Hỗ trợ bảng HTML và bảng có ARIA role grid/table/row.
- Chuẩn hóa tiêu đề tiếng Việt có dấu/không dấu trước khi ánh xạ cột.
- Thêm thông tin chẩn đoán số hàng/tiêu đề khi bảng được tìm thấy nhưng không đọc được bệnh nhân.
- Giữ nguyên .NET Framework 4.8 và CefSharp 109.


## v0.5.4 - Ánh xạ theo cấu trúc bảng bệnh viện đã xác nhận

- Ưu tiên selector `table._st-table_gefxy_208` đã được xác nhận bằng báo cáo DOM.
- Đọc các dòng có đủ 13 ô và ánh xạ theo vị trí cột thực tế (STT ở cột 0; Số phiếu cột 1; Mã BN cột 3; ...).
- Không phụ thuộc tiêu đề cột có bị rút gọn hay không.
- Trả về số dòng ứng viên và số dòng hợp lệ để hỗ trợ chẩn đoán.
- Giữ nguyên cặp đối chiếu Mã BN + Số phiếu.


## v0.5.5 - Hoàn thiện thao tác tạo lịch từ kết quả quét

- Sửa thứ tự phần tử trong DockPanel để thanh nút Trả hẹn / Giữ lịch / Tạo lịch hẹn / Hủy luôn hiển thị ở cạnh dưới cửa sổ.
- Mở rộng cửa sổ kết quả quét.
- Mở rộng cột Dịch vụ và thêm tooltip chứa toàn bộ tên dịch vụ khi rê chuột.
- Cửa sổ tạo lịch chỉ yêu cầu người dùng chọn Ngày hẹn và Buổi hẹn; các trường bệnh nhân, mã BN, số phiếu, khoa/phòng, dịch vụ và trạng thái tiêm vẫn tự lấy từ dữ liệu quét.
- Giữ kiểm tra trùng lần cuối trước khi ghi SQLite.


## v0.5.6 - Sửa ngày hiển thị, phân loại tiêm và bố cục nút tạo lịch

- Đổi định dạng SQLite `strftime` từ `%y` sang `%Y` để tương thích phiên bản SQLite cũ đi kèm ứng dụng; ngày hẹn và ngày tạo lịch không còn trả về NULL do mã định dạng không hỗ trợ.
- Chuẩn hóa dấu tiếng Việt trước khi phân loại Có tiêm/Không tiêm; ưu tiên cụm phủ định trước cụm khẳng định, không dùng riêng từ chung `cản quang` để kết luận Có tiêm.
- Tăng chiều cao tự động cửa sổ tạo lịch, cho phép co giãn và căn giữa nội dung nút Tạo lịch hẹn/Hủy để không bị cắt.
- Trường hợp dịch vụ vẫn không đủ dữ liệu để kết luận vẫn giữ trạng thái Chưa xác định, không tự đoán.

## v0.5.7 - Hoàn thiện thao tác lịch và giao diện thông báo

- Bỏ nút DOM khỏi giao diện người dùng; giữ nguyên phần mã đọc cấu trúc nội bộ để phục vụ phát triển.
- Thay nút Giữ lịch bằng Đổi lịch. Đổi lịch chỉ cập nhật ngày/buổi trên bản ghi đang hẹn và ghi nhật ký thao tác, không tạo bản ghi mới.
- Quy tắc thao tác: chưa có lịch hoặc đã trả hẹn chỉ được Tạo lịch hẹn; đang có lịch được Đổi lịch hoặc Trả hẹn.
- Trạng thái Chưa xác định vẫn cho phép tạo lịch, hiển thị cảnh báo và lưu nguyên trạng thái này.
- Bộ lọc Tiêm thuốc cản quang bổ sung Chưa xác định; Tất cả vẫn bao gồm mọi trạng thái. Tổng số và số ca còn hẹn tiếp tục tính cả ca chưa xác định.
- Đọc tên dịch vụ đầy đủ từ nội dung Popover đã có trong DOM thông qua aria-describedby, không mô phỏng rê chuột hoặc chờ tooltip; nếu không có nội dung đầy đủ thì giữ dữ liệu đọc được và phân loại an toàn là Chưa xác định.
- Chuẩn hóa các hộp thông báo của ứng dụng thành hộp thoại bo góc, nút đồng nhất. Tăng chiều cao nút trong cửa sổ tạo/đổi lịch để tránh hụt chữ.
- Giữ nguyên cơ sở dữ liệu hiện có, không xóa lịch cũ; không thay đổi CefSharp 109.1.110 và .NET Framework 4.8.

**Trạng thái kiểm tra:** Đã kiểm tra XML XAML/Project và rà soát các thay đổi mã nguồn. Môi trường tạo gói hiện tại không có MSBuild/Visual Studio nên chưa thể xác nhận biên dịch. Cần chạy GitHub Actions và kiểm tra thực tế trên Windows 7/Windows 11 trước khi sử dụng.

## v0.5.8 – Service extraction and appointment list readability

- Improved service-name extraction: reads existing Ant Design Popover content, title/ARIA/data attributes, and React props/fiber data when available. No mouse simulation or hover delay is used.
- If a service still appears truncated with `...`, the scan results remain available but the app shows a warning identifying that some service names are incomplete. Verify those rows before creating appointments.
- Appointment list columns are explicitly ordered with **Ngày tạo lịch** as the final column.
- Improved department column width/alignment, increased table text size and row height, and applied subtle alternating row backgrounds.
- Shift colors: Sáng (soft amber), Chiều (soft blue). Injection colors: Có tiêm (green), Không tiêm (gray), Chưa xác định (orange).
- Existing SQLite data and schema are preserved.

Build status: source package and static XML/JavaScript syntax checks only. A Windows/MSBuild compilation and live hospital-site scan are still required before treating full service extraction as verified.

## v0.5.9 – Appointment grid readability and click-to-open history

- Increased appointment table body text to 15.5 px, header text to 14.5 px, row height to 42 px, and header height to 44 px.
- Standardized column alignment: patient identifiers/names/department/service left-aligned; ID, order code, injection, appointment date, shift, status, and created date centered. Header alignment follows its column.
- Strengthened the background colors for morning/afternoon and injection classifications so the states are easier to distinguish.
- Clicking a browser history entry with the mouse now navigates immediately in the corresponding browser. Keyboard navigation through the history list does not automatically navigate; Enter and the Open button remain available for manually entered URLs.
- Preserves the v0.5.8 service extraction and appointment data logic.

Validation: XAML and project XML are well-formed. This environment does not have MSBuild/.NET Framework build tools, so a Windows build and live click-to-open test are still required.
