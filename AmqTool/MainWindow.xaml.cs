using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Mpv.NET.Player;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms.Integration;
using System.Xml.Serialization;
using Mpv.Net.Wpf;
using System.Net;
using System.Collections.Specialized;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Globalization;
using System.Diagnostics.Contracts;
using System.Windows.Shell;

namespace AmqTool
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Globals        
        public static bool isOnConsole = false;
        public MyLocker locker = new MyLocker();
        public static Random random = new Random();
        public bool window_closing = false;
        public bool playerLoaded = false;
        public bool canRun = true;
        public bool isProcessing = false;
        public static string bgsPath;
        public static string tmpPath;
        public static Config config;
        public static PrivateConfig privateConfig;
        public static string[] backgroundImgs;
        static Random rng = new Random();
        public static State state = new State();
        public static State previousState = new State();
        //public Process process = new Process();
        public Process process2 = new Process();
        public static List<Process> processList = new List<Process>();
        public static List<Dictionary<string, Tuple<Key, string>>> SetKeybinds = new List<Dictionary<string, Tuple<Key, string>>>();
        ObservableCollection<AmqJob> JobsOBS = new ObservableCollection<AmqJob>();
        public string[] allFiles;
        public double DisplayWidthBackup = 0;
        public double DisplayHeightBackup = 0;
        public string time_len_txt_existed = "";

        public event PropertyChangedEventHandler PropertyChanged;
        public void Changed([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string startTime = "00:00:00.000";
        public string StartTime { get => startTime; set { startTime = value; Changed(); } }

        private string endTime = "00:00:00.000";
        public string EndTime { get => endTime; set { endTime = value; Changed(); } }

        private string timeLen = "TimeLen(-t)";
        public string TimeLen { get => timeLen; set { timeLen = value; Changed(); } }

        private string processFileInPath = "I know the video file path (priority over player)";
        public string ProcessFileInPath { get => processFileInPath; set { processFileInPath = value; Changed(); } }

        private string getClipsOutFileTbx = "outFileNameHere";
        public string GetClipsOutFileTbx { get => getClipsOutFileTbx; set { getClipsOutFileTbx = value; Changed(); } }

        private double progBar_Jobs_Val = 0;
        public double ProgBar_Jobs_Val { get => progBar_Jobs_Val; set { progBar_Jobs_Val = value; Changed(); } }

        public int mkvmp4_done_num = 0;
        public int mkvmp4_total = 0;
        public int extractAudio_done_num = 0;
        public int extractAudio_total = 0;
        #endregion

        public MainWindow()
        {
            // If there are errors when loading this in a fresh VS instance. Click on App.xaml and change the build action to ApplicationDefinition.

            try
            {
                privateConfig = new PrivateConfig("private_config.json");
                config = new Config("config.json");
                if (config.conf.WindowProps.LastStarted == "-1")
                {
                    canRun = new ConfigSetup().ShowDialog() ?? false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow, ex.Message, "Failed to load config file. Exiting.", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }

            InitializeComponent();


            if (!canRun)
            {
                MessageBox.Show(Application.Current.MainWindow, "Can not start without a valid setup config.", "No or invalid config", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            config.conf.WindowProps.LastStarted = DateTime.Now.ToString("dd-MM-yyyyTHH:mm:ssK");

            bgsPath = config.conf.Folders.BgsPath;
            tmpPath = config.conf.Folders.TmpPath;
            ToolTipService.ShowDurationProperty.OverrideMetadata(
                typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));
            Mpv.Net.Wpf.MpvDisplay.GetDataFromMpvDisplay += MpvDisplay_GetDataFromMpvDisplay;
            Mpv.Net.Wpf.MpvPlayer.GetJumpTime += MpvPlayer_GetJumpTime;
            Mpv.Net.Wpf.MpvPlayer.PlayerLoaded += MpvPlayer_PlayerLoaded;
            Mpv.Net.Wpf.MpvPlayer.MediaError += MpvPlayer_MediaError;
            VolumeJobs.LoadIntoPlayer += VolumeJobs_LoadIntoPlayer;
            VolumeJobs.ShowDetailedInfo += VolumeJobs_ShowDetailedInfo;
            VolumeJobs.SetStatusBar += VolumeJobs_SetStatusBar;
            DataContext = this;

            //restore jobs
            if (!File.Exists("jobs.json")) File.Create("jobs.json").Close();
            JobsOBS = JsonConvert.DeserializeObject<ObservableCollection<AmqJob>>(File.ReadAllText("jobs.json")) ?? new ObservableCollection<AmqJob>();
            JobsLV.ItemsSource = JobsOBS;
            foreach (var j in JobsOBS)
                j.EnableBasics();
            ReCheckJobs();
        }


        private void VolumeJobs_SetStatusBar(object sender, string text, bool left)
        {
            Dispatcher.Invoke(() =>
            {
                SetStatusBarText(text, left ? SB.left : SB.right);
            });
        }

        private void VolumeJobs_ShowDetailedInfo(object sender, string details)
        {
            outputBox.Document.Blocks.Clear();
            foreach (var line in details.Split('\n'))
            {
                outputBox.AppendText(line);
                outputBox.AppendText("\u2028");
            }
            outputBox.ScrollToEnd();
            ToggleToConsole();
        }

        private void VolumeJobs_LoadIntoPlayer(object sender, string path)
        {
            LoadVideoIntoPlayer(path);
        }

        #region WindowStateSaving
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            cleanTmp();
            Application.Current.MainWindow.Height = config.conf.WindowProps.Y;
            Application.Current.MainWindow.Width = config.conf.WindowProps.X;
            Application.Current.MainWindow.Top = config.conf.WindowProps.Top;
            Application.Current.MainWindow.Left = config.conf.WindowProps.Left;
            Application.Current.MainWindow.WindowState = config.conf.WindowProps.Fullscreen ? WindowState.Maximized : WindowState.Normal;

            showHome();
            setRandomBg(firstTime: true);
            setupAdvancedCbx();
            setupKeybinds();
            ToggleOnTextBoxClearing(updateConfFile: false);

            //_AdvCbx.ItemsSource = advExtensions;
            //_AdvCbx.SelectedIndex = 0;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!canRun) return;
            window_closing = true;
            try
            {
                process2?.Kill();
                process2?.Dispose();
                isProcessing = false;
                processList = processList.Where(x => x != null).ToList();
                foreach (var p in processList)
                {
                    try
                    {
                        p?.Kill();
                        p?.Dispose();
                    }
                    catch {; }
                }
            }
            catch {; }
            config.conf.WindowProps.Y = Convert.ToInt32(Application.Current.MainWindow.Height);
            config.conf.WindowProps.X = Convert.ToInt32(Application.Current.MainWindow.Width);
            config.conf.WindowProps.Top = Convert.ToInt32(Application.Current.MainWindow.Top);
            config.conf.WindowProps.Left = Convert.ToInt32(Application.Current.MainWindow.Left);
            config.SaveUponClosing();
            if (Player != null)
            {
                if (Player.isPlaying())
                {
                    Player.isClosing = true;
                }
                Player.Stop();
                await Task.Delay(1000);
                Player.Dispose();
            }
            cleanTmp();
        }

        private void cleanTmp()
        {
            if (tmpPath != null && Directory.Exists(tmpPath))
            {
                foreach (FileInfo f in new DirectoryInfo(tmpPath).GetFiles())
                    try
                    {
                        f.Delete();
                    }
                    catch { }
            }

        }

        private void ToggleOnTextBoxClearing(bool updateConfFile)
        {
            if (updateConfFile)
                config.conf.WindowProps.DontClearTextBoxes = !config.conf.WindowProps.DontClearTextBoxes;
            if (config.conf.WindowProps.DontClearTextBoxes)
                _Dont_clear_textboxes.Icon = new Image { Source = new BitmapImage(new Uri("Assets/Icons/checkmark.png", UriKind.Relative)) };
            else
                _Dont_clear_textboxes.Icon = null;
            if (updateConfFile)
                config.UpdateConfig();
        }

        private void SetIntoVegasMp4Path()
        {
            var folder = getFolderPathFromOfd(config.conf.Folders.IntoVegasMp4Path);
            if (folder != null)
            {
                //var tmp = folder.Split('\\'); Whoops, the commented parts take one path less because of the media selecting
                //config.conf.WindowProps.IntoVegasMp4Path = string.Join("\\", tmp.Take(tmp.Length - 1));
                config.conf.Folders.IntoVegasMp4Path = folder;
                config.UpdateConfig();
                MessageBox.Show(Application.Current.MainWindow, $"Into vegas mp4 main path is set to:\n\n{folder}", "Feedback", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            config.UpdateConfig();
        }
        private void SetFromVegasFlacPath()
        {
            var folder = getFolderPathFromOfd(config.conf.Folders.FromVegasFlacPath);
            if (folder != null)
            {
                //var tmp = folder.Split('\\'); Whoops, the commented parts take one path less because of the media selecting
                //config.conf.WindowProps.FromVegasFlacPath = string.Join("\\", tmp.Take(tmp.Length - 1));
                config.conf.Folders.FromVegasFlacPath = folder;
                config.UpdateConfig();
                MessageBox.Show(Application.Current.MainWindow, $"Vegas flac source main path is set to:\n\n{folder}", "Feedback", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            config.UpdateConfig();
        }
        private void SetDefaultOutPath()
        {
            var folder = getFolderPathFromOfd(config.conf.Folders.DefaultOutPath);
            if (folder != null)
            {
                //var tmp = folder.Split('\\'); Whoops, the commented parts take one path less because of the media selecting
                //config.conf.WindowProps.FromVegasFlacPath = string.Join("\\", tmp.Take(tmp.Length - 1));
                config.conf.Folders.DefaultOutPath = folder;
                config.UpdateConfig();
                MessageBox.Show(Application.Current.MainWindow, $"Vegas flac source main path is set to:\n\n{folder}", "Feedback", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            config.UpdateConfig();
        }

        private void setupAdvancedCbx()
        {
            //foreach (var adv in config.conf.Extensions) 
            //    advExtensions.Add($" {adv.Name} v{adv.Version}");
        }

        private void setRandomBg(bool firstTime = false)
        {
            backgroundImgs = GetFilesFrom(bgsPath, new string[] { "jpg", "png" });
            if (!firstTime)
            {
                if (backgroundImgs.Length == 1)
                {
                    var res = MessageBox.Show(Application.Current.MainWindow, $"Only one background image exist.\n\nDo you want to open the relavant folder and add more to it?", "Can't change background image", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes) Process.Start(config.conf.Folders.BgsPath);
                    return;
                }
                if (backgroundImgs.Length == 0)
                {
                    var res = MessageBox.Show(Application.Current.MainWindow, $"No background images exist\n\nDo you want to open the relavant folder and add more to it?", "Can't change background image", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes) Process.Start(config.conf.Folders.BgsPath);
                    return;
                }
            }
            if (backgroundImgs.Length == 0) return;
            var img = new System.Windows.Controls.Image { Source = new BitmapImage(new Uri(backgroundImgs[rng.Next(backgroundImgs.Length)], UriKind.Absolute)) };
            _Home_Grid_BG.ImageSource = img.Source;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                config.conf.WindowProps.Fullscreen = false;
                _Top_Menu.Visibility = Visibility.Visible;
                _Status_SP.Visibility = Visibility.Visible;
            }
            if (WindowState == WindowState.Maximized)
            {
                config.conf.WindowProps.Fullscreen = true;
                if (ResizeMode != ResizeMode.NoResize) return;
                _Top_Menu.Visibility = Visibility.Collapsed;
                _Status_SP.Visibility = Visibility.Collapsed;
            }
        }
        private void File_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        #endregion        

        #region GeneralIOFunc
        private static string[] GetFilesFrom(string searchFolder, string[] filters, bool isRecursive = false)
        {
            List<string> filesFound = new List<string>();
            var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var filter in filters)
            {
                try
                {
                    filesFound.AddRange(System.IO.Directory.GetFiles(searchFolder, string.Format("*.{0}", filter), searchOption));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception in GetFilesFrom: {e.Message}");
                }
            }
            return filesFound.ToArray();
        }

        /// <summary>
        /// Get a folder path
        /// </summary>
        /// <param name="initDir">Leave empty for "config.conf.WindowProps.LastFolderPickerPath"</param>
        /// <returns></returns>
        public static string getFolderPathFromOfd(string initDir = "")
        {
            string initDirWas = initDir;
            if (initDir == "")
            {
                initDir = config.conf.Folders.LastFolderPickerPath;
            }
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = initDir;
            dialog.Multiselect = false;
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (initDirWas == "")
                {
                    var tmp = dialog.FileNames.First().Split('\\');
                    config.conf.Folders.LastFolderPickerPath = string.Join("\\", tmp.Take(tmp.Length - 1));
                    config.UpdateConfig();
                }
                return dialog.FileNames.First();
            }
            return null;
        }

        /// <summary>
        /// Get a media file path
        /// </summary>
        /// <param name="initDir">Leave empty for "config.conf.WindowProps.LastFolderPickerPath"</param>
        /// <returns></returns>
        public static string getMediaPathFromOfd(string initDir = "", string filter = "*.mkv; *.mp4; *.webm; *.mp3; *.flac")
        {
            string initDirWas = initDir;
            if (initDir == "")
            {
                initDir = config.conf.Folders.LastFolderPickerPath;
            }
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Title = "Open media";
            var f2 = filter;
            var f1 = f2.Replace(';', ',');
            ofd.Filter = $"Media files ({f1}) | {f2}";
            ofd.InitialDirectory = initDir;
            ofd.Multiselect = false;

            ofd.ShowDialog();
            string fileName = ofd.FileName; // C:\\Users\\Kiki\\Desktop\\SomeImg.png            
            if (fileName != "")
            {
                if (initDirWas == "")
                {
                    var tmp = fileName.Split('\\');
                    config.conf.Folders.LastFolderPickerPath = string.Join("\\", tmp.Take(tmp.Length - 1));
                    config.UpdateConfig();
                }
                return fileName;
            }
            return "</:?NoFileSelected*|>";
        }

        public static string[] getMultipleMediaPathFromOfd(string initDir = "", string filter = "*.mkv")
        {
            string initDirWas = initDir;
            if (initDir == "")
            {
                initDir = config.conf.Folders.LastFolderPickerPath;
            }
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Title = "Select multiple files";
            var f2 = filter;
            var f1 = f2.Replace(';', ',');
            ofd.Filter = $"Media files ({f1}) | {f2}";
            ofd.InitialDirectory = initDir;
            ofd.Multiselect = true;

            ofd.ShowDialog();
            var fileNames = ofd.FileNames;
            if (fileNames.Length > 0)
            {
                if (initDirWas == "")
                {
                    var tmp = fileNames.First().Split('\\');
                    config.conf.Folders.LastFolderPickerPath = string.Join("\\", tmp.Take(tmp.Length - 1));
                    config.UpdateConfig();
                }
                return fileNames;
            }
            return null;
        }
        #endregion

        #region ViewChanging
        private void showHome()
        {
            foreach (Grid child in _Top_Level_Grid.Children)
                child.Visibility = Visibility.Collapsed;
            _Top_Level_Grid.Children[(int)state.CurrentState].Visibility = Visibility.Visible;
        }

        private void ChangeView(bool firstTime = false)
        {
            if (isProcessing)
            {
                MessageBox.Show(Application.Current.MainWindow, "Can not change view while processing.", "Jobs not done", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Enable the state switching menu
            var mitems = _Mode_Menu.Items.OfType<MenuItem>();
            foreach (var item in mitems)
            {
                item.Icon = null;
                if (firstTime) item.IsEnabled = true;
            }

            // Checkmark the new state
            mitems.ToArray()[(int)state.CurrentState].Icon = new Image { Source = new BitmapImage(new Uri("Assets/Icons/checkmark.png", UriKind.Relative)) };

            // Collapse all grids -- in this case just _Home_Grid and _NonHomeGrid
            foreach (Grid child in _Top_Level_Grid.Children)
                child.Visibility = Visibility.Collapsed;

            // Collapse all stack panels inside non home grid
            foreach (var sp in Non_Home_Grid.Children.OfType<StackPanel>())
                sp.Visibility = Visibility.Collapsed;

            // Make the _Home_Grid or _NonHomeGrid grid visible
            int idx = state.CurrentState == States.Home ? 0 : 1;
            _Top_Level_Grid.Children[idx].Visibility = Visibility.Visible;

            int st = (int)state.CurrentState;
            if (st > 0) // if the state is not Home state
            {
                foreach (var sp in Non_Home_Grid.Children.OfType<StackPanel>())
                {
                    if (sp.Name.EndsWith("JobsRelated_") && st != (int)States.Volume)
                    {
                        sp.Visibility = Visibility.Visible;
                        if (state.CurrentState == States.Clips)
                            SetupBottomLeftForClipsMode(sp);
                        if (state.CurrentState == States.Process)
                            SetupBottomLeftForProcessMode(sp);
                        continue;
                    }
                    if (sp.Name.EndsWith(st.ToString()) && st != (int)States.Home)
                    {
                        sp.Visibility = Visibility.Visible;
                        continue;
                    }
                }
            }
            if (st == (int)States.Volume)
            {
                VolumeJobs.Visibility = Visibility.Visible;
                TR_JobsRelated_.Visibility = Visibility.Visible;
                JobsLV.Visibility = Visibility.Collapsed;
                BR_JobsRelated_.Visibility = Visibility.Visible;
                CLIPS_START_JOBS.Visibility = Visibility.Collapsed;
                CLEAR_ALL_JOBS.Visibility = Visibility.Visible;

                MULTI_INPUT_VOLUME.Visibility = Visibility.Visible;
            }
            if (st != (int)States.Volume)
            {
                VolumeJobs.Visibility = Visibility.Collapsed;
                JobsLV.Visibility = Visibility.Visible;
                CLIPS_START_JOBS.Visibility = Visibility.Visible;

                MULTI_INPUT_VOLUME.Visibility = Visibility.Collapsed;
            }
            if (previousState.CurrentState == States.Volume && state.CurrentState != States.Volume)
            {
                ToggleToMpv();
            }
        }

        private void SetupBottomLeftForClipsMode(StackPanel sp)
        {
            // Make everything collapsed except for what you need
            foreach (var s in sp.Children.OfType<StackPanel>())
                foreach (var el in s.Children)
                    (el as FrameworkElement).Visibility = Visibility.Collapsed;

            /// <Get_Clips>
            // timestamps
            GET_CLIPS_START.Visibility = Visibility.Visible;
            GET_CLIPS_END.Visibility = Visibility.Visible;
            MPV_Reset_GC.Visibility = Visibility.Visible;

            // out file name
            GET_CLIPS_OUT_TBX.Visibility = Visibility.Visible;
            MPV_ADD_JOB_GET_CLIPS.Visibility = Visibility.Visible;

            // Frame movements
            OneFrameBackPreviewerGC.Visibility = Visibility.Visible;
            OneFrameForwardPreviewerGC.Visibility = Visibility.Visible;
            AnotherCheckStartButton.Visibility = Visibility.Visible;
            OneFrameBackPreviewerGC2.Visibility = Visibility.Visible;
            OneFrameForwardPreviewerGC2.Visibility = Visibility.Visible;
            AnotherCheckStartButton2.Visibility = Visibility.Visible;

            // The hint in GC
            GC_Max_pos_hint.Visibility = Visibility.Visible;
            /// </Get_Clips>

        }

        private void SetupBottomLeftForProcessMode(StackPanel sp)
        {
            foreach (var s in sp.Children.OfType<StackPanel>())
                foreach (var el in s.Children)
                    (el as FrameworkElement).Visibility = Visibility.Collapsed;

            /// <Get_Clips>
            // timestamps
            GET_CLIPS_START.Visibility = Visibility.Visible;
            GET_CLIPS_END.Visibility = Visibility.Collapsed;
            MPV_Reset_GC.Visibility = Visibility.Collapsed;

            // out file name
            GET_CLIPS_OUT_TBX.Visibility = Visibility.Visible;
            MPV_ADD_JOB_GET_CLIPS.Visibility = Visibility.Visible;

            // Frame movements
            OneFrameBackPreviewerGC.Visibility = Visibility.Visible;
            OneFrameForwardPreviewerGC.Visibility = Visibility.Visible;
            AnotherCheckStartButton.Visibility = Visibility.Visible;
            OneFrameBackPreviewerGC2.Visibility = Visibility.Collapsed;
            OneFrameForwardPreviewerGC2.Visibility = Visibility.Collapsed;
            AnotherCheckStartButton2.Visibility = Visibility.Collapsed;

            // The hint in GC
            GC_Max_pos_hint.Visibility = Visibility.Collapsed;
            /// </Get_Clips>

            /// <Process>
            PROCESS_TIME_T.Visibility = Visibility.Visible;
            Process_FileNameHint.Visibility = Visibility.Visible;
            I_Know_the_process_input_file_name.Visibility = Visibility.Visible;
            I_Know_the_hint.Visibility = Visibility.Visible;

            PreviewIKnowPathBTN.Visibility = Visibility.Visible;
            LoadProcessVideoSource.Visibility = Visibility.Visible;
            CopyProcessVideoPath.Visibility = Visibility.Visible;
            ProcessClearCbx.Visibility = Visibility.Visible;
            /// </Process>
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var vi1 = "_Home";
            var vi2 = "_Volume";
            var vi3 = "_Get clips";
            var vi4 = "_Process";
            // Mode
            previousState.CurrentState = state.CurrentState;
            if (menuItem.Header.ToString() == vi1) state.CurrentState = States.Home;
            if (menuItem.Header.ToString() == vi2) state.CurrentState = States.Volume;
            if (menuItem.Header.ToString() == vi3) state.CurrentState = States.Clips;
            if (menuItem.Header.ToString() == vi4) state.CurrentState = States.Process;
            var h = menuItem.Header.ToString();
            if (h == vi1 || h == vi2 || h == vi3 || h == vi4)
            {
                if (Player != null)
                {
                    Player.PauseIfPlaying();
                }
                ChangeView();
            }

            // View
            //if (menuItem.Header.ToString() == "_Toggle dark theme") MessageBox.Show(Application.Current.MainWindow, "Currently not implemented");
            if (menuItem.Header.ToString() == "_Randomize home background") setRandomBg();

            // Edit
            if (menuItem.Header.ToString() == "_Keybindings") new KeybindSettings().ShowDialog();
            if (menuItem.Header.ToString() == "_Edit all config") new ConfigSetup().ShowDialog();
            if (menuItem.Header.ToString() == "_From vegas flac path") SetFromVegasFlacPath();
            if (menuItem.Header.ToString() == "_Into vegas mp4 path") SetIntoVegasMp4Path();
            if (menuItem.Header.ToString() == "_Default render output path") SetDefaultOutPath();
            if (menuItem.Header.ToString() == "_Don't clear textboxes on focus") ToggleOnTextBoxClearing(true);
            if (menuItem.Header.ToString() == "_ffmpeg settings")
            {
                var savePressed = new editffmpegstuff().ShowDialog() ?? false;
                if (savePressed)
                {
                    // nothing here for now
                }
                config.UpdateConfig(); // so that the 1/2 pass boolean gets updated
                return;
            }

            // Help
            if (menuItem.Header.ToString() == "_About") new About().ShowDialog();

            // Context menu on image in organizing mode
            //if (menuItem.Header.ToString() == "_Copy Image") copyImageToClipboard();
            //if (menuItem.Header.ToString() == "_Delete Image") deleteImage();
            //if (menuItem.Header.ToString() == "_Rotate right") rotateImageRight();
            //if (menuItem.Header.ToString() == "_Open in editing mode") enterEditingMode(PicHandler.CurrentImage.imageSrc);
            //if (menuItem.Header.ToString() == "_Open in advanced mode") enterAdvancedMode(PicHandler.CurrentImage.imageSrc);

            // File
            if (menuItem.Header.ToString() == "_Open media")
            {
                if (state.CurrentState == States.Home)
                {
                    var res = MessageBox.Show(Application.Current.MainWindow, "Opening a file while on the home screen will send you to GET CLIPS mode.\n\n" +
                        "Do you wish to continue?", "Drag event", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.No) return;
                }
                var files = new string[] { getMediaPathFromOfd() };
                if (files.Length > 0 && (files[0] == null || files[0] == ""))
                {
                    MessageBox.Show(Application.Current.MainWindow, "Something went wrong when opening this file.", "Error!", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (files.Length > 0 && files[0] == "</:?NoFileSelected*|>")
                {
                    return;
                }
                enterGetClipsMode(files, ActuallyDontChangeView: true);
                Player.LoadFile(files[0]);
                return;
            }
            if (menuItem.Header.ToString() == "_Open default render output directory")
            {
                var path = config.conf.Folders.DefaultOutPath;
                if (Directory.Exists(path))
                    Process.Start(path);
                else
                    MessageBox.Show(Application.Current.MainWindow, $"{path}\n\nDeafult render output path doesn't exist", "Please fix in Edit > Default output path (or Edit all config)", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (menuItem.Header.ToString() == "_Open default volume checker directory")
            {
                var path = config.conf.Folders.VolumeCheckerPath;
                if (Directory.Exists(path))
                    Process.Start(path);
                else
                    MessageBox.Show(Application.Current.MainWindow, $"{path}\n\nDeafult volume checker out path doesn't exist", "Please fix in Edit > Edit all config", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (menuItem.Header.ToString() == "_Export current jobs list")
            {

            }

            if (menuItem.Header.ToString() == "_Import expored jobs json")
            {
                //getMediaPathFromOfd TODODODODODODODODODODDODO
            }
            //if (menuItem.Header.ToString() == "_Save") _Edi_Save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            //if (menuItem.Header.ToString() == "_Save as") saveThePic(true);
        }

        private async void Selec_Mode_Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Name == "B_Mp4_to_mkv")
            {
                var files = await Mp4ToMkv();
                if (files == null) return;
                var res = MessageBox.Show(Application.Current.MainWindow, string.Join("\n", files) + "\n\nDo you want to open the output folder?", "Done converting to MP4", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                    Process.Start(config.conf.Folders.IntoVegasMp4Path);
                return;
            }

            if (btn.Name == "B_Extract_audio")
            {
                var files = await AnythingToAudio();
                if (files == null) return;
                var res = MessageBox.Show(Application.Current.MainWindow, string.Join("\n", files) + "\n\nDo you want to open the output folder?", "Done extracting audio", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                    Process.Start(config.conf.Folders.IntoVegasMp4Path);
                return;
            }

            if (Player != null)
                Player.Stop();
            if (btn.Name == "B_Volume")
                if (!enterVolumeMode()) return;
            if (btn.Name == "B_Clips")
                if (!enterGetClipsMode()) return;
            if (btn.Name == "B_Process")
                if (!enterProcessMode()) return;

            ChangeView(true);
        }

        private async Task<string[]> Mp4ToMkv()
        {
            var files = getMultipleMediaPathFromOfd(filter: "*.mkv; *.ts");
            if (files == null) return null;
            mkvmp4_total += files.Length;
            foreach (var f in files)
            {
                MkvToMp4Hint.Text = $"processing... Done with {mkvmp4_done_num}/{mkvmp4_total}";
                var o = f.Split('\\').Last().Replace(".mkv", "");
                o = $"{config.conf.Folders.IntoVegasMp4Path}\\{o}.mp4";
                string _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", mkv_to_mp4_for_vegas(f, o)));
                mkvmp4_done_num++;
                MkvToMp4Hint.Text = $"processing... Done with {mkvmp4_done_num}/{mkvmp4_total}";
            }
            if (mkvmp4_done_num == mkvmp4_total)
            {
                mkvmp4_done_num = 0;
                mkvmp4_total = 0;
                MkvToMp4Hint.Text = "Select files to convert to mp4";
            }
            return files;
        }

        private async Task<string[]> AnythingToAudio()
        {
            var files = getMultipleMediaPathFromOfd(filter: "*.mkv; *.ts; *.mp4; *.webm; *.avi");
            if (files == null) return null;
            extractAudio_total += files.Length;
            foreach (var f in files)
            {
                ExtractAudioHint.Text = $"processing... Done with {extractAudio_done_num}/{extractAudio_total}";
                var o = f.Split('\\').Last().Split('.').First();
                o = $"{config.conf.Folders.IntoVegasMp4Path}\\{o}.flac";
                string _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", make_audio_from_input(f, o)));
                extractAudio_done_num++;
                ExtractAudioHint.Text = $"processing... Done with {extractAudio_done_num}/{extractAudio_total}";
            }
            if (extractAudio_done_num == extractAudio_total)
            {
                extractAudio_done_num = 0;
                extractAudio_total = 0;
                ExtractAudioHint.Text = "Select files to convert to flac";
            }
            return files;

        }
        #endregion

        #region Some Events
        private void ProcessVideoSource_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Name == "LoadProcessVideoSource")
            {
                var path = getMediaPathFromOfd();
                if (File.Exists(path))
                {
                    I_Know_the_process_input_file_name.Text = path;
                    // PROCESS_TIME_T.Text = "TimeLen(-t)";
                }
            }
            if (btn.Name == "CopyProcessVideoPath")
                Clipboard.SetText(I_Know_the_process_input_file_name.Text);
            if (btn.Name == "ProcessClearCbx")
                I_Know_the_process_input_file_name.Text = "I know the video file path (priority over player)";
            if (btn.Name == "PreviewIKnowPathBTN")
            {
                time_len_txt_existed = PROCESS_TIME_T.Text; // because media loaded event will overwrite it, don't want that here
                LoadVideoIntoPlayer(I_Know_the_process_input_file_name.Text);
                Player.PlayIfPaused();
            }
        }

        private async void OneFramePreviewerGC_Click(object sender, RoutedEventArgs e)
        {
            var btn = (sender as Button);
            if (Player._player.Duration == TimeSpan.FromSeconds(0)) return;

            OneFrameBackPreviewerGC.IsEnabled = false;
            OneFrameForwardPreviewerGC.IsEnabled = false;
            AnotherCheckStartButton.IsEnabled = false;

            bool isPlaying = Player.isPlaying();
            Player.PauseIfPlaying();

            bool blue = true;
            // blue
            if (btn.Name == "OneFrameBackPreviewerGC")
            {
                Player._player.Position = Player.startTimestamp.Time;
                while (Player._player.Position == Player.startTimestamp.Time)
                {
                    if (Player._player.Position == TimeSpan.FromSeconds(0))
                        break;
                    Player._player.PreviousFrame();
                    await Task.Delay(2);
                }
            }
            if (btn.Name == "OneFrameForwardPreviewerGC")
            {
                Player._player.Position = Player.startTimestamp.Time;
                while (Player._player.Position == Player.startTimestamp.Time)
                {
                    if (Player._player.Position == Player._player.Duration)
                        break;
                    Player._player.NextFrame();
                    await Task.Delay(2);
                }
            }

            // orange
            if (btn.Name == "OneFrameBackPreviewerGC2")
            {
                if (Player.endTimestamp.Time < Player._player.Duration)
                {
                    blue = false;
                    if (Player.maxPossible != null)
                        Player.maxPossible = TimeSpan.MaxValue;

                    Player._player.Position = Player.endTimestamp.Time;
                    while (Player._player.Position == Player.endTimestamp.Time)
                    {
                        if (Player._player.Position == TimeSpan.FromSeconds(0))
                            break;
                        Player._player.PreviousFrame();
                        await Task.Delay(2);
                    }
                    if (Player.maxPossible != null)
                        Player.maxPossible = Player._player.Position;
                }
                else
                {
                    MessageBox.Show(Application.Current.MainWindow, "Functionality currently disabled when the end timestamp is at max because of mpv's buggy behaviour.");
                }

            }
            if (btn.Name == "OneFrameForwardPreviewerGC2")
            {
                if (Player.endTimestamp.Time < Player._player.Duration)
                {
                    blue = false;
                    if (Player.maxPossible != null)
                        Player.maxPossible = TimeSpan.MaxValue;

                    Player._player.Position = Player.endTimestamp.Time;
                    while (Player._player.Position == Player.endTimestamp.Time)
                    {
                        if (Player._player.Position == Player._player.Duration)
                            break;
                        Player._player.NextFrame();
                        await Task.Delay(2);
                    }
                    if (Player.maxPossible != null)
                        Player.maxPossible = TimeSpan.MaxValue;
                }
                else
                {
                    MessageBox.Show(Application.Current.MainWindow, "Functionality currently disabled when the end timestamp is at max because of mpv's buggy behaviour.");
                }
            }

            await Task.Delay(5);
            var str = Player._player.Position.ToString().TrimEnd('0');
            if (!Player._player.Position.ToString().Contains(".")) str = Player._player.Position.ToString();

            if (blue)
            {
                Player.startTimestamp.UpdateTime(str);
                GET_CLIPS_START.Text = str;
            }
            else
            {
                Player.endTimestamp.UpdateTime(str);
                GET_CLIPS_END.Text = str;
            }

            if (isPlaying) Player.PlayIfPaused();

            OneFrameBackPreviewerGC.IsEnabled = true;
            OneFrameForwardPreviewerGC.IsEnabled = true;
            AnotherCheckStartButton.IsEnabled = true;
        }

        private void AnotherCheckStartButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Name == "AnotherCheckStartButton2")
            {
                Player.maxPossible = Player.endTimestamp.Time;

                Player._player.Position = TimeSpan.FromSeconds(Player.endTimestamp.Time.TotalSeconds - 3);
                //Player.MoveBySeconds(-4);
                Player.PlayIfPaused();
            }
            else
            {
                Player._player.Position = Player.startTimestamp.Time;
                Player.PlayIfPaused();
            }
        }
        private void Player_Grid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Player != null)
            {
                int _chg = 5;
                int change = e.Delta > 0 ? _chg : -_chg;
                Player.ChangeVolume(change);
            }

        }

        private void TXT_Chg(object sender, TextChangedEventArgs e)
        {
            var tbx = (sender as TextBox);
            if (tbx.Name == "GET_CLIPS_START")
            {
                if (Player == null) return;
                Player.startTimestamp.UpdateTime(tbx.Text);
            }
            if (tbx.Name == "GET_CLIPS_END")
            {
                if (Player == null) return;
                Player.endTimestamp.UpdateTime(tbx.Text);
            }
            if (tbx.Name == "I_Know_the_process_input_file_name")
            {
                if (tbx.Text != "I know the video file path (priority over player)" && tbx.Text != "")
                {
                    if (File.Exists(tbx.Text))
                    {
                        //PROCESS_TIME_T.Text = "TimeLen(-t)"; // clear for the new timestamps to be inputted
                        I_Know_the_hint.Text = "Path is valid.";
                        PreviewIKnowPathBTN.Style = (Style)Application.Current.Resources["JobsBTN_CheckVideo_ik_valid"];
                    }
                    else
                    {
                        I_Know_the_hint.Text = "Invalid path!";
                        PreviewIKnowPathBTN.Style = (Style)Application.Current.Resources["JobsBTN_CheckVideo_ik_invalid"];
                    }
                }
                if (tbx.Text == "I know the video file path (priority over player)")
                {
                    I_Know_the_hint.Text = "";
                    PreviewIKnowPathBTN.Style = (Style)Application.Current.Resources["JobsBTN_CheckVideo_ik_invalid"];
                }

            }
        }

        public string tmp_txt = "";
        private void TXT_GotFocus(object sender, RoutedEventArgs e)
        {
            if (config.conf.WindowProps.DontClearTextBoxes) return;
            var tbx = (sender as TextBox);
            if (tbx.IsReadOnly) return;
            if (tbx.ToolTip?.ToString() == "Set output file name. (anything but 'outFileNameHere' is allowed)" && tbx.Text != "outFileNameHere") return;
            if (tbx.ToolTip?.ToString() == "Set output sub folder name. (anything but 'outFileSubDirectory' is allowed)") return;
            if (tbx.Name == "I_Know_the_process_input_file_name") tbx.MinWidth = tbx.ActualWidth;

            tmp_txt = tbx.Text;
            tbx.Text = "";
        }

        private void TXT_LostFocus(object sender, RoutedEventArgs e)
        {
            if (config.conf.WindowProps.DontClearTextBoxes) return;
            var tbx = (sender as TextBox);
            if (tbx.IsReadOnly) return;
            if (tbx.ToolTip?.ToString() == "Set output file name. (anything but 'outFileNameHere' is allowed)" && tbx.Text != "" && tmp_txt != "outFileNameHere") return;
            if (tbx.ToolTip?.ToString() == "Set output sub folder name. (anything but 'outFileSubDirectory' is allowed)") return;
            if (tbx.Name == "I_Know_the_process_input_file_name") tbx.MinWidth = 235; // same as in xaml


            if (tbx.Text == "" && tmp_txt != "")
            {
                tbx.Text = tmp_txt;
                tmp_txt = "";
            }
        }

        private async void MPV_Btn_Click(object sender, RoutedEventArgs e)
        {
            var btn = (sender as Button);

            if (btn.Name == "MULTI_INPUT_VOLUME")
            {
                new MultiInputWindow().ShowDialog();
            }

            if (btn.Name == "STOP_JOBS")
            {
                if (MessageBox.Show(Application.Current.MainWindow, "Are you sure?", "Cancel all jobs", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                if (process2 != null)
                    process2.Kill();
                isProcessing = false; // force stop the jobs loop
                // MessageBox.Show(Application.Current.MainWindow, "Jobs stopped.", "", MessageBoxButton.OK, MessageBoxImage.Information);
                // SwitchToMpv(); No need to do this here because it will be done in if (btn.Name == "CLIPS_START_JOBS") anyway
                return;
            }

            if (btn.Name == "CLEAR_ALL_JOBS")
            {
                if (state.CurrentState == States.Volume)
                {
                    VolumeJobs.ClearJobsOBS();
                    return;
                }

                if (JobsOBS.Count == 0)
                {
                    MessageBox.Show(Application.Current.MainWindow, "There are no jobs in the list right now.", "No jobs to clear", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var res = MessageBox.Show(Application.Current.MainWindow, "Are you sure that you want to clear the entire jobs list?\nThis action can not be undone!", "Clearing jobs", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    JobsOBS.Clear();
                    UpdateJobsJsonFile();
                }

                return;
            }

            if (btn.Name == "MPV_Reset_GC")
            {
                Player.PauseIfPlaying();
                Player.startTimestamp.Reset(Player._player, start: true);
                Player.endTimestamp.Reset(Player._player, start: false);
                StartTime = "00:00:00.000";
                EndTime = Player.endTimestamp.StrVal;
            }

            if (btn.Name == "CLIPS_START_JOBS")
            {
                if (!AnyJobsToProcess())
                {
                    MessageBox.Show(Application.Current.MainWindow, "There are no unprocessed jobs in the list.", "Cancelling.", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string t = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
                BackupJobsJsonFile(t);

                List<string> test = new List<string>();
                foreach (var j in JobsOBS)
                {
                    if (j.JobFinished) continue;
                    bool isOK = true;

                    foreach (var q in new string[] { "720", "480", "mp3" })
                    {
                        // This isgnores the subdirectory setting
                        var aa = j.BuildFinalOutName(q, 0, true);
                        if (test.IndexOf(aa) >= 0 && q != "mp3")
                        {
                            isOK = false;
                            j.DisableJob();
                            UpdateJobsJsonFile();
                            var res = MessageBox.Show(Application.Current.MainWindow, $"Why are you trying to render this same file twice? Disabling and skipping this job.\n\n{aa}\n\nStart/continue render process anyway?", "Invalid job.", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (res == MessageBoxResult.No) return;
                            break;
                        }
                        test.Add(aa);
                    }
                    if (!isOK) continue;

                    var err = j.ValidateJob(); // Check here if job is fine
                    if (err == "") continue;
                    j.DisableJob();
                    UpdateJobsJsonFile();
                    j.JobFinished = true;
                    MessageBox.Show(Application.Current.MainWindow, $"{j.ShowTitleToolTip}\n\n{err}", "Invalid job. Re-add it or just delete it.", MessageBoxButton.OK, MessageBoxImage.Warning);
                    j.ShowTitleToolTip += $"   DISABLED BECASUE:   {err}";
                    return;
                }
                LogWhichTimestampWhatWasDone(t, string.Join("\n", test));
                test.Clear();

                ProgBar_Jobs_Val = 0;
                Dispatcher.Invoke(() =>
                {
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
                    TaskbarItemInfo.ProgressValue = 0.1;
                });

                IsFalseAllPropsJobs();
                PrepareSubpathsForJobOutputs();
                SwitchToConsole();

                isProcessing = true;
                bool isTerminated = false;
                double progBarUp = 0;

                #region inline methods
                async Task doProcess(string quality, AmqJob j, string crf)
                {
                    await Task.Delay(200);
                    try
                    {
                        var outF = j.BuildFinalOutName(quality, 0);
                        var bs = GetOrFixUpOutDirForThisJob(j);
                        var o1 = $"{bs}\\{outF}";


                        if (config.conf.WindowProps.Use2PassEnc)
                        {
                            await Task.Run(() => LaunchProcessAndDisplayInfoInRTB("ffmpeg", render_gcP1(j.Timestamps.startTime_SS, j.Timestamps.endTime_TO, j.ShowTitleFullPath, (j.UsedAudioId_Warning1Based - 1).ToString(), crf, quality, o1)));
                            await Task.Run(() => LaunchProcessAndDisplayInfoInRTB("ffmpeg", render_gcP2(j.Timestamps.startTime_SS, j.Timestamps.endTime_TO, j.ShowTitleFullPath, (j.UsedAudioId_Warning1Based - 1).ToString(), crf, quality, o1)));
                        }
                        else
                        {
                            await Task.Run(() => LaunchProcessAndDisplayInfoInRTB("ffmpeg", render_gcJust1(j.Timestamps.startTime_SS, j.Timestamps.endTime_TO, j.ShowTitleFullPath, (j.UsedAudioId_Warning1Based - 1).ToString(), crf, quality, o1)));
                        }

                        if (!isStillProcessing(j)) return;
                        if (quality == "720")
                        {
                            j.Rendered720File = o1;
                            j.Rendered720File_VOL0 = j.Rendered720File;
                        }
                        if (quality == "480")
                        {
                            j.Rendered480File = o1;
                            j.Rendered480File_VOL0 = j.Rendered480File;
                        }
                        if (!File.Exists(o1) || new FileInfo(o1).Length == 0)
                        {
                            var er = File.Exists(o1) ? "file size is 0" : "file doesn't exist";
                            throw new Exception($"Something went wrong. ({er}) ({o1})");
                        }

                        if (!isStillProcessing(j)) return;
                        if (j.FromState == States.Process)
                        {
                            var fin = $"{bs}\\{outF.Substring(5)}"; // Remove the TMP-- from thhe file name
                            await Task.Run(() => LaunchProcessAndDisplayInfoInRTB("ffmpeg", put_flac_on_vid(o1, j.AudioReplacementPath, fin)));
                            if (quality == "720")
                            {
                                j.Rendered720File = fin;
                                j.Rendered720File_VOL0 = j.Rendered720File;
                            }
                            if (quality == "480")
                            {
                                j.Rendered480File = fin;
                                j.Rendered480File_VOL0 = j.Rendered480File;
                            }
                        }

                        if (j.VolumeChangeValue != 0)
                        {
                            string actualFin = "";
                            if (quality == "720")
                            {
                                j.Rendered720File_VOL0 = j.Rendered720File;
                                actualFin = await make_new_changedvolume_media(j.VolumeChangeValue, j.Rendered720File);
                                j.Rendered720File = actualFin;
                            }
                            if (quality == "480")
                            {
                                j.Rendered480File_VOL0 = j.Rendered480File;
                                actualFin = await make_new_changedvolume_media(j.VolumeChangeValue, j.Rendered480File);
                                j.Rendered480File = actualFin;
                            }
                            j.maxVolume_meanVolume = await get_volume_data_db_only_video(actualFin);
                        }

                        if (!isStillProcessing(j)) return;
                        if (quality == "720")
                        {
                            j.FixUpBitrates(quality, await get_video_bitrate(j.Rendered720File));
                            j.AboveChkbx7 = new AboveChbxRenderStatus().done;
                        }
                        if (quality == "480")
                        {
                            j.FixUpBitrates(quality, await get_video_bitrate(j.Rendered480File));
                            j.AboveChkbx4 = new AboveChbxRenderStatus().done;
                        }

                    }
                    catch (Exception ex)
                    {
                        if (quality == "720")
                            j.AboveChkbx7 = new AboveChbxRenderStatus().failed;
                        if (quality == "480")
                            j.AboveChkbx4 = new AboveChbxRenderStatus().failed;
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.InnerException);
                    }
                    finally
                    {
                        ProgBar_Jobs_Val += progBarUp;
                        Dispatcher.Invoke(() =>
                        {
                            TaskbarItemInfo.ProgressValue = ProgBar_Jobs_Val / 100;
                        });
                    }
                }

                bool isStillProcessing(AmqJob j)
                {
                    if (isProcessing == false)
                    {
                        isTerminated = true;
                        try
                        {
                            //j.DisableJob();
                            process2?.Kill();
                            process2?.Dispose();
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
                #endregion

                await Task.Run(async () =>
                {
                    int cnt = 0;
                    foreach (var j in JobsOBS)
                    {
                        if (j.JobFinished) continue;
                        if (j.Render720_ChkB) cnt++;
                        if (j.Render480_ChkB) cnt++;
                        if (j.RenderMP3_ChkB) cnt++;
                    }
                    progBarUp = Math.Round(100.0 / cnt, 2);

                    foreach (var j in JobsOBS)
                    {
                        if (j.JobFinished) continue;
                        // TODO .... what about the volume logic /o\

                        if (!isStillProcessing(j)) break;

                        #region 720
                        if (j.Render720_ChkB)
                            await doProcess("720", j, j.Crf720);
                        else
                            j.AboveChkbx7 = new AboveChbxRenderStatus().skipping;
                        j.AboveChkbx7.Freeze();
                        // todo stuff?
                        #endregion

                        if (!isStillProcessing(j)) break;

                        #region 480
                        if (j.Render480_ChkB)
                            await doProcess("480", j, j.Crf480);
                        else
                            j.AboveChkbx4 = new AboveChbxRenderStatus().skipping;
                        j.AboveChkbx4.Freeze();
                        #endregion

                        if (!isStillProcessing(j)) break;

                        #region mp3
                        if (j.RenderMP3_ChkB)
                        {
                            try
                            {
                                // TODO IN the check before check if the audio time is actually the same as the input
                                var outF = j.BuildFinalOutName("mp3", 0);
                                var bs = GetOrFixUpOutDirForThisJob(j);
                                var o1 = $"{bs}\\{outF}";

                                if (state.CurrentState == States.Clips)
                                {
                                    await Task.Delay(200);
                                    await Task.Run(() => LaunchProcessAndDisplayInfoInRTB("ffmpeg", make_audio_from_input_ss_to(j.Timestamps.startTime_SS, j.Timestamps.endTime_TO, j.ShowTitleFullPath, (j.UsedAudioId_Warning1Based - 1).ToString(), o1)));
                                }
                                if (j.FromState == States.Process)
                                {
                                    await Task.Run(() => LaunchProcessAndDisplayInfoInRTB("ffmpeg", make_audio_from_input(j.AudioReplacementPath, o1)));
                                }

                                if (!File.Exists(o1) || new FileInfo(o1).Length == 0)
                                {
                                    var er = File.Exists(o1) ? "file size is 0" : "file doesn't exist";
                                    throw new Exception($"Something went wrong. ({er}) ({o1})");
                                }

                                j.RenderedMP3File_VOL0 = o1;
                                if (j.VolumeChangeValue != 0)
                                    o1 = await make_new_changedvolume_media(j.VolumeChangeValue, o1);
                                j.RenderedMP3File = o1;
                                j.AboveChkbx3 = new AboveChbxRenderStatus().done;
                            }
                            catch (Exception ex)
                            {
                                j.AboveChkbx3 = new AboveChbxRenderStatus().failed;
                                Console.WriteLine(ex.Message);
                                Console.WriteLine(ex.InnerException);
                            }
                            finally
                            {
                                ProgBar_Jobs_Val += progBarUp;
                                Dispatcher.Invoke(() =>
                                {
                                    TaskbarItemInfo.ProgressValue = ProgBar_Jobs_Val / 100;
                                });
                            }
                        }
                        else { j.AboveChkbx3 = new AboveChbxRenderStatus().skipping; }
                        j.AboveChkbx3.Freeze();
                        // todo stuff?
                        #endregion

                        j.JobFinished = true;
                        Dispatcher.Invoke(() => UpdateJobsJsonFile());
                    }
                    ProgBar_Jobs_Val = 100;
                    Dispatcher.Invoke(() =>
                    {
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                        TaskbarItemInfo.ProgressValue = 1;
                    });

                });

                if (!isProcessing) isTerminated = true;
                if (isTerminated)
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Error;

                MessageBox.Show(Application.Current.MainWindow, "Job process has finished.", isTerminated ? "Terminated" : "Done", MessageBoxButton.OK, MessageBoxImage.Information);

                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                TaskbarItemInfo.ProgressValue = 0;

                foreach (var j in JobsOBS)
                    j.WeDone();

                UpdateJobsJsonFile();

                isProcessing = false;
                SwitchToMpv();
            }
        }

        private string GetOrFixUpOutDirForThisJob(AmqJob j)
        {
            var bs = config.conf.Folders.DefaultOutPath;
            if (!string.IsNullOrEmpty(j.OutFileSubDirectory) && j.OutFileSubDirectory != "outFileSubDirectory")
            {
                var sub = $"{config.conf.Folders.DefaultOutPath}\\{j.OutFileSubDirectory}";
                Directory.CreateDirectory(sub);
                return sub;
            }
            return bs;
        }

        private void IsFalseAllPropsJobs()
        {
            foreach (AmqJob item in JobsLV.Items)
            {
                item.SetAllToFalse();
            }
        }

        private void PrepareSubpathsForJobOutputs()
        {
            foreach (AmqJob item in JobsLV.Items)
            {
                item.SetAllToFalse();
            }
        }

        private StackPanel GetTopSPParent(FrameworkElement sp)
        {
            if (sp.Parent is DockPanel) return GetTopSPParent(sp.Parent as FrameworkElement); // for the duplicate btn
            if (sp.Parent == null) return sp as StackPanel;
            return GetTopSPParent(sp.Parent as StackPanel);
        }

        // The ID set inside the AmqJob (could not be the same as the current index!)
        private int MyID(FrameworkElement sender) => (int)GetTopSPParent(sender.Parent as FrameworkElement).Children.OfType<Label>().First().Content;

        // The index of the JobsOBS array
        private int MyIdxById(int id) => JobsOBS.IndexOf(JobsOBS.First(x => x.Id == id));


        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //int val = Convert.ToInt32(e.NewValue);
            double val = Convert.ToDouble(e.NewValue);
            Slider curSlid = sender as Slider;
            var tbx1 = (curSlid.Parent as StackPanel).Children.OfType<TextBlock>().First();
            var txt = $"{val}dB";
            if (val >= 0) txt = "+" + txt;
            tbx1.Text = txt;
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }

        private void ValidateCRF(object sender, TextChangedEventArgs e)
        {
            var tbx = (sender as TextBox);
            if (tbx.Text.Length > 2)
            {
                MessageBox.Show(Application.Current.MainWindow, "Crf may not be more than a 2 digit number (also try to keep it between 4-63 for VPX if you want the ffmpeg commands to work", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                tbx.Text = tbx.Text.Substring(0, 2);
                e.Handled = true;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = new string[] { "" };
                try
                {
                    files = (string[])e.Data.GetData(DataFormats.FileDrop);
                }
                catch
                {
                    MessageBox.Show(Application.Current.MainWindow, "Something went wrong at drag and drop. Maybe try loading the file with File > Open instead.", "Error!", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (state.CurrentState == States.Home)
                {
                    var res = MessageBox.Show(Application.Current.MainWindow, "Draggin in a file while on the home screen will send you to GET CLIPS mode.\n\n" +
                        "Do you wish to continue?", "Drag event", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.No) return;
                    enterGetClipsMode(files);
                    return;
                }
                if (state.CurrentState == States.Volume)
                {
                    var toCheck = new List<MultiInputWindow.VolumeCheckerFromRTB>();
                    foreach (var r in files)
                        toCheck.Add(new MultiInputWindow.VolumeCheckerFromRTB() { CatboxUrl = r });
                    toCheck = toCheck.GroupBy(o => new { o.CatboxUrl, o.SaveFileName }).Select(o => o.FirstOrDefault()).ToList();
                    CheckTheseMV?.Invoke(toCheck);
                    return;
                }
                allFiles = files;
                Player.LoadFile(files[0]);
                SetStatusBarText(files[0], SB.left);
                SetStatusBarText($"1/{allFiles.Length}", SB.right);
                Player.PlayIfPaused();
            }
            if (e.Data.GetDataPresent(DataFormats.Text))
            {

                var recv = e.Data.GetData(DataFormats.Text) as string;
                recv = checkIfActuallyUrl(recv);
                if (recv != "")
                {
                    Player.LoadFile(recv);
                    SetStatusBarText(recv, SB.left);
                    SetStatusBarText($"1/1", SB.right);
                    Player.PlayIfPaused();
                }
                else
                {
                    MessageBox.Show(Application.Current.MainWindow, "The player will not be able to play this url. Are you sure that you are not missing http or https at the start?", "Invalid url dropped?", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        private void TBX_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var tbx = (sender as TextBox);
            if (tbx.Name == "Volume_TBX_Intro")
            {
                if (tbx.Text == "Media web url") tbx.Text = "";
            }
        }
        #endregion

        #region Not needed rn
        //private void Go_Back_Click(object sender, RoutedEventArgs e)
        //{
        //    if ((sender as Button).Name == "_Edi_Go_Back")
        //    {
        //        var conf = MessageBox.Show(Application.Current.MainWindow, "Going back will discard any changes\nAre you sure you want to to back?", "Go back and discard changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
        //        if (conf == MessageBoxResult.No) return;
        //        ResetEditingMode(false);
        //        ImageE = null;
        //    }
        //    var tmp = state.CurrentState;
        //    state.CurrentState = previousState.CurrentState;
        //    ChangeView();
        //    previousState.CurrentState = tmp;
        //}        
        #endregion

        #region EnterModes        
        private bool enterVolumeMode()
        {
            previousState.CurrentState = state.CurrentState;
            state.CurrentState = States.Volume;
            ChangeView();
            return true;
        }

        // I guess this is a shortcut for now
        public void LoadVideoIntoPlayer(string path) => enterGetClipsMode(new string[] { path }, ActuallyDontChangeView: true);

        private bool enterGetClipsMode(string[] file_paths = null, bool ActuallyDontChangeView = false)
        {
            if (MPV_Clips.Visibility != Visibility.Visible)
                ToggleToMpv();
            string path;
            if (file_paths == null)
            {
                path = getMediaPathFromOfd();
                if (path == "</:?NoFileSelected*|>")
                {
                    return false;
                }
            }
            else
            {
                path = file_paths[0];
            }

            Player.LoadFile(path);
            SetStatusBarText(path, SB.left);
            allFiles = file_paths ?? new string[] { path };
            SetStatusBarText($"1/{allFiles.Length}", SB.right);
            EndTime = Player.endTimestamp.StrVal;
            Player.PlayPause();

            if (!ActuallyDontChangeView)
            {
                previousState.CurrentState = state.CurrentState;
                state.CurrentState = States.Clips;
                ChangeView(previousState.CurrentState == States.Home);
            }
            return true;
        }

        private void MpvPlayer_MediaError(object sender, string stackTrace)
        {
            MessageBox.Show(Application.Current.MainWindow, stackTrace, "Media error!", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MpvPlayer_PlayerLoaded(object sender, string type, object extra_payload = null)
        {
            StartTime = "00:00:00.000";
            EndTime = Player.endTimestamp.StrVal;
            if (time_len_txt_existed == "")
            {
                // this is here because it also works for "media loaded"
                PROCESS_TIME_T.Text = "TimeLen(-t)";
            }
            else
            {
                PROCESS_TIME_T.Text = time_len_txt_existed;
                time_len_txt_existed = "";
            }
            playerLoaded = true;
        }

        private void MpvPlayer_GetJumpTime(object sender, string jumpType, string timeStr)
        {
            if (jumpType == "Start") GET_CLIPS_START.Text = timeStr;
            if (state.CurrentState == States.Clips)
            {
                if (jumpType == "End") GET_CLIPS_END.Text = timeStr;
            }
        }

        public static event MultiInputWindow.CheckTheseDelegate CheckTheseMV;
        private void MpvDisplay_GetDataFromMpvDisplay(object sender, string event_type, object extra_payload)
        {
            if (event_type == "MouseDownR") Player.PlayPause();
            if (event_type == "MouseWheelDown") Player.ChangeVolume(-5);
            if (event_type == "MouseWheelUp") Player.ChangeVolume(5);

            if (event_type == "DropFile")
            {
                if (extra_payload == null)
                {
                    MessageBox.Show(Application.Current.MainWindow, "Something went wrong at drag and drop. Maybe try loading the file with File > Open instead.", "Error!", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var recv = extra_payload as string[];
                allFiles = recv;
                if (state.CurrentState == States.Volume)
                {
                    var toCheck = new List<MultiInputWindow.VolumeCheckerFromRTB>();
                    foreach (var r in recv)
                        toCheck.Add(new MultiInputWindow.VolumeCheckerFromRTB() { CatboxUrl = r });
                    toCheck = toCheck.GroupBy(o => new { o.CatboxUrl, o.SaveFileName }).Select(o => o.FirstOrDefault()).ToList();
                    CheckTheseMV?.Invoke(toCheck);
                    return;
                }
                Player.LoadFile(recv[0]);
                SetStatusBarText(recv[0], SB.left);
                SetStatusBarText($"1/{allFiles.Length}", SB.right);
                Player.PlayIfPaused();
            }
            if (event_type == "DropText")
            {
                if (extra_payload == null)
                {
                    MessageBox.Show(Application.Current.MainWindow, "Something went wrong at drag and drop. Maybe try loading the file with File > Open instead.", "Error!", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var recv = extra_payload as string;
                recv = checkIfActuallyUrl(recv);
                if (recv != "")
                {
                    if (state.CurrentState == States.Volume)
                    {
                        var toCheck = new List<MultiInputWindow.VolumeCheckerFromRTB>();
                        toCheck.Add(new MultiInputWindow.VolumeCheckerFromRTB() { CatboxUrl = recv });
                        CheckTheseMV?.Invoke(toCheck);
                        return;
                    }
                    Player.LoadFile(recv);
                    SetStatusBarText(recv, SB.left);
                    SetStatusBarText($"1/1", SB.right);
                    Player.PlayIfPaused();
                }
                else
                {
                    MessageBox.Show(Application.Current.MainWindow, "The player will not be able to play this url. Are you sure that you are not missing http(s) at the start?", "Invalid url dropped?", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

            }
            if (event_type == "DoubleClick") TogglePlayerFullscreen();
        }

        private bool enterProcessMode()
        {

            previousState.CurrentState = state.CurrentState;
            state.CurrentState = States.Process;
            ChangeView();
            return true;
        }
        #endregion

        #region Keybindings
        public void setupKeybinds()
        {
            var kbs = config.conf.Keybindings;

            SetKeybinds.Add(getKeybindPair(kbs.OrganizingModeDynamic));
            SetKeybinds.Add(getKeybindPair(kbs.OrganizingMode));
            SetKeybinds.Add(getKeybindPair(kbs.EditMode));
            SetKeybinds.Add(getKeybindPair(kbs.AdvancedMode));
            SetKeybinds.Add(getKeybindPair(kbs.HomeMode));
            SetKeybinds.Add(getKeybindPair(kbs.GlobalKeybinds));
        }

        private Dictionary<string, Tuple<Key, string>> getKeybindPair(HotkeyProps[] props)
        {
            var ret = new Dictionary<string, Tuple<Key, string>>();
            if (props == null) return ret;
            foreach (var prop in props)
            {
                Key key;
                if (!Enum.TryParse(prop.Key, out key))
                {
                    Console.WriteLine($"Could not parse: {prop.ActionName} | {prop.Key} | {prop.Mods}");
                }
                List<ModifierKeys> mods = new List<ModifierKeys>();
                ret.Add(prop.ActionName, new Tuple<Key, string>(key, prop.Mods));
            }
            return ret;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // in case you were to to anything before KeyDown
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //// No keybind capturing in advanced mode because of typing
            //if (state.CurrentState == States.Process) return;

            // ST: Home-0, Organize-1, Edit-2, Advanced-3
            // KB: Dynamic-0, Organizing-1, Edit-2, Adv-3, Home-4, Global-5
            var priority = new List<int>() { 5, (int)state.CurrentState };
            if (priority.Contains(0)) priority = new List<int>() { 5, 4 };
            if (priority.Contains(1)) priority = new List<int>() { 5, 1, 0 };
            foreach (int p in priority)
            {
                foreach (var kb in SetKeybinds[p])
                {
                    bool re = Keyboard.IsKeyDown(Key.E);
                    var m = kb.Value.Item2; // 011 for control alt for example
                    bool modsBool = (!(Keyboard.IsKeyDown(Key.LeftShift) ^ m[0] == '1') &&
                        !(Keyboard.IsKeyDown(Key.LeftCtrl) ^ m[1] == '1') &&
                        !(Keyboard.IsKeyDown(Key.LeftAlt) ^ m[2] == '1'));
                    bool anyMod = ((Keyboard.IsKeyDown(Key.LeftShift) || (Keyboard.IsKeyDown(Key.LeftAlt) || (Keyboard.IsKeyDown(Key.LeftCtrl)))));
                    if (m == "000" && anyMod) continue;
                    if (Keyboard.IsKeyDown(kb.Value.Item1) && (m == "000" || modsBool))
                    {
                        // if(kb.Key == "Something") { e.Handled = true; SOMETHING; return; }
                        if (p == 0)
                        {
                            if (HandleDynamicKB(kb.Key))
                            {
                                e.Handled = true;
                                return;
                            }
                        }
                        if (kb.Key == "Keybind settings") { e.Handled = true; _Keybindings_MI.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); return; }
                        if (kb.Key == "Toggle text box focus clear") { e.Handled = true; _Dont_clear_textboxes.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); return; }
                        if (kb.Key == "Toggle fullscreen") { e.Handled = true; GoFullScreen(); return; }
                        //if (kb.Key == "Go Home") { e.Handled = true; _GoHome_Menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); return; }
                        if (Player != null && state.CurrentState != States.Home)
                        {
                            if (kb.Key == "Set left timestamp marker") { e.Handled = true; GET_CLIPS_START.Text = Player.GetPosition(); return; }
                            if (kb.Key == "Set right timestamp marker") { e.Handled = true; GET_CLIPS_END.Text = Player.GetPosition(); ; return; }
                            if (kb.Key == "Pause or play") { e.Handled = true; Player.PlayPause(); return; }
                            if (kb.Key == "Play next file") { e.Handled = true; StartPlayingFromPlaylist(SB.right); return; }
                            if (kb.Key == "Play previous file") { e.Handled = true; StartPlayingFromPlaylist(SB.left); return; }

                            if (kb.Key == "Go back one frame") { e.Handled = true; Player.PauseIfPlaying(); Player._player.PreviousFrame(); return; }
                            if (kb.Key == "Go forward one frame") { e.Handled = true; Player.PauseIfPlaying(); Player._player.NextFrame(); return; }
                            if (kb.Key == "Go back one second") { e.Handled = true; Player.MoveBySeconds(-1); return; }
                            if (kb.Key == "Go forward one second") { e.Handled = true; Player.MoveBySeconds(1); return; }
                            if (kb.Key == "Go back five seconds") { e.Handled = true; Player.MoveBySeconds(-5); return; }
                            if (kb.Key == "Go forward five seconds") { e.Handled = true; Player.MoveBySeconds(5); return; }
                        }
                    }
                }
            }
        }

        private bool HandleDynamicKB(string action)
        {
            return false; // not supported right now

            //if (_Org_Grid_GRIDVIEW.Visibility == Visibility.Visible) return false;
            //var newLoc = action.Substring(8);
            //newLoc = $"{newLoc}\\{PicHandler.CurrentImage.imageSrc.Split('\\').Last()}";
            //if (PicHandler.CurrentImage.imageSrc == newLoc) return false;
            //PicHandler.cleanFstreams();
            //File.Move(PicHandler.CurrentImage.imageSrc, newLoc);
            //PicHandler.CurrentImage.imageSrc = newLoc;
            //SetStatusBarText(newLoc);
            //return true;
        }

        #endregion

        #region Task Debouncer
        private Task _debounceTask = Task.CompletedTask;
        private volatile Action _debounceAction;
        private async void DebounceAction(Action act, int delay = 500)
        {
            _debounceAction = act;
            await _debounceTask;

            await Task.Delay(50);
            if (_debounceAction == act)
            {
                _debounceTask = Task.Delay(delay);
                act();
            }
        }
        #endregion

        #region General Functions

        public static string GetRandomString(int len)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, len)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// This will check all completed and uncompleted jobs.
        /// </summary>
        private void ReCheckJobs()
        {
            foreach (var j in JobsOBS)
                j.ReCheck();
            UpdateJobsJsonFile();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns>isValid, thisDoesnExistPathString, theActualOneCuzItCanBeMultipleFormats</returns>
        public Tuple<bool, string, string> isValidAudioPath(string path)
        {
            bool isOk = true;
            var pathMaybe = $"{config.conf.Folders.FromVegasFlacPath}\\{path}";

            string[] possibleAudioPaths = new string[]
            {
                        $"{pathMaybe}.flac",
                        $"{pathMaybe}.mp3"
            };
            var audioPath = possibleAudioPaths.FirstOrDefault(File.Exists);
            if (audioPath == null)
            {
                isOk = false;
            }
            return new Tuple<bool, string, string>(isOk, pathMaybe, audioPath);
        }

        private void UpdateJobsJsonFile()
        {
            var jsonString = JsonConvert.SerializeObject(JobsOBS);
            File.WriteAllText("jobs.json", jsonString);
        }

        private void BackupJobsJsonFile(string t)
        {
            if (!Directory.Exists(config.conf.Folders.DefaultOutPath))
            {
                MessageBox.Show(Application.Current.MainWindow, $"{config.conf.Folders.DefaultOutPath}\n\nDefault out directory doesn't eixst anymore? Please fix that.");
                return;
            }
            var bkupDir = $"{config.conf.Folders.DefaultOutPath}\\backupData";
            if (!Directory.Exists(bkupDir))
                new DirectoryInfo(config.conf.Folders.DefaultOutPath).CreateSubdirectory("backupData");
            var path = $"{bkupDir}\\{t}_jobsBackup.json";
            // TODO: Import button for these auto backups
            var jsonString = JsonConvert.SerializeObject(JobsOBS);
            File.WriteAllText(path, jsonString);
        }

        private void LogWhichTimestampWhatWasDone(string t, string toAdd)
        {
            if (!Directory.Exists(config.conf.Folders.DefaultOutPath))
            {
                MessageBox.Show(Application.Current.MainWindow, $"{config.conf.Folders.DefaultOutPath}\n\nDefault out directory doesn't eixst anymore? Please fix that.");
                return;
            }
            var bkupDir = $"{config.conf.Folders.DefaultOutPath}\\backupData";
            if (!Directory.Exists(bkupDir))
                new DirectoryInfo(config.conf.Folders.DefaultOutPath).CreateSubdirectory("backupData");
            var path = $"{bkupDir}\\_timestampJobsDetailsMap.txt";
            File.AppendAllText(path, $"\n\n------{t}------\n{toAdd}");
        }

        public static string checkIfActuallyUrl(string maybeUrl)
        {
            Uri uriResult;
            bool result = Uri.TryCreate(maybeUrl, UriKind.Absolute, out uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            if (uriResult == null) return "";
            return uriResult.AbsoluteUri;
        }

        public static string getTitleFromAnimePath(string path)
        {
            var lastPart = path.Split('\\').LastOrDefault().Split('/').LastOrDefault();
            string pattern = @"(?:^\[.*?\]\s?|^)(?<Title>.*?)\.([^.]*)$";
            var m = Regex.Match(lastPart, pattern).Groups["Title"].Value;
            return m;
        }
        private void StartPlayingFromPlaylist(SB previousOrNext)
        {
            var files = allFiles.ToList();
            var curIdx = files.IndexOf(Player.PlayingFilePath);
            if (curIdx == -1) throw new Exception("For some reason the playing file is not in the list");
            int newIdx = curIdx + (previousOrNext == SB.right ? 1 : -1);
            if (newIdx < 0) return;
            if (newIdx > (files.Count - 1)) return;
            Player.LoadFile(files[newIdx]);
            SetStatusBarText(files[newIdx], SB.left);
            SetStatusBarText($"{newIdx + 1}/{allFiles.Length}", SB.right);
            Player.PlayIfPaused();
        }

        public enum SB { left, right }
        public void SetStatusBarText(string text, SB left_right = SB.left)
        {
            if (left_right == SB.left) _StatusBar_Left.Text = text;
            if (left_right == SB.right) _StatusBar_Right.Text = text;
        }

        public delegate void SetSB_Delegate(string text, SB l_r);
        public void SetStatus_Dispatch(string text, SB l_r, SetSB_Delegate f) => Dispatcher.Invoke(() => f(text, l_r));

        private void TogglePlayerFullscreen()// broken af rn...
        {
            if (ResizeMode != ResizeMode.NoResize)
            {
                // lets back these up while not in fullscreen
                DisplayWidthBackup = Display.Width;
                DisplayHeightBackup = Display.Height;
            }
            GoFullScreen(); // todo: this isn't fail proof if the user messes around with F11!!!
            if (ResizeMode == ResizeMode.NoResize)
            {
                // we have entered fullscreen
                Display.Width = Width;
                Display.Height = Height;
            }
            else
            {
                Display.Width = DisplayWidthBackup;
                Display.Height = DisplayHeightBackup;
            }
        }

        private void GoFullScreen()
        {
            if (WindowState == WindowState.Maximized && ResizeMode == ResizeMode.NoResize)
            {
                // from fullscreen
                ResizeMode = ResizeMode.CanResize;
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
            }
            else if (WindowState != WindowState.Maximized && ResizeMode != ResizeMode.NoResize)
            {
                // to fullscreen
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
            else if (WindowState == WindowState.Maximized && ResizeMode != ResizeMode.NoResize)
            {
                // to fullscreen2
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
        }
        #endregion   

        #region Process stuff

        /// <summary>
        /// Don't forget SwitchToConsole() call before calling this!
        /// </summary>
        /// <param name="exec_name"></param>
        /// <param name="args"></param>
        void LaunchProcessAndDisplayInfoInRTB(string exec_name, string args)
        {
            process2 = new Process();
            process2.EnableRaisingEvents = true;
            process2.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
            process2.ErrorDataReceived += new DataReceivedEventHandler(process_ErrorDataReceived);
            process2.Exited += new System.EventHandler(process_Exited);

            process2.StartInfo.FileName = config.conf.Execs.First(x => x.Name == exec_name).Path;
            process2.StartInfo.Arguments = args;
            process2.StartInfo.UseShellExecute = false;
            process2.StartInfo.CreateNoWindow = true;
            process2.StartInfo.RedirectStandardOutput = true;
            process2.StartInfo.RedirectStandardError = true;

            process2.Start();
            process2.BeginOutputReadLine();
            process2.BeginErrorReadLine(); // for some reason ffmpeg likes to output to error? :thinking:

            //below line is optional if we want a blocking call
            process2.WaitForExit();
            process2 = null;
        }

        void process_Exited(object sender, EventArgs e)
        {
            if (window_closing) return;
            doOutput($"process exited with code {process2.ExitCode}\n");
        }

        void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            //Console.WriteLine(e.Data + "\n");
            //Console.WriteLine("BBB");
            if (e.Data == null) return;
            doOutput(e.Data, 2);
        }

        void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            //Console.WriteLine(e.Data + "\n");
            //Console.WriteLine("AAA");
            if (e.Data == null) return;
            doOutput(e.Data, 2);
        }

        public void doOutput(string txt, int undoTimes = 0)
        {
            Dispatcher.Invoke(() =>
            {
                if (txt.StartsWith("frame=") || txt.StartsWith("size="))
                {
                    for (int i = 0; i < undoTimes; i++)
                    {
                        outputBox.Undo();
                    }
                }
                outputBox.AppendText(txt);
                outputBox.AppendText("\u2028");
                outputBox.ScrollToEnd();
            });
        }

        public static string processRetStr = "";
        /// <summary>
        /// Use Task.Run for async mode
        /// </summary>
        public static string RunCommandAndGetOutput(string exec_name, string args)
        {
            //* Create your Process
            processRetStr = "";
            var process = new Process();
            process.EnableRaisingEvents = true;
            process.StartInfo.FileName = config.conf.Execs.First(x => x.Name == exec_name).Path; ;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            //* Set ONLY ONE handler here.
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorOutputHandler);
            //* Start process
            process.Start();
            processList.Add(process);
            //* Read one element asynchronously
            process.BeginErrorReadLine();
            //* Read the other one synchronously
            string output = process.StandardOutput.ReadToEnd(); // this doesn't really get anything
            Console.WriteLine(output); //ig.. in case it would give anything
            process.WaitForExit();
            //processList.Find(x => x.Id == process.Id)?.Dispose();
            try
            {
                processList.RemoveAll(x => x.HasExited);
            }
            catch { }
            return processRetStr;
        }

        static void ErrorOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            //* Do your stuff with the output (write to console/log/StringBuilder)
            //Console.WriteLine(outLine.Data);
            if (outLine.Data == null) return;
            processRetStr += (outLine.Data.ToString() + "\n");
        }

        #endregion

        #region Jobs Related
        public bool AnyJobsToProcess()
        {
            foreach (var job in JobsOBS)
                if (!job.JobFinished)
                    return true;
            return false;
        }

        public void ToggleToConsole()
        {
            Player?.PauseIfPlaying();
            MPV_Clips.Visibility = Visibility.Collapsed;
            _CONSOLE_GRID.Visibility = Visibility.Visible;
            isOnConsole = true;
        }

        public void ToggleToMpv()
        {
            outputBox.Document.Blocks.Clear();
            MPV_Clips.Visibility = Visibility.Visible;
            _CONSOLE_GRID.Visibility = Visibility.Collapsed;
            isOnConsole = false;
        }

        public void SwitchToConsole()
        {
            //Navigation
            foreach (var item in _Mode_Menu.Items.OfType<MenuItem>()) item.IsEnabled = false;

            //Player
            Player?.PauseIfPlaying();
            // Player.Stop(); No need to remove the video actually

            //Grids
            MPV_Clips.Visibility = Visibility.Collapsed;
            _CONSOLE_GRID.Visibility = Visibility.Visible;
            isOnConsole = true;

            //Bottom left options
            BL_JobsRelated_.Visibility = Visibility.Collapsed;

            //Buttons
            STOP_JOBS.Visibility = Visibility.Visible;
            CLIPS_START_JOBS.Visibility = Visibility.Collapsed;
            CLEAR_ALL_JOBS.Visibility = Visibility.Collapsed;

            PROG_BAR_JOBS_GRID.Visibility = Visibility.Visible;
        }

        public void SwitchToMpv()
        {
            outputBox.Document.Blocks.Clear();
            //Navigation
            foreach (var item in _Mode_Menu.Items.OfType<MenuItem>()) item.IsEnabled = true;

            //Player (nothing to do here in this case)

            //Grids
            MPV_Clips.Visibility = Visibility.Visible;
            _CONSOLE_GRID.Visibility = Visibility.Collapsed;
            isOnConsole = false;

            //Hide this one
            BL_JobsRelated_.Visibility = Visibility.Visible;

            //Buttons
            STOP_JOBS.Visibility = Visibility.Collapsed;
            CLIPS_START_JOBS.Visibility = Visibility.Visible;
            CLEAR_ALL_JOBS.Visibility = Visibility.Visible;

            PROG_BAR_JOBS_GRID.Visibility = Visibility.Collapsed;
        }

        private async void MPV_ADD_JOB_GET_CLIPS_Click(object sender, RoutedEventArgs e)
        {
            var theTime = PROCESS_TIME_T.Text;
            try
            {
                MPV_ADD_JOB_GET_CLIPS.IsEnabled = false;
                MPV_ADD_JOB_GET_CLIPS.Content = "Adding...";
                MPV_ADD_JOB_GET_CLIPS.Opacity = 0.5;

                if (GetClipsOutFileTbx == "outFileNameHere")
                {
                    MessageBox.Show(Application.Current.MainWindow, "You are not allowed to use 'outFileNameHere' as the out file", "Invalid outfile name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string audioReplacement = "";
                if (state.CurrentState == States.Process)
                {

                    if (theTime == "TimeLen(-t)")
                    {
                        var ret = MessageBox.Show(Application.Current.MainWindow, "Do you wish to just use the entire audio length, meaning that you are replacing audio on an already rendered webm?", "You forgot to set the TimeLen(-t)", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                        if (ret == MessageBoxResult.Yes)
                        {
                            theTime = "59:59.5959";
                        }
                        else
                        {
                            return;
                        }
                        //MessageBox.Show(Application.Current.MainWindow, "You forgot to set the TimeLen(-t)", "Missing input", MessageBoxButton.OK, MessageBoxImage.Warning);
                        //return;
                    }

                    TimeSpan ts;
                    var tt = theTime.TrimStart('0').TrimStart(':');
                    if (tt.Count(c => c == ':') == 1) tt = $"0:{tt}";
                    else if (tt.Count(c => c == ':') == 0) tt = $"0:0:{tt}";

                    if (!TimeSpan.TryParse(tt, out ts))
                    {
                        MessageBox.Show(Application.Current.MainWindow, "Invalid time in the second textbox (-t)", "TimeSpan TryParse error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    Player.startTimestamp.UpdateTime(GET_CLIPS_START.Text);
                    Player.endTimestamp.Time = TimeSpan.FromMilliseconds(Player.startTimestamp.Time.TotalMilliseconds + ts.TotalMilliseconds);


                    if (Player.endTimestamp.Time.TotalMilliseconds > Player._player.Duration.TotalMilliseconds && Player._player.Duration != TimeSpan.FromSeconds(0) && I_Know_the_hint.Text != "Path is valid.")
                    {
                        Player.endTimestamp.Time = Player._player.Duration;
                        MessageBox.Show(Application.Current.MainWindow, "This duration plus the start time goes over the media duration.", "Invalid -t", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var pathMaybe = $"{config.conf.Folders.FromVegasFlacPath}\\{GetClipsOutFileTbx}";

                    string[] possibleAudioPaths = new string[]
                    {
                        $"{pathMaybe}.flac",
                        $"{pathMaybe}.mp3"
                    };
                    var audioPath = possibleAudioPaths.FirstOrDefault(File.Exists);
                    if (audioPath != null)
                    {
                        audioReplacement = audioPath;
                    }
                    else
                    {
                        MessageBox.Show(Application.Current.MainWindow, $"The file {pathMaybe}.[flac|mp3] does not exist. Did you setup your audio file locations? (See the Edit context menu)", "Invalid outfile audio path", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (state.CurrentState == States.Clips && Player.endTimestamp.Time.TotalMilliseconds > Player._player.Duration.TotalMilliseconds)
                {
                    MessageBox.Show(Application.Current.MainWindow, $"End timestamp is greater than the player length!!\n\nPlayer len time: {Player._player.Duration.ToString()}\nEnd time: {Player.endTimestamp.StrVal}", "Can not add job.", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (Player.endTimestamp.Time.TotalMilliseconds <= Player.startTimestamp.Time.TotalMilliseconds)
                {
                    MessageBox.Show(Application.Current.MainWindow, $"End time is not greater than start time!\n\nStart time: {Player.startTimestamp.StrVal}\nEnd time: {Player.endTimestamp.StrVal}", "Can not add job.", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (Player.startTimestamp.Time.TotalSeconds < 0 || Player.endTimestamp.Time.TotalSeconds < 0)
                {
                    MessageBox.Show(Application.Current.MainWindow, $"The times can not be negative!\n\nStart time: {Player.startTimestamp.StrVal}\nEnd time: {Player.endTimestamp.StrVal}", "Can not add job.", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                long aid = 0;
                long sid = 0;
                try
                {
                    if (Player.aid.Count > 0)
                        aid = Player._player.API.GetPropertyLong("aid") - 1; // first track will be 1 for reason in this api, so we -1
                    //if (Player.sid.Count > 0)
                    //    sid = Player._player.API.GetPropertyLong("sid"); // subs index seems to working normally
                    // actually, we don't care about subs
                }
                catch (Exception ex)
                {
                    if (Player._player.Duration == TimeSpan.FromSeconds(0))
                    {
                        // Maybe the media isn't loaded

                    }
                    else
                    {
                        // Something else weird happened
                        throw ex;
                    }
                }


                string inPath = Player.PlayingFilePath;

                if (state.CurrentState == States.Process && I_Know_the_hint.Text == "Path is valid.")
                {
                    inPath = I_Know_the_process_input_file_name.Text;
                }

                if (inPath == "")
                {
                    MessageBox.Show(Application.Current.MainWindow, "Please load a video or input the path if you're in process mode", "No media source", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }


                var tmpOut = $"{tmpPath}\\__TMP_ONLY_FOR_QUICK_VOLUME_CHECK.mp4";
                string _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", temp_mp4_ss_to(ss: Player.startTimestamp.StrVal, to: Player.endTimestamp.StrVal, _in: inPath, aid: aid.ToString(), _out: tmpOut)));

                var volData = await get_bitrate_and_volume_data_video(tmpOut);
                File.Delete(tmpOut);
                var titleShorter = getTitleFromAnimePath(inPath);
                if (titleShorter.Length > 35) { titleShorter = $"{titleShorter.Substring(0, 32)}..."; }

                var outFname = string.IsNullOrEmpty(GetClipsOutFileTbx) ? "outFileNameHere" : GetClipsOutFileTbx;
                // Test if this outFaleNameHere tbx text is valid
                var isInvalid = PathHelper.PossiblyInvalid($"{config.conf.Folders.DefaultOutPath}\\{outFname}.webm");
                if (isInvalid != "")
                {
                    MessageBox.Show(Application.Current.MainWindow, isInvalid + "\n\n" + outFname, "Invalid out file path", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (GetClipsOutFileTbx.Contains('_'))
                {
                    MessageBox.Show(Application.Current.MainWindow, GetClipsOutFileTbx + "\n\n" + "Can not use the character \"_\" (underscore) in the out file name.", "Illegal character", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int id = JobsOBS.Count;
                for (; JobsOBS.FirstOrDefault(x => x.Id == id) != null; ++id) { }

                var job = new AmqJob(id)
                {
                    OutFileName = outFname,
                    ShowTitleFullPath = inPath,
                    FromState = state.CurrentState,
                    Bitrates = $"[{volData.Item1} | No 720p | No 480p ] kb/s",
                    UsedAudioId_Warning1Based = aid + 1, //do -1 if you will use these in ffmpeg commands (the +1 is for the api!!)
                    UsedSubsId_Warning0Based = sid,
                    ShowTitle = titleShorter
                };

                if (state.CurrentState == States.Clips)
                {
                    job.ShowTitle = $"[C] {job.ShowTitle}";
                    job.ShowTitleToolTip = inPath;
                    job.__GetVolumeBTN = true; // added this here too ig
                    job.maxVolume_meanVolume = $"({volData.Item2} | {volData.Item3}) dB";
                    job.Timestamps = new JobTimeStamps(Player.startTimestamp.StrVal, Player.endTimestamp.StrVal, "");
                }

                if (state.CurrentState == States.Process)
                {
                    var volDataAudio = await get_volume_data_audio(audioReplacement);
                    job.Timestamps = new JobTimeStamps(Player.startTimestamp.StrVal, Player.endTimestamp.StrVal, theTime);
                    job.ShowTitle = $"[P] {job.ShowTitle}";
                    job.AudioReplacementPath = audioReplacement;
                    job.ShowTitleToolTip = $"VIDEO: {inPath}  +   AUDIO: {audioReplacement}";
                    job.maxVolume_meanVolume = $"[{volDataAudio.Item1} | {volDataAudio.Item2}] dB";
                    job.__GetVolumeBTN = true;
                    job.__VolumeEditSlider = false; // useless in process mode, edit the volume in vegas!
                }
                var t_ = TimeSpan.FromMilliseconds(Player.startTimestamp.Time.TotalMilliseconds - Player.endTimestamp.Time.TotalMilliseconds).ToString();
                var t = t_.TrimEnd('0');
                if (!t_.Contains(".")) t = t_;
                job.ShowTitleToolTip += $"  SS: {Player.startTimestamp.StrVal}  TO: {Player.endTimestamp.StrVal}  T: {t}";

                JobsOBS.Add(job);

                //Backup jobs
                UpdateJobsJsonFile();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow, $"Something went wrong when adding a job!\n\n{ex.Message}\n\n{ex.InnerException}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException);
            }
            finally
            {
                MPV_ADD_JOB_GET_CLIPS.IsEnabled = true;
                MPV_ADD_JOB_GET_CLIPS.Content = "Add job";
                MPV_ADD_JOB_GET_CLIPS.Opacity = 1;
            }
        }

        #endregion

        #region ffmpeg arg builder
        public static string volume_check(string _in) => config.conf.Extensions[0].Usage[0].Call.Replace("[:in]", _in);
        public static string volume_and_silence_check(string _in) => config.conf.Extensions[0].Usage[1].Call.Replace("[:in]", _in);
        public static string opus_bitrate_check(string _in, string _out) => config.conf.Extensions[0].Usage[2].Call.Replace("[:in]", _in).Replace("[:out]", _out);
        public static string temp_mp4_ss_to(string ss, string to, string _in, string aid, string _out) => config.conf.Extensions[1].Usage[0].Call.Replace("[:ss]", ss).Replace("[:to]", to).Replace("[:in]", _in).Replace("[:aid]", aid).Replace("[:out]", _out);
        public static string temp_mp4_ss_t(string ss, string t, string _in, string aid, string _out) => config.conf.Extensions[1].Usage[1].Call.Replace("[:ss]", ss).Replace("[:t]", t).Replace("[:in]", _in).Replace("[:aid]", aid).Replace("[:out]", _out);
        public static string temp_mp4_ss(string ss, string _in, string aid, string _out) => config.conf.Extensions[1].Usage[1].Call.Replace("[:ss]", ss).Replace("[:in]", _in).Replace("[:aid]", aid).Replace("[:out]", _out);
        public static string render_gcJust1(string ss, string to, string _in, string aid, string crf, string width, string _out) => config.conf.Extensions[2].Usage[0].Call.Replace("[:ss]", ss).Replace("[:to]", to).Replace("[:in]", _in).Replace("[:aid]", aid).Replace("[:crf]", crf).Replace("[:width]", width).Replace("[:out]", _out);
        public static string render_gcP1(string ss, string to, string _in, string aid, string crf, string width, string _out) => config.conf.Extensions[2].Usage[1].Call.Replace("[:ss]", ss).Replace("[:to]", to).Replace("[:in]", _in).Replace("[:aid]", aid).Replace("[:crf]", crf).Replace("[:width]", width).Replace("[:out]", _out);
        public static string render_gcP2(string ss, string to, string _in, string aid, string crf, string width, string _out) => config.conf.Extensions[2].Usage[2].Call.Replace("[:ss]", ss).Replace("[:to]", to).Replace("[:in]", _in).Replace("[:aid]", aid).Replace("[:crf]", crf).Replace("[:width]", width).Replace("[:out]", _out);
        //public static string render_time_length(string ss, string t, string _in, string crf, string width, string _out) => config.conf.Extensions[2].Usage[3].Call.Replace("[:ss]", ss).Replace("[:t]", t).Replace("[:in]", _in).Replace("[:crf]", crf).Replace("[:width]", width).Replace("[:out]", _out);
        public static string put_flac_on_vid(string vid_path, string flac_path, string _out) => config.conf.Extensions[2].Usage[3].Call.Replace("[:i_Vid]", vid_path).Replace("[:i_Flac]", flac_path).Replace("[:out]", _out);
        public static string make_audio_from_input(string _in, string _out) => config.conf.Extensions[3].Usage[0].Call.Replace("[:in]", _in).Replace("[:out]", _out);
        public static string make_audio_from_input_ss_to(string ss, string to, string _in, string aid, string _out) => config.conf.Extensions[3].Usage[1].Call.Replace("[:in]", _in).Replace("[:out]", _out).Replace("[:ss]", ss).Replace("[:to]", to).Replace("[:aid]", aid);
        public static string mkv_to_mp4_for_vegas(string _in, string _out) => config.conf.Extensions[4].Usage[0].Call.Replace("[:in]", _in).Replace("[:out]", _out);
        public static string mkv_to_mp4_for_vegas_ss(string _in, string _out, string ss) => config.conf.Extensions[4].Usage[1].Call.Replace("[:in]", _in).Replace("[:out]", _out).Replace("[:ss]", ss);
        public static string mkv_to_mp4_for_vegas_ss_to(string _in, string _out, string ss, string to) => config.conf.Extensions[4].Usage[2].Call.Replace("[:in]", _in).Replace("[:out]", _out).Replace("[:ss]", ss).Replace("[:to]", to);
        public static string volume_edit(string _in, string _out, string volume_change_plus_minus) => config.conf.Extensions[5].Usage[0].Call.Replace("[:in]", _in).Replace("[:out]", _out).Replace("[:volChg]", volume_change_plus_minus);
        public static string volume_edit_audio(string _in, string _out, string volume_change_plus_minus) => config.conf.Extensions[5].Usage[1].Call.Replace("[:in]", _in).Replace("[:out]", _out).Replace("[:volChg]", volume_change_plus_minus);
        #endregion

        #region ffmpeg command executions helpers etc.

        public static async Task<string> get_volume_data_db_only_video(string inPath)
        {
            var volData = await get_bitrate_and_volume_data_video(inPath);
            return $"[{volData.Item2} | {volData.Item3}] dB";
        }

        public static async Task<string> get_volume_data_db_only_audio(string inPath)
        {
            var volData = await get_volume_data_audio(inPath);
            return $"[{volData.Item1} | {volData.Item2}] dB";
        }

        public struct VideoInfo
        {
            public string bitrate;
            public string venc;
            public string fps;
            public string aenc;
            public string mean;
            public string max;
            public string encoderOpusBitrate;
            public string detailed;

            public VideoInfo(string bitrate, string venc, string fps, string aenc, string mean, string max, string encoderOpusBitrate, string detailed = "")
            {
                this.bitrate = bitrate;
                this.venc = venc;
                this.fps = fps;
                this.aenc = aenc;
                this.mean = mean;
                this.max = max;
                this.encoderOpusBitrate = encoderOpusBitrate;
                this.detailed = detailed;
            }
        }

        public delegate void UpdateProgBarDelegate(DownloadProgressChangedEventArgs e, int jobId);
        public static event UpdateProgBarDelegate UpdateProgBar;

        public async static Task<VideoInfo> get_all_video_info(string inPath, int jobId = -1)
        {
            bool delete_after = false;
            var ext = inPath.Split('.').Last();
            if (!File.Exists(inPath))
            {
                delete_after = true;
                // link I guess
                // need to save cuz two ffmpeg commands will be executed
                using (var client = new WebClient())
                {
                    client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko");
                    var path = $"{config.conf.Folders.TmpPath}\\{GetRandomString(20)}.{ext}";
                    try
                    {
                        bool isDownloaded = false;
                        client.DownloadFileCompleted += (s, e) => isDownloaded = true;
                        client.DownloadProgressChanged += (s, e) => UpdateProgBar?.Invoke(e, jobId);
                        await Task.Run(() => { try { client.DownloadFileAsync(new Uri(inPath), path); } catch { } });
                        await Task.Run(async () => { while (!isDownloaded) { await Task.Delay(250); } });
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    inPath = path;
                    if (!File.Exists(inPath))
                    {
                        MessageBox.Show(Application.Current.MainWindow, "Something went wrong with checking this", "File missing - download failed");
                        return new VideoInfo("x", "x", "x", "??", "??", "??", "??", "download failed");
                    }
                }
            }

            string volumeData = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_and_silence_check(inPath)));

            //string pattern = @"bitrate:\s(\d+\skb\/s).*?Video:\s(.*?)\s.*?DAR\s\d+:\d+.*?\s(.*?)\sfps,.*?Audio:\s(.*?),\s.*?mean_volume:\s(.*?)\sdB.*?max_volume:\s(.*?)\sdB";
            string pattern = @"bitrate:\s(\d+\skb\/s).*?Video:\s(.*?)[,|\s].*?,\s(\d+\.?\d+?)\sfps,.*?Audio:\s(.*?)[,|\s].*?mean_volume:\s(.*?)\sdB.*?max_volume:\s(.*?)\sdB";

            string bitrate = "NaN!!";
            string vEec = "??";
            string fps = "??";
            string aEnc = "??";
            string mean = "??";
            string max = "??";

            var ms = Regex.Matches(volumeData, pattern, RegexOptions.Singleline);
            if (ms.Count != 0)
            {
                var m = ms[0];
                bitrate = m.Groups[1].Value.Split(' ')[0];
                vEec = m.Groups[2].Value;
                fps = m.Groups[3].Value;
                aEnc = m.Groups[4].Value;
                mean = m.Groups[5].Value;
                max = m.Groups[6].Value;
            }

            pattern = @"bitrate= (.*?)kbits\/s";
            var tmpOut = $"{config.conf.Folders.TmpPath}\\tmp_for_opuscheck_{GetRandomString(20)}.{ext}";
            string bitrateInfoig = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", opus_bitrate_check(inPath, tmpOut)));
            string opusbitrate = "??";
            ms = Regex.Matches(bitrateInfoig, pattern, RegexOptions.Singleline);
            if (ms.Count != 0)
            {
                var m = ms[0];
                opusbitrate = m.Groups[1].Value;
            }

            if (File.Exists(tmpOut)) File.Delete(tmpOut);
            if (delete_after)
                if (File.Exists(inPath)) File.Delete(inPath);

            return new VideoInfo(bitrate, vEec, fps, aEnc, mean, max, opusbitrate, volumeData);
        }

        public async static Task<VideoInfo> get_all_audo_info(string inPath, int jobId = -1)
        {
            bool delete_after = false;
            var ext = inPath.Split('.').Last();
            if (!File.Exists(inPath))
            {
                delete_after = true;
                // link I guess
                // need to save cuz two ffmpeg commands will be executed
                using (var client = new WebClient())
                {
                    client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko");
                    var path = $"{config.conf.Folders.TmpPath}\\{GetRandomString(20)}.{ext}";
                    try
                    {
                        bool isDownloaded = false;
                        client.DownloadFileCompleted += (s, e) => isDownloaded = true;
                        client.DownloadProgressChanged += (s, e) => UpdateProgBar?.Invoke(e, jobId);
                        await Task.Run(() => { try { client.DownloadFileAsync(new Uri(inPath), path); } catch { } });
                        await Task.Run(async () => { while (!isDownloaded) { await Task.Delay(250); } });
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    inPath = path;
                    if (!File.Exists(inPath))
                    {
                        MessageBox.Show(Application.Current.MainWindow, "Something went wrong with checking this", "File missing - download failed");
                        return new VideoInfo("x", "x", "x", "??", "??", "??", "??", "download failed");
                    }
                }
            }

            string volumeData = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_and_silence_check(inPath)));

            string pattern = @"Audio:\s(.*?),\s.*?mean_volume:\s(.*?)\sdB.*?max_volume:\s(.*?)\sdB";

            string bitrate = "x";
            string vEec = "x";
            string fps = "x";
            string aEnc = "??";//
            string mean = "??";//
            string max = "??";//

            var ms = Regex.Matches(volumeData, pattern, RegexOptions.Singleline);
            if (ms.Count != 0)
            {
                var m = ms[0];
                aEnc = m.Groups[1].Value;
                aEnc = aEnc.Replace(",", "");
                mean = m.Groups[2].Value;
                max = m.Groups[3].Value;
            }

            // wheeee duplicate code
            pattern = @"bitrate= (.*?)kbits\/s";
            var tmpOut = $"{config.conf.Folders.TmpPath}\\tmp_for_opuscheck_{GetRandomString(20)}.{ext}";
            string bitrateInfoig = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", opus_bitrate_check(inPath, tmpOut)));
            string opusbitrate = "??";
            ms = Regex.Matches(bitrateInfoig, pattern, RegexOptions.Singleline);
            if (ms.Count != 0)
            {
                var m = ms[0];
                opusbitrate = m.Groups[1].Value;
            }

            if (File.Exists(tmpOut)) File.Delete(tmpOut);
            if (delete_after)
                if (File.Exists(inPath)) File.Delete(inPath);

            return new VideoInfo(bitrate, vEec, fps, aEnc, mean, max, opusbitrate, volumeData);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="inPath"></param>
        /// <returns>bitrate, max, mean</returns>
        public static async Task<Tuple<string, string, string>> get_bitrate_and_volume_data_video(string inPath)
        {
            string volumeData = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_check(inPath)));

            //string pattern = @"bitrate:\s(\d+\skb\/s).*?Video:\s(.*?)\s.*?DAR\s\d+:\d+.*?\s(.*?)\sfps,.*?Audio:\s(.*?),\s.*?mean_volume:\s(.*?)\sdB.*?max_volume:\s(.*?)\sdB";
            string pattern = @"bitrate:\s(\d+\skb\/s).*?Video:\s(.*?)[,|\s].*?,\s(\d+\.?\d+?)\sfps,.*?Audio:\s(.*?)[,|\s].*?mean_volume:\s(.*?)\sdB.*?max_volume:\s(.*?)\sdB";

            string bitrate = "NaN!!";
            string max = "??";
            string mean = "??";

            var ms = Regex.Matches(volumeData, pattern, RegexOptions.Singleline);
            if (ms.Count != 0)
            {
                var m = ms[0];
                max = m.Groups[6].Value;
                mean = m.Groups[5].Value;
                bitrate = m.Groups[1].Value.Split(' ')[0];
            }

            return new Tuple<string, string, string>(bitrate, max, mean);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inPath"></param>
        /// <returns>max, mean, encoding</returns>
        public static async Task<Tuple<string, string, string>> get_volume_data_audio(string inPath)
        {
            string volumeData = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_check(inPath)));

            string pattern = @"Audio:\s(.*?),\s.*?mean_volume:\s(.*?)\sdB.*?max_volume:\s(.*?)\sdB";

            string max = "??";
            string mean = "??";
            string encoding = "??";

            var ms = Regex.Matches(volumeData, pattern, RegexOptions.Singleline);
            if (ms.Count != 0)
            {
                var m = ms[0];
                max = m.Groups[3].Value;
                mean = m.Groups[2].Value;
                encoding = m.Groups[1].Value;
            }

            return new Tuple<string, string, string>(max, mean, encoding);
        }

        public async Task<string> get_video_bitrate(string inPath)
        {
            string ffmpegi = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", $"-i \"{inPath}\""));
            string pattern = @"bitrate:\s(\d+\skb\/s)";
            var ms = Regex.Matches(ffmpegi, pattern, RegexOptions.Singleline);
            string bitrate = "NaN!!";
            if (ms.Count != 0)
            {
                var m = ms[0];
                bitrate = m.Groups[1].Value.Split(' ')[0];
            }

            return bitrate;
        }

        /// <summary>
        /// make sure to call this only of volume change != 0
        /// This will make a new version and delete the old one
        /// </summary>
        /// <param name="vol">The volume value to change the video for</param>
        /// <param name="inPathOld">The old path that will then be deleted IF ITS NOT THE VOL 0 FILE!</param>
        /// <returns>New path</returns>
        public async Task<string> make_new_changedvolume_media(double vol, string inPathOld)
        {
            var spl = inPathOld.Split('_');
            if (spl.Length < 4)
            {
                MessageBox.Show(Application.Current.MainWindow, $"{inPathOld}\n\nCan not edit volume for this file, was the file name changed?");
                return inPathOld;
            }
            var oldVol = spl[3];
            int oldVolTest;
            if (!int.TryParse(oldVol, out oldVolTest))
            {
                MessageBox.Show(Application.Current.MainWindow, $"{oldVol}\n\n{inPathOld}\n\nCould not parse volume from this file name.");
                return inPathOld;
            }
            if (vol != oldVolTest)
            {
                string ret = inPathOld;
                string pattern = @"(.*?_.*?_.*?_)(.*?)(_.*?$)";
                var ms = Regex.Matches(inPathOld, pattern, RegexOptions.Singleline);
                if (ms.Count != 0)
                {
                    var m = ms[0];
                    ret = $"{m.Groups[1].Value}{vol}{m.Groups[3].Value}";
                    string volChg = vol > 0 ? $"+{vol}" : $"+{vol}";
                    if (inPathOld.EndsWith(".mp3"))
                        await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_edit_audio(inPathOld, ret, volChg)));
                    else
                        await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_edit(inPathOld, ret, volChg)));
                    if (File.Exists(ret))
                    {
                        // if it was actually created
                        if (oldVolTest != 0) // lets not delete the original lol
                            File.Delete(inPathOld);
                        return ret;
                    }
                }
                {
                    return ret;
                }

            }
            else
            {
                return inPathOld;
            }

        }
        #endregion

        #region Jobs events
        private void OutFileName_TextEdited(object sender, TextChangedEventArgs e)
        {
            var tbx = sender as TextBox;
            Tuple<bool, string, string> file;
            DebounceAction(() =>
            {
                Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var job = JobsOBS[MyIdxById(MyID(tbx))];
                        job.__GetVolumeBTN = false;
                        file = isValidAudioPath(tbx.Text);
                        if (file.Item1)
                        {
                            job.__GetVolumeBTN = true;
                            job.AudioReplacementPath = file.Item3;
                        }
                    });
                });
            });
        }

        private void JobsBTN_Duplicate_Click(object sender, RoutedEventArgs e)
        {
            var idx = MyIdxById(MyID(sender as Button));
            var j = JobsOBS[idx];

            int id = JobsOBS.Count;
            for (; JobsOBS.FirstOrDefault(x => x.Id == id) != null; ++id) { }
            // this could techically be a copy contructor, but eh
            var job = new AmqJob(id)
            {
                OutFileName = j.OutFileName,
                OutFileSubDirectory = j.OutFileSubDirectory,
                ShowTitleFullPath = j.ShowTitleFullPath,
                FromState = j.FromState,
                Bitrates = j.Bitrates,
                UsedAudioId_Warning1Based = j.UsedAudioId_Warning1Based,
                UsedSubsId_Warning0Based = j.UsedSubsId_Warning0Based,
                VolumeChangeValue = j.VolumeChangeValue,
                ShowTitle = j.ShowTitle.Replace(j.DisabledPrefix, ""),
                maxVolume_meanVolume = j.maxVolume_meanVolume,
                Timestamps = j.Timestamps,
                ShowTitleToolTip = j.ShowTitleToolTip.Split(new string[] { "      DISABLED BECASUE:" }, StringSplitOptions.None).FirstOrDefault(),
                AudioReplacementPath = j.AudioReplacementPath,
                __CatboxLink720 = false,
                __CatboxLink480 = false,
                __CatboxLinkMP3 = false,
                Crf720 = j.Crf720,
                Crf480 = j.Crf480,
            };
            JobsOBS.Insert(idx + 1, job);
            //JobsOBS.Add(job);
            UpdateJobsJsonFile();
        }

        private async void JobsBTN_Simulate_Click(object sender, MouseButtonEventArgs e)
        {
            (sender as Button).IsEnabled = false;

            try
            {
                var job = JobsOBS[MyIdxById(MyID(sender as Button))];

                if (e.MiddleButton == MouseButtonState.Pressed)
                {
                    var pp = config.conf.Folders.DefaultOutPath;
                    if (Directory.Exists(pp))
                        Process.Start(pp);
                    return;
                }


                if (e.RightButton == MouseButtonState.Pressed)
                {
                    var _path2 = job.BuildFinalOutName("NO_RERENDER_AUDIO_OVER", 0, true);
                    var path2 = $"{GetOrFixUpOutDirForThisJob(job)}\\{_path2}";
                    var __ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", put_flac_on_vid(job.ShowTitleFullPath, job.AudioReplacementPath, path2)));
                    return;
                }

                var _path = job.BuildFinalOutName("PREVIEW_CUT", 0, true);
                var path = $"{GetOrFixUpOutDirForThisJob(job)}\\{_path}";

                path = path.Replace(".webm", ".mp4");
                string _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", temp_mp4_ss_to(ss: job.Timestamps.startTime_SS, to: job.Timestamps.endTime_TO, _in: job.ShowTitleFullPath, aid: (job.UsedAudioId_Warning1Based - 1).ToString(), _out: path)));

                if (job.FromState == States.Process)
                {
                    // TODO, TEST IF THIS WORKS
                    var _path2 = job.BuildFinalOutName("PREVIEW_WITH_AUDIO_OVER", 0, true);
                    var path2 = $"{GetOrFixUpOutDirForThisJob(job)}\\{_path2}";
                    path2 = path2.Replace(".webm", ".mp4");
                    _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", put_flac_on_vid(path, job.AudioReplacementPath, path2)));
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow, ex.Message, "Something went wrong");
            }
            finally
            {
                (sender as Button).IsEnabled = true;
            }
        }

        private async void JobsBTN_Edit_Click(object sender, RoutedEventArgs e)
        {
            var job = JobsOBS[MyIdxById(MyID(sender as Button))];
            if (!File.Exists(job.ShowTitleFullPath))
            {
                MessageBox.Show(Application.Current.MainWindow, $"The file {job.ShowTitleFullPath} doesn't exist anymore.", "Failed to load job", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (state.CurrentState != job.FromState)
            {
                state.CurrentState = job.FromState;
                ChangeView();
            }
            playerLoaded = false;
            LoadVideoIntoPlayer(job.ShowTitleFullPath);
            await Task.Run(() => { while (!playerLoaded) { Task.Delay(200); } });
            await Task.Delay(200);
            I_Know_the_process_input_file_name.Text = job.ShowTitleFullPath;
            GET_CLIPS_START.Text = job.Timestamps.startTime_SS;
            GET_CLIPS_END.Text = job.Timestamps.endTime_TO;
            //PROCESS_TIME_T.Text = job.Timestamps.duration_T;
            TimeLen = job.Timestamps.duration_T;
            //GET_CLIPS_OUT_TBX.Text = job.OutFileName;
            GetClipsOutFileTbx = job.OutFileName;
        }
        private void JobsBTN_Cancel_Click(object sender, RoutedEventArgs e)
        {
            //USING MOUSE DOWN INSTEAD
            var idx = MyIdxById(MyID(sender as Button));
            JobsOBS.RemoveAt(idx);
            UpdateJobsJsonFile();
        }


        private void JobsBTN_Cancel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
            {
                var job = JobsOBS[MyIdxById(MyID(sender as Button))];
                var res = MessageBox.Show(Application.Current.MainWindow, "Also delete all rendered files?", "Remving job", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (res == MessageBoxResult.Cancel) return;
                if (res == MessageBoxResult.Yes)
                {
                    foreach (var f in new string[] { job.Rendered720File, job.Rendered480File, job.RenderedMP3File, job.Rendered720File_VOL0, job.Rendered480File_VOL0, job.RenderedMP3File_VOL0, $"TMP--{job.Rendered720File_VOL0}", $"TMP--{job.Rendered480File_VOL0}" }) // todo: check why this doesnt get deleted
                    {
                        try
                        {
                            if (File.Exists(f)) File.Delete(f);
                        }
                        catch { }
                    }
                }

            }
            var idx = MyIdxById(MyID(sender as Button));
            JobsOBS.RemoveAt(idx);
            UpdateJobsJsonFile();
        }


        private void JobsBTN_CheckVideo_Click(object sender, RoutedEventArgs e)
        {
            var job = JobsOBS[MyIdxById(MyID(sender as Button))];
            var load = job.Rendered720File;
            if (load == null)
                load = job.Rendered480File;
            if (load == null)
                load = job.RenderedMP3File;
            if (!File.Exists(load))
            {
                MessageBox.Show(Application.Current.MainWindow, $"{load}\n\nThis file does not exist.", "Failed to load", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadVideoIntoPlayer(load);
        }
        private void JobsBTN_CopyUploaded7_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(JobsOBS[MyIdxById(MyID(sender as Button))].CatboxLink720);
        }
        private void JobsBTN_CopyUploaded4_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(JobsOBS[MyIdxById(MyID(sender as Button))].CatboxLink480);
        }
        private void JobsBTN_CopyUploaded3_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(JobsOBS[MyIdxById(MyID(sender as Button))].CatboxLinkMP3);
        }
        private async void JobsBTN_VolumeCheck_Click(object sender, RoutedEventArgs e)
        {
            var job = JobsOBS[MyIdxById(MyID(sender as Button))];
            if (job.FromState == States.Process && !job.JobFinished)
            {
                job.__GetVolumeBTN = false;
                var volDataAudio = await get_volume_data_audio(job.AudioReplacementPath);
                job.maxVolume_meanVolume = $"[{volDataAudio.Item1} | {volDataAudio.Item2}] dB";
                job.__GetVolumeBTN = true;
                return;
            }

            if (job.FromState == States.Clips && !job.JobFinished)
            {
                job.__GetVolumeBTN = false;

                await UpdateVolumeValuesTextbox(job);

                job.__GetVolumeBTN = true;
                return;
            }

            // if we're checking already rendered files
            var opts = new string[] { job.Rendered720File_VOL0, job.Rendered480File_VOL0, job.RenderedMP3File_VOL0 };
            var file = opts.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(file))
            {
                var opt = string.Join("\n", opts);
                MessageBox.Show(Application.Current.MainWindow, $"{opt}\n\nAll renderd files have gone missing (been deleted)??", "Can't check volume", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // await UpdateVolumeValuesTextbox(job, file, false);
            job.maxVolume_meanVolume = await get_volume_data_db_only_video(file);
        }

        public async Task UpdateVolumeValuesTextbox(AmqJob job, string testFile = "", bool deleteTestFiles = true)
        {
            var aa = GetRandomString(20);
            var tmpFile = $"{tmpPath}\\_{aa}_VOLUME_DATA_EDIT_TMP_FILE.mp4";
            var newtmpFileToCheck = $"{tmpPath}\\_{aa}_VOLCHG_VOLUME_DATA_EDIT_TMP_FILE.mp4";

            if (testFile == "")
            {
                testFile = job.ShowTitleFullPath;
                string _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", temp_mp4_ss_to(ss: job.Timestamps.startTime_SS, to: job.Timestamps.endTime_TO, _in: testFile, aid: (job.UsedAudioId_Warning1Based - 1).ToString(), _out: tmpFile)));
            }
            else
            {
                // we're actually checking the edited value of the test file instead of the cut in get clips mode
                tmpFile = testFile;
            }

            var v = job.VolumeChangeValue;
            string volChg = v > 0 ? $"+{v}" : $"{v}";
            string __ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_edit(tmpFile, newtmpFileToCheck, volChg)));

            job.maxVolume_meanVolume = await get_volume_data_db_only_video(newtmpFileToCheck);

            if (deleteTestFiles) // this should be false if you're checking the volume of a rendered file 
                File.Delete(tmpFile);

            File.Delete(newtmpFileToCheck);
        }

        public async Task ExecuteVolumeEdit(AmqJob job)
        {
            var v = job.VolumeChangeValue;
            job.TryDeleteAllButVol0And(v);
            string volChg = v > 0 ? $"+{v}" : $"{v}";

            string wasDone = "";
            bool wasMp3 = false;

            if (!string.IsNullOrEmpty(job.RenderedMP3File_VOL0))
            {
                var _outPath = job.BuildFinalOutName("mp3", v);
                var outPath = $"{GetOrFixUpOutDirForThisJob(job)}\\{_outPath}";
                string _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_edit_audio(job.RenderedMP3File_VOL0, outPath, volChg)));
                job.RenderedMP3File = outPath;
                wasDone = outPath;
                wasMp3 = true;
            }
            if (!string.IsNullOrEmpty(job.Rendered720File_VOL0))
            {
                var _outPath = job.BuildFinalOutName("720", v);
                var outPath = $"{GetOrFixUpOutDirForThisJob(job)}\\{_outPath}";
                string _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_edit(job.Rendered720File_VOL0, outPath, volChg)));
                job.Rendered720File = outPath;
                wasDone = outPath;
                wasMp3 = false;
            }
            if (!string.IsNullOrEmpty(job.Rendered480File_VOL0))
            {
                var _outPath = job.BuildFinalOutName("480", v);
                var outPath = $"{GetOrFixUpOutDirForThisJob(job)}\\{_outPath}";
                string _ = await Task.Run(() => RunCommandAndGetOutput("ffmpeg", volume_edit(job.Rendered480File_VOL0, outPath, volChg)));
                job.Rendered480File = outPath;
                wasDone = outPath;
                wasMp3 = false;
            }

            if (!string.IsNullOrEmpty(wasDone))
            {
                if (!wasMp3)
                    job.maxVolume_meanVolume = await get_volume_data_db_only_video(wasDone);
                else
                    job.maxVolume_meanVolume = await get_volume_data_db_only_audio(wasDone);
            }
        }

        public bool only_one_upl2 = true; // todo, maybe allow multiple at once :hmmm:
        private async void JobsBTN_Upload_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var job = JobsOBS[MyIdxById(MyID(sender as Button))];

            bool isUploaded = btn.Style == Application.Current.Resources["JobsBTN_Uploaded"];
            if (btn.Style == Application.Current.Resources["JobsBTN_Uploading"])
            {
                MessageBox.Show(Application.Current.MainWindow, "Don't click me while in this state (yellow/uploading)");
                return;
            }
            if (isUploaded)
            {
                Clipboard.SetText($"**{job.OutFileName}**\n{job.ShowTitle}\n<{job.CatboxLink720}>\n<{job.CatboxLink480}>\n<{job.CatboxLinkMP3}>");
                return;
            }

            job.__UploadToCatboxAllBTN = false;

            btn.Style = Application.Current.Resources["JobsBTN_Uploading"] as Style;

            await Task.Run(async () =>
            {
                while (!only_one_upl2)
                    await Task.Delay(200);
            });

            await Task.Run(async () =>
            {
                try
                {
                    only_one_upl2 = false;
                    #region reee
                    using (HttpClient c = new HttpClient())
                    {
                        c.Timeout = TimeSpan.FromMinutes(10);
                        c.BaseAddress = new Uri(@"https://catbox.moe");
                        //client.Headers.Add("userhash", privateConfig.conf.catbox_userhash);
                        //client.Headers.Add("reqtype", "fileupload");

                        var upl = new string[] { job.Rendered720File, job.Rendered480File, job.RenderedMP3File };
                        for (int i = 0; i < upl.Length; i++)
                        {
                            if (string.IsNullOrEmpty(upl[i])) continue;
                            if (i == 0 && job.__CatboxLink720) continue;
                            if (i == 1 && job.__CatboxLink480) continue;
                            if (i == 2 && job.__CatboxLinkMP3) continue;
                            await Task.Delay(300);

                            var link = "";
                            byte[] media;
                            using (FileStream f = File.OpenRead(upl[i]))
                            {
                                media = new byte[f.Length];
                                await f.ReadAsync(media, 0, media.Length);
                            }

                            //var requestContent = new MultipartFormDataContent();
                            //var mediaContent = new ByteArrayContent(media);

                            string ext = "mp3";
                            if (i != 2)
                            {
                                ext = "webm";
                            }

                            string t = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
                            var formContent = new MultipartFormDataContent
                            {
                                {new StringContent("fileupload"), "reqtype"},
                                {new StringContent(privateConfig.conf.catbox_userhash), "userhash" },
                                {new StreamContent(new MemoryStream(media)), "fileToUpload",$"{t}_upload.{ext}"}
                            };

                            var res = await c.PostAsync("/user/api.php", formContent);

                            if (res.IsSuccessStatusCode)
                            {
                                link = await res.Content.ReadAsStringAsync();
                                if (i == 0) { job.CatboxLink720 = link; job.__CatboxLink720 = true; }
                                if (i == 1) { job.CatboxLink480 = link; job.__CatboxLink480 = true; }
                                if (i == 2) { job.CatboxLinkMP3 = link; job.__CatboxLinkMP3 = true; }
                                Dispatcher.Invoke(() => UpdateJobsJsonFile());
                            }
                            else
                            {
                                MessageBox.Show(Application.Current.MainWindow, $"{await res.Content.ReadAsStringAsync()}\n\nFailed to upload: {upl[i]}", "Catbox upload failed", MessageBoxButton.OK, MessageBoxImage.Error);
                                job.__UploadToCatboxAllBTN = true;
                            }
                        }
                        // MessageBox.Show(Application.Current.MainWindow, "Upload to catbox has finished.", "Done", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    }
                    #endregion

                }
                catch (Exception ex)
                {
                    string extra = "";
                    if (ex.Message == "A task was canceled.") extra = "\n\nTimeout has occurred (10 minutes)";
                    MessageBox.Show(Application.Current.MainWindow, $"{ex.Message}\n{ex.InnerException}{extra}", "Something went wrong when uploading to catbox", MessageBoxButton.OK, MessageBoxImage.Error);
                    //job.__UploadToCatboxAllBTN = true;

                }
                finally
                {
                    //job.__UploadToCatboxAllBTN = true; actually, don't
                    only_one_upl2 = true;
                    Dispatcher.Invoke(() => UpdateJobsJsonFile());
                    Dispatcher.Invoke(() => { btn.Style = Application.Current.Resources["JobsBTN_Upload"] as Style; });
                }
            });

            // TODO: Put something like this recheck jobs
            bool tryAgain = false;
            // if the button is disabled (aka. no link)
            // and the rendered file is not empty
            // and the upload didn't fail becasue on an exception/catbox
            if (!job.__CatboxLink720 && !string.IsNullOrEmpty(job.Rendered720File) && !job.__UploadToCatboxAllBTN) tryAgain = true;
            if (!job.__CatboxLink480 && !string.IsNullOrEmpty(job.Rendered480File) && !job.__UploadToCatboxAllBTN) tryAgain = true;
            if (!job.__CatboxLinkMP3 && !string.IsNullOrEmpty(job.RenderedMP3File) && !job.__UploadToCatboxAllBTN) tryAgain = true;
            if (tryAgain)
            {
                // this is in case the race condition fails for the locker
                job.__UploadToCatboxAllBTN = true;
                (sender as Button).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                return;
            }
            btn.Style = Application.Current.Resources["JobsBTN_Uploaded"] as Style; // Todo: set this style if needed recheck jobs
            job.__UploadToCatboxAllBTN = true;
            Dispatcher.Invoke(() => UpdateJobsJsonFile());
        }

        private async void JobsBTN_VolumeEdit_Click(object sender, RoutedEventArgs e)
        {
            var job = JobsOBS[MyIdxById(MyID(sender as Button))];
            job.__EditVolumeValueOrangeBTN = false;

            await ExecuteVolumeEdit(job);
            UpdateJobsJsonFile();

            job.__EditVolumeValueOrangeBTN = true;
        }

        private void TextBlock_MouseDown_Preview7(object sender, MouseButtonEventArgs e)
        {
            var f = JobsOBS[MyIdxById(MyID(sender as TextBlock))].Rendered720File;
            if (!File.Exists(f))
            {
                MessageBox.Show(Application.Current.MainWindow, $"{f}\n\nThis file does not exist.", "Failed to load", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                Process.Start(f);
                return;
            }
            LoadVideoIntoPlayer(f);
        }
        private void TextBlock_MouseDown_Preview4(object sender, MouseButtonEventArgs e)
        {
            var f = JobsOBS[MyIdxById(MyID(sender as TextBlock))].Rendered480File;
            if (!File.Exists(f))
            {
                MessageBox.Show(Application.Current.MainWindow, $"{f}\n\nThis file does not exist.", "Failed to load", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                Process.Start(f);
                return;
            }
            LoadVideoIntoPlayer(f);
        }
        private void TextBlock_MouseDown_Preview3(object sender, MouseButtonEventArgs e)
        {
            var f = JobsOBS[MyIdxById(MyID(sender as TextBlock))].RenderedMP3File;
            if (!File.Exists(f))
            {
                MessageBox.Show(Application.Current.MainWindow, $"{f}\n\nThis file does not exist.", "Failed to load", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                Process.Start(f);
                return;
            }
            LoadVideoIntoPlayer(f);
        }

        private void UPLButton_Loaded(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var job = JobsOBS[MyIdxById(MyID(sender as Button))];
            if (!job.JobFinished) return;

            var a = new List<bool>() { !string.IsNullOrEmpty(job.Rendered720File), !string.IsNullOrEmpty(job.CatboxLink720), job.__CatboxLink720 };
            var b = new List<bool>() { !string.IsNullOrEmpty(job.Rendered480File), !string.IsNullOrEmpty(job.CatboxLink480), job.__CatboxLink480 };
            var c = new List<bool>() { !string.IsNullOrEmpty(job.RenderedMP3File), !string.IsNullOrEmpty(job.CatboxLinkMP3), job.__CatboxLinkMP3 };
            bool a1 = a.All(x => x == a.First());
            bool b1 = b.All(x => x == b.First());
            bool c1 = c.All(x => x == c.First());
            bool ddd = !string.IsNullOrEmpty(job.CatboxLink720) || !string.IsNullOrEmpty(job.CatboxLink480) || !string.IsNullOrEmpty(job.CatboxLinkMP3);
            if ((a1 == true && b1 == true && c1 == true) || ddd)
            {
                btn.Style = Application.Current.Resources["JobsBTN_Uploaded"] as Style;
                job.__UploadToCatboxAllBTN = true;
            }
        }

        #endregion

        private void TextBlockSLID_Loaded(object sender, RoutedEventArgs e)
        {
            var job = JobsOBS[MyIdxById(MyID(sender as TextBlock))];
            double val = job.VolumeChangeValue;
            var txt = $"{val}dB";
            if (val >= 0) txt = "+" + txt;
            (sender as TextBlock).Text = txt;
        }

    }
}


