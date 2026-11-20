
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
