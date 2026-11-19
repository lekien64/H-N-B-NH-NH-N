
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
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
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

CREATE TABLE IF NOT EXISTS AuditLog(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AppointmentId INTEGER NOT NULL,
    Action TEXT NOT NULL,
    ActionAt TEXT NOT NULL,
    Note TEXT NOT NULL DEFAULT ''
);";
                    cmd.ExecuteNonQuery();
                }

                // Tự nâng cấp database cũ, không xóa dữ liệu.
                if (!ColumnExists(c, "Appointments", "Department"))
                {
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText =
                            "ALTER TABLE Appointments " +
                            "ADD COLUMN Department TEXT NOT NULL DEFAULT ''";

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
                cmd.CommandText =
                    "PRAGMA table_info(" + tableName + ")";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name =
                            Convert.ToString(reader["name"]);

                        if (string.Equals(
                            name,
                            columnName,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
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

                    cmd.Parameters.AddWithValue(
                        "@p", patientCode ?? "");

                    cmd.Parameters.AddWithValue(
                        "@e", examCode ?? "");

                    return Convert.ToInt32(
                        cmd.ExecuteScalar()) > 0;
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
    PatientCode,
    PatientName,
    Department,
    ExamCode,
    ServiceName,
    AppointmentDate,
    Shift,
    Status,
    CreatedAt
)
VALUES
(
    @p,
    @n,
    @department,
    @e,
    @s,
    @d,
    @h,
    'Đã hẹn',
    @now
)";

                    cmd.Parameters.AddWithValue(
                        "@p", a.PatientCode ?? "");

                    cmd.Parameters.AddWithValue(
                        "@n", a.PatientName ?? "");

                    cmd.Parameters.AddWithValue(
                        "@department", a.Department ?? "");

                    cmd.Parameters.AddWithValue(
                        "@e", a.ExamCode ?? "");

                    cmd.Parameters.AddWithValue(
                        "@s", a.ServiceName ?? "");

                    cmd.Parameters.AddWithValue(
                        "@d",
                        a.AppointmentDate.ToString("yyyy-MM-dd"));

                    cmd.Parameters.AddWithValue(
                        "@h", a.Shift ?? "");

                    cmd.Parameters.AddWithValue(
                        "@now", DateTime.Now.ToString("s"));

                    cmd.ExecuteNonQuery();

                    tx.Commit();
                }
            }
        }

        public static DataTable GetAppointments(
            DateTime from,
            DateTime to,
            string shift,
            bool includeReturned)
        {
            var table = new DataTable();

            using (var c = new SQLiteConnection(ConnectionString))
            {
                c.Open();

                using (var da = new SQLiteDataAdapter())
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT
    Id,
    PatientCode AS 'Mã BN',
    PatientName AS 'Tên bệnh nhân',
    Department AS 'Khoa phòng',
    ExamCode AS 'Mã chỉ định',
    ServiceName AS 'Dịch vụ',
    AppointmentDate AS 'Ngày hẹn',
    Shift AS 'Buổi',
    Status AS 'Trạng thái'
FROM Appointments
WHERE AppointmentDate >= @from
AND AppointmentDate <= @to
AND (@shift = 'Cả ngày' OR Shift = @shift)
AND (@returned = 1 OR Status = 'Đã hẹn')
ORDER BY
    AppointmentDate,
    CASE Shift WHEN 'Sáng' THEN 0 ELSE 1 END,
    Id DESC";

                    cmd.Parameters.AddWithValue(
                        "@from", from.ToString("yyyy-MM-dd"));

                    cmd.Parameters.AddWithValue(
                        "@to", to.ToString("yyyy-MM-dd"));

                    cmd.Parameters.AddWithValue(
                        "@shift", shift ?? "Cả ngày");

                    cmd.Parameters.AddWithValue(
                        "@returned", includeReturned ? 1 : 0);

                    da.SelectCommand = cmd;
                    da.Fill(table);
                }
            }

            return table;
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

                    cmd.Parameters.AddWithValue(
                        "@now", DateTime.Now.ToString("s"));

                    cmd.Parameters.AddWithValue("@id", id);

                    cmd.ExecuteNonQuery();

                    using (var log = c.CreateCommand())
                    {
                        log.Transaction = tx;

                        log.CommandText = @"
INSERT INTO AuditLog
(AppointmentId, Action, ActionAt)
VALUES
(@id, 'Trả lịch', @now)";

                        log.Parameters.AddWithValue("@id", id);

                        log.Parameters.AddWithValue(
                            "@now", DateTime.Now.ToString("s"));

                        log.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
            }
        }
    }
}
