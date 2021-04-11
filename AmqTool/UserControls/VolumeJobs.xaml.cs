using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AmqTool.UserControls
{
    /// <summary>
    /// Interaction logic for VolumeJobs.xaml
    /// </summary>
    public partial class VolumeJobs : UserControl
    {
        ObservableCollection<VolumeJob> VolumeJobsOBS = new ObservableCollection<VolumeJob>();
        public int VolumeJobsRemainingFromFile = 0;
        public int VolumeJobsRemainingFromMultiInput = 0;
        public string InputBox = "Url to test or download";
        public string DlFile = "Downloaded file name";
        public delegate void LoadIntoPlayerDelegate(object sender, string path);
        public event LoadIntoPlayerDelegate LoadIntoPlayer;
        public delegate void ShowConsoleDelegate(object sender, string details);
        public event ShowConsoleDelegate ShowDetailedInfo;
        public delegate void StatusBarDelegate(object sender, string text, bool left);
        public event StatusBarDelegate SetStatusBar;

        public VolumeJobs()
        {
            InitializeComponent();
            Tbx.Text = InputBox;
            DlTbx.Text = DlFile;
            MainWindow.UpdateProgBar += UpdateDlProgress;
            MultiInputWindow.CheckThese += MultiInputWindow_CheckThese;
            MainWindow.CheckTheseMV += MultiInputWindow_CheckThese;

            //restore volume jobs
            if (!File.Exists("volume_jobs.json")) File.Create("volume_jobs.json").Close();
            VolumeJobsOBS = JsonConvert.DeserializeObject<ObservableCollection<VolumeJob>>(File.ReadAllText("volume_jobs.json")) ?? new ObservableCollection<VolumeJob>();
            VolumeJobsOBS = new ObservableCollection<VolumeJob>(VolumeJobsOBS.Where(j => j.__IsRegularJob && File.Exists(j.SavedFilePath)).ToList());
            VolumeJobsLV.ItemsSource = VolumeJobsOBS;

            UpdateJobsJsonFile();
        }

        private async void MultiInputWindow_CheckThese(List<MultiInputWindow.VolumeCheckerFromRTB> check)
        {
            VolumeJobsRemainingFromMultiInput += check.Count;
            foreach (var c in check)
            {
                SetStatusBar?.Invoke(null, $"Volume jobs (from multi input) remaining: {VolumeJobsRemainingFromMultiInput--}", true);
                var uri = c.CatboxUrl;
                var toCheck = uri;
                int id = -1;

                if (c.SaveFileName != "")
                {
                    toCheck = c.SaveFileName;
                    var ext = uri.Split('.').Last();
                    var path = $"{MainWindow.config.conf.Folders.VolumeCheckerPath}\\{toCheck}.{ext}";
                    if (!File.Exists(path))
                    {
                        using (var client = new WebClient())
                        {
                            client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko");
                            id = 200000;
                            for (; VolumeJobsOBS.FirstOrDefault(x => x.Id == id) != null; ++id) { }
                            VolumeJobsOBS.Insert(0, new VolumeJob(id) { Id = id }.SetupTmp(uri));

                            bool isDownloaded = false;
                            client.DownloadFileCompleted += (s, ee) => isDownloaded = true;
                            client.DownloadProgressChanged += (s, ee) => UpdateDlProgress(ee, id);
                            await Task.Run(() => { try { client.DownloadFileAsync(new Uri(uri), path); } catch { } });

                            await Task.Run(async () => { while (!isDownloaded) { await Task.Delay(250); } });

                            if (!File.Exists(path))
                                continue; // failed to download
                            toCheck = path;
                        }
                    }
                }
                else
                {
                    //Chkbox.IsChecked = false;
                }

                await addJobFromUriPath(uri, toCheck, id: id);

                SetStatusBar?.Invoke(null, $"Volume jobs (from multi input) remaining: {VolumeJobsRemainingFromMultiInput}", true);
            }
        }

        #region Some toggle events
        private void Tbx_GotFocus(object sender, RoutedEventArgs e) { if (Tbx.Text == InputBox) Tbx.Text = ""; }
        private void Tbx_LostFocus(object sender, RoutedEventArgs e) { if (Tbx.Text == "") Tbx.Text = InputBox; }
        private void Tbx2_GotFocus(object sender, RoutedEventArgs e) { if (DlTbx.Text == DlFile) DlTbx.Text = ""; }
        private void Tbx2_LostFocus(object sender, RoutedEventArgs e) { if (DlTbx.Text == "") DlTbx.Text = DlFile; }
        private void Chk_Checked(object sender, RoutedEventArgs e)
        {
            AddBTN.Style = (Style)Application.Current.Resources["JobsBTN_PlusDL"];
            DlTbx.IsEnabled = true;
        }
        private void Chk_Unchecked(object sender, RoutedEventArgs e)
        {
            AddBTN.Style = (Style)Application.Current.Resources["JobsBTN_Plus"];
            DlTbx.IsEnabled = false;
        }
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
        #endregion

        private async void AddBTN_Click(object sender, RoutedEventArgs e)
        {
            var uri = MainWindow.checkIfActuallyUrl(Tbx.Text);
            var ext = "";
            if (uri == "")
            {
                MessageBox.Show($"{Tbx.Text}\n\nPlease fix the url (missing http(s) maybe)", "Can not check/download this", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var toCheck = uri;
            int id = -1;

            if (Chkbox.IsChecked == true)
            {
                toCheck = DlTbx.Text;
                if (toCheck == DlFile && toCheck == "")
                {
                    MessageBox.Show($"Please enter file name where it says \"{DlFile}\"", "Missing file name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                ext = uri.Split('.').Last();
                var path = $"{MainWindow.config.conf.Folders.VolumeCheckerPath}\\{toCheck}.{ext}";
                var isInvalid = PathHelper.PossiblyInvalid(path);
                if (isInvalid != "")
                {
                    MessageBox.Show(isInvalid + "\n\n" + path, "Invalid out file path", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (File.Exists(path))
                {
                    MessageBox.Show(path + "\n\n" + "A file with the desire out name already exists", "File exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                using (var client = new WebClient())
                {
                    client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko");
                    Tbx.Text = InputBox;
                    if (Tbx.IsFocused) Tbx.Text = "";
                    DlTbx.Text = DlFile;
                    Chkbox.IsChecked = false;

                    //var tmpID = MainWindow.GetRandomString(30);
                    //for (; VolumeJobsOBS.FirstOrDefault(x => x.TmpId == tmpID) != null; tmpID = MainWindow.GetRandomString(30)) { }
                    id = 200000;
                    for (; VolumeJobsOBS.FirstOrDefault(x => x.Id == id) != null; ++id) { }
                    VolumeJobsOBS.Insert(0, new VolumeJob(id) { Id = id }.SetupTmp(uri));

                    bool isDownloaded = false;
                    client.DownloadFileCompleted += (s, ee) => isDownloaded = true;
                    client.DownloadProgressChanged += (s, ee) => UpdateDlProgress(ee, id);
                    await Task.Run(() => { try { client.DownloadFileAsync(new Uri(uri), path); } catch { } });

                    await Task.Run(async () => { while (!isDownloaded) { await Task.Delay(250); } });

                    //var j = VolumeJobsOBS.Where(x => x.Id == id).FirstOrDefault();
                    //if (j != null) VolumeJobsOBS.Remove(j);

                    if (!File.Exists(path))
                    {
                        MessageBox.Show($"Url: {uri}\nPath:{path}\n\nFailed to download!");
                        return;
                    }
                    toCheck = path;
                }
            }
            else
            {
                Tbx.Text = InputBox;
                if (Tbx.IsFocused) Tbx.Text = "";
                DlTbx.Text = DlFile;
                Chkbox.IsChecked = false;
            }

            await addJobFromUriPath(uri, toCheck, id: id);
        }


        // "", path, false, id combo -- if just updating detailed info 
        public async Task addJobFromUriPath(string uri, string toCheck, bool makeNewJob = true, int editIdJob = 0, int id = -1)
        {
            if (uri == toCheck && id == -1)
            {
                id = 200000;
                for (; VolumeJobsOBS.FirstOrDefault(x => x.Id == id) != null; ++id) { }
                VolumeJobsOBS.Insert(0, new VolumeJob(id) { Id = id }.SetupTmp(uri));
            }

            var ext = toCheck.Split('.').Last();
            MainWindow.VideoInfo info;

            try
            {
                if (ext == "mp3" || ext == "flac")
                    info = await MainWindow.get_all_audo_info(toCheck, id);
                else
                    info = await MainWindow.get_all_video_info(toCheck, id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}\n\n{ex.InnerException}", "Something went wrong when cecking data");
                return;
            }


            if (uri == toCheck || id != -1)
            {
                var j = VolumeJobsOBS.Where(x => x.Id == id).FirstOrDefault();
                if (j != null) VolumeJobsOBS.Remove(j);
            }

            id = VolumeJobsOBS.Count;
            for (; VolumeJobsOBS.FirstOrDefault(x => x.Id == id) != null; ++id) { }
            var titleShorter = MainWindow.getTitleFromAnimePath(toCheck);
            if (titleShorter.Length > 35) { titleShorter = $"{titleShorter.Substring(0, 32)}..."; }
            if (makeNewJob)
            {
                var job = new VolumeJob(id)
                {
                    SavedFilePath = uri != toCheck ? toCheck : "",
                    CatboxLinkOORIGINAL = uri,
                    MediaInfo = $"V: {info.bitrate} [{info.fps}] ({info.venc}) | A: {info.encoderOpusBitrate} ({info.aenc})",
                    MaxVolume_meanVolume = $"({info.max} | {info.mean})",
                    DetailedInfo = info.detailed,
                    __IsDownloaded = uri != toCheck,
                    __UploadToCatboxAllBTN = uri != toCheck,
                    Title = titleShorter,
                    Title_for_tooltip = uri != toCheck ? $"{uri} || {toCheck}" : uri
                };
                job.SavedFilePathEdited = job.SavedFilePath;
                if (info.detailed == "download failed")
                    job.Title = "download failed";
                VolumeJobsOBS.Insert(0, job);
            }
            else
            {
                var job = VolumeJobsOBS.Where(x => x.Id == editIdJob).FirstOrDefault();
                if (job != null)
                {
                    VolumeJobsOBS.Where(x => x.Id == editIdJob).FirstOrDefault().DetailedInfo = info.detailed;
                }
            }
            UpdateJobsJsonFile();
        }

        private async void FromFolder_Click(object sender, RoutedEventArgs e)
        {
            var files = MainWindow.getMultipleMediaPathFromOfd("", "*.mkv; *.mp4; *.webm; *.mp3; *.flac");

            if (files != null)
            {
                VolumeJobsRemainingFromFile += files.Length;
                foreach (var f in files)
                {
                    SetStatusBar?.Invoke(sender, $"Volume jobs (from file) remaining: {VolumeJobsRemainingFromFile}", false);
                    var ext = f.Split('.').Last();
                    MainWindow.VideoInfo info;
                    try
                    {
                        if (ext == "mp3" || ext == "flac")
                            info = await MainWindow.get_all_audo_info(f);
                        else
                            info = await MainWindow.get_all_video_info(f);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{ex.Message}\n\n{ex.InnerException}", "Something went wrong when cecking data");
                        return;
                    }

                    int id = VolumeJobsOBS.Count;
                    for (; VolumeJobsOBS.FirstOrDefault(x => x.Id == id) != null; ++id) { }
                    var titleShorter = MainWindow.getTitleFromAnimePath(f);
                    if (titleShorter.Length > 35) { titleShorter = $"{titleShorter.Substring(0, 32)}..."; }
                    var job = new VolumeJob(id)
                    {
                        SavedFilePath = f,
                        MediaInfo = $"V: {info.bitrate} [{info.fps}] ({info.venc}) | A: {info.encoderOpusBitrate} ({info.aenc})",
                        MaxVolume_meanVolume = $"({info.max} | {info.mean})",
                        DetailedInfo = info.detailed,
                        __IsDownloaded = true,
                        __UploadToCatboxAllBTN = true,
                        Title = titleShorter,
                        Title_for_tooltip = $"{f}"
                    };
                    job.SavedFilePathEdited = job.SavedFilePath;
                    VolumeJobsOBS.Insert(0, job);
                    UpdateJobsJsonFile();
                    VolumeJobsRemainingFromFile--;
                }
                SetStatusBar?.Invoke(sender, $"Volume jobs (from file) remaining: {VolumeJobsRemainingFromFile}", false);
            }
        }

        private void UpdateJobsJsonFile()
        {
            var jsonString = JsonConvert.SerializeObject(VolumeJobsOBS);
            File.WriteAllText("volume_jobs.json", jsonString);
        }
        public void ClearJobsOBS()
        {
            VolumeJobsOBS.Clear();
            UpdateJobsJsonFile();
        }

        public void UpdateDlProgress(DownloadProgressChangedEventArgs e, int jobId)
        {
            var j = VolumeJobsOBS.Where(x => x.Id == jobId).FirstOrDefault();
            if (j != null)
            {
                var left = j.Title.Split(' ').First();
                var perc = e.ProgressPercentage;
                if (perc == 100) perc = 99;
                j.Title = $"{left} {perc}%";
            }
        }

        public void RestoreCorrectButtonStyle(VolumeJob job)
        {
            var a = VolumeJobsLV;
            var b = VolumeJobsLV.ItemsPanel;
            var c = VolumeJobsLV.ItemsSource;
            var d = VolumeJobsLV.Items;
            var aaa = VolumeJobsLV.ItemContainerGenerator.ContainerFromIndex(0);
            var bbb = VolumeJobsLV.ItemContainerGenerator.ContainerFromIndex(1);
            var ccc = VolumeJobsLV.ItemContainerGenerator.Items;
        }

        #region Duplicate code reee
        private StackPanel GetTopSPParent(FrameworkElement sp)
        {
            if (sp.Parent == null) return sp as StackPanel;
            return GetTopSPParent(sp.Parent as StackPanel);
        }
        private int MyID(FrameworkElement sender) => (int)GetTopSPParent(sender.Parent as FrameworkElement).Children.OfType<Label>().First().Content;

        // The index of the VolumeJobsOBS array
        private int MyIdxById(int id) => VolumeJobsOBS.IndexOf(VolumeJobsOBS.First(x => x.Id == id));
        #endregion

        #region Button actions

        private void JobsBTN_Cancel_Click(object sender, RoutedEventArgs e)
        {
            var id = MyID(sender as Button);
            var j = VolumeJobsOBS.Where(x => x.Id == id).FirstOrDefault();
            if (j != null)
            {
                VolumeJobsOBS.Remove(j);
                UpdateJobsJsonFile();
            }
        }

        private void JobsBTN_Cancel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
            {
                var job = VolumeJobsOBS.Where(x => x.Id == MyID(sender as Button)).FirstOrDefault();
                if (job != null)
                {
                    var del = "";
                    if (job.SavedFilePath == job.SavedFilePathEdited)
                    {
                        del = $"{job.SavedFilePath}";
                    }
                    else
                    {
                        del = $"{job.SavedFilePath}\n{job.SavedFilePathEdited}";
                    }
                    var res = MessageBox.Show($"Do you also wish to delete:\n\n{del}", "Remving job", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Cancel) return;
                    if (res == MessageBoxResult.Yes)
                    {
                        foreach (var f in new string[] { job.SavedFilePath, job.SavedFilePathEdited })
                        {
                            try
                            {
                                if (File.Exists(f)) File.Delete(f);
                            }
                            catch { }
                        }
                    }
                }

            }
            var id = MyID(sender as Button);
            var j = VolumeJobsOBS.Where(x => x.Id == id).FirstOrDefault();
            if (j != null)
            {
                VolumeJobsOBS.Remove(j);
                UpdateJobsJsonFile();
            }
        }

        private void JobsBTN_CheckVideo_Click(object sender, RoutedEventArgs e)
        {
            var job = VolumeJobsOBS[MyIdxById(MyID(sender as Button))];
            var load = job.SavedFilePathEdited;
            if (!job.__IsDownloaded)
            {
                load = job.CatboxLinkOORIGINAL;
                LoadIntoPlayer?.Invoke(sender, load);
                return;
            }
            if (!File.Exists(load))
            {
                MessageBox.Show($"{load}\n\nThis file does not exist.", "Failed to load", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadIntoPlayer?.Invoke(sender, load);
        }

        private void JobsBTN_CheckVideo_Click2(object sender, RoutedEventArgs e)
        {
            var job = VolumeJobsOBS[MyIdxById(MyID(sender as Button))];
            var load = job.SavedFilePath;
            if (!job.__IsDownloaded)
            {
                load = job.CatboxLinkOORIGINAL;
                LoadIntoPlayer?.Invoke(sender, load);
                return;
            }
            if (!File.Exists(load))
            {
                MessageBox.Show($"{load}\n\nThis file does not exist.", "Failed to load", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadIntoPlayer?.Invoke(sender, load);
        }
        private async void JobsBTN_MakeNewDetailedInfo(object sender, RoutedEventArgs e)
        {
            (sender as Button).IsEnabled = false;
            var job = VolumeJobsOBS[MyIdxById(MyID(sender as Button))];
            await addJobFromUriPath("", job.SavedFilePathEdited, false, job.Id);
            if (MainWindow.isOnConsole)
                ShowDetailedInfo?.Invoke(sender, job.DetailedInfo);
            (sender as Button).IsEnabled = true;
        }

        private void JobsBTN_ShowDetailedInfo(object sender, RoutedEventArgs e)
        {
            var job = VolumeJobsOBS[MyIdxById(MyID(sender as Button))];
            ShowDetailedInfo?.Invoke(sender, job.DetailedInfo);
        }

        public bool only_one_upl2 = true; // todo, maybe allow multiple at once :hmmm:
        private async void JobsBTN_Upload_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var job = VolumeJobsOBS[MyIdxById(MyID(sender as Button))];

            //bool a = btn.Style == Application.Current.Resources["JobsBTN_Upload"];
            //bool b = btn.Style == Application.Current.Resources["JobsBTN_Uploading"];
            bool isUploaded = btn.Style == Application.Current.Resources["JobsBTN_Uploaded"];

            if (isUploaded)
            {
                Clipboard.SetText(job.CatboxLinkNEW);
                return;
            }

            job.__UploadToCatboxAllBTN = false;
            btn.Style = Application.Current.Resources["JobsBTN_Uploading"] as Style;
            await Task.Run(async () => { while (!only_one_upl2) await Task.Delay(200); });
            await Task.Run(async () =>
            {
                try
                {
                    only_one_upl2 = false;
                    using (HttpClient c = new HttpClient())
                    {
                        c.Timeout = TimeSpan.FromMinutes(10);
                        c.BaseAddress = new Uri(@"https://catbox.moe");
                        await Task.Delay(300);

                        var link = "";
                        byte[] media;
                        using (FileStream f = File.OpenRead(job.SavedFilePathEdited))
                        {
                            media = new byte[f.Length];
                            await f.ReadAsync(media, 0, media.Length);
                        }
                        string ext = job.SavedFilePathEdited.Split('.').Last();
                        string t = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
                        var formContent = new MultipartFormDataContent
                        {
                            {new StringContent("fileupload"), "reqtype"},
                            {new StringContent(MainWindow.privateConfig.conf.catbox_userhash), "userhash" },
                            {new StreamContent(new MemoryStream(media)), "fileToUpload",$"{t}_upload.{ext}"}
                        };
                        var res = await c.PostAsync("/user/api.php", formContent);

                        if (res.IsSuccessStatusCode)
                        {
                            link = await res.Content.ReadAsStringAsync();
                            job.CatboxLinkNEW = link;
                            Dispatcher.Invoke(() => UpdateJobsJsonFile());
                        }
                        else
                        {
                            MessageBox.Show($"{await res.Content.ReadAsStringAsync()}\n\nFailed to upload: {job.SavedFilePathEdited}", "Catbox upload failed", MessageBoxButton.OK, MessageBoxImage.Error);
                            job.__UploadToCatboxAllBTN = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    string extra = "";
                    if (ex.Message == "A task was canceled.") extra = "\n\nTimeout has occurred (10 minutes)";
                    MessageBox.Show($"{ex.Message}\n{ex.InnerException}{extra}", "Something went wrong when uploading to catbox", MessageBoxButton.OK, MessageBoxImage.Error);
                    //job.__UploadToCatboxAllBTN = true;
                }
                finally
                {
                    only_one_upl2 = true;
                    Dispatcher.Invoke(() => UpdateJobsJsonFile());
                    Dispatcher.Invoke(() => { btn.Style = Application.Current.Resources["JobsBTN_Upload"] as Style; });
                }
            });

            bool tryAgain = false;
            if (string.IsNullOrEmpty(job.CatboxLinkNEW) && !string.IsNullOrEmpty(job.SavedFilePathEdited) && !job.__UploadToCatboxAllBTN) tryAgain = true;
            if (tryAgain)
            {
                job.__UploadToCatboxAllBTN = true;
                btn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                return;
            }
            btn.Style = Application.Current.Resources["JobsBTN_Uploaded"] as Style;
            job.__UploadToCatboxAllBTN = true; // TODO: Different behaviour at the start!!
            Dispatcher.Invoke(() => UpdateJobsJsonFile());
        }

        private async void JobsBTN_VolumeEdit_Click(object sender, RoutedEventArgs e)
        {
            var job = VolumeJobsOBS[MyIdxById(MyID(sender as Button))];
            (sender as Button).IsEnabled = false;

            await ExecuteVolumeEdit(job);
            UpdateJobsJsonFile();

            (sender as Button).IsEnabled = true;
        }

        private void UPLButton_Loaded(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var job = VolumeJobsOBS[MyIdxById(MyID(sender as Button))];
            if (!string.IsNullOrEmpty(job.CatboxLinkNEW))
            {
                job.__UploadToCatboxAllBTN = true;
                btn.Style = Application.Current.Resources["JobsBTN_Uploaded"] as Style;
            }
        }

        private void TextBlockSLID_Loaded(object sender, RoutedEventArgs e)
        {
            var job = VolumeJobsOBS[MyIdxById(MyID(sender as TextBlock))];
            double val = job.VolumeChangeValue;
            var txt = $"{val}dB";
            if (val >= 0) txt = "+" + txt;
            (sender as TextBlock).Text = txt;
        }
        #endregion


        private async Task ExecuteVolumeEdit(VolumeJob job)
        {
            var v = job.VolumeChangeValue;
            if (File.Exists(job.SavedFilePathEdited) && job.SavedFilePath != job.SavedFilePathEdited)
            {
                File.Delete(job.SavedFilePathEdited);
            }

            string volChg = v > 0 ? $"+{v}" : $"{v}";

            var originalName = job.SavedFilePath.Split('\\').LastOrDefault().Split('/').LastOrDefault();
            var outPath = $"{MainWindow.config.conf.Folders.VolumeCheckerPath}\\__{v}__{originalName}";

            if (v == 0)
            {
                outPath = job.SavedFilePath;
            }

            var ext = outPath.Split('.').Last();

            if (!File.Exists(outPath))
            {
                if (ext == "flac" || ext == "mp3")
                    await Task.Run(() => MainWindow.RunCommandAndGetOutput("ffmpeg", MainWindow.volume_edit_audio(job.SavedFilePath, outPath, volChg)));
                else
                    await Task.Run(() => MainWindow.RunCommandAndGetOutput("ffmpeg", MainWindow.volume_edit(job.SavedFilePath, outPath, volChg)));
            }

            job.SavedFilePathEdited = outPath;

            if (ext == "flac" || ext == "mp3")
                job.MaxVolume_meanVolume = await MainWindow.get_volume_data_db_only_audio(outPath);
            else
                job.MaxVolume_meanVolume = await MainWindow.get_volume_data_db_only_video(outPath);

        }

    }
}
