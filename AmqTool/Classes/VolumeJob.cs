using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AmqTool
{
    public class VolumeJob : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string savedFilePath = "";
        private string savedFilePathEdited = "";
        private string catboxLinkOORIGINAL = "";
        private string catboxLinkNEW = "";
        private string mediaInfo = "V: x [x] (x) | A: x (x)";
        private string maxVolume_meanVolume = "(Max | Mean)";
        private string detailedInfo = ""; // all the outputs joined from ffmpeg
        private int id;
        private double volumeChangeValue = 0.0;
        private string title = "";
        private string uplBtnStyle = "JobsBTN_Upload";
        private string title_for_tooltip = "";

        private bool isDownloaded = false;
        private bool isRegularJob = true;
        private bool uploadToCatboxAllBTN = false;


        public string SavedFilePath { get => savedFilePath; set { savedFilePath = value; Changed(); } }
        public string SavedFilePathEdited { get => savedFilePathEdited; set { savedFilePathEdited = value; Changed(); } }
        public string CatboxLinkOORIGINAL { get => catboxLinkOORIGINAL; set { catboxLinkOORIGINAL = value; Changed(); } }
        public string CatboxLinkNEW { get => catboxLinkNEW; set { catboxLinkNEW = value; Changed(); } }
        public string MediaInfo { get => mediaInfo; set { mediaInfo = value; Changed(); } }
        public string MaxVolume_meanVolume { get => maxVolume_meanVolume; set { maxVolume_meanVolume = value; Changed(); } }
        public string DetailedInfo { get => detailedInfo; set { detailedInfo = value; Changed(); } }
        public int Id { get => id; set { id = value; Changed(); } }
        public double VolumeChangeValue { get => volumeChangeValue; set { volumeChangeValue = value; Changed(); } }
        public bool __IsDownloaded { get => isDownloaded; set { isDownloaded = value; Changed(); } }
        public bool __UploadToCatboxAllBTN { get => uploadToCatboxAllBTN; set { uploadToCatboxAllBTN = value; Changed(); } }
        public bool __IsRegularJob { get => isRegularJob; set { isRegularJob = value; Changed(); } }
        public string Title { get => title; set { title = value; Changed(); } }
        public string Title_for_tooltip { get => title_for_tooltip; set { title_for_tooltip = value; Changed(); } }
        public string UplBtnStyle { get => uplBtnStyle; set { uplBtnStyle = value; Changed(); } }



        public VolumeJob(int id)
        {
            Id = id;
        }

        public VolumeJob SetupTmp(string uri)
        {
            __IsDownloaded = false;
            __IsRegularJob = false;
            __UploadToCatboxAllBTN = false;
            Title = "Downloading/Measuring 0%";
            Title_for_tooltip = $"{uri}";
            return this;
        }


    }
}
