using System;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;
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

        private void UrlBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenUrl_Click(sender, new RoutedEventArgs());
            }
        }

        private void AddUrlBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                AddOpenUrl_Click(sender, new RoutedEventArgs());
            }
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

        // Chỉ đọc cấu trúc bảng/cột, không đọc nội dung từng bệnh nhân.
        private const string DomInspectionScript = @"
(function () {
    function clean(value) {
        return (value || '').replace(/\s+/g, ' ').trim();
    }

    function visible(element) {
        if (!element) return false;
        var style = window.getComputedStyle(element);
        return style.display !== 'none' &&
               style.visibility !== 'hidden' &&
               element.getClientRects().length > 0;
    }

    var selectors = [
        'table',
        '[role=""grid""]',
        '[role=""table""]',
        '[role=""treegrid""]',
        '.ant-table',
        '.ag-root',
        '.dataTable',
        '.table-responsive'
    ];

    var found = [];
    var seen = [];

    selectors.forEach(function (selector) {
        var elements = document.querySelectorAll(selector);
        for (var i = 0; i < elements.length; i++) {
            var element = elements[i];
            if (!visible(element) || seen.indexOf(element) >= 0) continue;
            seen.push(element);

            var headers = [];
            var headerNodes = element.querySelectorAll(
                'thead th, [role=""columnheader""], th'
            );

            for (var h = 0; h < headerNodes.length && h < 30; h++) {
                var headerText = clean(headerNodes[h].innerText ||
                                       headerNodes[h].textContent);
                if (headerText && headers.indexOf(headerText) < 0)
                    headers.push(headerText);
            }

            if (headers.length === 0) {
                var firstRow = element.querySelector('tr');
                if (firstRow) {
                    var cells = firstRow.querySelectorAll('th, td');
                    for (var c = 0; c < cells.length && c < 30; c++) {
                        var cellText = clean(cells[c].innerText ||
                                              cells[c].textContent);
                        if (cellText) headers.push(cellText);
                    }
                }
            }

            var rowCount = element.querySelectorAll(
                'tbody tr, [role=""row""]'
            ).length;

            if (rowCount === 0)
                rowCount = element.querySelectorAll('tr').length;

            var id = element.id ? '#' + element.id : '';
            var className = '';
            if (typeof element.className === 'string' && element.className)
                className = '.' + element.className.trim().replace(/\s+/g, '.');

            found.push({
                selector: selector,
                tag: element.tagName.toLowerCase(),
                id: id,
                className: className,
                rows: rowCount,
                headers: headers
            });
        }
    });

    var lines = [];
    lines.push('KIỂM TRA CẤU TRÚC DOM');
    lines.push('Tên miền: ' + location.origin);
    lines.push('Số bảng/vùng dữ liệu tìm thấy: ' + found.length);
    lines.push('');

    if (found.length === 0) {
        lines.push('Không tìm thấy bảng HTML hoặc vùng dữ liệu theo các selector phổ biến.');
        lines.push('Website có thể dựng bảng bằng canvas hoặc cấu trúc DOM riêng.');
    }

    found.forEach(function (item, index) {
        lines.push('VÙNG ' + (index + 1));
        lines.push('Phần tử: ' + item.tag);
        lines.push('Selector dò được: ' + item.selector);
        lines.push('ID: ' + (item.id || '(không có)'));
        lines.push('Class: ' + (item.className || '(không có)'));
        lines.push('Số dòng: ' + item.rows);
        lines.push('Tiêu đề cột:');
        if (item.headers.length === 0) {
            lines.push('  (chưa đọc được tiêu đề)');
        } else {
            item.headers.forEach(function (header, headerIndex) {
                lines.push('  ' + (headerIndex + 1) + '. ' + header);
            });
        }
        lines.push('');
    });

    lines.push('LƯU Ý: Công cụ chỉ lấy cấu trúc và tiêu đề cột, không lấy mã BN, họ tên hay dữ liệu từng dòng.');
    return lines.join('\n');
})()";

        private async Task InspectBrowserDomAsync(
            ChromiumWebBrowser browser,
            string browserName)
        {
            if (browser == null || !browser.IsBrowserInitialized)
            {
                MessageBox.Show(
                    "Trình duyệt chưa khởi tạo xong. Hãy mở website và thử lại.",
                    "Chưa sẵn sàng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var frame = browser.GetMainFrame();
                if (frame == null || !frame.IsValid)
                {
                    MessageBox.Show("Không truy cập được nội dung trang hiện tại.");
                    return;
                }

                JavascriptResponse response =
                    await frame.EvaluateScriptAsync(DomInspectionScript);

                if (!response.Success)
                {
                    MessageBox.Show(
                        "Không thể kiểm tra DOM.\n" + response.Message,
                        "Lỗi đọc trang",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string report = response.Result as string;
                if (string.IsNullOrWhiteSpace(report))
                    report = "Website không trả về thông tin cấu trúc DOM.";

                ShowDomReport(browserName, report);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể kiểm tra cấu trúc website.\n\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShowDomReport(string browserName, string report)
        {
            var window = new Window
            {
                Title = "Kết quả kiểm tra DOM - " + browserName,
                Width = 760,
                Height = 620,
                MinWidth = 520,
                MinHeight = 400,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = System.Windows.Media.Brushes.White
            };

            var root = new DockPanel { Margin = new Thickness(12) };

            var note = new TextBlock
            {
                Text = "Hãy sao chép toàn bộ kết quả và gửi lại để cấu hình bộ đọc chỉ định. Báo cáo không chứa dữ liệu từng bệnh nhân.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = System.Windows.Media.Brushes.DarkSlateGray
            };
            DockPanel.SetDock(note, Dock.Top);
            root.Children.Add(note);

            var output = new TextBox
            {
                Text = report,
                IsReadOnly = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12
            };
            root.Children.Add(output);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var copyButton = new Button
            {
                Content = "Sao chép báo cáo",
                Width = 135,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0)
            };
            copyButton.Click += (s, e) =>
            {
                Clipboard.SetText(report);
                MessageBox.Show(window, "Đã sao chép báo cáo DOM.");
            };

            var closeButton = new Button
            {
                Content = "Đóng",
                Width = 85,
                Height = 34
            };
            closeButton.Click += (s, e) => window.Close();

            buttons.Children.Add(copyButton);
            buttons.Children.Add(closeButton);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            window.Content = root;
            window.ShowDialog();
        }

        private async void InspectDom_Click(object sender, RoutedEventArgs e)
        {
            await InspectBrowserDomAsync(HospitalBrowser, "Trình duyệt bệnh viện");
        }

        private async void InspectAddDom_Click(object sender, RoutedEventArgs e)
        {
            await InspectBrowserDomAsync(AddHospitalBrowser, "Trình duyệt thêm lịch");
        }

        private void AddAppointment_Click(object sender, RoutedEventArgs e)
        {
            string code = (PatientCodeBox.Text ?? "").Trim();
            string name = (PatientNameBox.Text ?? "").Trim();
            string department = (DepartmentBox.Text ?? "").Trim();
            string exam = (ExamCodeBox.Text ?? "").Trim();
            string service = (ServiceBox.Text ?? "").Trim();
            var injectionItem = InjectionStatusBox.SelectedItem as ComboBoxItem;
            string injectionStatus = injectionItem == null ? "Không tiêm" : injectionItem.Content.ToString();

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
                    InjectionStatus = injectionStatus,
                    AppointmentDate = AppointmentDatePicker.SelectedDate.Value,
                    Shift = selectedShift.Content.ToString()
                });

                MessageBox.Show("Đã thêm lịch hẹn thành công.");

                PatientCodeBox.Clear();
                PatientNameBox.Clear();
                DepartmentBox.Clear();
                ExamCodeBox.Clear();

                ServiceBox.Text = "Chụp CLVT";
                InjectionStatusBox.SelectedIndex = 0;
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
