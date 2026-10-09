using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AmqTool
{
    /// <summary>
    /// Help > Check for updates. Looks at the latest GitHub release (tag like v0.13.0, with the portable .zip
    /// made by build\build-release.ps1 attached) and can update in place: the running exe can't overwrite itself,
    /// so after downloading we start a hidden PowerShell script that waits for AmqTool to exit, copies the new
    /// files over the install folder and starts it again. config.json, jobs.json etc. aren't in the zip, so they stay.
    /// </summary>
    public static class Updater
    {
        // Must be a public repo, the API call is anonymous
        public const string GitHubRepo = "Kikimanox/AmqTool";
        public static string ReleasesPageUrl => $"https://github.com/{GitHubRepo}/releases";
        // Set AMQTOOL_UPDATE_URL to a local "releases/latest"-style json to test updating without publishing anything
        static string LatestReleaseApiUrl => Environment.GetEnvironmentVariable("AMQTOOL_UPDATE_URL") ?? $"https://api.github.com/repos/{GitHubRepo}/releases/latest";

        static readonly string[] UserDataFiles = { "config.json", "private_config.json", "jobs.json", "volume_jobs.json" };

        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
        }

        public class ReleaseInfo
        {
            public Version Version { get; set; }
            public string Notes { get; set; }
            public string PageUrl { get; set; }
            public string ZipUrl { get; set; }
            public bool IsNewer => Version != null && Version > CurrentVersion;
        }

        public static async Task<ReleaseInfo> GetLatestReleaseAsync()
        {
            using (var c = MakeClient(TimeSpan.FromSeconds(20)))
            {
                c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                using (var res = await c.GetAsync(LatestReleaseApiUrl))
                {
                    if (res.StatusCode == HttpStatusCode.NotFound)
                        throw new Exception($"No releases found on github.com/{GitHubRepo} (nothing published yet, or the repo is private).");
                    res.EnsureSuccessStatusCode();

                    var j = JObject.Parse(await res.Content.ReadAsStringAsync());
                    var zip = (j["assets"] as JArray)?.FirstOrDefault(a => ((string)a["name"] ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                    return new ReleaseInfo
                    {
                        Version = ParseVersion((string)j["tag_name"]),
                        Notes = ((string)j["body"] ?? "").Trim(),
                        PageUrl = (string)j["html_url"] ?? ReleasesPageUrl,
                        ZipUrl = (string)zip?["browser_download_url"],
                    };
                }
            }
        }

        /// <returns>"v0.13" -> 0.13.0, "v0.13.1" -> 0.13.1, null if the tag has no version number</returns>
        public static Version ParseVersion(string tag)
        {
            var m = Regex.Match(tag ?? "", @"\d+(\.\d+){1,2}");
            if (!m.Success || !Version.TryParse(m.Value, out var v)) return null;
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }

        /// <summary>False for the old Program Files (x86) installs (unless someone did the permissions workaround)</summary>
        public static bool CanWriteToAppFolder()
        {
            try
            {
                var test = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"__write_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(test, "");
                File.Delete(test);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Downloads and extracts the release zip, then starts the script that swaps the files once we've exited.
        /// The caller has to shut the app down right after this returns.
        /// </summary>
        public static async Task DownloadAndStartUpdateAsync(ReleaseInfo release, IProgress<int> progress)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            var work = Path.Combine(Path.GetTempPath(), "AmqTool_update");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);

            var zipPath = Path.Combine(work, "update.zip");
            using (var c = MakeClient(TimeSpan.FromMinutes(10)))
            using (var res = await c.GetAsync(release.ZipUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                res.EnsureSuccessStatusCode();
                var total = res.Content.Headers.ContentLength ?? -1;
                using (var src = await res.Content.ReadAsStreamAsync())
                using (var dst = File.Create(zipPath))
                {
                    var buf = new byte[81920];
                    long done = 0;
                    int n;
                    while ((n = await src.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await dst.WriteAsync(buf, 0, n);
                        done += n;
                        if (total > 0) progress?.Report((int)(done * 100 / total));
                    }
                }
            }

            var extracted = Path.Combine(work, "files");
            ZipFile.ExtractToDirectory(zipPath, extracted);
            // The zip has an AmqTool\ folder inside, but don't rely on that
            var newDir = Directory.GetFiles(extracted, "AmqTool.exe", SearchOption.AllDirectories).Select(Path.GetDirectoryName).FirstOrDefault();
            if (newDir == null) throw new Exception("The downloaded zip doesn't contain AmqTool.exe");

            string Q(string s) => "'" + s.Replace("'", "''") + "'"; // PowerShell single quoted string
            var log = Path.Combine(Path.GetTempPath(), "AmqTool_update.log");
            var script = string.Join("\n",
                $"$src = {Q(newDir)}; $dst = {Q(appDir)}; $work = {Q(work)}; $log = {Q(log)}",
                $"Wait-Process -Id {Process.GetCurrentProcess().Id} -Timeout 120 -ErrorAction SilentlyContinue",
                // robocopy retries while files are still locked; user data never ships in the zip but exclude it anyway
                $"robocopy $src $dst /E /R:30 /W:1 /NP /XF {string.Join(" ", UserDataFiles)} \"/LOG:$log\" | Out-Null",
                "Start-Process -FilePath (Join-Path $dst 'AmqTool.exe') -WorkingDirectory $dst",
                "Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue");

            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            Process.Start(new ProcessStartInfo(powershell, $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encoded}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            });
        }

        static HttpClient MakeClient(TimeSpan timeout)
        {
            var c = new HttpClient { Timeout = timeout };
            c.DefaultRequestHeaders.UserAgent.ParseAdd($"AmqTool/{CurrentVersion}"); // GitHub rejects requests without a user agent
            return c;
        }
    }
}
