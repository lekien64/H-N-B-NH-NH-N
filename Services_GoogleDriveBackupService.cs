using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Upload;

namespace PatientAppointmentManager
{
    internal sealed class GoogleDriveBackupService
    {
        private const string FolderName = "SoHenBN_Backups";
        private readonly DriveService drive;
        private GoogleDriveBackupService(DriveService service) { drive = service; }

        public static async Task<GoogleDriveBackupService> ConnectAsync(string clientJson)
        {
            if (string.IsNullOrWhiteSpace(clientJson) || !File.Exists(clientJson))
                throw new FileNotFoundException("Không tìm thấy file OAuth client JSON.", clientJson);
            using (var stream = new FileStream(clientJson, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var secrets = GoogleClientSecrets.FromStream(stream);
                var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets.Secrets, new[] { DriveService.Scope.DriveFile }, "sohenbn-shared-drive",
                    CancellationToken.None,
                    new FileDataStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatientAppointmentManager", "GoogleAuth"), true));
                var service = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "SoHenBN Backup"
                });
                return new GoogleDriveBackupService(service);
            }
        }

        public Task<string> GetAccountAsync()
        {
            // drive.file deliberately avoids requesting broad access to the user's Drive.
            return Task.FromResult("Tài khoản Google đã cấp quyền cho SoHenBN");
        }

        private async Task<string> GetOrCreateFolderAsync()
        {
            var list = drive.Files.List();
            list.Q = "mimeType='application/vnd.google-apps.folder' and name='" + FolderName + "' and trashed=false";
            list.Fields = "files(id,name)";
            var found = await list.ExecuteAsync();
            var folder = found.Files == null ? null : found.Files.FirstOrDefault();
            if (folder != null) return folder.Id;
            var metadata = new Google.Apis.Drive.v3.Data.File { Name = FolderName, MimeType = "application/vnd.google-apps.folder" };
            var created = await drive.Files.Create(metadata).ExecuteAsync();
            return created.Id;
        }

        public async Task UploadSnapshotAsync(string localPath, string category, string hash)
        {
            string folderId = await GetOrCreateFolderAsync();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string name = "SoHenBN_" + category + "_" + stamp + "_" + hash.Substring(0, 10) + ".db";
            var metadata = new Google.Apis.Drive.v3.Data.File { Name = name, Parents = new List<string> { folderId }, Description = "SoHenBN automatic SQLite backup; category=" + category + "; sha256=" + hash };
            using (var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var request = drive.Files.Create(metadata, stream, "application/x-sqlite3");
                request.Fields = "id,name,size";
                var result = await request.UploadAsync();
                if (result.Status != UploadStatus.Completed)
                    throw result.Exception ?? new Exception("Google Drive không hoàn tất tải bản sao lưu.");
            }
            await ApplyRetentionAsync(folderId, category, category == "DAY" ? 30 : category == "WEEK" ? 12 : 12);
        }

        private async Task ApplyRetentionAsync(string folderId, string category, int keep)
        {
            var request = drive.Files.List();
            request.Q = "'" + folderId + "' in parents and trashed=false and name contains 'SoHenBN_" + category + "_'";
            request.OrderBy = "createdTime desc";
            request.PageSize = 1000;
            request.Fields = "files(id,name,createdTime)";
            var result = await request.ExecuteAsync();
            var files = result.Files ?? new List<Google.Apis.Drive.v3.Data.File>();
            foreach (var old in files.Skip(keep))
                await drive.Files.Delete(old.Id).ExecuteAsync();
        }

        public static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
