using System;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Windows;
using CefSharp;

namespace PatientAppointmentManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Chuẩn hóa toàn bộ ngày tháng theo Việt Nam: dd/MM/yyyy.
            var viCulture = new CultureInfo("vi-VN");
            viCulture.DateTimeFormat.ShortDatePattern = "dd/MM/yy";
            Thread.CurrentThread.CurrentCulture = viCulture;
            Thread.CurrentThread.CurrentUICulture = viCulture;
            CultureInfo.DefaultThreadCurrentCulture = viCulture;
            CultureInfo.DefaultThreadCurrentUICulture = viCulture;
            var cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PatientAppointmentManager",
                "CefCache");

            Directory.CreateDirectory(cacheFolder);

            var settings = new CefSharp.Wpf.CefSettings
            {
                CachePath = cacheFolder,
                LogSeverity = LogSeverity.Warning,
                BrowserSubprocessPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CefSharp.BrowserSubprocess.exe")
            };

            // Chromium 109 is selected as the last Chromium major version supporting Windows 7.
            // Use this embedded browser only with trusted hospital/internal websites.
            if (!Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null))
            {
                MessageBox.Show(
                    "Không thể khởi tạo Chromium. Hãy kiểm tra bộ file CefSharp đi kèm ứng dụng và Microsoft Visual C++ Redistributable x64.",
                    "Không khởi tạo được trình duyệt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (Cef.IsInitialized)
                Cef.Shutdown();

            base.OnExit(e);
        }
    }
}
