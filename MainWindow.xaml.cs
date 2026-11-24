using System;
using System.IO;
using System.Diagnostics;
using System.Data;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;
using CefSharp.Wpf;
using PatientAppointmentManager.Data;
using PatientAppointmentManager.Models;
using Google.Apis.Drive.v3;

namespace PatientAppointmentManager
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_PerformanceLoaded;

            Database.Initialize();
            LoadAppSettings();
            UpdateSidebarSelection("add");

            AppointmentDatePicker.SelectedDate = DateTime.Today.AddDays(1);
            FromDatePicker.SelectedDate = DateTime.Today;
            ToDatePicker.SelectedDate = DateTime.Today;
            DeleteFromDatePicker.SelectedDate = DateTime.Today;
            DeleteToDatePicker.SelectedDate = DateTime.Today;

            ConfigureBrowserTab(FirstBrowserTab, AddHospitalBrowser, "Trang 1");
            RegisterBrowser(AddHospitalBrowser);
            LoadBrowserHistory(AddUrlBox);
            StartCloudBackupScheduler();
        }


        private string SettingsFilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PatientAppointmentManager");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "settings.json");
            }
        }

        private string CurrentPerformanceMode = "low";

        private void LoadAppSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var serializer = new JavaScriptSerializer();
                    var values = serializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(SettingsFilePath, Encoding.UTF8));
                    if (values != null && values.ContainsKey("PerformanceMode"))
                        CurrentPerformanceMode = values["PerformanceMode"];
                }
            }
            catch { CurrentPerformanceMode = "low"; }

            if (CurrentPerformanceMode != "low") CurrentPerformanceMode = "high";
            if (PerformanceModeBox != null)
                PerformanceModeBox.SelectedIndex = CurrentPerformanceMode == "low" ? 1 : 0;

            ApplyPerformanceMode(CurrentPerformanceMode);
        }

        private void ApplyPerformanceMode(string mode)
        {
            CurrentPerformanceMode = mode == "low" ? "low" : "high";
            bool low = CurrentPerformanceMode == "low";

            if (AppointmentsGrid != null)
            {
                AppointmentsGrid.EnableRowVirtualization = true;
                AppointmentsGrid.EnableColumnVirtualization = true;
                VirtualizingPanel.SetIsVirtualizing(AppointmentsGrid, true);
                VirtualizingPanel.SetVirtualizationMode(AppointmentsGrid, VirtualizationMode.Recycling);
                ScrollViewer.SetCanContentScroll(AppointmentsGrid, true);
                ScrollViewer.SetIsDeferredScrollingEnabled(AppointmentsGrid, true);
            }

            if (low)
            {
                var classic = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(240, 240, 240));
                Background = classic;
                if (Sidebar != null) Sidebar.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(224, 224, 224));
                if (MainContentPanel != null) MainContentPanel.Background = System.Windows.Media.Brushes.White;
                if (AddView != null) AddView.Background = System.Windows.Media.Brushes.White;
                if (ListView != null) ListView.Background = System.Windows.Media.Brushes.White;
                if (SettingsView != null) SettingsView.Background = System.Windows.Media.Brushes.White;

                if (AppointmentsGrid != null)
                {
                    AppointmentsGrid.Background = System.Windows.Media.Brushes.White;
                    AppointmentsGrid.RowBackground = System.Windows.Media.Brushes.White;
                    AppointmentsGrid.AlternatingRowBackground = System.Windows.Media.Brushes.White;
                    AppointmentsGrid.GridLinesVisibility = DataGridGridLinesVisibility.All;
                    AppointmentsGrid.HorizontalGridLinesBrush = System.Windows.Media.Brushes.LightGray;
                    AppointmentsGrid.VerticalGridLinesBrush = System.Windows.Media.Brushes.LightGray;
                }

                if (IsLoaded) ApplyLowPerformanceVisuals(this);
            }
            else
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(234, 243, 244));
                if (MainContentPanel != null)
                    MainContentPanel.Background = new System.Windows.Media.LinearGradientBrush(
                        System.Windows.Media.Color.FromRgb(244, 250, 250),
                        System.Windows.Media.Color.FromRgb(230, 241, 242), 45);
            }
        }

        private void MainWindow_PerformanceLoaded(object sender, RoutedEventArgs e)
        {
            if (CurrentPerformanceMode == "low")
            {
                // Run after WPF has created control templates, so nested cards/buttons
                // are flattened too, not only the first visible sidebar borders.
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    ApplyLowPerformanceVisuals(this);
                }), DispatcherPriority.ApplicationIdle);
            }
        }

        private static void ApplyLowPerformanceVisuals(DependencyObject parent)
        {
            if (parent == null) return;

            var border = parent as Border;
            if (border != null)
            {
                border.CornerRadius = new CornerRadius(0);
                border.Effect = null;
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(160, 160, 160));
                border.BorderThickness = new Thickness(1);
                FlattenGradientBackground(border);
                if (border.Background != null)
                    border.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(240, 240, 240));
            }

            var ui = parent as UIElement;
            if (ui != null) ui.Effect = null;

            var button = parent as Button;
            if (button != null)
            {
                button.Effect = null;
                button.Foreground = System.Windows.Media.Brushes.Black;
                button.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(240, 240, 240));
                button.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(128, 128, 128));
                button.BorderThickness = new Thickness(1);
                button.FontWeight = FontWeights.Normal;
                button.Cursor = Cursors.Arrow;
                button.Padding = new Thickness(8, 4, 8, 4);
                button.Template = CreateClassicButtonTemplate();
                // Do not walk into the newly assigned template. Its Border uses
                // TemplateBinding and must retain the button's classic colors.
                return;
            }

            var control = parent as Control;
            if (control != null)
            {
                control.Effect = null;
                control.SnapsToDevicePixels = true;
                if (control is TextBox || control is ComboBox || control is DatePicker)
                {
                    control.Background = System.Windows.Media.Brushes.White;
                    control.Foreground = System.Windows.Media.Brushes.Black;
                    control.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(128, 128, 128));
                    control.BorderThickness = new Thickness(1);
                }
            }

            var text = parent as TextBlock;
            if (text != null)
            {
                text.Foreground = System.Windows.Media.Brushes.Black;
                text.FontWeight = text.FontWeight == FontWeights.Bold ? FontWeights.Bold : FontWeights.Normal;
            }

            var panel = parent as Panel;
            if (panel != null) FlattenGradientBackground(panel);

            var header = parent as DataGridColumnHeader;
            if (header != null)
            {
                header.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(224, 224, 224));
                header.Foreground = System.Windows.Media.Brushes.Black;
                header.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(128, 128, 128));
                header.FontWeight = FontWeights.Normal;
                header.Padding = new Thickness(4, 3, 4, 3);
            }

            var grid = parent as DataGrid;
            if (grid != null)
            {
                grid.Background = System.Windows.Media.Brushes.White;
                grid.RowBackground = System.Windows.Media.Brushes.White;
                grid.AlternatingRowBackground = System.Windows.Media.Brushes.White;
                grid.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(128, 128, 128));
                grid.BorderThickness = new Thickness(1);
                grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
                grid.HorizontalGridLinesBrush = System.Windows.Media.Brushes.LightGray;
                grid.VerticalGridLinesBrush = System.Windows.Media.Brushes.LightGray;
                grid.FontSize = 12;
                grid.RowHeight = 28;
                grid.ColumnHeaderHeight = 30;
            }

            int childCount = 0;
            try { childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); }
            catch { return; }

            for (int i = 0; i < childCount; i++)
            {
                try { ApplyLowPerformanceVisuals(System.Windows.Media.VisualTreeHelper.GetChild(parent, i)); }
                catch { /* A non-visual WPF child must not prevent the window from opening. */ }
            }
        }

        private static ControlTemplate CreateClassicButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            var templatedParent = new System.Windows.Data.RelativeSource(
                System.Windows.Data.RelativeSourceMode.TemplatedParent);

            // FrameworkElementFactory.SetBinding requires BindingBase. A
            // TemplateBindingExtension is a XAML markup extension, not a BindingBase.
            border.SetBinding(Border.BackgroundProperty,
                new System.Windows.Data.Binding("Background") { RelativeSource = templatedParent });
            border.SetBinding(Border.BorderBrushProperty,
                new System.Windows.Data.Binding("BorderBrush") { RelativeSource = templatedParent });
            border.SetBinding(Border.BorderThicknessProperty,
                new System.Windows.Data.Binding("BorderThickness") { RelativeSource = templatedParent });
            border.SetBinding(Border.PaddingProperty,
                new System.Windows.Data.Binding("Padding") { RelativeSource = templatedParent });

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.ContentProperty,
                new System.Windows.Data.Binding("Content") { RelativeSource = templatedParent });
            presenter.SetBinding(ContentPresenter.ContentTemplateProperty,
                new System.Windows.Data.Binding("ContentTemplate") { RelativeSource = templatedParent });
            presenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty,
                new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource = templatedParent });
            presenter.SetBinding(ContentPresenter.VerticalAlignmentProperty,
                new System.Windows.Data.Binding("VerticalContentAlignment") { RelativeSource = templatedParent });

            border.AppendChild(presenter);
            template.VisualTree = border;
            return template;
        }

        private static void FlattenGradientBackground(Border border)
        {
            var gradient = border.Background as System.Windows.Media.GradientBrush;
            if (gradient != null && gradient.GradientStops.Count > 0)
                border.Background = new System.Windows.Media.SolidColorBrush(gradient.GradientStops[0].Color);
        }

        private static void FlattenGradientBackground(Control control)
        {
            var gradient = control.Background as System.Windows.Media.GradientBrush;
            if (gradient != null && gradient.GradientStops.Count > 0)
                control.Background = new System.Windows.Media.SolidColorBrush(gradient.GradientStops[0].Color);
        }

        private static void FlattenGradientBackground(Panel panel)
        {
            var gradient = panel.Background as System.Windows.Media.GradientBrush;
            if (gradient != null && gradient.GradientStops.Count > 0)
                panel.Background = new System.Windows.Media.SolidColorBrush(gradient.GradientStops[0].Color);
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            string mode = PerformanceModeBox.SelectedIndex == 0 ? "high" : "low";

            try
            {
                var values = ReadAppSettings();
                values["PerformanceMode"] = mode;
                WriteAppSettings(values);
                SettingsStatusText.Text = "Đã lưu. Cấu hình mới áp dụng sau khi khởi động lại.";
                ShowAppMessage("Đã lưu cấu hình. Vui lòng khởi động lại phần mềm để áp dụng đầy đủ.");
            }
            catch (Exception ex)
            {
                ShowAppMessage("Không thể lưu cài đặt: " + ex.Message);
            }
        }

        private DispatcherTimer cloudBackupTimer;
        private GoogleDriveBackupService cloudDrive;
        private bool cloudBackupBusy;
        private const string AppointmentDatabasePath = "";

        private Dictionary<string, string> ReadAppSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                    return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsFilePath, Encoding.UTF8)) ?? new Dictionary<string, string>();
            }
            catch { }
            return new Dictionary<string, string>();
        }

        private void WriteAppSettings(Dictionary<string, string> values)
        {
            File.WriteAllText(SettingsFilePath, new JavaScriptSerializer().Serialize(values), Encoding.UTF8);
        }

        private string CloudDatabasePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatientAppointmentManager", "appointments.db"); }
        }

        private void StartCloudBackupScheduler()
        {
            cloudBackupTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            cloudBackupTimer.Tick += async (s, e) =>
            {
                if (cloudBackupBusy || cloudDrive == null || DateTime.Now.TimeOfDay < new TimeSpan(16, 30, 0)) return;
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                var settings = ReadAppSettings();
                if (settings.ContainsKey("LastCloudBackupDate") && settings["LastCloudBackupDate"] == today) return;
                await PerformCloudBackupAsync(false);
            };
            cloudBackupTimer.Start();
            // Khi đã từng kết nối, thử khôi phục phiên đã cấp quyền trước đó.
            var saved = ReadAppSettings();
            if (saved.ContainsKey("GoogleOAuthJsonPath") && File.Exists(saved["GoogleOAuthJsonPath"]))
                _ = RestoreCloudSessionAsync(saved["GoogleOAuthJsonPath"]);
        }

        private async Task RestoreCloudSessionAsync(string jsonPath)
        {
            try
            {
                cloudDrive = await GoogleDriveBackupService.ConnectAsync(jsonPath);
                string account = await cloudDrive.GetAccountAsync();
                GoogleDriveStatusText.Text = "Đã kết nối Google Drive: " + account;
            }
            catch (Exception ex) { GoogleDriveStatusText.Text = "Chưa kết nối Drive: " + ex.Message; }
        }

        private async void ConnectGoogleDrive_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn file OAuth Client JSON đã tải từ Google Cloud",
                Filter = "OAuth client JSON (*.json)|*.json|All files (*.*)|*.*"
            };
            var saved = ReadAppSettings();
            if (saved.ContainsKey("GoogleOAuthJsonPath")) dialog.FileName = saved["GoogleOAuthJsonPath"];
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                GoogleDriveStatusText.Text = "Đang mở đăng nhập Google...";
                cloudDrive = await GoogleDriveBackupService.ConnectAsync(dialog.FileName);
                string account = await cloudDrive.GetAccountAsync();
                saved["GoogleOAuthJsonPath"] = dialog.FileName;
                WriteAppSettings(saved);
                GoogleDriveStatusText.Text = "Đã kết nối Google Drive: " + account;
                ShowAppMessage("Đã kết nối Google Drive thành công. Tài khoản: " + account);
            }
            catch (Exception ex)
            {
                cloudDrive = null;
                GoogleDriveStatusText.Text = "Kết nối thất bại: " + ex.Message;
                ShowAppMessage("Không thể kết nối Google Drive:\n" + ex.Message, "Lỗi Google OAuth", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CloudBackupNow_Click(object sender, RoutedEventArgs e)
        {
            if (cloudDrive == null)
            {
                ShowAppMessage("Bạn cần kết nối Google Drive trước.");
                return;
            }
            await PerformCloudBackupAsync(true);
        }

        private async void RestoreFromGoogleDrive_Click(object sender, RoutedEventArgs e)
        {
            if (cloudBackupBusy) return;
            if (cloudDrive == null)
            {
                ShowAppMessage("Bạn cần kết nối Google Drive trước khi khôi phục.");
                return;
            }

            cloudBackupBusy = true;
            try
            {
                GoogleDriveStatusText.Text = "Đang tải danh sách bản sao lưu...";
                var backups = await cloudDrive.ListBackupsAsync();
                if (backups == null || backups.Count == 0)
                {
                    ShowAppMessage("Không tìm thấy bản sao lưu SoHenBN trong thư mục Google Drive.");
                    GoogleDriveStatusText.Text = "Không tìm thấy bản sao lưu trên Drive.";
                    return;
                }

                var picker = new Window
                {
                    Title = "Chọn bản sao lưu Google Drive",
                    Owner = this,
                    Width = 720,
                    Height = 430,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.CanResize,
                    Background = System.Windows.Media.Brushes.White
                };
                var panel = new DockPanel { Margin = new Thickness(14) };
                var hint = new TextBlock
                {
                    Text = "Chọn bản sao lưu cần khôi phục. Danh sách được sắp xếp mới nhất trước.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                DockPanel.SetDock(hint, Dock.Top);
                panel.Children.Add(hint);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var cancel = new Button { Content = "Hủy", Width = 90, Height = 32, Margin = new Thickness(0, 10, 8, 0), IsCancel = true };
                var ok = new Button { Content = "Chọn bản này", Width = 125, Height = 32, Margin = new Thickness(0, 10, 0, 0), IsDefault = true };
                buttons.Children.Add(cancel); buttons.Children.Add(ok);
                DockPanel.SetDock(buttons, Dock.Bottom);
                panel.Children.Add(buttons);
                var list = new ListBox { ItemsSource = backups, DisplayMemberPath = "DisplayName", MinHeight = 220 };
                list.SelectedIndex = 0;
                panel.Children.Add(list);
                ok.Click += (s, args) => { if (list.SelectedItem != null) picker.DialogResult = true; };
                picker.Content = panel;
                if (picker.ShowDialog() != true || !(list.SelectedItem is GoogleDriveBackupService.CloudBackupInfo selected))
                    return;

                string warning = "Bạn sắp khôi phục dữ liệu từ Google Drive:\n" + selected.DisplayName +
                    "\n\nDữ liệu hiện tại sẽ được sao lưu cục bộ trước khi thay thế. Tiếp tục?";
                if (ShowAppMessage(warning, "Xác nhận khôi phục từ Drive", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                string backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatientAppointmentManager", "Backups");
                Directory.CreateDirectory(backupFolder);
                string safeName = Path.GetFileName(selected.Name);
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "SoHenBN_DriveRestore.db";
                string downloaded = Path.Combine(backupFolder, "drive_restore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + safeName);
                GoogleDriveStatusText.Text = "Đang tải bản sao lưu từ Drive...";
                await cloudDrive.DownloadBackupAsync(selected.Id, downloaded);

                // Validate downloaded SQLite before touching the live database.
                using (var check = new System.Data.SQLite.SQLiteConnection("Data Source=" + downloaded + ";Version=3;Read Only=True;"))
                {
                    check.Open();
                    using (var command = check.CreateCommand())
                    {
                        command.CommandText = "PRAGMA integrity_check";
                        string integrity = Convert.ToString(command.ExecuteScalar());
                        if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Bản sao lưu tải về không hợp lệ (SQLite integrity_check thất bại).");
                    }
                }

                string currentBackup = Database.BackupDatabase();
                Database.RestoreDatabase(downloaded);
                LoadAppointments();
                GoogleDriveStatusText.Text = "Khôi phục từ Google Drive thành công: " + selected.Name;
                ShowAppMessage("Đã khôi phục dữ liệu từ Google Drive.\n\nBản sao lưu vừa tải: " + downloaded +
                    "\nBản sao lưu dữ liệu trước khi khôi phục: " + currentBackup,
                    "Khôi phục thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                GoogleDriveStatusText.Text = "Khôi phục Drive thất bại: " + ex.Message;
                ShowAppMessage("Không thể khôi phục từ Google Drive:\n" + ex.Message, "Lỗi khôi phục", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { cloudBackupBusy = false; }
        }

        private async Task PerformCloudBackupAsync(bool force)
        {
            if (cloudDrive == null || cloudBackupBusy) return;
            cloudBackupBusy = true;
            try
            {
                if (!File.Exists(CloudDatabasePath)) throw new FileNotFoundException("Không tìm thấy cơ sở dữ liệu lịch hẹn.", CloudDatabasePath);
                string hash = GoogleDriveBackupService.Sha256(CloudDatabasePath);
                var settings = ReadAppSettings();
                string lastHash = settings.ContainsKey("LastCloudBackupHash") ? settings["LastCloudBackupHash"] : "";
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                if (!force && hash == lastHash)
                {
                    settings["LastCloudBackupDate"] = today;
                    WriteAppSettings(settings);
                    GoogleDriveStatusText.Text = "16:30: Dữ liệu không thay đổi, bỏ qua sao lưu hôm nay.";
                    return;
                }

                string localBackup = Database.BackupDatabase();
                await cloudDrive.UploadSnapshotAsync(localBackup, "DAY", hash);
                if (DateTime.Today.DayOfWeek == DayOfWeek.Monday)
                    await cloudDrive.UploadSnapshotAsync(localBackup, "WEEK", hash);
                if (DateTime.Today.Day == 1)
                    await cloudDrive.UploadSnapshotAsync(localBackup, "MONTH", hash);
                settings["LastCloudBackupHash"] = hash;
                settings["LastCloudBackupDate"] = today;
                WriteAppSettings(settings);
                GoogleDriveStatusText.Text = "Sao lưu Google Drive thành công lúc " + DateTime.Now.ToString("HH:mm:ss") + ".";
                if (force) ShowAppMessage("Đã tải bản sao lưu lên Google Drive thành công.");
            }
            catch (Exception ex)
            {
                GoogleDriveStatusText.Text = "Sao lưu Drive thất bại: " + ex.Message;
                if (force) ShowAppMessage("Sao lưu Google Drive thất bại:\n" + ex.Message, "Lỗi sao lưu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { cloudBackupBusy = false; }
        }

        private void BackupDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Database.BackupDatabase();
                ShowAppMessage("Đã sao lưu dữ liệu thành công:\n" + path);
            }
            catch (Exception ex) { ShowAppMessage("Sao lưu thất bại:\n" + ex.Message); }
        }


        private void RestoreDatabase_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn file sao lưu lịch hẹn",
                Filter = "SQLite database (*.db)|*.db|All files (*.*)|*.*",
                InitialDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PatientAppointmentManager", "Backups")
            };

            if (dialog.ShowDialog(this) != true) return;
            if (ShowAppMessage("Khôi phục sẽ thay thế dữ liệu lịch hẹn hiện tại. Phần mềm sẽ sao lưu dữ liệu hiện tại trước. Tiếp tục?",
                "Xác nhận khôi phục", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                string currentBackup = Database.BackupDatabase();
                Database.RestoreDatabase(dialog.FileName);
                LoadAppointments();
                ShowAppMessage("Đã khôi phục dữ liệu.\nBản sao lưu dữ liệu trước khi khôi phục:\n" + currentBackup);
            }
            catch (Exception ex)
            {
                ShowAppMessage("Khôi phục dữ liệu thất bại:\n" + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PatientAppointmentManager");
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", folder);
            }
            catch (Exception ex) { ShowAppMessage("Không mở được thư mục dữ liệu:\n" + ex.Message); }
        }

        private void ExecuteBulkDelete(DateTime? from, DateTime? to, bool returnedOnly, string description)
        {
            try
            {
                int count = Database.CountAppointments(from, to, returnedOnly);
                if (count == 0)
                {
                    ShowAppMessage("Không có lịch hẹn phù hợp để xóa.");
                    return;
                }

                string detail = description + "\nSố lịch sẽ xóa: " + count +
                    "\n\nPhần mềm sẽ sao lưu dữ liệu trước khi xóa. Bạn có chắc chắn?";
                if (ShowAppMessage(detail, "Xác nhận xóa lịch",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                string backup = Database.BackupDatabase();
                int deleted = Database.DeleteAppointments(from, to, returnedOnly);
                LoadAppointments();
                ShowAppMessage("Đã xóa " + deleted + " lịch hẹn.\nBản sao lưu:\n" + backup);
            }
            catch (Exception ex) { ShowAppMessage("Không thể xóa lịch:\n" + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void DeleteAppointmentsByDate_Click(object sender, RoutedEventArgs e)
        {
            if (!DeleteFromDatePicker.SelectedDate.HasValue || !DeleteToDatePicker.SelectedDate.HasValue)
            {
                ShowAppMessage("Hãy chọn đầy đủ ngày bắt đầu và ngày kết thúc.");
                return;
            }
            DateTime from = DeleteFromDatePicker.SelectedDate.Value.Date;
            DateTime to = DeleteToDatePicker.SelectedDate.Value.Date;
            if (to < from) { ShowAppMessage("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu."); return; }
            ExecuteBulkDelete(from, to, false, "Xóa lịch hẹn từ " + from.ToString("dd/MM/yyyy") + " đến " + to.ToString("dd/MM/yyyy"));
        }

        private void DeleteReturnedAppointments_Click(object sender, RoutedEventArgs e)
        {
            ExecuteBulkDelete(null, null, true, "Xóa tất cả lịch đã trả hẹn");
        }

        private void DeleteAllAppointments_Click(object sender, RoutedEventArgs e)
        {
            ExecuteBulkDelete(null, null, false, "XÓA TOÀN BỘ LỊCH HẸN");
        }

        private void DeleteSelectedAppointment_Click(object sender, RoutedEventArgs e)
        {
            var view = AppointmentsGrid.SelectedItem as DataRowView;
            if (view == null) { ShowAppMessage("Hãy chọn một lịch hẹn cần xóa."); return; }

            long id = Convert.ToInt64(view["Id"]);
            string name = Convert.ToString(view["Tên bệnh nhân"]);
            string code = Convert.ToString(view["Mã BN"]);
            if (ShowAppMessage("Xóa lịch hẹn của:\n" + name + "\nMã BN: " + code +
                "\n\nHồ sơ bệnh nhân không bị xóa. Phần mềm sẽ sao lưu trước.",
                "Xác nhận xóa lịch", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                string backup = Database.BackupDatabase();
                int deleted = Database.DeleteAppointment(id);
                LoadAppointments();
                ShowAppMessage(deleted > 0 ? "Đã xóa lịch hẹn.\nBản sao lưu:\n" + backup : "Lịch hẹn không còn tồn tại.");
            }
            catch (Exception ex) { ShowAppMessage("Không thể xóa lịch:\n" + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SettingsTab_Click(object sender, RoutedEventArgs e) => ShowView("settings");

        private void ShowView(string name)
        {
            AddView.Visibility = name == "add" ? Visibility.Visible : Visibility.Collapsed;
            ListView.Visibility = name == "list" ? Visibility.Visible : Visibility.Collapsed;
            SettingsView.Visibility = name == "settings" ? Visibility.Visible : Visibility.Collapsed;

            UpdateSidebarSelection(name);
            if (name == "list")
                LoadAppointments();
        }

        private void UpdateSidebarSelection(string selected)
        {
            SetNavButtonState(AddNavButton, selected == "add");
            SetNavButtonState(ListNavButton, selected == "list");
            SetNavButtonState(SettingsNavButton, selected == "settings");
        }

        private static void SetNavButtonState(Button button, bool selected)
        {
            button.Background = selected
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 235, 229))
                : System.Windows.Media.Brushes.White;
            button.BorderBrush = selected
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 174, 160))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(214, 232, 232));
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            button.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 125, 125));
        }

        private ChromiumWebBrowser ActiveBrowser
        {
            get
            {
                var item = BrowserTabsControl.SelectedItem as TabItem;
                return item == null ? AddHospitalBrowser : item.Content as ChromiumWebBrowser;
            }
        }

        private void RegisterBrowser(ChromiumWebBrowser browser)
        {
            browser.LifeSpanHandler = new TabLifeSpanHandler(targetUrl =>
            {
                Dispatcher.BeginInvoke(new Action(() => CreateBrowserTab(targetUrl)));
            });
            browser.FrameLoadEnd += (sender, args) =>
            {
                if (!args.Frame.IsMain) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ReferenceEquals(browser, ActiveBrowser))
                        AddUrlBox.Text = args.Url;
                    SaveVisitedAddress(args.Url, AddUrlBox);
                }));
            };
        }

        private void ConfigureBrowserTab(TabItem tab, ChromiumWebBrowser browser, string title)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            var close = new Button { Content = "×", Width = 20, Height = 20, Padding = new Thickness(0), FontSize = 13, ToolTip = "Đóng tab" };
            close.Click += (s, e) => CloseBrowserTab(tab, browser);
            header.Children.Add(close);
            tab.Header = header;
        }

        private void CloseBrowserTab(TabItem tab, ChromiumWebBrowser browser)
        {
            if (BrowserTabsControl.Items.Count <= 1) return;
            BrowserTabsControl.Items.Remove(tab);
            if (browser != null) browser.Dispose();
        }

        private void CreateBrowserTab(string address)
        {
            var browser = new ChromiumWebBrowser { Address = string.IsNullOrWhiteSpace(address) ? "about:blank" : address };
            RegisterBrowser(browser);
            var tab = new TabItem { Content = browser };
            ConfigureBrowserTab(tab, browser, "Trang " + (BrowserTabsControl.Items.Count + 1));
            BrowserTabsControl.Items.Add(tab);
            BrowserTabsControl.SelectedItem = tab;
            AddUrlBox.Text = browser.Address ?? string.Empty;
        }

        private void LoadBrowserHistory(ComboBox box)
        {
            try
            {
                box.Items.Clear();
                foreach (string address in Database.GetBrowserHistory(30))
                    box.Items.Add(address);
            }
            catch { /* Lịch sử không được làm gián đoạn khởi động ứng dụng. */ }
        }

        private void SaveVisitedAddress(string address, ComboBox box)
        {
            if (string.IsNullOrWhiteSpace(address) || address == "about:blank") return;
            try
            {
                Database.SaveBrowserHistory(address);
                string current = box.Text;
                LoadBrowserHistory(box);
                box.Text = current;
            }
            catch { }
        }

        private void BrowserTabsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || e.Source != BrowserTabsControl) return;
            var browser = ActiveBrowser;
            if (browser != null)
                AddUrlBox.Text = browser.Address ?? string.Empty;
        }

        private void NewBrowserTab_Click(object sender, RoutedEventArgs e)
        {
            CreateBrowserTab("about:blank");
            AddUrlBox.Focus();
        }

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

        private void AddUrlBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (AddUrlBox.Items.Count > 0) AddUrlBox.IsDropDownOpen = true;
        }

        // Chỉ điều hướng khi người dùng click chuột vào một mục lịch sử.
        // Di chuyển bằng phím mũi tên không tự ý tải trang.
        private void HistoryItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ComboBoxItem;
            if (item == null) return;

            var box = ItemsControl.ItemsControlFromItemContainer(item) as ComboBox;
            if (box == null) return;

            string address = Convert.ToString(item.Content);
            if (string.IsNullOrWhiteSpace(address)) return;

            box.SelectedItem = address;
            box.Text = address;
            box.IsDropDownOpen = false;
            box.Dispatcher.BeginInvoke(new Action(() => box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next))), System.Windows.Threading.DispatcherPriority.Input);

            if (box == AddUrlBox)
            {
                string normalized;
                if (TryGetWebAddress(address, out normalized) && ActiveBrowser != null)
                    ActiveBrowser.Address = normalized;
            }

            e.Handled = true;
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

            if (!TryGetWebAddress(AddUrlBox.Text, out address))
            {
                ShowAppMessage("Địa chỉ website không hợp lệ.");
                return;
            }

            ActiveBrowser.Address = address;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoBack) ActiveBrowser.Back();
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoForward) ActiveBrowser.Forward();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null) ActiveBrowser.Reload();
        }

        private ChromiumWebBrowser CredentialBrowser
        {
            get { return ActiveBrowser; }
        }

        private string CurrentBrowserHost()
        {
            Uri uri;
            string address = CredentialBrowser == null ? null : CredentialBrowser.Address;
            return Uri.TryCreate(address, UriKind.Absolute, out uri) ? uri.Host.ToLowerInvariant() : null;
        }

        private static byte[] CredentialEntropy(string host)
        {
            return Encoding.UTF8.GetBytes("SoHenCT|" + (host ?? string.Empty));
        }

        private void SaveLogin_Click(object sender, RoutedEventArgs e)
        {
            string host = CurrentBrowserHost();
            if (string.IsNullOrWhiteSpace(host))
            {
                ShowAppMessage("Hãy mở trang đăng nhập trước khi lưu tài khoản.");
                return;
            }

            var dialog = new Window
            {
                Title = "Lưu đăng nhập cho " + host,
                Width = 420, Height = 270, ResizeMode = ResizeMode.NoResize,
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = System.Windows.Media.Brushes.White
            };
            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(new TextBlock { Text = "Tên đăng nhập", Margin = new Thickness(0, 0, 0, 5) });
            var userBox = new TextBox { Height = 34, Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(userBox);
            panel.Children.Add(new TextBlock { Text = "Mật khẩu", Margin = new Thickness(0, 0, 0, 5) });
            var passBox = new PasswordBox { Height = 34, Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(passBox);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var save = new Button { Content = "Lưu mã hóa", Width = 110, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Hủy", Width = 80, Height = 34 };
            save.Click += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(userBox.Text) || string.IsNullOrEmpty(passBox.Password))
                {
                    ShowAppMessage(dialog, "Nhập cả tên đăng nhập và mật khẩu.");
                    return;
                }
                try
                {
                    byte[] raw = Encoding.UTF8.GetBytes(userBox.Text + "\n" + passBox.Password);
                    byte[] protectedBytes = ProtectedData.Protect(raw, CredentialEntropy(host), DataProtectionScope.CurrentUser);
                    Database.SaveProtectedLogin(host, Convert.ToBase64String(protectedBytes));
                    Array.Clear(raw, 0, raw.Length);
                    passBox.Clear();
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch (Exception ex) { ShowAppMessage(dialog, "Không thể lưu thông tin đăng nhập: " + ex.Message); }
            };
            cancel.Click += (s, args) => dialog.Close();
            buttons.Children.Add(save); buttons.Children.Add(cancel); panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.ShowDialog();
            if (dialog.DialogResult == true)
                ShowAppMessage("Đã lưu thông tin đăng nhập đã mã hóa cho " + host + ". Chỉ tài khoản Windows hiện tại giải mã được.");
        }

        private async void FillLogin_Click(object sender, RoutedEventArgs e)
        {
            string host = CurrentBrowserHost();
            if (string.IsNullOrWhiteSpace(host)) { ShowAppMessage("Hãy mở website cần đăng nhập."); return; }
            try
            {
                string stored = Database.GetProtectedLogin(host);
                if (string.IsNullOrWhiteSpace(stored))
                {
                    ShowAppMessage("Chưa có tài khoản được lưu cho website này. Hãy chọn Lưu đăng nhập trước.", "Thông tin đăng nhập");
                    return;
                }
                byte[] protectedBytes = Convert.FromBase64String(stored);
                byte[] raw = ProtectedData.Unprotect(protectedBytes, CredentialEntropy(host), DataProtectionScope.CurrentUser);
                string[] parts = Encoding.UTF8.GetString(raw).Split(new[] { '\n' }, 2);
                Array.Clear(raw, 0, raw.Length);
                if (parts.Length != 2) throw new InvalidOperationException("Dữ liệu đăng nhập không hợp lệ.");
                string user = EscapeJavaScript(parts[0]);
                string pass = EscapeJavaScript(parts[1]);
                string script = "(function(){var p=document.querySelector('input[type=password]');if(!p)return 'Không tìm thấy ô mật khẩu';var u=document.querySelector('input[type=email],input[autocomplete=username],input[type=text]');function set(el,v){if(!el)return;var d=Object.getOwnPropertyDescriptor(Object.getPrototypeOf(el),'value');if(d&&d.set)d.set.call(el,v);else el.value=v;el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));}set(u,'" + user + "');set(p,'" + pass + "');return 'Đã điền thông tin, chưa tự gửi biểu mẫu';})()";
                var frame = CredentialBrowser.GetMainFrame();
                var result = await frame.EvaluateScriptAsync(script);
                ShowAppMessage(Convert.ToString(result.Result ?? "Không thể tự điền"), "Tự điền đăng nhập");
            }
            catch (Exception ex) { ShowAppMessage("Không thể giải mã hoặc điền thông tin: " + ex.Message); }
        }

        private static string EscapeJavaScript(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private void BrowserFit_Click(object sender, RoutedEventArgs e) { if (ActiveBrowser != null) ActiveBrowser.ZoomLevel = 0; }
        private void BrowserZoomIn_Click(object sender, RoutedEventArgs e) { if (ActiveBrowser != null) ActiveBrowser.ZoomLevel = Math.Min(5, ActiveBrowser.ZoomLevel + 0.5); }
        private void BrowserZoomOut_Click(object sender, RoutedEventArgs e) { if (ActiveBrowser != null) ActiveBrowser.ZoomLevel = Math.Max(-5, ActiveBrowser.ZoomLevel - 0.5); }
        private void AddBrowserFit_Click(object sender, RoutedEventArgs e) { BrowserFit_Click(sender, e); }
        private void AddBrowserZoomIn_Click(object sender, RoutedEventArgs e) { BrowserZoomIn_Click(sender, e); }
        private void AddBrowserZoomOut_Click(object sender, RoutedEventArgs e) { BrowserZoomOut_Click(sender, e); }

        private void AddOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            string address;

            if (!TryGetWebAddress(AddUrlBox.Text, out address))
            {
                ShowAppMessage("Địa chỉ website không hợp lệ.");
                return;
            }

            ActiveBrowser.Address = address;
        }

        private void AddBack_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoBack) ActiveBrowser.Back();
        }

        private void AddForward_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null && ActiveBrowser.CanGoForward) ActiveBrowser.Forward();
        }

        private void AddReload_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser != null) ActiveBrowser.Reload();
        }

        private sealed class HospitalScanRow
        {
            public string ReceiptCode { get; set; }
            public string ExamCode { get { return ReceiptCode; } }
            public string PatientCode { get; set; }
            public string PatientName { get; set; }
            public string Age { get; set; }
            public string InjectionStatus { get; set; }
            public string Department { get; set; }
            public string OrderedAt { get; set; }
            public string ServiceName { get; set; }
            public string Room { get; set; }
            public string Status { get; set; }
            public long ExistingAppointmentId { get; set; }
            public string ExistingAppointmentDate { get; set; }
            public string ExistingShift { get; set; }
        }

        private const string HospitalScanScript = @"
(function () {
  function clean(v) { return (v || '').replace(/\s+/g, ' ').trim(); }
  function txt(el) { return clean(el ? (el.innerText || el.textContent || '') : ''); }
  function visible(el) {
    if (!el) return false;
    var st = window.getComputedStyle(el);
    return st.display !== 'none' && st.visibility !== 'hidden' && el.getClientRects().length > 0;
  }
  function norm(v) {
    var x = clean(v).toLowerCase();
    try { x = x.normalize('NFD').replace(/[\u0300-\u036f]/g, ''); } catch(e) {}
    return x.replace(/đ/g, 'd');
  }
  function serviceCandidate(value, shown) {
    var v = clean(value);
    if (!v || v.length <= shown.length) return '';
    var n = norm(v);
    // Ưu tiên chuỗi mô tả dịch vụ có cùng phần đầu với nội dung đang hiển thị.
    var prefix = norm(shown).replace(/[.…]+$/g, '').trim();
    if (prefix && n.indexOf(prefix.substring(0, Math.min(prefix.length, 18))) < 0 &&
        n.indexOf('chup') < 0 && n.indexOf('dich vu') < 0) return '';
    if (v.length > 1200) return '';
    return v;
  }
  function deepServiceStrings(root, shown) {
    var found = [], seen = [];
    function walk(obj, depth) {
      if (obj == null || depth > 5) return;
      if (typeof obj === 'string') {
        var candidate = serviceCandidate(obj, shown);
        if (candidate) found.push(candidate);
        return;
      }
      if (typeof obj !== 'object' || seen.indexOf(obj) >= 0) return;
      seen.push(obj);
      var keys;
      try { keys = Object.keys(obj).slice(0, 80); } catch(e) { return; }
      for (var i=0;i<keys.length;i++) {
        var key = keys[i];
        if (/^(_owner|_store|return|child|sibling|stateNode|alternate|dependencies)$/i.test(key)) continue;
        try { walk(obj[key], depth + 1); } catch(e) {}
      }
    }
    walk(root, 0);
    found.sort(function(a,b){ return b.length-a.length; });
    return found.length ? found[0] : '';
  }
  function getFullService(cell, shown) {
    if (!cell) return shown;
    var candidates = [];
    // Thuộc tính title/aria-label/data-* có thể giữ nguyên nội dung trước khi CSS rút gọn.
    var nodes = [cell].concat(Array.prototype.slice.call(cell.querySelectorAll('*')));
    nodes.forEach(function(el) {
      ['title','aria-label','data-original-title','data-title','data-content','data-tooltip-content'].forEach(function(a) {
        try { var v = serviceCandidate(el.getAttribute(a), shown); if (v) candidates.push(v); } catch(e) {}
      });
      // React lưu dữ liệu gốc trong props/fiber dù textContent đã bị rút gọn.
      try {
        Object.keys(el).forEach(function(k) {
          if (k.indexOf('__reactProps$') === 0) {
            var v = deepServiceStrings(el[k], shown); if (v) candidates.push(v);
          }
          if (k.indexOf('__reactFiber$') === 0) {
            var fiber = el[k], hops = 0;
            while (fiber && hops++ < 8) {
              var v1 = deepServiceStrings(fiber.memoizedProps, shown);
              var v2 = deepServiceStrings(fiber.pendingProps, shown);
              if (v1) candidates.push(v1); if (v2) candidates.push(v2);
              fiber = fiber.return;
            }
          }
        });
      } catch(e) {}
    });
    // Đọc Popover theo aria-describedby nếu phần tử đã tồn tại, không giả lập rê chuột.
    var serviceSpan = cell.querySelector('span[aria-describedby]');
    if (serviceSpan) {
      var describedId = serviceSpan.getAttribute('aria-describedby');
      var describedNode = describedId ? document.getElementById(describedId) : null;
      if (describedNode) {
        var popoverInner = describedNode.querySelector('.ant-popover-inner') ||
                           (describedNode.classList && describedNode.classList.contains('ant-popover-inner') ? describedNode : null);
        var fullText = txt(popoverInner || describedNode).replace(/^Tên dịch vụ\s*/i, '').trim();
        var c = serviceCandidate(fullText, shown); if (c) candidates.push(c);
      }
    }
    candidates.sort(function(a,b){ return b.length-a.length; });
    return candidates.length ? candidates[0] : shown;
  }

  // Selector đã được xác nhận bằng công cụ DOM trên website bệnh viện.
  var tables = Array.prototype.slice.call(document.querySelectorAll('table._st-table_gefxy_208'));
  if (!tables.length) tables = Array.prototype.slice.call(document.querySelectorAll('table'));
  tables = tables.filter(visible);
  if (!tables.length) return JSON.stringify({error:'Không tìm thấy bảng chỉ định. Hãy mở trang danh sách bệnh nhân.'});

  var chosen = null, bestRows = [], bestScore = -1, bestHeaders = [];
  tables.forEach(function(t) {
    var allRows = Array.prototype.slice.call(t.querySelectorAll('tr'));
    var dataRows = allRows.filter(function(r) {
      var cells = r.querySelectorAll('td');
      return cells.length >= 13 && visible(r);
    });
    var headers = [];
    var head = t.querySelector('thead tr');
    if (head) {
      var hc = head.querySelectorAll('th,td');
      for (var i=0;i<hc.length;i++) headers.push(txt(hc[i]));
    }
    // Bảng đã xác nhận có 13 cột, gồm STT ở vị trí 0.
    var score = dataRows.length * 10 + (t.className && String(t.className).indexOf('_st-table_gefxy_208') >= 0 ? 100 : 0);
    if (score > bestScore) { chosen=t; bestRows=dataRows; bestScore=score; bestHeaders=headers; }
  });
  if (!chosen) return JSON.stringify({error:'Không tìm thấy bảng chỉ định phù hợp.'});

  var rows = [];
  bestRows.forEach(function(r) {
    var c = r.querySelectorAll('td');
    function at(i) { return i < c.length ? txt(c[i]) : ''; }
    // Cột theo DOM đã xác nhận: 0 STT, 1 Số phiếu, 2 Mã lần khám,
    // 3 Mã BN, 4 Tên BN, 5 Tuổi, 6 Khoa/phòng, 7 Thời gian,
    // 8 Tên dịch vụ, 9 Phòng, 10 KTV, 11 Thứ tự, 12 Thao tác.
    var serviceCell = c.length > 8 ? c[8] : null;
    var displayedService = at(8);
    var serviceName = getFullService(serviceCell, displayedService);
    var item = {
      receiptCode: at(1),
      patientCode: at(3),
      patientName: at(4),
      age: at(5),
      department: at(6),
      orderedAt: at(7),
      serviceName: serviceName,
      room: at(9)
    };
    if (item.receiptCode && item.patientCode) rows.push(item);
  });
  return JSON.stringify({
    headers: bestHeaders,
    rowCount: rows.length,
    rows: rows,
    debug: {selector: 'table._st-table_gefxy_208', candidateRows: bestRows.length,
            acceptedRows: rows.length, cellCountExpected: 13}
  });
})()";

        private const string AutoDetectAppointmentPairsScript = @"
(function(){
 function clean(v){return (v||'').replace(/\s+/g,' ').trim();}
 function norm(v){var x=clean(v).toLowerCase();try{x=x.normalize('NFD').replace(/[\u0300-\u036f]/g,'');}catch(e){}return x.replace(/đ/g,'d');}
 function text(el){return clean(el?(el.innerText||el.textContent||''):'');}
 var result=[], seen={};
 function add(patient,receipt,name,dept,service,age){
  patient=clean(patient);receipt=clean(receipt);
  if(!patient||!receipt||patient.length<3||receipt.length<3||!/[0-9]/.test(patient)||!/[0-9]/.test(receipt))return;
  var key=patient+'|'+receipt;if(seen[key])return;seen[key]=1;
  result.push({patientCode:patient,receiptCode:receipt,patientName:clean(name),department:clean(dept),serviceName:clean(service),age:clean(age)});
 }
 // Bảng chỉ định của bệnh viện đã được xác nhận: 13 cột cố định.
 var known=Array.prototype.slice.call(document.querySelectorAll('table._st-table_gefxy_208'));
 known.forEach(function(table){
  Array.prototype.slice.call(table.querySelectorAll('tr')).forEach(function(tr){
   var c=Array.prototype.slice.call(tr.querySelectorAll('td'));if(c.length<13)return;
   function at(i){return text(c[i]);}
   add(at(3),at(1),at(4),at(6),at(8),at(5));
  });
 });
 // Các bảng kết quả tìm bệnh nhân: dò theo tiêu đề thay vì phụ thuộc thứ tự cột.
 var tables=Array.prototype.slice.call(document.querySelectorAll('table'));
 tables.forEach(function(table){
  if(known.indexOf(table)>=0)return;
  var headerRow=table.querySelector('thead tr')||table.querySelector('tr');if(!headerRow)return;
  var hs=Array.prototype.slice.call(headerRow.querySelectorAll('th,td')).map(text);
  var pi=-1,ri=-1,ni=-1,di=-1,si=-1,ai=-1;
  hs.forEach(function(h,i){var n=norm(h);
   if(pi<0&&(n==='ma bn'||n.indexOf('ma benh nhan')>=0))pi=i;
   if(ri<0&&(n.indexOf('so phieu')>=0||n.indexOf('ma chi dinh')>=0))ri=i;
   if(ni<0&&(n.indexOf('ten benh nhan')>=0||n==='ho ten'))ni=i;
   if(di<0&&(n.indexOf('khoa')>=0||n.indexOf('phong')>=0))di=i;
   if(si<0&&(n.indexOf('dich vu')>=0||n.indexOf('ten chi dinh')>=0))si=i;
   if(ai<0&&n.indexOf('tuoi')>=0)ai=i;
  });
  if(pi<0||ri<0)return;
  var trs=Array.prototype.slice.call(table.querySelectorAll('tbody tr'));
  if(!trs.length)trs=Array.prototype.slice.call(table.querySelectorAll('tr')).slice(1);
  trs.forEach(function(tr){var c=Array.prototype.slice.call(tr.querySelectorAll('td'));if(!c.length)return;
   function at(i){return i>=0&&i<c.length?text(c[i]):'';}
   add(at(pi),at(ri),at(ni),at(di),at(si),at(ai));
  });
 });
 return JSON.stringify(result);
})()";

        private void ShowDuplicateNotice(HospitalScanRow row, Appointment saved)
        {
            // Thông báo trùng lịch gọn, dùng cùng bảng màu xanh đậm/xanh ngọc của ứng dụng.
            var dialog = new Window
            {
                Title = "Phát hiện bệnh nhân đã có lịch",
                Width = 470,
                SizeToContent = SizeToContent.Height,
                MinHeight = 190,
                MaxHeight = 300,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = System.Windows.Media.Brushes.Transparent,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ShowInTaskbar = false
            };
            var outer = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 252, 252)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 125, 120)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(22)
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Phát hiện bệnh nhân đã có lịch",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 61, 73)),
                Margin = new Thickness(0, 0, 0, 12)
            });
            string message = "Bệnh nhân " + (row.PatientName ?? "").Trim() +
                " đã có lịch hẹn buổi " + (saved.Shift ?? "") +
                ", ngày " + saved.AppointmentDate.ToString("dd/MM/yyyy") + ".";
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 72, 79)),
                Margin = new Thickness(0, 0, 0, 20)
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Action<Button> styleButton = b =>
            {
                b.Height = 40; b.MinWidth = 94; b.Padding = new Thickness(12, 7, 12, 7);
                b.Margin = new Thickness(0, 0, 8, 0);
                b.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(221, 239, 239));
                b.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 61, 73));
                b.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 220, 220));
            };
            var returnButton = new Button { Content = "Trả hẹn" }; styleButton(returnButton);
            returnButton.Click += (s, e) =>
            {
                try { Database.Return(saved.Id); LoadAppointments(); dialog.DialogResult = true; dialog.Close(); }
                catch (Exception ex) { ShowAppMessage(dialog, "Không thể trả hẹn: " + ex.Message); }
            };
            var changeButton = new Button { Content = "Đổi lịch" }; styleButton(changeButton);
            changeButton.Click += (s, e) =>
            {
                row.ExistingAppointmentId = saved.Id;
                row.ExistingAppointmentDate = saved.AppointmentDate.ToString("dd/MM/yyyy");
                row.ExistingShift = saved.Shift;
                dialog.Hide();
                bool changed = ChangeAppointmentFromScan(row, this);
                if (changed) LoadAppointments();
                dialog.Close();
            };
            var closeButton = new Button { Content = "Đóng", Margin = new Thickness(0) }; styleButton(closeButton);
            closeButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            actions.Children.Add(returnButton); actions.Children.Add(changeButton); actions.Children.Add(closeButton);
            panel.Children.Add(actions); outer.Child = panel; dialog.Content = outer;
            dialog.ShowDialog();
        }

        private async void ScanHospitalList_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveBrowser == null || !ActiveBrowser.IsBrowserInitialized)
            {
                ShowAppMessage("Trình duyệt bệnh viện chưa sẵn sàng.", "Quét danh sách");
                return;
            }
            try
            {
                var frame = ActiveBrowser.GetMainFrame();
                if (frame == null || !frame.IsValid) { ShowAppMessage("Không đọc được trang hiện tại."); return; }
                var response = await frame.EvaluateScriptAsync(HospitalScanScript);
                if (!response.Success || response.Result == null)
                {
                    ShowAppMessage("Không thể đọc dữ liệu trang: " + response.Message, "Quét danh sách");
                    return;
                }
                var serializer = new JavaScriptSerializer();
                var payload = serializer.Deserialize<Dictionary<string, object>>(Convert.ToString(response.Result));
                if (payload.ContainsKey("error")) { ShowAppMessage(Convert.ToString(payload["error"]), "Quét danh sách"); return; }
                var rows = payload.ContainsKey("rows") ? payload["rows"] as System.Collections.ArrayList : null;
                if (rows == null || rows.Count == 0)
                {
                    string debug = payload.ContainsKey("debug") ? new JavaScriptSerializer().Serialize(payload["debug"]) : "Không có thông tin chẩn đoán";
                    ShowAppMessage("Đã tìm thấy bảng nhưng chưa đọc được dòng bệnh nhân.\n\nChi tiết: " + debug + "\n\nHãy gửi ảnh này cùng báo cáo DOM để điều chỉnh bộ đọc.", "Quét danh sách");
                    return;
                }
                var results = new List<HospitalScanRow>();
                foreach (var item in rows)
                {
                    var d = item as Dictionary<string, object>; if (d == null) continue;
                    string receipt = GetScanValue(d, "receiptCode");
                    string patient = GetScanValue(d, "patientCode");
                    if (string.IsNullOrWhiteSpace(receipt) || string.IsNullOrWhiteSpace(patient)) continue;
                    var match = Database.FindLatestAppointment(patient, receipt);
                    var r = new HospitalScanRow { ReceiptCode=receipt, PatientCode=patient,
                        PatientName=GetScanValue(d,"patientName"), Age=GetScanValue(d,"age"), Department=GetScanValue(d,"department"),
                        InjectionStatus=DetectInjectionStatus(GetScanValue(d,"serviceName")), OrderedAt=GetScanValue(d,"orderedAt"), ServiceName=GetScanValue(d,"serviceName"), Room=GetScanValue(d,"room") };
                    if (match != null) { r.Status = match.Status == "Đã hẹn" ? "ĐÃ CÓ LỊCH" : "ĐÃ TRẢ HẸN"; r.ExistingAppointmentId=match.Id; r.ExistingAppointmentDate=match.AppointmentDate.ToString("dd/MM/yyyy"); r.ExistingShift=match.Shift; }
                    else r.Status="CHƯA CÓ LỊCH";
                    results.Add(r);
                }
                if (results.Count == 0)
                {
                    ShowAppMessage("Đã đọc được bảng nhưng không có dòng nào có đủ Số phiếu và Mã BN. Hãy kiểm tra tiêu đề/cấu trúc hàng trong báo cáo DOM.", "Quét danh sách");
                    return;
                }
                int incompleteServices = results.Count(x => string.IsNullOrWhiteSpace(x.ServiceName) ||
                    x.ServiceName.TrimEnd('.', '…').Length <= 0 || x.ServiceName.EndsWith("...", StringComparison.Ordinal));
                // Chỉ cảnh báo khi người dùng chủ động bấm Quét DS.
                // Một lượt quét chỉ hiện một cảnh báo, các dòng trùng còn lại vẫn được đánh dấu trong bảng.
                // Nếu trang có nhiều bệnh nhân, luôn hiển thị bảng kết quả để
                // người dùng xem và chọn đúng dòng. Chỉ dùng hộp thoại trùng lịch
                // rút gọn khi lượt quét chỉ có duy nhất một bệnh nhân.
                if (results.Count == 1)
                {
                    var duplicate = results[0];
                    if (duplicate.ExistingAppointmentId > 0)
                    {
                        var savedAppointment = Database.FindLatestAppointment(duplicate.PatientCode, duplicate.ReceiptCode);
                        if (savedAppointment != null)
                        {
                            ShowDuplicateNotice(duplicate, savedAppointment);
                            return;
                        }
                    }
                }
                ShowHospitalScanResults(results);
                if (incompleteServices > 0)
                    ShowAppMessage("Đã đọc " + results.Count + " dòng, nhưng có " + incompleteServices +
                        " dòng chưa lấy được tên dịch vụ đầy đủ. Vui lòng kiểm tra các dòng có dấu ... trước khi tạo lịch.",
                        "Cảnh báo dữ liệu chỉ định", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { ShowAppMessage("Lỗi khi quét danh sách bệnh nhân.\n\n" + ex.Message, "Quét danh sách", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private static string GetScanValue(Dictionary<string, object> data, string key)
        {
            object value; return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value).Trim() : "";
        }

        private void ShowHospitalScanResults(List<HospitalScanRow> rows)
        {
            var window = new Window { Title="Kết quả quét chỉ định", Width=1250, Height=680, MinWidth=900, MinHeight=480,
                Owner=this, WindowStartupLocation=WindowStartupLocation.CenterOwner, Background=System.Windows.Media.Brushes.White };
            var root = new DockPanel { Margin=new Thickness(12) };
            var summary = new TextBlock { Text="Đã quét " + rows.Count + " dòng. Đối chiếu theo Mã BN + Số phiếu. Ca chưa xác định tiêm vẫn có thể tạo lịch hẹn.",
                TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,10), Foreground=System.Windows.Media.Brushes.DarkSlateGray };
            DockPanel.SetDock(summary,Dock.Top); root.Children.Add(summary);
            var grid = new DataGrid { ItemsSource=rows, AutoGenerateColumns=false, IsReadOnly=true, SelectionMode=DataGridSelectionMode.Single,
                CanUserAddRows=false, CanUserDeleteRows=false, HeadersVisibility=DataGridHeadersVisibility.Column };
            grid.Columns.Add(new DataGridTextColumn { Header="Kết quả", Binding=new System.Windows.Data.Binding("Status"), Width=105 });
            grid.Columns.Add(new DataGridTextColumn { Header="Số phiếu / Mã chỉ định", Binding=new System.Windows.Data.Binding("ReceiptCode"), Width=145 });
            grid.Columns.Add(new DataGridTextColumn { Header="Tuổi", Binding=new System.Windows.Data.Binding("Age"), Width=55 });
            grid.Columns.Add(new DataGridTextColumn { Header="Tiêm cản quang", Binding=new System.Windows.Data.Binding("InjectionStatus"), Width=105 });
            grid.Columns.Add(new DataGridTextColumn { Header="Mã BN", Binding=new System.Windows.Data.Binding("PatientCode"), Width=110 });
            grid.Columns.Add(new DataGridTextColumn { Header="Tên bệnh nhân", Binding=new System.Windows.Data.Binding("PatientName"), Width=160 });
            grid.Columns.Add(new DataGridTextColumn { Header="Khoa/phòng", Binding=new System.Windows.Data.Binding("Department"), Width=145 });
            grid.Columns.Add(new DataGridTextColumn { Header="Thời gian chỉ định", Binding=new System.Windows.Data.Binding("OrderedAt"), Width=140 });
            var serviceColumn = new DataGridTextColumn { Header="Dịch vụ (toàn bộ)", Binding=new System.Windows.Data.Binding("ServiceName"), Width=260 };
            serviceColumn.ElementStyle = new Style(typeof(TextBlock));
            serviceColumn.ElementStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            serviceColumn.ElementStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("ServiceName")));
            grid.Columns.Add(serviceColumn);
            grid.Columns.Add(new DataGridTextColumn { Header="Lịch đã lưu", Binding=new System.Windows.Data.Binding("ExistingAppointmentDate"), Width=95 });
            grid.Columns.Add(new DataGridTextColumn { Header="Buổi", Binding=new System.Windows.Data.Binding("ExistingShift"), Width=65 });
            if (rows.Count == 1) grid.SelectedIndex = 0;

            var buttons = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,10,0,0) };
            var returnButton = new Button { Content="Trả hẹn lịch cũ", MinWidth=135, Height=42, Padding=new Thickness(12,8,12,8), Margin=new Thickness(0,0,8,0) };
            returnButton.Click += (s,e) => {
                var selected=grid.SelectedItem as HospitalScanRow ?? (rows.Count == 1 ? rows[0] : null);
                if (selected==null || selected.ExistingAppointmentId<=0) { ShowAppMessage(window,"Chọn một dòng đang có lịch hẹn trước."); return; }
                try { Database.Return(selected.ExistingAppointmentId); LoadAppointments(); window.Close(); }
                catch(Exception ex) { ShowAppMessage(window,"Không thể trả hẹn: " + ex.Message); }
            };
            var changeButton = new Button { Content="Đổi lịch", MinWidth=100, Height=42, Padding=new Thickness(12,8,12,8), Margin=new Thickness(0,0,8,0) };
            changeButton.Click += (s,e) => {
                var selected=grid.SelectedItem as HospitalScanRow ?? (rows.Count == 1 ? rows[0] : null);
                if(selected==null || selected.ExistingAppointmentId<=0 || selected.Status=="ĐÃ TRẢ HẸN") { ShowAppMessage(window,"Chỉ bệnh nhân đang có lịch hẹn mới được đổi lịch."); return; }
                if (ChangeAppointmentFromScan(selected, window)) {
                    // Đọc lại lịch vừa cập nhật để hiển thị ngày/buổi mới trong bảng quét.
                    var updated=Database.FindActiveAppointment(selected.PatientCode,selected.ReceiptCode);
                    if(updated!=null){selected.ExistingAppointmentDate=updated.AppointmentDate.ToString("dd/MM/yyyy");selected.ExistingShift=updated.Shift;}
                    LoadAppointments(); window.Close();
                }
            };
            var createButton = new Button { Content="Tạo lịch hẹn", MinWidth=115, Height=42, Padding=new Thickness(12,8,12,8), Margin=new Thickness(0,0,8,0) };
            createButton.Click += (s,e) => {
                var selected=grid.SelectedItem as HospitalScanRow ?? (rows.Count == 1 ? rows[0] : null);
                if(selected==null){ShowAppMessage(window,"Hãy chọn một bệnh nhân.");return;}
                if(selected.ExistingAppointmentId>0 && selected.Status!="ĐÃ TRẢ HẸN"){ShowAppMessage(window,"Bệnh nhân đang có lịch hẹn. Hãy dùng nút Đổi lịch hoặc Trả hẹn lịch cũ.");return;}
                if (CreateAppointmentFromScan(selected, window)) {
                    selected.Status="ĐÃ CÓ LỊCH";
                    var match=Database.FindActiveAppointment(selected.PatientCode,selected.ReceiptCode);
                    if(match!=null){selected.ExistingAppointmentId=match.Id;selected.ExistingAppointmentDate=match.AppointmentDate.ToString("dd/MM/yyyy");selected.ExistingShift=match.Shift;}
                    grid.Items.Refresh(); LoadAppointments();
                    // Không bật thông báo nổi sau khi tạo lịch: tránh Windows
                    // chuyển Focus hoặc thu nhỏ cửa sổ ứng dụng khi popup tự đóng.
                    // Cửa sổ kết quả đóng lại và trả người dùng về màn hình làm việc.
                    window.Close();
                }
            };
            var closeButton = new Button { Content="Đóng", MinWidth=90, Height=42, Padding=new Thickness(12,8,12,8) }; closeButton.Click += (s,e)=>window.Close();
            buttons.Children.Add(returnButton); buttons.Children.Add(changeButton); buttons.Children.Add(createButton); buttons.Children.Add(closeButton);
            DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons); root.Children.Add(grid); window.Content=root; window.ShowDialog();
        }

        private bool ChangeAppointmentFromScan(HospitalScanRow row, Window owner)
        {
            var date = new DatePicker { SelectedDate=DateTime.ParseExact(row.ExistingAppointmentDate,"dd/MM/yyyy",System.Globalization.CultureInfo.InvariantCulture), Height=36, Margin=new Thickness(0,0,0,14) };
            var shift = new ComboBox { Height=36, Margin=new Thickness(0,0,0,14) };
            shift.Items.Add("Sáng"); shift.Items.Add("Chiều"); shift.SelectedItem = row.ExistingShift == "Chiều" ? "Chiều" : "Sáng";
            return ShowScheduleDialog(row, owner, date, shift, true);
        }

        private bool ShowScheduleDialog(HospitalScanRow row, Window owner, DatePicker date, ComboBox shift, bool isChange)
        {
            var dialog = new Window { Title=isChange?"Đổi lịch hẹn":"Tạo lịch hẹn mới", Width=490, SizeToContent=SizeToContent.Height, MinHeight=330, MaxHeight=620,
                ResizeMode=ResizeMode.NoResize, Owner=owner, WindowStartupLocation=WindowStartupLocation.CenterOwner, Background=System.Windows.Media.Brushes.White };
            var panel = new StackPanel { Margin=new Thickness(22), Width=440 };
            panel.Children.Add(new TextBlock { Text=row.PatientName + "  |  " + row.PatientCode, FontSize=17, FontWeight=FontWeights.Bold, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,8) });
            panel.Children.Add(new TextBlock { Text="Số phiếu: " + row.ReceiptCode + "\nDịch vụ: " + row.ServiceName, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,12) });
            if(row.InjectionStatus=="Chưa xác định") panel.Children.Add(new TextBlock { Text="⚠ Chưa xác định được trạng thái tiêm. Bạn vẫn có thể tạo lịch; trạng thái này sẽ được lưu để kiểm tra sau.", Foreground=System.Windows.Media.Brushes.DarkOrange, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,12) });
            panel.Children.Add(new TextBlock { Text="Ngày hẹn", Margin=new Thickness(0,0,0,4) }); panel.Children.Add(CreateScheduleDateControl(date));
            panel.Children.Add(new TextBlock { Text="Buổi hẹn", Margin=new Thickness(0,0,0,4) }); panel.Children.Add(shift);
            panel.Children.Add(new TextBlock { Text=isChange?"Chỉ cập nhật ngày và buổi của lịch hiện tại.":"Chỉ cần chọn ngày hẹn và buổi hẹn. Thông tin bệnh nhân và chỉ định sẽ được tự điền.", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,16), Foreground=System.Windows.Media.Brushes.DimGray });
            var actions = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=new Thickness(0,4,0,0) };
            var save = new Button { Content=isChange?"Lưu đổi lịch":"Tạo lịch hẹn", MinWidth=140, Height=44, Padding=new Thickness(14,9,14,9), IsDefault=true, Margin=new Thickness(0,0,10,0) };
            var cancel = new Button { Content="Hủy", MinWidth=100, Height=44, Padding=new Thickness(14,9,14,9), IsCancel=true };
            bool saved=false;
            save.Click += (s,e) => {
                if(date.SelectedDate==null){ShowAppMessage(dialog,"Hãy chọn ngày hẹn.");return;}
                try {
                    if(isChange) Database.UpdateAppointmentSchedule(row.ExistingAppointmentId,date.SelectedDate.Value.Date,Convert.ToString(shift.SelectedItem));
                    else {
                        if(Database.FindActiveAppointment(row.PatientCode,row.ReceiptCode)!=null){ShowAppMessage(dialog,"Lịch này vừa được tạo ở nơi khác hoặc đã tồn tại. Không tạo trùng.");return;}
                        Database.Add(new Appointment { PatientCode=row.PatientCode, PatientName=row.PatientName, Department=row.Department, ExamCode=row.ReceiptCode, ServiceName=row.ServiceName, InjectionStatus=row.InjectionStatus, AppointmentDate=date.SelectedDate.Value.Date, Shift=Convert.ToString(shift.SelectedItem), Status="Đã hẹn", CreatedAt=DateTime.Now });
                    }
                    saved=true; dialog.DialogResult=true; dialog.Close();
                } catch(Exception ex){ShowAppMessage(dialog,(isChange?"Không thể đổi lịch: ":"Không thể tạo lịch: ") + ex.Message);}
            };
            actions.Children.Add(save); actions.Children.Add(cancel); panel.Children.Add(actions); dialog.Content=panel;
            return dialog.ShowDialog()==true && saved;
        }

        private static DockPanel CreateScheduleDateControl(DatePicker date)
        {
            var panel = new DockPanel { LastChildFill = true, Margin = new Thickness(0,0,0,14) };
            var minus = new Button { Content = "−", Width = 34, Height = 36, Margin = new Thickness(0,0,5,0), Padding = new Thickness(0), ToolTip = "Lùi một ngày" };
            var plus = new Button { Content = "+", Width = 34, Height = 36, Margin = new Thickness(5,0,0,0), Padding = new Thickness(0), ToolTip = "Tiến một ngày" };
            minus.Click += (s,e) => date.SelectedDate = (date.SelectedDate ?? DateTime.Today).AddDays(-1);
            plus.Click += (s,e) => date.SelectedDate = (date.SelectedDate ?? DateTime.Today).AddDays(1);
            DockPanel.SetDock(minus, Dock.Left); DockPanel.SetDock(plus, Dock.Right);
            date.Height = 36; date.Margin = new Thickness(0);
            panel.Children.Add(minus); panel.Children.Add(plus); panel.Children.Add(date);
            return panel;
        }

        private bool CreateAppointmentFromScan(HospitalScanRow row, Window owner)
        {
            if (string.IsNullOrWhiteSpace(row.PatientCode) || string.IsNullOrWhiteSpace(row.ReceiptCode) || string.IsNullOrWhiteSpace(row.PatientName))
            { ShowAppMessage(owner, "Thiếu mã BN, số phiếu hoặc tên bệnh nhân. Không thể tạo lịch."); return false; }
            var date = new DatePicker { SelectedDate=DateTime.Today.AddDays(1), Height=36, Margin=new Thickness(0,0,0,14) };
            var shift = new ComboBox { Height=36, SelectedIndex=0, Margin=new Thickness(0,0,0,14) }; shift.Items.Add("Sáng"); shift.Items.Add("Chiều");
            bool result=ShowScheduleDialog(row,owner,date,shift,false);
            return result;
        }

        private static string DetectInjectionStatus(string serviceName)
        {
            string value = (serviceName ?? "").ToLowerInvariant();
            string[] noTerms = { "không tiêm", "khong tiem", "không dùng thuốc cản quang", "khong dung thuoc can quang", "không sử dụng thuốc cản quang", "khong su dung thuoc can quang" };
            foreach (string term in noTerms) if (value.Contains(term)) return "Không tiêm";
            string[] yesTerms = { "có tiêm", "co tiem", "tiêm thuốc cản quang", "tiem thuoc can quang", "cản quang", "can quang" };
            foreach (string term in yesTerms) if (value.Contains(term)) return "Có tiêm";
            return "Chưa xác định";
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
                ShowAppMessage(
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
                    ShowAppMessage("Không truy cập được nội dung trang hiện tại.");
                    return;
                }

                JavascriptResponse response =
                    await frame.EvaluateScriptAsync(DomInspectionScript);

                if (!response.Success)
                {
                    ShowAppMessage(
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
                ShowAppMessage(
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
                ShowAppMessage(window, "Đã sao chép báo cáo DOM.");
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
            await InspectBrowserDomAsync(ActiveBrowser, "Trình duyệt bệnh viện");
        }

        private async void InspectAddDom_Click(object sender, RoutedEventArgs e)
        {
            await InspectBrowserDomAsync(ActiveBrowser, "Trình duyệt thêm lịch");
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
                ShowAppMessage(
                    "Vui lòng nhập đầy đủ mã bệnh nhân, tên bệnh nhân, khoa phòng, ngày hẹn và buổi hẹn.",
                    "Thiếu thông tin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (Database.HasActiveAppointment(code, exam))
            {
                ShowAppMessage(
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

                ShowAppMessage("Đã thêm lịch hẹn thành công.");

                PatientCodeBox.Clear();
                PatientNameBox.Clear();
                DepartmentBox.Clear();
                ExamCodeBox.Clear();

                ServiceBox.Text = "Chụp CLVT";
                InjectionStatusBox.SelectedIndex = 2;
                AppointmentDatePicker.SelectedDate = DateTime.Today.AddDays(1);
                ShiftBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowAppMessage("Không thể lưu lịch hẹn.\n\n" + ex.Message);
            }
        }

        private void AppointmentsGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var row = e.Row.DataContext as DataRowView;
            if (row == null) return;
            bool returned = Convert.ToString(row["Trạng thái"]) == "Đã trả hẹn";
            e.Row.Foreground = returned ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.DarkSlateGray;
            e.Row.Opacity = returned ? 0.72 : 1.0;
            // Không đặt Background/BorderBrush cục bộ ở đây: các DataTrigger của CellStyle
            // cần được quyền tô nền và viền cho hai cột trạng thái.
            foreach (var cell in FindVisualChildren<DataGridCell>(e.Row))
            {
                var text = cell.Content as TextBlock;
                if (text != null)
                    text.TextDecorations = returned ? TextDecorations.Strikethrough : null;
                string header = cell.Column == null ? "" : Convert.ToString(cell.Column.Header);
                if (header != "Buổi" && header != "Tiêm thuốc cản quang")
                    cell.Foreground = returned ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.DarkSlateGray;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T) yield return (T)child;
                foreach (T descendant in FindVisualChildren<T>(child)) yield return descendant;
            }
        }

        private void LoadAppointments()
        {
            DateTime from = FromDatePicker.SelectedDate ?? DateTime.Today;
            DateTime to = ToDatePicker.SelectedDate ?? from;

            if (to.Date < from.Date)
            {
                ResetAppointmentCounters();
                ShowAppMessage("Ngày đến phải bằng hoặc sau ngày bắt đầu.");
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
                // Bộ đếm luôn dựa trên toàn bộ lịch trong khoảng ngày/buổi,
                // không bị ảnh hưởng bởi bộ lọc tiêm hoặc tùy chọn ẩn lịch đã trả.
                DataTable allData = Database.GetAppointments(
                    from.Date, to.Date, shift, true, "Tất cả");

                int totalCount = allData.Rows.Count;
                int totalInjectionCount = 0;
                int totalNoInjectionCount = 0;
                int activeCount = 0;
                int activeInjectionCount = 0;
                int activeNoInjectionCount = 0;

                foreach (DataRow row in allData.Rows)
                {
                    string category = GetInjectionCategory(row);
                    bool isActive = Convert.ToString(row["Trạng thái"]) == "Đã hẹn";

                    if (category == "Có tiêm")
                    {
                        totalInjectionCount++;
                        if (isActive)
                        {
                            activeCount++;
                            activeInjectionCount++;
                        }
                    }
                    else if (category == "Không tiêm")
                    {
                        totalNoInjectionCount++;
                        if (isActive)
                        {
                            activeCount++;
                            activeNoInjectionCount++;
                        }
                    }
                    else if (isActive)
                    {
                        // Ca chưa xác định loại tiêm vẫn thuộc tổng số ca còn hẹn.
                        activeCount++;
                    }
                }

                DataTable visibleData = Database.GetAppointments(
                    from.Date,
                    to.Date,
                    shift,
                    IncludeReturnedCheck.IsChecked == true,
                    injectionFilter);

                ConfigureAppointmentsGrid(visibleData);
                AppointmentsGrid.ItemsSource = visibleData.DefaultView;

                TotalCountText.Text = totalCount + " ca";
                TotalInjectionCountText.Text = totalInjectionCount.ToString();
                TotalNoInjectionCountText.Text = totalNoInjectionCount.ToString();
                ActiveCountText.Text = activeCount + " ca";
                ActiveInjectionCountText.Text = activeInjectionCount.ToString();
                ActiveNoInjectionCountText.Text = activeNoInjectionCount.ToString();
            }
            catch (Exception ex)
            {
                ShowAppMessage(
                    "Không thể tải danh sách lịch hẹn.\\n\\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string GetInjectionCategory(DataRow row)
        {
            string explicitStatus = row.Table.Columns.Contains("Tiêm thuốc cản quang")
                ? Convert.ToString(row["Tiêm thuốc cản quang"]).Trim()
                : "";

            if (string.Equals(explicitStatus, "Có tiêm", StringComparison.OrdinalIgnoreCase))
                return "Có tiêm";
            if (string.Equals(explicitStatus, "Không tiêm", StringComparison.OrdinalIgnoreCase))
                return "Không tiêm";
            if (string.Equals(explicitStatus, "Chưa xác định", StringComparison.OrdinalIgnoreCase))
                return "Chưa xác định";

            string service = row.Table.Columns.Contains("Dịch vụ")
                ? Convert.ToString(row["Dịch vụ"]).ToLowerInvariant()
                : "";

            if (service.Contains("không tiêm") || service.Contains("khong tiem") ||
                service.Contains("không sử dụng thuốc cản quang") ||
                service.Contains("không dùng thuốc cản quang") ||
                service.Contains("khong su dung thuoc can quang"))
                return "Không tiêm";

            if (service.Contains("tiêm") || service.Contains("tiem"))
                return "Có tiêm";

            return "Chưa xác định";
        }

        private static void AddGridCellTrigger(Style style, string value, System.Windows.Media.Color background, System.Windows.Media.Color border, System.Windows.Media.Color foreground)
        {
            var trigger = new DataTrigger { Binding = new System.Windows.Data.Binding("[Tiêm thuốc cản quang]"), Value = value };
            trigger.Setters.Add(new Setter(Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(background)));
            trigger.Setters.Add(new Setter(Control.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(border)));
            trigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            trigger.Setters.Add(new Setter(Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(foreground)));
            style.Triggers.Add(trigger);
        }

        private void ConfigureAppointmentsGrid(DataTable data)
        {
            AppointmentsGrid.AutoGenerateColumns = false;
            AppointmentsGrid.Columns.Clear();
            // Khôi phục cỡ chữ gọn của v0.5.8 để phù hợp màn hình độ phân giải thấp.
            AppointmentsGrid.FontSize = 13.5;
            AppointmentsGrid.RowHeight = 34;
            AppointmentsGrid.ColumnHeaderHeight = 36;
            AppointmentsGrid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            AppointmentsGrid.HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 233, 236));
            AppointmentsGrid.VerticalGridLinesBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 242, 244));
            AppointmentsGrid.RowBackground = System.Windows.Media.Brushes.White;
            AppointmentsGrid.AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 251, 252));

            string[] order = { "Id", "Mã BN", "Tên bệnh nhân", "Khoa phòng", "Mã chỉ định", "Dịch vụ", "Tiêm thuốc cản quang", "Ngày hẹn", "Buổi", "Trạng thái", "Ngày tạo lịch" };
            foreach (string name in order)
            {
                if (!data.Columns.Contains(name)) continue;
                double width = 78;
                if (name == "Id") width = 32;
                else if (name == "Mã BN") width = 68;
                else if (name == "Tên bệnh nhân") width = 70;
                else if (name == "Khoa phòng") width = 65;
                else if (name == "Mã chỉ định") width = 60;
                else if (name == "Dịch vụ") width = 75;
                else if (name == "Tiêm thuốc cản quang") width = 78;
                else if (name == "Ngày hẹn") width = 68;
                else if (name == "Buổi") width = 42;
                else if (name == "Trạng thái") width = 60;
                else if (name == "Ngày tạo lịch") width = 80;

                bool leftAligned = name == "Mã BN" || name == "Tên bệnh nhân" || name == "Khoa phòng" || name == "Dịch vụ";
                var alignment = leftAligned ? TextAlignment.Left : TextAlignment.Center;

                var textStyle = new Style(typeof(TextBlock));
                textStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 13.5));
                textStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                textStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
                textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                textStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(4, 0, 4, 0)));
                textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, alignment));

                var headerStyle = new Style(typeof(DataGridColumnHeader));
                headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,
                    leftAligned ? HorizontalAlignment.Left : HorizontalAlignment.Center));
                headerStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
                headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
                headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3, 5, 3, 5)));

                var cellStyle = new Style(typeof(DataGridCell));
                cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3,1,3,1)));
                cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
                if (name == "Buổi")
                {
                    var morning = new DataTrigger { Binding = new System.Windows.Data.Binding("[Buổi]"), Value = "Sáng" };
                    morning.Setters.Add(new Setter(Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,229,153))));
                    morning.Setters.Add(new Setter(Control.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210,155,35))));
                    morning.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                    morning.Setters.Add(new Setter(Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(125,83,0))));
                    cellStyle.Triggers.Add(morning);
                    var afternoon = new DataTrigger { Binding = new System.Windows.Data.Binding("[Buổi]"), Value = "Chiều" };
                    afternoon.Setters.Add(new Setter(Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(190,220,255))));
                    afternoon.Setters.Add(new Setter(Control.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75,135,205))));
                    afternoon.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                    afternoon.Setters.Add(new Setter(Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28,83,145))));
                    cellStyle.Triggers.Add(afternoon);
                }
                else if (name == "Tiêm thuốc cản quang")
                {
                    AddGridCellTrigger(cellStyle, "Có tiêm", System.Windows.Media.Color.FromRgb(210,242,218), System.Windows.Media.Color.FromRgb(75,170,105), System.Windows.Media.Color.FromRgb(24,112,55));
                    AddGridCellTrigger(cellStyle, "Không tiêm", System.Windows.Media.Color.FromRgb(232,237,242), System.Windows.Media.Color.FromRgb(145,158,170), System.Windows.Media.Color.FromRgb(75,87,96));
                    AddGridCellTrigger(cellStyle, "Chưa xác định", System.Windows.Media.Color.FromRgb(255,229,199), System.Windows.Media.Color.FromRgb(220,145,65), System.Windows.Media.Color.FromRgb(150,83,12));
                }
                var column = new DataGridTextColumn
                {
                    Header = name,
                    Binding = new System.Windows.Data.Binding { Path = new PropertyPath("[" + name + "]") },
                    Width = (name == "Tên bệnh nhân" || name == "Khoa phòng" || name == "Dịch vụ")
                        ? new DataGridLength(1, DataGridLengthUnitType.Star)
                        : new DataGridLength(width),
                    MinWidth = width,
                    ElementStyle = textStyle,
                    HeaderStyle = headerStyle,
                    CellStyle = cellStyle,
                    SortMemberPath = name
                };
                AppointmentsGrid.Columns.Add(column);
            }
        }

        private void AppointmentsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            try
            {
                var view = AppointmentsGrid.ItemsSource as DataView;
                if (view == null) return;
                string header = Convert.ToString(e.Column.Header);
                string sortColumn = header;
                if (header == "Ngày hẹn") sortColumn = "AppointmentDateSort";
                else if (header == "Tiêm thuốc cản quang") sortColumn = "InjectionSort";
                else if (header == "Ngày tạo lịch") sortColumn = "CreatedAtSort";
                ListSortDirection direction = e.Column.SortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending : ListSortDirection.Ascending;
                view.Sort = "[" + sortColumn.Replace("]", "]]" ) + "] " + (direction == ListSortDirection.Ascending ? "ASC" : "DESC");
                foreach (var column in AppointmentsGrid.Columns) column.SortDirection = null;
                e.Column.SortDirection = direction;
            }
            catch (Exception ex)
            {
                ShowAppMessage("Không thể sắp xếp cột này. Danh sách vẫn được giữ nguyên.\n\n" + ex.Message,
                    "Sắp xếp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ResetAppointmentCounters()
        {
            TotalCountText.Text = "0 ca";
            TotalInjectionCountText.Text = "0";
            TotalNoInjectionCountText.Text = "0";
            ActiveCountText.Text = "0 ca";
            ActiveInjectionCountText.Text = "0";
            ActiveNoInjectionCountText.Text = "0";
            AppointmentsGrid.ItemsSource = null;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAppointments();
        }

        private void AdjustAppointmentDate(int days)
        {
            AppointmentDatePicker.SelectedDate = (AppointmentDatePicker.SelectedDate ?? DateTime.Today).AddDays(days);
        }

        private void AppointmentDateMinus_Click(object sender, RoutedEventArgs e) { AdjustAppointmentDate(-1); }
        private void AppointmentDatePlus_Click(object sender, RoutedEventArgs e) { AdjustAppointmentDate(1); }

        private void AdjustFilterDate(DatePicker picker, int days, bool isFrom)
        {
            DateTime current = picker.SelectedDate ?? DateTime.Today;
            DateTime next = current.AddDays(days);
            if (isFrom && ToDatePicker.SelectedDate.HasValue && next.Date > ToDatePicker.SelectedDate.Value.Date)
                ToDatePicker.SelectedDate = next.Date;
            if (!isFrom && FromDatePicker.SelectedDate.HasValue && next.Date < FromDatePicker.SelectedDate.Value.Date)
                FromDatePicker.SelectedDate = next.Date;
            picker.SelectedDate = next.Date;
            LoadAppointments();
        }

        private void FromDateMinus_Click(object sender, RoutedEventArgs e) { AdjustFilterDate(FromDatePicker, -1, true); }
        private void FromDatePlus_Click(object sender, RoutedEventArgs e) { AdjustFilterDate(FromDatePicker, 1, true); }
        private void ToDateMinus_Click(object sender, RoutedEventArgs e) { AdjustFilterDate(ToDatePicker, -1, false); }
        private void ToDatePlus_Click(object sender, RoutedEventArgs e) { AdjustFilterDate(ToDatePicker, 1, false); }

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
                ShowAppMessage("Hãy chọn một lịch trong danh sách.");
                return;
            }

            if (Convert.ToString(view["Trạng thái"]) != "Đã hẹn")
            {
                ShowAppMessage("Lịch này đã được trả trước đó hoặc không còn hiệu lực.");
                return;
            }

            string patientName = Convert.ToString(view["Tên bệnh nhân"]);
            string patientCode = Convert.ToString(view["Mã BN"]);

            if (ShowAppMessage(
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
                ShowAppMessage("Đã trả lịch thành công.");
            }
            catch (Exception ex)
            {
                ShowAppMessage("Không thể trả lịch.\n\n" + ex.Message);
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private void RestoreMainWindowFocus()
        {
            try
            {
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;

                Show();
                Activate();
                Focus();

                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, 9); // SW_RESTORE
                    SetForegroundWindow(handle);
                }
            }
            catch
            {
                // Không để lỗi khôi phục Focus làm gián đoạn thao tác tạo lịch.
            }
        }

        private static MessageBoxResult ShowAppMessage(string message)
        { return ShowAppMessage(null, message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); }
        private static MessageBoxResult ShowAppMessage(string message, string caption)
        { return ShowAppMessage(null, message, caption, MessageBoxButton.OK, MessageBoxImage.Information); }
        private static MessageBoxResult ShowAppMessage(string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
        { return ShowAppMessage(null, message, caption, buttons, image); }
        private static MessageBoxResult ShowAppMessage(Window owner, string message)
        { return ShowAppMessage(owner, message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); }
        private static MessageBoxResult ShowAppMessage(Window owner, string message, string caption)
        { return ShowAppMessage(owner, message, caption, MessageBoxButton.OK, MessageBoxImage.Information); }
        private static MessageBoxResult ShowAppMessage(Window owner, string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            var dialog = new Window { Title=caption, Width=440, SizeToContent=SizeToContent.Height, MinHeight=170, MaxHeight=520,
                ResizeMode=ResizeMode.NoResize, WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,
                Owner=owner, Background=System.Windows.Media.Brushes.Transparent, WindowStyle=WindowStyle.None, AllowsTransparency=true, ShowInTaskbar=false };
            var outer = new Border { Background=System.Windows.Media.Brushes.White, BorderBrush=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(207,226,228)), BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(16), Padding=new Thickness(22) };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text=caption, FontSize=17, FontWeight=FontWeights.Bold, Margin=new Thickness(0,0,0,12), Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24,61,72)) });
            panel.Children.Add(new TextBlock { Text=message??"", TextWrapping=TextWrapping.Wrap, FontSize=13, Margin=new Thickness(0,0,0,20), Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45,72,79)) });
            var buttonsPanel = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right };
            MessageBoxResult result=MessageBoxResult.None;
            Action<string,MessageBoxResult> add=(label,value)=>{var b=new Button{Content=label,MinWidth=88,Height=40,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,8,0),IsDefault=value==MessageBoxResult.OK||value==MessageBoxResult.Yes};b.Click+=(s,e)=>{result=value;dialog.DialogResult=true;dialog.Close();};buttonsPanel.Children.Add(b);};
            if(buttons==MessageBoxButton.YesNo || buttons==MessageBoxButton.YesNoCancel){add("Có",MessageBoxResult.Yes);add("Không",MessageBoxResult.No);if(buttons==MessageBoxButton.YesNoCancel)add("Hủy",MessageBoxResult.Cancel);}
            else if(buttons==MessageBoxButton.OKCancel){add("OK",MessageBoxResult.OK);add("Hủy",MessageBoxResult.Cancel);}
            else add("Đóng",MessageBoxResult.OK);
            panel.Children.Add(buttonsPanel); outer.Child=panel; dialog.Content=outer; dialog.ShowDialog(); return result;
        }

    }

    // Chặn cửa sổ popup của Chromium và mở URL đích trong tab tích hợp.
    internal sealed class TabLifeSpanHandler : ILifeSpanHandler
    {
        private readonly Action<string> openInTab;

        public TabLifeSpanHandler(Action<string> openInTab)
        {
            this.openInTab = openInTab;
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
            string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition,
            bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo,
            IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;
            if (!string.IsNullOrWhiteSpace(targetUrl))
                openInTab(targetUrl);
            return true;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { return false; }
        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
    }

}
