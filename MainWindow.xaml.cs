using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using PatientAppointmentManager.Data;
using PatientAppointmentManager.Models;

namespace PatientAppointmentManager
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Database.Initialize();
            AppointmentDatePicker.SelectedDate = DateTime.Today.AddDays(1);
            FromDatePicker.SelectedDate = DateTime.Today;
            ToDatePicker.SelectedDate = DateTime.Today.AddDays(30);
        }

        private void ShowView(string name)
        {
            BrowserView.Visibility = name == "browser" ? Visibility.Visible : Visibility.Collapsed;
            AddView.Visibility = name == "add" ? Visibility.Visible : Visibility.Collapsed;
            ListView.Visibility = name == "list" ? Visibility.Visible : Visibility.Collapsed;
            if (name == "list") LoadAppointments();
        }
        private void BrowserTab_Click(object sender, RoutedEventArgs e) { ShowView("browser"); }
        private void AddTab_Click(object sender, RoutedEventArgs e) { ShowView("add"); }
        private void ListTab_Click(object sender, RoutedEventArgs e) { ShowView("list"); }

        private void OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Bản v0.1 chưa tích hợp Chromium. Chức năng mở website sẽ được bổ sung ở bước tích hợp trình duyệt.", "Trình duyệt", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddAppointment_Click(object sender, RoutedEventArgs e)
        {
            var code = (PatientCodeBox.Text ?? "").Trim();
            var name = (PatientNameBox.Text ?? "").Trim();
            if (code.Length == 0 || name.Length == 0 || AppointmentDatePicker.SelectedDate == null || ShiftBox.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng nhập mã bệnh nhân, tên bệnh nhân, ngày hẹn và buổi hẹn.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var exam = (ExamCodeBox.Text ?? "").Trim();
            if (Database.HasActiveAppointment(code, exam))
            {
                var choice = MessageBox.Show("Bệnh nhân đã có lịch hẹn đang hiệu lực. Bạn có muốn giữ lịch cũ không? Chọn No để hủy thao tác thêm lịch, sau đó kiểm tra và trả lịch cũ nếu cần.", "Phát hiện lịch trùng", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.Yes) return;
                return;
            }
            var shift = ((ComboBoxItem)ShiftBox.SelectedItem).Content.ToString();
            Database.Add(new Appointment { PatientCode = code, PatientName = name, ExamCode = exam, ServiceName = ServiceBox.Text, AppointmentDate = AppointmentDatePicker.SelectedDate.Value, Shift = shift });
            MessageBox.Show("Đã thêm lịch hẹn vào cơ sở dữ liệu trên máy tính.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            PatientCodeBox.Clear(); PatientNameBox.Clear(); ExamCodeBox.Clear();
        }

        private void LoadAppointments()
        {
            var from = FromDatePicker.SelectedDate ?? DateTime.Today;
            var to = ToDatePicker.SelectedDate ?? from;
            if (to < from) { MessageBox.Show("Ngày đến phải bằng hoặc sau ngày bắt đầu."); return; }
            var shift = ((ComboBoxItem)FilterShiftBox.SelectedItem).Content.ToString();
            DataTable data = Database.GetAppointments(from, to, shift, IncludeReturnedCheck.IsChecked == true);
            AppointmentsGrid.ItemsSource = data.DefaultView;
            CountText.Text = data.Rows.Count + " ca";
        }
        private void Refresh_Click(object sender, RoutedEventArgs e) { LoadAppointments(); }
        private void Filter_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) LoadAppointments(); }

        private void ReturnAppointment_Click(object sender, RoutedEventArgs e)
        {
            var view = AppointmentsGrid.SelectedItem as DataRowView;
            if (view == null) { MessageBox.Show("Hãy chọn một lịch trong danh sách."); return; }
            if (MessageBox.Show("Xác nhận trả lịch đã chọn? Dữ liệu vẫn được giữ trong lịch sử.", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Database.Return(Convert.ToInt64(view["Id"]));
            LoadAppointments();
        }
    }
}
