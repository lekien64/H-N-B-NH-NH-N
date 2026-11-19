
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

            // Đồng bộ địa chỉ trình duyệt tab 1
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

            // Đồng bộ địa chỉ trình duyệt tab 2
            AddHospitalBrowser.FrameLoadEnd += (sender, args) =>
            {
                if (args.Frame.IsMain)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AddUrlBox.Text = args.Url;
                    }));
                }
            };
        }

        // ==============================
        // CHUYỂN TAB
        // ==============================

        private void ShowView(string name)
        {
            BrowserView.Visibility =
                name == "browser" ? Visibility.Visible : Visibility.Collapsed;

            AddView.Visibility =
                name == "add" ? Visibility.Visible : Visibility.Collapsed;

            ListView.Visibility =
                name == "list" ? Visibility.Visible : Visibility.Collapsed;

            if (name == "list")
                LoadAppointments();
        }

        private void BrowserTab_Click(object sender, RoutedEventArgs e)
        {
            ShowView("browser");
        }

        private void AddTab_Click(object sender, RoutedEventArgs e)
        {
            ShowView("add");
        }

        private void ListTab_Click(object sender, RoutedEventArgs e)
        {
            ShowView("list");
        }

        // ==============================
        // HÀM KIỂM TRA VÀ MỞ URL
        // ==============================

        private bool TryGetWebAddress(string input, out string address)
        {
            address = (input ?? "").Trim();

            if (address.Length == 0)
                return false;

            if (!address.Contains("://"))
                address = "http://" + address;

            Uri uri;

            if (!Uri.TryCreate(address, UriKind.Absolute, out uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps)
                return false;

            address = uri.AbsoluteUri;
            return true;
        }

        // ==============================
        // TRÌNH DUYỆT TAB 1
        // ==============================

        private void OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            string address;

            if (!TryGetWebAddress(UrlBox.Text, out address))
            {
                MessageBox.Show(
                    "Địa chỉ website không hợp lệ. Vui lòng nhập địa chỉ http:// hoặc https://.",
                    "Lỗi địa chỉ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            HospitalBrowser.Address = address;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (HospitalBrowser.CanGoBack)
                HospitalBrowser.Back();
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            if (HospitalBrowser.CanGoForward)
                HospitalBrowser.Forward();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            HospitalBrowser.Reload();
        }

        // ==============================
        // TRÌNH DUYỆT RIÊNG TAB 2
        // ==============================

        private void AddOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            string address;

            if (!TryGetWebAddress(AddUrlBox.Text, out address))
            {
                MessageBox.Show(
                    "Địa chỉ website không hợp lệ. Vui lòng nhập địa chỉ http:// hoặc https://.",
                    "Lỗi địa chỉ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            AddHospitalBrowser.Address = address;
        }

        private void AddBack_Click(object sender, RoutedEventArgs e)
        {
            if (AddHospitalBrowser.CanGoBack)
                AddHospitalBrowser.Back();
        }

        private void AddForward_Click(object sender, RoutedEventArgs e)
        {
            if (AddHospitalBrowser.CanGoForward)
                AddHospitalBrowser.Forward();
        }

        private void AddReload_Click(object sender, RoutedEventArgs e)
        {
            AddHospitalBrowser.Reload();
        }

        // ==============================
        // THÊM LỊCH HẸN
        // ==============================

        private void AddAppointment_Click(object sender, RoutedEventArgs e)
        {
            string code = (PatientCodeBox.Text ?? "").Trim();
            string name = (PatientNameBox.Text ?? "").Trim();
            string department = (DepartmentBox.Text ?? "").Trim();
            string exam = (ExamCodeBox.Text ?? "").Trim();
            string service = (ServiceBox.Text ?? "").Trim();

            if (code.Length == 0 ||
                name.Length == 0 ||
                department.Length == 0 ||
                AppointmentDatePicker.SelectedDate == null ||
                ShiftBox.SelectedItem == null)
            {
                MessageBox.Show(
                    "Vui lòng nhập đầy đủ mã bệnh nhân, tên bệnh nhân, khoa phòng, ngày hẹn và buổi hẹn.",
                    "Thiếu thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Kiểm tra lịch cũ trước khi thêm.
            // Không tự động trả hoặc ghi đè lịch cũ.
            if (Database.HasActiveAppointment(code, exam))
            {
                MessageBox.Show(
                    "Bệnh nhân đã có lịch hẹn đang hiệu lực.\n\n" +
                    "Vui lòng vào tab Lịch đã hẹn để kiểm tra lịch cũ. " +
                    "Chỉ thêm lịch mới sau khi đã quyết định giữ hoặc trả lịch cũ.",
                    "Phát hiện lịch trùng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selectedShift = ShiftBox.SelectedItem as ComboBoxItem;

            if (selectedShift == null)
                return;

            string shift = selectedShift.Content.ToString();

            try
            {
                Database.Add(new Appointment
                {
                    PatientCode = code,
                    PatientName = name,
                    Department = department,
                    ExamCode = exam,
                    ServiceName = service,
                    AppointmentDate = AppointmentDatePicker.SelectedDate.Value,
                    Shift = shift
                });

                MessageBox.Show(
                    "Đã thêm lịch hẹn thành công.\nDữ liệu được lưu trên máy tính.",
                    "Thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                PatientCodeBox.Clear();
                PatientNameBox.Clear();
                DepartmentBox.Clear();
                ExamCodeBox.Clear();

                ServiceBox.Text = "Chụp CLVT";

                AppointmentDatePicker.SelectedDate =
                    DateTime.Today.AddDays(1);

                ShiftBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể lưu lịch hẹn.\n\nChi tiết: " + ex.Message,
                    "Lỗi cơ sở dữ liệu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ==============================
        // TẢI DANH SÁCH LỊCH
        // ==============================

        private void LoadAppointments()
        {
            DateTime from =
                FromDatePicker.SelectedDate ?? DateTime.Today;

            DateTime to =
                ToDatePicker.SelectedDate ?? from;

            if (to < from)
            {
                MessageBox.Show(
                    "Ngày đến phải bằng hoặc sau ngày bắt đầu.",
                    "Khoảng ngày không hợp lệ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selected =
                FilterShiftBox.SelectedItem as ComboBoxItem;

            string shift =
                selected == null
                    ? "Cả ngày"
                    : selected.Content.ToString();

            try
            {
                DataTable data = Database.GetAppointments(
                    from,
                    to,
                    shift,
                    IncludeReturnedCheck.IsChecked == true);

                AppointmentsGrid.ItemsSource = data.DefaultView;

                CountText.Text = data.Rows.Count + " ca";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể tải danh sách lịch hẹn.\n\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAppointments();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && FilterShiftBox.SelectedItem != null)
                LoadAppointments();
        }

        // ==============================
        // TRẢ LỊCH
        // ==============================

        private void ReturnAppointment_Click(object sender, RoutedEventArgs e)
        {
            var view = AppointmentsGrid.SelectedItem as DataRowView;

            if (view == null)
            {
                MessageBox.Show(
                    "Hãy chọn một lịch trong danh sách.",
                    "Chưa chọn lịch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string status = Convert.ToString(view["Trạng thái"]);

            if (status != "Đã hẹn")
            {
                MessageBox.Show(
                    "Lịch này đã được trả trước đó hoặc không còn hiệu lực.",
                    "Không thể trả lịch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string patientName =
                Convert.ToString(view["Tên bệnh nhân"]);

            string patientCode =
                Convert.ToString(view["Mã BN"]);

            if (MessageBox.Show(
                "Xác nhận trả lịch của bệnh nhân:\n\n" +
                patientName + "\nMã BN: " + patientCode +
                "\n\nLịch sẽ được giữ trong lịch sử và không còn hiệu lực.",
                "Xác nhận trả lịch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                long id = Convert.ToInt64(view["Id"]);

                Database.Return(id);

                LoadAppointments();

                MessageBox.Show(
                    "Đã trả lịch thành công.",
                    "Hoàn tất",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể trả lịch.\n\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
