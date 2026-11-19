using System;
using System.IO;
using System.Windows;
using CefSharp;

namespace PatientAppointmentManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PatientAppointmentManager",
                "CefCache");

            Directory.CreateDirectory(cacheFolder);

            var settings = new CefSettings
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
