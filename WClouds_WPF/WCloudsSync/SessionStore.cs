using System;
using System.IO;
using System.Text.Json;

namespace WCloudsSync
{
    /// <summary>
    /// Liest/schreibt die aktive Sitzung (UserId + ApiKey) aus einer gemeinsam
    /// genutzten JSON-Datei im AppData-Ordner.  Die WPF-App schreibt beim Login,
    /// WCloudsSync liest beim Start.
    /// </summary>
    public static class SessionStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WClouds", "session.json");

        public static void Save(int userId, string apiKey, string apiUrl)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var data = new SessionData { UserId = userId, ApiKey = apiKey, ApiUrl = apiUrl };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data));
        }

        public static SessionData? TryRead()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                return JsonSerializer.Deserialize<SessionData>(File.ReadAllText(FilePath));
            }
            catch
            {
                return null;
            }
        }

        public static void Clear()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }

    public class SessionData
    {
        public int UserId  { get; set; }
        public string ApiKey { get; set; } = "";
        public string ApiUrl { get; set; } = "http://127.0.0.1:8000";
    }
}
