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
