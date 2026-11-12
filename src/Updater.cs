using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace LidLock
{
    /// <summary>Busca nuevas versiones en los releases de GitHub y se actualiza verificando el SHA-256.</summary>
    internal static class Updater
    {
        public const int UpToDate = 0, Available = 1, Failed = 2, NoRepo = 3;

        public static string LatestTag;
        public static bool HasUpdate;

        private static string assetUrl;
        private static string checksumUrl;

        /// <summary>Las compilaciones locales no tienen repositorio ("OWNER/..."): sin actualizaciones.</summary>
        public static bool Enabled
        {
            get { return !BuildInfo.Repo.StartsWith("OWNER/", StringComparison.Ordinal); }
        }

        private static Version Parse(string tag)
        {
            string text = (tag ?? "").Trim().TrimStart('v', 'V');
            int dash = text.IndexOf('-');
            if (dash >= 0) text = text.Substring(0, dash);
            Version version;
            return Version.TryParse(text, out version) ? version : null;
        }

        /// <summary>Lee el valor de texto de una clave JSON a partir de pos; deja pos al final del valor.</summary>
        private static string JsonValue(string json, string key, ref int pos)
        {
            int i = json.IndexOf("\"" + key + "\"", pos, StringComparison.Ordinal);
            if (i < 0) return null;
            i = json.IndexOf(':', i + key.Length + 2);
            if (i < 0) return null;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;

            StringBuilder value = new StringBuilder();
            for (i++; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length) { value.Append(json[++i]); continue; }
                if (c == '"') break;
                value.Append(c);
            }
            pos = i;
            return value.ToString();
        }

        public static int Check()
        {
            if (!Enabled) return NoRepo;
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + BuildInfo.Repo + "/releases/latest");
                request.UserAgent = "LidLock/" + BuildInfo.Version;
                request.Accept = "application/vnd.github+json";
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;

                string json;
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    json = reader.ReadToEnd();

                int pos = 0;
                string tag = JsonValue(json, "tag_name", ref pos);
                Version latest = Parse(tag);
                Version current = Parse(BuildInfo.Version);
                if (latest == null || current == null) return Failed;

                assetUrl = null;
                checksumUrl = null;
                string url;
                while ((url = JsonValue(json, "browser_download_url", ref pos)) != null)
                {
                    if (url.EndsWith("/LidLock.exe", StringComparison.OrdinalIgnoreCase)) assetUrl = url;
                    else if (url.EndsWith("/LidLock.exe.sha256", StringComparison.OrdinalIgnoreCase)) checksumUrl = url;
                }

                LatestTag = tag;
                HasUpdate = latest > current && assetUrl != null && checksumUrl != null;
                return HasUpdate ? Available : UpToDate;
            }
            catch (Exception)
            {
                return Failed;
            }
        }

        /// <summary>Descarga la nueva version, comprueba su SHA-256 y programa el reemplazo. True si hay que cerrar la app.</summary>
        public static bool DownloadAndStage()
        {
            if (!HasUpdate || !Installer.IsInstalled) return false;
            try
            {
                string stagedPath = Installer.InstalledExe + ".new";
                byte[] data;
                string checksumText;
                using (WebClient client = new WebClient())
                {
                    client.Headers["User-Agent"] = "LidLock/" + BuildInfo.Version;
                    data = client.DownloadData(assetUrl);
                    checksumText = client.DownloadString(checksumUrl);
                }

                string[] words = checksumText.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string actual;
                using (SHA256 sha = SHA256.Create())
                    actual = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();

                if (words.Length == 0 || !string.Equals(words[0], actual, StringComparison.OrdinalIgnoreCase)) return false;
                if (data.Length < 1024 || data[0] != (byte)'M' || data[1] != (byte)'Z') return false;

                File.WriteAllBytes(stagedPath, data);

                // Tras cerrar esta instancia, un cmd oculto reemplaza el exe y lo vuelve a abrir.
                string exe = Installer.InstalledExe;
                ProcessStartInfo swap = new ProcessStartInfo("cmd.exe",
                    "/c ping -n 3 127.0.0.1 >nul & move /y \"" + stagedPath + "\" \"" + exe + "\" >nul & start \"\" \"" + exe + "\"");
                swap.CreateNoWindow = true;
                swap.UseShellExecute = false;
                Process.Start(swap);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
