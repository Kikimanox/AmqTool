using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace AmqTool
{
    /// <summary>
    /// The 2021 installer put AmqTool into Program Files (x86), where it can't write its own config without
    /// permission hacks. New installs go to a user-writable folder instead, so on the first start of a new install
    /// (no config.json yet) offer to copy the settings and job lists over from an old Program Files install.
    /// </summary>
    public static class OldInstallImport
    {
        static readonly string[] DataFiles = { "config.json", "private_config.json", "jobs.json", "volume_jobs.json" };

        public static void OfferIfFirstRun()
        {
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                var cfg = Path.Combine(appDir, "config.json");
                if (File.Exists(cfg) && new FileInfo(cfg).Length > 2) return; // not a first run

                var oldDir = new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }
                    .Select(Environment.GetFolderPath)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => Path.Combine(p, "AmqTool"))
                    .FirstOrDefault(d => !string.Equals(d, appDir, StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(d, "config.json")));
                if (oldDir == null) return;

                var res = MessageBox.Show($"Found an older AmqTool install with settings in:\n\n{oldDir}\n\n" +
                    "Copy its settings (folders, ffmpeg path, keybinds, ffmpeg commands) and job lists over to this install?\n\n" +
                    "Nothing in the old folder gets changed. Once you're happy with this version you can uninstall the old one.",
                    "Import settings from old install?", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                foreach (var f in DataFiles)
                {
                    var src = Path.Combine(oldDir, f);
                    if (File.Exists(src)) File.Copy(src, Path.Combine(appDir, f), true);
                }

                // tmp_path normally pointed at <old install>\tmp, give this install its own (it gets wiped on every start)
                JObject json;
                using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(cfg))) { DateParseHandling = DateParseHandling.None })
                    json = JObject.Load(reader);
                var tmp = (string)json["folders"]?["tmp_path"];
                if (string.IsNullOrEmpty(tmp) || tmp.StartsWith(oldDir, StringComparison.OrdinalIgnoreCase))
                {
                    var newTmp = Path.Combine(appDir, "tmp");
                    Directory.CreateDirectory(newTmp);
                    json["folders"]["tmp_path"] = newTmp;
                    File.WriteAllText(cfg, json.ToString(Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't (fully) import the old settings:\n\n{ex.Message}\n\nCheck Edit > Edit all config once AmqTool is open.", "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
