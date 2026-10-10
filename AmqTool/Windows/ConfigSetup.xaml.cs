using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AmqTool
{
    public partial class ConfigSetup : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void Changed([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string fromVegasPath = "From Vegas audio path";
        public string FromVegasPath { get => fromVegasPath; set { fromVegasPath = value; Changed(); } }

        private string intoVegasMp4Path = "Mp4 save location for Vegas";
        public string IntoVegasMp4Path { get => intoVegasMp4Path; set { intoVegasMp4Path = value; Changed(); } }

        private string defaultOutPath = "Default output path";
        public string DefaultOutPath { get => defaultOutPath; set { defaultOutPath = value; Changed(); } }

        private string tmpPath = "Default tmp path";
        public string TmpPath { get => tmpPath; set { tmpPath = value; Changed(); } }

        private string volumeCheckerPath = "Volume mode download path";
        public string VolumeCheckerPath { get => volumeCheckerPath; set { volumeCheckerPath = value; Changed(); } }

        private string bgsPath = "Home bg storage path";
        public string BgsPath { get => bgsPath; set { bgsPath = value; Changed(); } }

        private string catboxHash = "Catbox hash";
        public string CatboxHash { get => catboxHash; set { catboxHash = value; Changed(); } }

        private string ffmpegPath = "Ffmpeg executable";
        public string FfmpegPath { get => ffmpegPath; set { ffmpegPath = value; Changed(); } }

        private string powerShellPath = "Powershell executable";
        public string PowerShellPath { get => powerShellPath; set { powerShellPath = value; Changed(); } }


        public ConfigSetup()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FromVegasPath_0.ToolTip = FromVegasPath;
            IntoVegasMp4Path_0.ToolTip = IntoVegasMp4Path;
            DefaultOutPath_0.ToolTip = DefaultOutPath;
            TmpPath_0.ToolTip = TmpPath;
            BgsPath_0.ToolTip = BgsPath;
            VolumeCheckerPath_0.ToolTip = VolumeCheckerPath;
            CatboxHash_0.ToolTip = CatboxHash;
            FfmpegPath_0.ToolTip = FfmpegPath;
            PowerShellPath_0.ToolTip = PowerShellPath;

            TmpPath = MainWindow.config.conf.Folders.TmpPath;
            VolumeCheckerPath = MainWindow.config.conf.Folders.VolumeCheckerPath;
            FfmpegPath = MainWindow.config.conf.Execs[0].Path;
            if (!File.Exists(FfmpegPath)) FfmpegPath = FindOnPath("ffmpeg.exe") ?? FfmpegPath;
            PowerShellPath = MainWindow.config.conf.Execs[2].Path;

            FromVegasPath = MainWindow.config.conf.Folders.FromVegasFlacPath;
            IntoVegasMp4Path = MainWindow.config.conf.Folders.IntoVegasMp4Path;
            DefaultOutPath = MainWindow.config.conf.Folders.DefaultOutPath;
            BgsPath = MainWindow.config.conf.Folders.BgsPath;
            BgsSubfolders_Chk.IsChecked = MainWindow.config.conf.Folders.BgsIncludeSubfolders;
            CatboxHash = MainWindow.privateConfig.conf.catbox_userhash;
            CatboxHash_0.Password = CatboxHash;
        }

        private void Tbx_TextChanged(object sender, TextChangedEventArgs e)
        {
            var t = sender as FrameworkElement;
            var sp = t.Parent as StackPanel;
            if (sp.Children.Count == 5)
            {
                var tb = sp.Children.OfType<TextBlock>().FirstOrDefault();
                var box = sp.Children.OfType<TextBox>().FirstOrDefault();
                tb.Text = Directory.Exists(box.Text) ? "OK" : "invalid";
                if (tb.Name.StartsWith("FfmpegPath") || tb.Name.StartsWith("PowerShellPath"))
                    tb.Text = File.Exists(box.Text) ? "OK" : "invalid";
                // the small "invalid" text is easy to miss, so also outline the box in red
                if (tb.Text == "OK")
                {
                    box.ClearValue(BorderBrushProperty);
                    box.ClearValue(BorderThicknessProperty);
                }
                else
                {
                    box.BorderBrush = Brushes.Red;
                    box.BorderThickness = new Thickness(2);
                }
                if (tb.Text == "OK")
                {
                    var tbx = sender as TextBox;
                    // ig I have to do this cuz bogged
                    var left = tbx.Name.Split('_')[0];
                    if (left == "FromVegasPath") FromVegasPath = tbx.Text;
                    if (left == "IntoVegasMp4Path") intoVegasMp4Path = tbx.Text;
                    if (left == "DefaultOutPath") DefaultOutPath = tbx.Text;
                    if (left == "TmpPath") TmpPath = tbx.Text;
                    if (left == "BgsPath") BgsPath = tbx.Text;
                    if (left == "VolumeCheckerPath") VolumeCheckerPath = tbx.Text;
                    if (left == "FfmpegPath") FfmpegPath = tbx.Text;
                    if (left == "PowerShellPath") PowerShellPath = tbx.Text;
                }
            }
            else
            {
                if (t.Name.StartsWith("CatboxHash"))
                {
                    var pbx = sender as PasswordBox;
                    var tb = TBXSP.FindName($"CatboxHash_4") as TextBlock;
                    if (tb == null) return;
                    if (pbx.Password.Length == 0) tb.Text = "optional";
                    else if (pbx.Password.Length < 20) tb.Text = "prolly wrong";
                    else tb.Text = "filled";
                    CatboxHash = pbx.Password;
                }
            }
            if (e != null)
                e.Handled = true;
        }

        private void Tbx_LostFocus(object sender, RoutedEventArgs e)
        {
            // actually lets not do anything here
        }

        private void Tbx_GotFocus(object sender, RoutedEventArgs e)
        {
            // actually lets not do anything here
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Name == "Save_BTN") MaybeSave();
            if (btn.Name == "Cancel_BTN") DialogResult = false;

            var left = btn.Name.Split('_')[0];
            var right = btn.Name.Split('_')[1];

            if (right == "1" && (left == "FfmpegPath" || left == "PowerShellPath"))
            {
                // these want the .exe itself, not a folder
                var ofd = new Microsoft.Win32.OpenFileDialog { Title = "Select the .exe", Filter = "Programs (*.exe)|*.exe" };
                if (ofd.ShowDialog() == true)
                    (TBXSP.FindName($"{left}_0") as TextBox).Text = ofd.FileName;
            }
            else if (right == "1")
            {
                var folder = MainWindow.getFolderPathFromOfd(MainWindow.config.conf.Folders.LastFolderPickerPath);
                if (Directory.Exists(folder))
                    (TBXSP.FindName($"{left}_0") as TextBox).Text = folder;
            }

            if (right == "2")
            {
                MessageBox.Show(GetHelp(left), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            if (right == "3")
            {
                if (left != "CatboxHash")
                {
                    (TBXSP.FindName($"{left}_0") as TextBox).Text = "";
                }
                else
                {
                    (TBXSP.FindName($"{left}_0") as PasswordBox).Password = "";
                }
            }

        }

        private void MaybeSave()
        {
            foreach (var sp in TBXSP.Children.OfType<StackPanel>())
            {
                // The catbox hash row has a PasswordBox instead of a TextBox and is optional (litterbox uploads don't need it),
                // collapsed rows (powershell) aren't shown so they can't be fixed by the user either
                var tbx = sp.Children.OfType<TextBox>().FirstOrDefault();
                if (tbx == null || sp.Visibility != Visibility.Visible) continue;
                var tb = sp.Children.OfType<TextBlock>().FirstOrDefault();
                if (tb.Text.Contains("invalid") || tb.Text.Contains("missing"))
                {
                    var n = tbx.Name.Split('_').First();
                    var t = tbx.Text;
                    MessageBox.Show($"Text: {t}\nAt: {n}\n\nInvalid value in text box, can not save. Please edit all invalid text boxes then try again.", "Can not save this config", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            MainWindow.config.conf.Folders.TmpPath = TmpPath;
            MainWindow.config.conf.Folders.VolumeCheckerPath = VolumeCheckerPath;
            MainWindow.config.conf.Execs[0].Path = FfmpegPath;
            MainWindow.config.conf.Execs[2].Path = PowerShellPath;

            MainWindow.config.conf.Folders.FromVegasFlacPath = FromVegasPath;
            MainWindow.config.conf.Folders.IntoVegasMp4Path = IntoVegasMp4Path;
            MainWindow.config.conf.Folders.DefaultOutPath = DefaultOutPath;
            MainWindow.config.conf.Folders.BgsPath = BgsPath;
            MainWindow.config.conf.Folders.BgsIncludeSubfolders = BgsSubfolders_Chk.IsChecked == true;
            MainWindow.privateConfig.conf.catbox_userhash = CatboxHash;

            MainWindow.config.UpdateConfig();
            MainWindow.privateConfig.UpdateConfig();

            DialogResult = true;
        }

        private string GetHelp(string left)
        {
            if (left == "FromVegasPath") return "This path is the one where the program will take the audio input give in process mode. --> Meaning, that for process mode you will first render out an appropriate flac file, then use that.";

            if (left == "IntoVegasMp4Path") return "This is the path where you want your mp4 files to be placed into after converting them from mkv to mp4 with this program. You will be using those in Vegas.";

            if (left == "DefaultOutPath") return "This is the default path where rendered videos will be saved to. You can also define sub-folder paths in the program itself later while defining jobs. But they will all be the subpath of this path.";

            if (left == "TmpPath") return "This is just the path where any temporary files of any kind will be saved. This folder will be wiped on every program close and open in case any leftover files don't get dleeted there.\n\n[AGAIN, THIS TIME IN ALL CAPS: WARNING, THE CONTENTS OF THE FOLDER SET HERE WILL BE WIPED ON EVERY PROGRAM START AND SHUT DOWN, SO BE SURE TO SET IT TO SOME EMPTY DIRECTORY (FOLDER)!]";

            if (left == "VolumeCheckerPath") return "This is where the files will be downloaded in the volume mode if the user checks the checkbox to also download the file";

            if (left == "BgsPath") return "This is the path where you will place various image files (jpg, png, webp or bmp). Out of those on every program startup a random one will be chosen as the background (View > Randomize home background picks another one). If you only want to have one then just place one picture in the folder.\n\nTick the checkbox below it to also use the pictures in its sub-folders.";

            if (left == "CatboxHash") return "Not used anymore: every upload goes to litterbox now, which doesn't need an account.";

            if (left == "FfmpegPath") return "Set this path to the ffmpeg executable file (ffmpeg.exe).\n\nDon't have ffmpeg? Open a terminal and run:  winget install Gyan.FFmpeg\nthen reopen AmqTool, it picks up ffmpeg from PATH by itself.";

            if (left == "PowerShellPath") return "Set this path to the powershell executable file.";

            return "Missing help";
        }

        private static string FindOnPath(string exe)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try
                {
                    var p = System.IO.Path.Combine(dir.Trim().Trim('"'), exe);
                    if (File.Exists(p)) return p;
                }
                catch { } // junk PATH entries
            }
            return null;
        }

        private void CatboxHash_0_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Tbx_TextChanged(sender, null);
        }
    }
}