
using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using PatientAppointmentManager.Models;

namespace PatientAppointmentManager.Data
{
    public static class Database
    {
        private static readonly string Folder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PatientAppointmentManager");

        private static readonly string DbPath =
            Path.Combine(Folder, "appointments.db");

        private static string ConnectionString =>
            "Data Source=" + DbPath + ";Version=3;";

        public static void Initialize()
        {
            Directory.CreateDirectory(Folder);

            if (!File.Exists(DbPath))
                SQLiteConnection.CreateFile(DbPath);

            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();

                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Appointments(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PatientCode TEXT NOT NULL,
    PatientName TEXT NOT NULL,
    Department TEXT NOT NULL DEFAULT '',
    ExamCode TEXT NOT NULL DEFAULT '',
    ServiceName TEXT NOT NULL DEFAULT '',
    InjectionStatus TEXT NOT NULL DEFAULT '',
    AppointmentDate TEXT NOT NULL,
    Shift TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Đã hẹn',
    CreatedAt TEXT NOT NULL,
    ReturnedAt TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_Appointments_Patient
ON Appointments(PatientCode);

CREATE INDEX IF NOT EXISTS IX_Appointments_Date
ON Appointments(AppointmentDate);

CREATE TABLE IF NOT EXISTS SavedLogins(
    Host TEXT PRIMARY KEY,
    ProtectedData TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS BrowserHistory(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Url TEXT NOT NULL UNIQUE,
    LastVisited TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS AuditLog(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AppointmentId INTEGER NOT NULL,
    Action TEXT NOT NULL,
    ActionAt TEXT NOT NULL,
    Note TEXT NOT NULL DEFAULT ''
);";
                    cmd.ExecuteNonQuery();
                }

                if (!ColumnExists(c, "Appointments", "Department"))
                {
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText =
                            "ALTER TABLE Appointments ADD COLUMN Department TEXT NOT NULL DEFAULT ''";
                        cmd.ExecuteNonQuery();
                    }
                }

                if (!ColumnExists(c, "Appointments", "InjectionStatus"))
                {
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText =
                            "ALTER TABLE Appointments ADD COLUMN InjectionStatus TEXT NOT NULL DEFAULT ''";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private static bool ColumnExists(
            SQLiteConnection connection,
            string tableName,
            string columnName)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(" + tableName + ")";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(
                            Convert.ToString(reader["name"]),
                            columnName,
                            StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }



        public static void SaveProtectedLogin(string host, string protectedData)
        {
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = @"INSERT OR REPLACE INTO SavedLogins(Host, ProtectedData, UpdatedAt)
VALUES(@host, @data, @updated)";
                    cmd.Parameters.AddWithValue("@host", host);
                    cmd.Parameters.AddWithValue("@data", protectedData);
                    cmd.Parameters.AddWithValue("@updated", DateTime.Now.ToString("s"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static string GetProtectedLogin(string host)
        {
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "SELECT ProtectedData FROM SavedLogins WHERE Host=@host";
                    cmd.Parameters.AddWithValue("@host", host);
                    object value = cmd.ExecuteScalar();
                    return value == null || value == DBNull.Value ? null : Convert.ToString(value);
                }
            }
        }

        public static void DeleteProtectedLogin(string host)
        {
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM SavedLogins WHERE Host=@host";
                    cmd.Parameters.AddWithValue("@host", host);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void SaveBrowserHistory(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return;
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = @"INSERT OR REPLACE INTO BrowserHistory(Url, LastVisited)
VALUES(@url, @visited)";
                    cmd.Parameters.AddWithValue("@url", url.Trim());
                    cmd.Parameters.AddWithValue("@visited", DateTime.Now.ToString("s"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static System.Collections.Generic.List<string> GetBrowserHistory(int limit)
        {
            var result = new System.Collections.Generic.List<string>();
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "SELECT Url FROM BrowserHistory ORDER BY LastVisited DESC LIMIT @limit";
                    cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read()) result.Add(Convert.ToString(reader[0]));
                }
            }
            return result;
        }

        public static bool HasActiveAppointment(
            string patientCode,
            string examCode)
        {
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();

                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT COUNT(*)
FROM Appointments
WHERE PatientCode = @p
AND Status = 'Đã hẹn'
AND (@e = '' OR ExamCode = @e)";

                    cmd.Parameters.AddWithValue("@p", patientCode ?? "");
                    cmd.Parameters.AddWithValue("@e", examCode ?? "");

                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public static Appointment FindActiveAppointment(string patientCode, string receiptCode)
        {
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = @"SELECT Id, PatientCode, PatientName, Department, ExamCode, ServiceName, InjectionStatus, AppointmentDate, Shift, Status, CreatedAt
FROM Appointments WHERE PatientCode=@p AND ExamCode=@e AND Status='Đã hẹn' ORDER BY Id DESC LIMIT 1";
                    cmd.Parameters.AddWithValue("@p", patientCode ?? "");
                    cmd.Parameters.AddWithValue("@e", receiptCode ?? "");
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new Appointment
                        {
                            Id = Convert.ToInt64(reader["Id"]),
                            PatientCode = Convert.ToString(reader["PatientCode"]),
                            PatientName = Convert.ToString(reader["PatientName"]),
                            Department = Convert.ToString(reader["Department"]),
                            ExamCode = Convert.ToString(reader["ExamCode"]),
                            ServiceName = Convert.ToString(reader["ServiceName"]),
                            InjectionStatus = Convert.ToString(reader["InjectionStatus"]),
                            AppointmentDate = DateTime.Parse(Convert.ToString(reader["AppointmentDate"])),
                            Shift = Convert.ToString(reader["Shift"]),
                            Status = Convert.ToString(reader["Status"]),
                            CreatedAt = DateTime.Parse(Convert.ToString(reader["CreatedAt"]))
                        };
                    }
                }
            }
        }

        public static Appointment FindLatestAppointment(string patientCode, string receiptCode)
        {
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = @"SELECT Id, PatientCode, PatientName, Department, ExamCode, ServiceName, InjectionStatus, AppointmentDate, Shift, Status, CreatedAt
FROM Appointments WHERE PatientCode=@p AND ExamCode=@e ORDER BY Id DESC LIMIT 1";
                    cmd.Parameters.AddWithValue("@p", patientCode ?? "");
                    cmd.Parameters.AddWithValue("@e", receiptCode ?? "");
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new Appointment
                        {
                            Id = Convert.ToInt64(reader["Id"]),
                            PatientCode = Convert.ToString(reader["PatientCode"]),
                            PatientName = Convert.ToString(reader["PatientName"]),
                            Department = Convert.ToString(reader["Department"]),
                            ExamCode = Convert.ToString(reader["ExamCode"]),
                            ServiceName = Convert.ToString(reader["ServiceName"]),
                            InjectionStatus = Convert.ToString(reader["InjectionStatus"]),
                            AppointmentDate = DateTime.Parse(Convert.ToString(reader["AppointmentDate"])),
                            Shift = Convert.ToString(reader["Shift"]),
                            Status = Convert.ToString(reader["Status"]),
                            CreatedAt = DateTime.Parse(Convert.ToString(reader["CreatedAt"]))
                        };
                    }
                }
            }
        }

        public static void Add(Appointment a)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();

                using (var tx = c.BeginTransaction())
                using (var cmd = c.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText = @"
INSERT INTO Appointments
(
    PatientCode, PatientName, Department,
    ExamCode, ServiceName, InjectionStatus, AppointmentDate,
    Shift, Status, CreatedAt
)
VALUES
(
    @p, @n, @department,
    @e, @s, @injection, @d,
    @h, 'Đã hẹn', @now
)";

                    cmd.Parameters.AddWithValue("@p", a.PatientCode ?? "");
                    cmd.Parameters.AddWithValue("@n", a.PatientName ?? "");
                    cmd.Parameters.AddWithValue("@department", a.Department ?? "");
                    cmd.Parameters.AddWithValue("@e", a.ExamCode ?? "");
                    cmd.Parameters.AddWithValue("@s", a.ServiceName ?? "");
                    cmd.Parameters.AddWithValue("@injection", a.InjectionStatus ?? "");
                    cmd.Parameters.AddWithValue("@d", a.AppointmentDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@h", a.Shift ?? "");
                    cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));

                    cmd.ExecuteNonQuery();
                    tx.Commit();
                }
            }
        }

        // Phân loại tiêm dựa trên tên dịch vụ.
        private static bool IsInjectionService(string serviceName)
        {
            string name = serviceName ?? "";

            bool explicitlyNoInjection =
                name.IndexOf("không tiêm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("khong tiem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("không sử dụng thuốc cản quang", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("không dùng thuốc cản quang", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("khong su dung thuoc can quang", StringComparison.OrdinalIgnoreCase) >= 0;

            if (explicitlyNoInjection)
                return false;

            return
                name.IndexOf("tiêm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("tiem", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsExplicitlyNoInjection(string serviceName)
        {
            string name = serviceName ?? "";

            return
                name.IndexOf("không tiêm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("khong tiem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("không sử dụng thuốc cản quang", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("không dùng thuốc cản quang", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("khong su dung thuoc can quang", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static DataTable GetAppointments(
            DateTime from,
            DateTime to,
            string shift,
            bool includeReturned,
            string injectionFilter)
        {
            var table = new DataTable();

            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();

                using (var da = new SQLiteDataAdapter())
                using (var cmd = c.CreateCommand())
                {
                    string sql = @"
SELECT
    Id,
    PatientCode AS 'Mã BN',
    PatientName AS 'Tên bệnh nhân',
    Department AS 'Khoa phòng',
    ExamCode AS 'Mã chỉ định',
    ServiceName AS 'Dịch vụ',
    InjectionStatus AS 'Tiêm thuốc cản quang',
    strftime('%d/%m/%Y', AppointmentDate) AS 'Ngày hẹn',
    strftime('%d/%m/%Y %H:%M', datetime(CreatedAt)) AS 'Ngày tạo lịch',
    TRIM(Shift) AS 'Buổi',
    CASE WHEN Status = 'Đã trả lịch' THEN 'Đã trả hẹn' ELSE Status END AS 'Trạng thái'
FROM Appointments
WHERE date(AppointmentDate) >= date(@from)
AND date(AppointmentDate) <= date(@to)";

                    if (!string.IsNullOrWhiteSpace(shift) && shift != "Cả ngày")
                    {
                        sql += " AND TRIM(Shift) = @shift";
                        cmd.Parameters.AddWithValue("@shift", shift.Trim());
                    }

                    if (!includeReturned)
                        sql += " AND Status = 'Đã hẹn'";

                    sql += @"
ORDER BY
    AppointmentDate,
    CASE TRIM(Shift)
        WHEN 'Sáng' THEN 0
        WHEN 'Chiều' THEN 1
        ELSE 2
    END,
    Id DESC";

                    cmd.CommandText = sql;

                    cmd.Parameters.AddWithValue(
                        "@from", from.Date.ToString("yyyy-MM-dd"));

                    // So sánh theo ngày lịch, không phụ thuộc phần giờ nếu dữ liệu cũ có lưu kèm giờ.
                    // Ngày kết thúc là bao gồm: 20/11 đến 21/11 chỉ lấy ngày 20 và 21.
                    cmd.Parameters.AddWithValue(
                        "@to", to.Date.ToString("yyyy-MM-dd"));

                    da.SelectCommand = cmd;
                    da.Fill(table);
                }
            }

            // Lọc tiêm sau khi lấy dữ liệu từ SQLite.
            // Cách này hỗ trợ chữ tiếng Việt có dấu và không dấu.
            if (string.IsNullOrWhiteSpace(injectionFilter) ||
                injectionFilter == "Tất cả")
                return table;

            DataTable filtered = table.Clone();

            foreach (DataRow row in table.Rows)
            {
                string service = Convert.ToString(row["Dịch vụ"]);
                string explicitStatus = table.Columns.Contains("Tiêm thuốc cản quang")
                    ? Convert.ToString(row["Tiêm thuốc cản quang"]) : "";

                bool isInjection = explicitStatus == "Có tiêm" ||
                    (string.IsNullOrWhiteSpace(explicitStatus) && IsInjectionService(service));
                bool isNoInjection = explicitStatus == "Không tiêm" ||
                    (string.IsNullOrWhiteSpace(explicitStatus) && IsExplicitlyNoInjection(service));
                bool isUnknown = explicitStatus == "Chưa xác định" ||
                    (!isInjection && !isNoInjection);

                if (injectionFilter == "Có tiêm" && isInjection)
                    filtered.ImportRow(row);
                else if (injectionFilter == "Không tiêm" && isNoInjection)
                    filtered.ImportRow(row);
                else if (injectionFilter == "Chưa xác định" && isUnknown)
                    filtered.ImportRow(row);
            }

            return filtered;
        }

        public static void UpdateAppointmentSchedule(long id, DateTime appointmentDate, string shift)
        {
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();
                using (var tx = c.BeginTransaction())
                using (var cmd = c.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"UPDATE Appointments
SET AppointmentDate=@date, Shift=@shift
WHERE Id=@id AND Status='Đã hẹn'";
                    cmd.Parameters.AddWithValue("@date", appointmentDate.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@shift", shift ?? "");
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() == 0)
                        throw new InvalidOperationException("Lịch không còn hiệu lực hoặc đã được trả hẹn.");
                    using (var log = c.CreateCommand())
                    {
                        log.Transaction = tx;
                        log.CommandText = @"INSERT INTO AuditLog(AppointmentId, Action, ActionAt, Note)
VALUES(@id, 'Đổi lịch', @now, @note)";
                        log.Parameters.AddWithValue("@id", id);
                        log.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
                        log.Parameters.AddWithValue("@note", appointmentDate.ToString("dd/MM/yyyy") + " - " + (shift ?? ""));
                        log.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
        }

        public static void Return(long id)
        {
            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();

                using (var tx = c.BeginTransaction())
                using (var cmd = c.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText = @"
UPDATE Appointments
SET Status = 'Đã trả lịch',
    ReturnedAt = @now
WHERE Id = @id
AND Status = 'Đã hẹn'";

                    cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
                    cmd.Parameters.AddWithValue("@id", id);

                    int affected = cmd.ExecuteNonQuery();

                    if (affected == 0)
                    {
                        tx.Rollback();
                        throw new InvalidOperationException(
                            "Lịch không tồn tại hoặc đã được trả trước đó.");
                    }

                    using (var log = c.CreateCommand())
                    {
                        log.Transaction = tx;

                        log.CommandText = @"
INSERT INTO AuditLog
(AppointmentId, Action, ActionAt)
VALUES
(@id, 'Trả lịch', @now)";

                        log.Parameters.AddWithValue("@id", id);
                        log.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
                        log.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
            }
        }
    }
}
