# Patient Appointment Manager v0.1

## Phạm vi bản đầu
- Ứng dụng WPF cho Windows, mục tiêu Windows 7 SP1 đến Windows 11.
- Cơ sở dữ liệu SQLite lưu tại `%LOCALAPPDATA%\\PatientAppointmentManager\\appointments.db`.
- Ba khu vực: Trình duyệt, Thêm lịch, Lịch đã hẹn.
- Thêm lịch thủ công bằng dữ liệu thử.
- Lọc từ ngày đến ngày, Sáng/Chiều/Cả ngày.
- Bộ đếm tổng số bản ghi khớp bộ lọc.
- Trả lịch bằng cách đổi trạng thái, không xóa bản ghi.

## Yêu cầu môi trường build
- Visual Studio 2022 hoặc MSBuild có hỗ trợ .NET Framework 4.8.
- .NET Framework 4.8 Developer Pack.
- NuGet restore.

Mở `PatientAppointmentManager.csproj`, restore NuGet rồi Build.

## Lưu ý quan trọng
Đây là bộ khung v0.1, chưa phải phần mềm dùng trong vận hành bệnh viện:
- Khu vực trình duyệt hiện là placeholder, chưa tích hợp Chromium.
- Chưa đọc DOM, chưa tự nhận diện mã bệnh nhân hoặc chỉ định CLVT.
- Chưa có kiểm thử trên Windows 7.
- Chưa có cơ chế phân quyền người dùng hay mã hóa cơ sở dữ liệu.
- Không nhập dữ liệu bệnh nhân thật cho đến khi hoàn tất kiểm thử và đánh giá bảo mật.

## Kiến trúc tiếp theo
1. Tích hợp Chromium Embedded Framework (CEF) với runtime tương thích Windows 7.
2. Xây dựng adapter đọc DOM có cấu hình, không phụ thuộc một URL cố định.
3. Nhận diện chỉ định CLVT và định danh từng chỉ định.
4. Hoàn thiện popup Giữ lịch / Trả lịch, kiểm tra trùng theo quy tắc đã thống nhất.
5. Bổ sung sao lưu/khôi phục, nhật ký đầy đủ và kiểm thử dữ liệu.
