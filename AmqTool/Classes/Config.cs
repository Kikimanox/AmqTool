using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Globalization;
using Newtonsoft.Json.Converters;
using System.IO;

namespace AmqTool
{
    public class PrivateConfig
    {
        public string configPath { get; set; }
        public PrivateConfig(string path)
        {
            var txt = "";
            if (!File.Exists(path))
            {
                txt = File.ReadAllText("private_config.template");
                File.WriteAllText(path, txt);
            }
            else
            {
                txt = File.ReadAllText(path);
            }

            configPath = path;

            if (txt == "[]" || txt == "{}" || txt == "")
            {
                txt = File.ReadAllText("private_config.template");
                File.WriteAllText(path, txt);
            }

            conf = JsonConvert.DeserializeObject<ConfigDefPrivate>(txt);
        }
        public ConfigDefPrivate conf { get; set; }

        public void UpdateConfig()
        {
            string json = JsonConvert.SerializeObject(conf);
            File.WriteAllText(configPath, json);
        }
    }
    public partial class ConfigDefPrivate
    {
        public string catbox_userhash { get; set; }

    }

    public class Config
    {
        public string configPath { get; set; }
        public ConfigDef conf { get; set; }

        public Config(string configPath)
        {
            if (!File.Exists(configPath))
                File.Create(configPath).Close();

            this.configPath = configPath;

            using (StreamReader r = new StreamReader(configPath))
            {
                string json = r.ReadToEnd();
                if (json == "" || json == "{}" || json == "[]")
                {
                    json = File.ReadAllText("config.template");
                }
                conf = ConfigDef.FromJson(json);
                MigrateOldDefaults();
                if (conf.WindowProps.LastStarted == "-1")
                {
                    var dir = $"{AppDomain.CurrentDomain.BaseDirectory}tmp";
                    Directory.CreateDirectory(dir);
                    conf.Folders.TmpPath = dir;
                }
            }
        }

        /// <summary>
        /// Fixes for ffmpeg commands that were shipped broken. Only an untouched old default gets replaced,
        /// commands someone edited in Edit > ffmpeg settings are left alone. (Saved with the next config save.)
        /// </summary>
        private void MigrateOldDefaults()
        {
            // "Audio from input" (Extract audio, process mode mp3): "-map a" takes every audio track, which a .flac can't
            // hold, so extracting from dual audio (JP + EN) files failed. Take the first track like the other commands do.
            const string oldAudioFromInput = "-hide_banner -i \"[:in]\" -b:a 320k -ac 2 -map a \"[:out]\" -y";
            var usage = conf.Extensions != null && conf.Extensions.Length > 3 ? conf.Extensions[3].Usage : null;
            if (usage != null && usage.Length > 0 && usage[0].Call == oldAudioFromInput)
                usage[0].Call = oldAudioFromInput.Replace("-map a ", "-map 0:a:0 ");
        }

        public void SaveUponClosing()
        {
            conf.WindowProps.LastClosed = DateTime.Now.ToString("dd-MM-yyyyTHH:mm:ssK");
            string json = Serialize.ToJson(conf);
            File.WriteAllText(configPath, json);
        }

        public void UpdateConfig()
        {
            string json = Serialize.ToJson(conf);
            File.WriteAllText(configPath, json);
        }
    }

    #region ConfigDef
    public partial class ConfigDef
    {
        [JsonProperty("windowProps")]
        public WindowProps WindowProps { get; set; }

        [JsonProperty("folders")]
        public Folders Folders { get; set; }

        [JsonProperty("keybindings")]
        public Keybindings Keybindings { get; set; }

        [JsonProperty("execs")]
        public Exec[] Execs { get; set; }

        [JsonProperty("extensions")]
        public Extension[] Extensions { get; set; }
    }

    public partial class Exec
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }
    }

    public partial class Extension
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("loc")]
        public string Loc { get; set; }

        [JsonProperty("exec")]
        public string Exec { get; set; }

        [JsonProperty("usage")]
        public Usage[] Usage { get; set; }
    }

    public partial class Usage
    {
        [JsonProperty("call")]
        public string Call { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public partial class Keybindings
    {
        [JsonProperty("Global Keybinds")]
        public HotkeyProps[] GlobalKeybinds { get; set; }
        [JsonProperty("Home Mode")]
        public HotkeyProps[] HomeMode { get; set; }

        [JsonProperty("Organizing Mode")]
        public HotkeyProps[] OrganizingMode { get; set; }

        [JsonProperty("Organizing Mode Dynamic")]
        public HotkeyProps[] OrganizingModeDynamic { get; set; }

        [JsonProperty("Edit Mode")]
        public HotkeyProps[] EditMode { get; set; }

        [JsonProperty("Advanced Mode")]
        public HotkeyProps[] AdvancedMode { get; set; }
    }

    public partial class HotkeyProps
    {
        public HotkeyProps(string actionName, string key, string mods)
        {
            ActionName = actionName;
            Key = key;
            Mods = mods;
            if (mods[0] == '1') ModsReadable = ModsReadable == "None" ? "Shift " : ModsReadable + "Shift ";
            if (mods[1] == '1') ModsReadable = ModsReadable == "None" ? "Ctrl " : ModsReadable + "Ctrl ";
            if (mods[2] == '1') ModsReadable = ModsReadable == "None" ? "Alt " : ModsReadable + "Alt ";
        }

        [JsonProperty("actionName")]
        public string ActionName { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("mods")]
        public string Mods { get; set; }

        [JsonProperty("ModsReadable")]
        public string ModsReadable { get; set; } = "None";
    }

    public partial class Folders
    {
        [JsonProperty("last_folder_picker_path")]
        public string LastFolderPickerPath { get; set; }

        [JsonProperty("from_vegas_flac_path")]
        public string FromVegasFlacPath { get; set; }

        [JsonProperty("into_vegas_mp4_path")]
        public string IntoVegasMp4Path { get; set; }

        [JsonProperty("default_out_path")]
        public string DefaultOutPath { get; set; }

        [JsonProperty("bgs_path")]
        public string BgsPath { get; set; }

        [JsonProperty("bgs_include_subfolders")]
        public bool BgsIncludeSubfolders { get; set; }

        [JsonProperty("tmp_path")]
        public string TmpPath { get; set; }

        [JsonProperty("volume_checker_path")]
        public string VolumeCheckerPath { get; set; }
    }

    public partial class WindowProps
    {
        [JsonProperty("x")]
        public long X { get; set; }

        [JsonProperty("y")]
        public long Y { get; set; }

        [JsonProperty("top")]
        public long Top { get; set; }

        [JsonProperty("left")]
        public long Left { get; set; }

        [JsonProperty("fullscreen")]
        public bool Fullscreen { get; set; }

        [JsonProperty("last_started")]
        public string LastStarted { get; set; } = DateTime.Now.ToString("dd-MM-yyyyTHH:mm:ssK");

        [JsonProperty("last_closed")]
        public string LastClosed { get; set; }

        [JsonProperty("_Dont_clear_textboxes")]
        public bool DontClearTextBoxes { get; set; }

        [JsonProperty("Use_2_Pass_Encoding")]
        public bool Use2PassEnc { get; set; }

        // Older config.json files don't have the ones below, so the initializers are the defaults
        [JsonProperty("Show_out_subfolder_textbox")]
        public bool ShowOutSubfolderTextbox { get; set; } = true;

        [JsonProperty("Check_for_updates_on_startup")]
        public bool CheckForUpdatesOnStartup { get; set; } = true;
    }

    public partial class ConfigDef
    {
        public static ConfigDef FromJson(string json) => JsonConvert.DeserializeObject<ConfigDef>(json, Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this ConfigDef self) => JsonConvert.SerializeObject(self, Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
            Formatting = Formatting.Indented
        };
    }
    #endregion

}
