using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using CefSharp;
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

            HospitalBrowser.FrameLoadEnd += (sender, args) =>
            {
                if (args.Frame.IsMain)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UrlBox.Text = args.Url;
                    }));
                }
            };
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
            var address = (UrlBox.Text ?? "").Trim();
            if (address.Length == 0) return;
            if (!address.Contains("://")) address = "https://" + address;

            Uri uri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show("Chỉ hỗ trợ địa chỉ website bắt đầu bằng http:// hoặc https://.",
                    "Địa chỉ không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HospitalBrowser.Address = uri.AbsoluteUri;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (HospitalBrowser.CanGoBack) HospitalBrowser.Back();
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            if (HospitalBrowser.CanGoForward) HospitalBrowser.Forward();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            HospitalBrowser.Reload();
        }

        private void AddAppointment_Click(object sender, RoutedEventArgs e)
        {
            var code = (PatientCodeBox.Text ?? "").Trim();
            var name = (PatientNameBox.Text ?? "").Trim();

            if (code.Length == 0 || name.Length == 0 ||
                AppointmentDatePicker.SelectedDate == null || ShiftBox.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng nhập mã bệnh nhân, tên bệnh nhân, ngày hẹn và buổi hẹn.",
                    "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var exam = (ExamCodeBox.Text ?? "").Trim();
            if (Database.HasActiveAppointment(code, exam))
            {
                MessageBox.Show(
                    "Bệnh nhân đã có lịch hẹn đang hiệu lực. Hãy kiểm tra lịch cũ và quyết định trả hoặc giữ trước khi thêm lịch mới.",
                    "Phát hiện lịch trùng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var shift = ((ComboBoxItem)ShiftBox.SelectedItem).Content.ToString();
            Database.Add(new Appointment
            {
                PatientCode = code,
                PatientName = name,
                ExamCode = exam,
                ServiceName = ServiceBox.Text,
                AppointmentDate = AppointmentDatePicker.SelectedDate.Value,
                Shift = shift
            });

            MessageBox.Show("Đã thêm lịch hẹn vào cơ sở dữ liệu trên máy tính.",
                "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            PatientCodeBox.Clear();
            PatientNameBox.Clear();
            ExamCodeBox.Clear();
        }

        private void LoadAppointments()
        {
            var from = FromDatePicker.SelectedDate ?? DateTime.Today;
            var to = ToDatePicker.SelectedDate ?? from;
            if (to < from)
            {
                MessageBox.Show("Ngày đến phải bằng hoặc sau ngày bắt đầu.");
                return;
            }

            var selected = FilterShiftBox.SelectedItem as ComboBoxItem;
            var shift = selected == null ? "Cả ngày" : selected.Content.ToString();
            DataTable data = Database.GetAppointments(from, to, shift, IncludeReturnedCheck.IsChecked == true);
            AppointmentsGrid.ItemsSource = data.DefaultView;
            CountText.Text = data.Rows.Count + " ca";
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) { LoadAppointments(); }
        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && FilterShiftBox.SelectedItem != null) LoadAppointments();
        }

        private void ReturnAppointment_Click(object sender, RoutedEventArgs e)
        {
            var view = AppointmentsGrid.SelectedItem as DataRowView;
            if (view == null)
            {
                MessageBox.Show("Hãy chọn một lịch trong danh sách.");
                return;
            }

            if (MessageBox.Show("Xác nhận trả lịch đã chọn? Dữ liệu vẫn được giữ trong lịch sử.",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            Database.Return(Convert.ToInt64(view["Id"]));
            LoadAppointments();
        }
    }
}
