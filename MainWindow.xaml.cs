
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
                    Dispatcher.BeginInvoke(new Action(() => UrlBox.Text = args.Url));
            };

            AddHospitalBrowser.FrameLoadEnd += (sender, args) =>
            {
                if (args.Frame.IsMain)
                    Dispatcher.BeginInvoke(new Action(() => AddUrlBox.Text = args.Url));
            };
        }

        private void ShowView(string name)
        {
            BrowserView.Visibility = name == "browser" ? Visibility.Visible : Visibility.Collapsed;
            AddView.Visibility = name == "add" ? Visibility.Visible : Visibility.Collapsed;
            ListView.Visibility = name == "list" ? Visibility.Visible : Visibility.Collapsed;

            if (name == "list")
                LoadAppointments();
        }

        private void BrowserTab_Click(object sender, RoutedEventArgs e) => ShowView("browser");
        private void AddTab_Click(object sender, RoutedEventArgs e) => ShowView("add");
        private void ListTab_Click(object sender, RoutedEventArgs e) => ShowView("list");

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

        private void OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            string address;

            if (!TryGetWebAddress(UrlBox.Text, out address))
            {
                MessageBox.Show("Địa chỉ website không hợp lệ.");
                return;
            }

            HospitalBrowser.Address = address;
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

        private void AddOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            string address;

            if (!TryGetWebAddress(AddUrlBox.Text, out address))
            {
                MessageBox.Show("Địa chỉ website không hợp lệ.");
                return;
            }

            AddHospitalBrowser.Address = address;
        }

        private void AddBack_Click(object sender, RoutedEventArgs e)
        {
            if (AddHospitalBrowser.CanGoBack) AddHospitalBrowser.Back();
        }

        private void AddForward_Click(object sender, RoutedEventArgs e)
        {
            if (AddHospitalBrowser.CanGoForward) AddHospitalBrowser.Forward();
        }

        private void AddReload_Click(object sender, RoutedEventArgs e)
        {
            AddHospitalBrowser.Reload();
        }

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

            if (Database.HasActiveAppointment(code, exam))
            {
                MessageBox.Show(
                    "Bệnh nhân đã có lịch hẹn đang hiệu lực. Vui lòng kiểm tra lịch cũ trước khi thêm lịch mới.",
                    "Phát hiện lịch trùng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selectedShift = ShiftBox.SelectedItem as ComboBoxItem;
            if (selectedShift == null) return;

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
                    Shift = selectedShift.Content.ToString()
                });

                MessageBox.Show("Đã thêm lịch hẹn thành công.");

                PatientCodeBox.Clear();
                PatientNameBox.Clear();
                DepartmentBox.Clear();
                ExamCodeBox.Clear();

                ServiceBox.Text = "Chụp CLVT";
                AppointmentDatePicker.SelectedDate = DateTime.Today.AddDays(1);
                ShiftBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể lưu lịch hẹn.\n\n" + ex.Message);
            }
        }

        private void LoadAppointments()
        {
            DateTime from = FromDatePicker.SelectedDate ?? DateTime.Today;
            DateTime to = ToDatePicker.SelectedDate ?? from;

            if (to.Date < from.Date)
            {
                TotalCountText.Text = "0 ca";
                ActiveCountText.Text = "0 ca";

                MessageBox.Show("Ngày đến phải bằng hoặc sau ngày bắt đầu.");
                return;
            }

            var shiftItem = FilterShiftBox.SelectedItem as ComboBoxItem;
            string shift = shiftItem == null ? "Cả ngày" : shiftItem.Content.ToString();

            var injectionItem = InjectionFilterBox.SelectedItem as ComboBoxItem;
            string injectionFilter = injectionItem == null
                ? "Tất cả"
                : injectionItem.Content.ToString();

            try
            {
                DataTable data = Database.GetAppointments(
                    from.Date,
                    to.Date,
                    shift,
                    IncludeReturnedCheck.IsChecked == true,
                    injectionFilter);

                AppointmentsGrid.ItemsSource = data.DefaultView;

                int totalCount = data.Rows.Count;
                int activeCount = 0;

                foreach (DataRow row in data.Rows)
                {
                    if (Convert.ToString(row["Trạng thái"]) == "Đã hẹn")
                        activeCount++;
                }

                TotalCountText.Text = totalCount + " ca";
                ActiveCountText.Text = activeCount + " ca";
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

        private void FilterSelection_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (IsLoaded &&
                FilterShiftBox.SelectedItem != null &&
                InjectionFilterBox.SelectedItem != null)
                LoadAppointments();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded &&
                FilterShiftBox.SelectedItem != null &&
                InjectionFilterBox.SelectedItem != null)
                LoadAppointments();
        }

        private void ReturnAppointment_Click(object sender, RoutedEventArgs e)
        {
            var view = AppointmentsGrid.SelectedItem as DataRowView;

            if (view == null)
            {
                MessageBox.Show("Hãy chọn một lịch trong danh sách.");
                return;
            }

            if (Convert.ToString(view["Trạng thái"]) != "Đã hẹn")
            {
                MessageBox.Show("Lịch này đã được trả trước đó hoặc không còn hiệu lực.");
                return;
            }

            string patientName = Convert.ToString(view["Tên bệnh nhân"]);
            string patientCode = Convert.ToString(view["Mã BN"]);

            if (MessageBox.Show(
                "Xác nhận trả lịch của bệnh nhân:\n\n" +
                patientName + "\nMã BN: " + patientCode +
                "\n\nLịch vẫn được lưu trong lịch sử.",
                "Xác nhận trả lịch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                Database.Return(Convert.ToInt64(view["Id"]));
                LoadAppointments();
                MessageBox.Show("Đã trả lịch thành công.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể trả lịch.\n\n" + ex.Message);
            }
        }
    }
}
