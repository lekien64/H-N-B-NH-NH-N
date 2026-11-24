using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;
using PatientAppointmentManager.Data;

namespace PatientAppointmentManager.Services
{
    /// <summary>
    /// Daily 16:30 local smart backup. A new backup is created only when the
    /// SQLite data revision differs from the last successfully backed-up revision.
    /// </summary>
    public static class SmartBackupService
    {
        private const string TaskName = "SoHenBN_DailyBackup";
        private static string DataFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatientAppointmentManager"); }
        }
        private static string StatePath { get { return Path.Combine(DataFolder, "smart-backup-state.json"); } }
        private static string LogPath { get { return Path.Combine(DataFolder, "smart-backup.log"); } }

        private sealed class BackupState
        {
            public long LastBackedUpRevision { get; set; }
            public string LastBackupAt { get; set; }
            public string LastBackupFile { get; set; }
        }

        public static void RegisterDailyTask()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                string taskRun = "\"" + exe + "\" --scheduled-backup";
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Create /TN \"" + TaskName + "\" /SC DAILY /ST 16:30 /TR \"\\\"" + exe + "\\\" --scheduled-backup\" /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var process = Process.Start(psi))
                {
                    process.WaitForExit(15000);
                    AppendLog("Đăng ký lịch 16:30: " + (process.HasExited ? process.ExitCode.ToString() : "timeout"));
                }
            }
            catch (Exception ex) { AppendLog("Không đăng ký được lịch: " + ex.Message); }
        }

        public static bool RunIfChanged(out string message)
        {
            Directory.CreateDirectory(DataFolder);
            var state = LoadState();
            long currentRevision = Database.GetDataRevision();
            if (state.LastBackedUpRevision == currentRevision && !string.IsNullOrEmpty(state.LastBackupFile) && File.Exists(state.LastBackupFile))
            {
                message = "Không có dữ liệu mới. Bỏ qua sao lưu.";
                AppendLog(message + " Revision=" + currentRevision);
                return false;
            }

            string backupPath = Database.BackupDatabase();
            VerifyDatabase(backupPath);
            state.LastBackedUpRevision = currentRevision;
            state.LastBackupAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            state.LastBackupFile = backupPath;
            SaveState(state);
            message = "Sao lưu cục bộ thành công: " + backupPath;
            AppendLog(message + " Revision=" + currentRevision);
            return true;
        }

        private static void VerifyDatabase(string path)
        {
            using (var c = new System.Data.SQLite.SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA integrity_check";
                    string result = Convert.ToString(cmd.ExecuteScalar());
                    if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Bản sao lưu không vượt qua kiểm tra integrity_check.");
                }
            }
        }

        private static BackupState LoadState()
        {
            try
            {
                if (File.Exists(StatePath))
                    return new JavaScriptSerializer().Deserialize<BackupState>(File.ReadAllText(StatePath)) ?? new BackupState();
            }
            catch (Exception ex) { AppendLog("Đọc trạng thái sao lưu lỗi: " + ex.Message); }
            return new BackupState();
        }

        private static void SaveState(BackupState state)
        {
            string temp = StatePath + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(state));
            if (File.Exists(StatePath)) File.Delete(StatePath);
            File.Move(temp, StatePath);
        }

        private static void AppendLog(string text)
        {
            try { Directory.CreateDirectory(DataFolder); File.AppendAllText(LogPath, DateTime.Now.ToString("s") + " " + text + Environment.NewLine); }
            catch { }
        }
    }
}
