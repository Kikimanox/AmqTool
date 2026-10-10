using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace AmqTool
{
    public struct JobTimeStamps
    {
        public JobTimeStamps(string startTime_SS, string endTime_TO = "", string duration_T = "")
        {
            this.startTime_SS = startTime_SS;
            this.endTime_TO = endTime_TO;
            this.duration_T = duration_T;
        }

        public string startTime_SS { get; set; }
        public string endTime_TO { get; set; }
        public string duration_T { get; set; }


    }

    // "enum"
    public class AboveChbxRenderStatus
    {
        public AboveChbxRenderStatus() { }
        public Brush _default { get; } = Brushes.Gray;
        public Brush rendering { get; } = (SolidColorBrush)(new BrushConverter().ConvertFrom("#00a6ff"));
        public Brush done { get; } = (SolidColorBrush)(new BrushConverter().ConvertFrom("#17e647"));
        public Brush failed { get; } = (SolidColorBrush)(new BrushConverter().ConvertFrom("#ff1100"));
        public Brush skipping { get; } = (SolidColorBrush)(new BrushConverter().ConvertFrom("#3B999999"));

    }

    public static class PathHelper
    {
        public static void ValidatePath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path).Delete();
        }

        /// <returns>"" if the path is valid, else the reason why its not</returns>
        public static string PossiblyInvalid(string testPath)
        {
            try
            {
                ValidatePath(testPath);
                return "";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }

    public class AmqJob : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private double volumeChangeValue = 0.0;
        private bool uploadToCatboxAllBTN = false;
        private bool outFileName = true;
        private bool outFileSubDirectory = true;
        private bool justAudioOverOriginal = false;
        private bool rendered720File = false;
        private bool rendered480File = false;
        private bool renderedMP3File = false;
        private bool crf720 = true;
        private bool crf480 = true;
        private bool catboxLink720 = false;
        private bool catboxLink480 = false;
        private bool catboxLinkMP3 = false;
        private bool render720 = true;
        private bool render480 = true;
        private bool renderMP3 = true;
        private bool editBTN = true;
        private bool duplicateJob = true;
        private bool cancelBTN = true;
        private bool rendered720File_EyeBtn = false;
        private bool getVolumeBTN = true;
        private bool editVolumeValueOrangeBTN = false;
        private bool volumeEditSlider = true;
        private string bitrates = "[first | No 720p | No 480p ] kb/s";
        private string maxVolume_meanVolume1 = "(Max | Mean)";
        private string showTitleToolTip;
        private Brush aboveChkbx3;
        private Brush aboveChkbx4;
        private Brush aboveChkbx7;
        [JsonIgnore]
        public readonly string DisabledPrefix = "[<DISABLED>] ";

        public AmqJob()
        {
        }

        public AmqJob(int id)
        {
            AboveChkbx7 = new AboveChbxRenderStatus()._default;
            AboveChkbx4 = new AboveChbxRenderStatus()._default;
            AboveChkbx3 = new AboveChbxRenderStatus()._default;
            Id = id;
        }
        /// <param name="quality">720/480/mp3</param>
        public string BuildFinalOutName(string quality, double volumeChangeLevel, bool justRet = false)
        {
            // outName_Quality_crf_
            string crf = $"_{Crf720}_";
            if (quality == "480") crf = $"_{Crf480}_";
            if (quality == "mp3") crf = $"_x_";

            var ext = quality == "mp3" ? "mp3" : "webm";
            // (the disabled marker has < > in it, which can't be in a file name)
            FinalOutFileName = $"{OutFileName}_{quality}{crf}{volumeChangeLevel}_{ShowTitle.Replace(DisabledPrefix, "").TrimEnd('.')}.{ext}";
            if (FromState == States.Process && quality != "mp3") FinalOutFileName = $"TMP--{FinalOutFileName}";

            if (justRet) return FinalOutFileName;

            if (quality == "720") Rendered720File = FinalOutFileName;
            if (quality == "480") Rendered480File = FinalOutFileName;
            if (quality == "mp3") RenderedMP3File = FinalOutFileName;
            return FinalOutFileName;
        }

        public void FixUpBitrates(string quality, string bitrate)
        {
            if (quality == "720") Bitrates = new Regex(@"\|\s(.*?)\s\|").Replace(Bitrates, $"| {bitrate} |");
            if (quality == "480") Bitrates = new Regex(@"\|\s(?:.(?!\|))+\s\]").Replace(Bitrates, $"| {bitrate} ]");
        }

        public void EnableBasics()
        {
            __EditBTN = true;
            __DuplicateJob = true;
            __CancelBTN = true;
            if (JobFailed) SetColorsToFailed();
        }

        public void DisableJob()
        {
            SetAllToFalse();
            __EditBTN = true;
            __DuplicateJob = true;
            __CancelBTN = true;
            JobFinished = true;
            JobFailed = true;
            if (!ShowTitle.StartsWith(DisabledPrefix))
                ShowTitle = $"{DisabledPrefix}{ShowTitle}";
            SetColorsToFailed();
            // well actually you can keep the catbox links buttons if the links exist...
            __CatboxLink720 = !string.IsNullOrEmpty(CatboxLink720);
            __CatboxLink480 = !string.IsNullOrEmpty(CatboxLink480);
            __CatboxLinkMP3 = !string.IsNullOrEmpty(CatboxLinkMP3);
        }
        public void SetColorsToFailed()
        {
            if (!AboveChkbx7.IsFrozen)
            {
                AboveChkbx7 = new AboveChbxRenderStatus().failed;
                AboveChkbx7.Freeze();
            }
            if (!AboveChkbx4.IsFrozen)
            {
                AboveChkbx4 = new AboveChbxRenderStatus().failed;
                AboveChkbx4.Freeze();
            }
            if (!AboveChkbx3.IsFrozen)
            {
                AboveChkbx3 = new AboveChbxRenderStatus().failed;
                AboveChkbx3.Freeze();
            }
        }

        public void WeDone()
        {
            // Jobs that got disabled (duplicate out name, missing input...) stay disabled. Without this they came back as
            // normal jobs after every run and were rendered the next time.
            if (JobFailed)
            {
                DisableJob();
                return;
            }

            __Rendered720File = Rendered720File != null;
            __Rendered480File = Rendered480File != null;
            __RenderedMP3File = RenderedMP3File != null;

            if (File.Exists(Rendered720File) || File.Exists(Rendered480File) || File.Exists(RenderedMP3File))
            {
                __CatboxLink720 = CatboxLink720 != null;
                __CatboxLink480 = CatboxLink480 != null;
                __CatboxLinkMP3 = CatboxLinkMP3 != null;

                __EditBTN = true;
                __DuplicateJob = true;
                __CancelBTN = true;
                __Rendered720File_EyeBtn = true;
                __GetVolumeBTN = true;
                __EditVolumeValueOrangeBTN = true;
                __VolumeEditSlider = true;
                __UploadToCatboxAllBTN = true;
            }
            else
            {
                JobFinished = false;
                __OutFileName = true;
                __OutFileSubDirectory = true;
                __Rendered720File = false;
                __Rendered480File = false;
                __RenderedMP3File = false;
                __Crf720 = true;
                __Crf480 = true;
                __CatboxLink720 = false;
                __CatboxLink480 = false;
                __CatboxLinkMP3 = false;
                __UploadToCatboxAllBTN = false;

                __Render720 = true;
                __Render480 = true;
                __RenderMP3 = true;
                __EditBTN = true;
                __DuplicateJob = true;
                __CancelBTN = true;
                __Rendered720File_EyeBtn = false;
                __GetVolumeBTN = true;
                __EditVolumeValueOrangeBTN = false;
                __VolumeEditSlider = true;
            }

            
        }

        public void SetAllToFalse()
        {
            __OutFileName = false;
            __OutFileSubDirectory = false;
            __Rendered720File = false;
            __Rendered480File = false;
            __RenderedMP3File = false;
            __Crf720 = false;
            __Crf480 = false;
            __CatboxLink720 = false;
            __CatboxLink480 = false;
            __CatboxLinkMP3 = false;
            __UploadToCatboxAllBTN = false;

            if (!string.IsNullOrEmpty(CatboxLink720)) __CatboxLink720 = true;
            if (!string.IsNullOrEmpty(CatboxLink480)) __CatboxLink480 = true;
            if (!string.IsNullOrEmpty(CatboxLinkMP3)) __CatboxLinkMP3 = true;

            if (__CatboxLink720 || __CatboxLink480 || __CatboxLinkMP3)
                __UploadToCatboxAllBTN = true;
            if (!__UploadToCatboxAllBTN && JobFinished)
                __UploadToCatboxAllBTN = true;

            __Render720 = false;
            __Render480 = false;
            __RenderMP3 = false;
            __EditBTN = false;
            __DuplicateJob = false;
            __CancelBTN = false;
            __Rendered720File_EyeBtn = false;
            __GetVolumeBTN = false;
            __EditVolumeValueOrangeBTN = false;
            __VolumeEditSlider = false;
        }

        public string ValidateJob()
        {
            if (new[] { Render720_ChkB, Render480_ChkB, RenderMP3_ChkB }.All(x => x == false)) return "Check at least one out of 720/480/mp3 checkboxes";

            if (!File.Exists(ShowTitleFullPath)) return "The input video path does not exist";

            if (FromState == States.Process && !File.Exists(AudioReplacementPath)) return "The audio replacement path does not exist";

            if (!Directory.Exists(MainWindow.config.conf.Folders.DefaultOutPath)) return $"The output directory does not exist anymore? Currently set dir:\n\n{MainWindow.config.conf.Folders.DefaultOutPath}";

            if (!string.IsNullOrEmpty(OutFileSubDirectory) && OutFileSubDirectory != "outFileSubDirectory")
            {
                var test = $"{MainWindow.config.conf.Folders.DefaultOutPath}\\{OutFileSubDirectory}";
                var isInvalid = PathHelper.PossiblyInvalid(test);
                if (isInvalid != "")
                {
                    return $"Invalid characters in out path / out file sub directory combination:\n\n{test}";
                }
            }

            return "";
        }

        public void ReCheck()
        {
            if (JobFailed)
            {
                // Don't really care about those that have already failed
                DisableJob();
                return;
            }
            if (JobFinished && !JobFailed) __EditVolumeValueOrangeBTN = true;

            string err = "";
            err = ValidateJob();
            if (err == "")
            {
                if (JobFinished)
                {
                    // Test if any of the rendered files were deleted
                    string dontExistAnymore = "";
                    var test = new string[] { Rendered720File, Rendered480File, RenderedMP3File };
                    var testCatbox = new string[] { CatboxLink720, CatboxLink480, CatboxLinkMP3 };
                    var testBtns = new bool[] { __CatboxLink720, __CatboxLink480, __CatboxLinkMP3 };
                    for (int i = 0; i < test.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(test[i]) && !File.Exists(test[i]))
                        {
                            dontExistAnymore += $" FILE MISSING \"{test[i]}]\"";
                            if (i == 0) { Rendered720File = null; };
                            if (i == 1) { Rendered480File = null; };
                            if (i == 2) { RenderedMP3File = null; };
                        }
                        if (!string.IsNullOrEmpty(testCatbox[i]) && !testBtns[i]) { if (i == 0) __CatboxLink720 = true; if (i == 1) __CatboxLink480 = true; if (i == 2) __CatboxLinkMP3 = true; };
                    }
                    var a = new List<bool>() { !string.IsNullOrEmpty(Rendered720File), !string.IsNullOrEmpty(CatboxLink720), __CatboxLink720 };
                    var b = new List<bool>() { !string.IsNullOrEmpty(Rendered480File), !string.IsNullOrEmpty(CatboxLink480), __CatboxLink480 };
                    var c = new List<bool>() { !string.IsNullOrEmpty(RenderedMP3File), !string.IsNullOrEmpty(CatboxLinkMP3), __CatboxLinkMP3 };
                    bool a1 = a.All(x => x == a.First());
                    bool b1 = b.All(x => x == b.First());
                    bool c1 = c.All(x => x == c.First());
                    if (a1 == true && b1 == true && c1 == true) __UploadToCatboxAllBTN = false;
                    else __UploadToCatboxAllBTN = true;
                    err += dontExistAnymore;

                    // Anything else to check for?
                }
            }

            if (err != "")
            {
                ShowTitleToolTip += $"      DISABLED BECASUE:    {err}";
                DisableJob();
            }
        }

        public void TryDeleteAllButVol0And(double volToNotDelete)
        {
            var outs = new string[] { Rendered720File, Rendered480File, RenderedMP3File };
            foreach (var o in outs)
            {
                if (!string.IsNullOrEmpty(o))
                {
                    // match on the file name only, an '_' in the folder path shifted everything
                    var dir = System.IO.Path.GetDirectoryName(o);
                    var name = System.IO.Path.GetFileName(o);
                    for (double i = -20; i <= 20; i += 0.5)
                    {
                        if (i == 0) continue; // don't delete the 0 volume change one
                        string pattern = @"(.*?_.*?_.*?_)(.*?)(_.*?$)";
                        var ms = Regex.Matches(name, pattern, RegexOptions.Singleline);
                        if (ms.Count != 0)
                        {
                            var m = ms[0];
                            var cand = System.IO.Path.Combine(dir, $"{m.Groups[1].Value}{i}{m.Groups[3].Value}");
                            if (File.Exists(cand))
                            {
                                File.Delete(cand);
                            }
                        }
                    }
                }
            }
        }

        //// Things that should be set additionally later
        public Brush AboveChkbx7 { get => aboveChkbx7; set { aboveChkbx7 = value; Changed(); } }
        public Brush AboveChkbx4 { get => aboveChkbx4; set { aboveChkbx4 = value; Changed(); } }
        public Brush AboveChkbx3 { get => aboveChkbx3; set { aboveChkbx3 = value; Changed(); } }
        public string FinalOutFileName { get; set; } = ""; // when doing batch jobs this will keep changing
        public bool JobFinished { get; set; } = false;
        public bool JobFailed { get; set; } = false;

        //// Bindings
        public int Id { get; set; }
        public double VolumeChangeValue { get => volumeChangeValue; set { volumeChangeValue = value; Changed(); } }
        public string ShowTitle { get; set; }
        public string ShowTitleToolTip { get => showTitleToolTip; set { showTitleToolTip = value; Changed(); } }
        public string OutFileName { get; set; } = "outFileNameHere";//e
        public string OutFileSubDirectory { get; set; } = "outFileSubDirectory";//e
        public string Rendered720File { get; set; }//
        public string Rendered720File_VOL0 { get; set; }//
        public string Rendered480File { get; set; }//
        public string Rendered480File_VOL0 { get; set; }//
        public string RenderedMP3File { get; set; }//
        public string RenderedMP3File_VOL0 { get; set; }//
        public string Crf720 { get; set; } = "20";//e
        public string Crf480 { get; set; } = "24";//e
        public string CatboxLink720 { get; set; }//
        public string CatboxLink480 { get; set; }//
        public string CatboxLinkMP3 { get; set; }//
        public bool Render720_ChkB { get; set; } = true;//e
        public bool Render480_ChkB { get; set; } = false;//e
        public bool RenderMP3_ChkB { get; set; } = false;//e
        public JobTimeStamps Timestamps { get; set; }//e
        public string EditVolumeValue { get; set; } = "+0dB";//
        public string maxVolume_meanVolume { get => maxVolume_meanVolume1; set { maxVolume_meanVolume1 = value; Changed(); } }
        public string Bitrates { get => bitrates; set { bitrates = value; Changed(); } }

        //These are used for IsEnabled / IsHitTestVisible settings
        public bool __OutFileName { get => outFileName; set { outFileName = value; Changed(); } }
        public bool __OutFileSubDirectory { get => outFileSubDirectory; set { outFileSubDirectory = value; Changed(); } }
        public bool __JustAudioOverOriginal { get => justAudioOverOriginal; set { justAudioOverOriginal = value; Changed(); } }
        public bool __Rendered720File { get => rendered720File; set { rendered720File = value; Changed(); } }
        public bool __Rendered480File { get => rendered480File; set { rendered480File = value; Changed(); } }
        public bool __RenderedMP3File { get => renderedMP3File; set { renderedMP3File = value; Changed(); } }
        public bool __Crf720 { get => crf720; set { crf720 = value; Changed(); } }
        public bool __Crf480 { get => crf480; set { crf480 = value; Changed(); } }
        public bool __CatboxLink720 { get => catboxLink720; set { catboxLink720 = value; Changed(); } }
        public bool __CatboxLink480 { get => catboxLink480; set { catboxLink480 = value; Changed(); } }
        public bool __CatboxLinkMP3 { get => catboxLinkMP3; set { catboxLinkMP3 = value; Changed(); } }
        public bool __Render720 { get => render720; set { render720 = value; Changed(); } }
        public bool __Render480 { get => render480; set { render480 = value; Changed(); } }
        public bool __RenderMP3 { get => renderMP3; set { renderMP3 = value; Changed(); } }
        public bool __EditBTN { get => editBTN; set { editBTN = value; Changed(); } }
        public bool __DuplicateJob { get => duplicateJob; set { duplicateJob = value; Changed(); } }
        public bool __CancelBTN { get => cancelBTN; set { cancelBTN = value; Changed(); } }
        public bool __Rendered720File_EyeBtn { get => rendered720File_EyeBtn; set { rendered720File_EyeBtn = value; Changed(); Changed(nameof(__ShowAutoVolumeBtn)); Changed(nameof(__ShowEyeBtn)); } }
        // Until a get clips job is rendered its (useless) eye button spot shows the auto volume bulb instead
        [JsonIgnore] public bool __ShowAutoVolumeBtn => FromState == States.Clips && !__Rendered720File_EyeBtn;
        [JsonIgnore] public bool __ShowEyeBtn => !__ShowAutoVolumeBtn;
        public bool __GetVolumeBTN { get => getVolumeBTN; set { getVolumeBTN = value; Changed(); } }
        public bool __EditVolumeValueOrangeBTN { get => editVolumeValueOrangeBTN; set { editVolumeValueOrangeBTN = value; Changed(); } }
        public bool __VolumeEditSlider { get => volumeEditSlider; set { volumeEditSlider = value; Changed(); } }
        public bool __UploadToCatboxAllBTN { get => uploadToCatboxAllBTN; set { uploadToCatboxAllBTN = value; Changed(); } }


        //// Other properties
        public long UsedAudioId_Warning1Based { get; set; }
        public long UsedSubsId_Warning0Based { get; set; }
        public string ShowTitleFullPath { get; set; }
        public string AudioReplacementPath { get; set; }
        public States FromState { get; set; }

    }
}
