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
