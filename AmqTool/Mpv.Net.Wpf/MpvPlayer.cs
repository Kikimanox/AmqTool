using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Mpv.Net.Wpf
{
    /// <summary>
    /// Mpv Player controller. Main Component
    /// </summary>
    public sealed class MpvPlayer : Control, IDisposable
    {

        public Mpv.NET.Player.MpvPlayer _player;
        private readonly Locker _locker;
        public bool isClosing = false;
        public string PlayingFilePath = "";
        public TimeSpan? maxPossible;

        public List<long> aid = new List<long>();
        public List<string> aN = new List<string>();

        public List<long> sid = new List<long>();
        public List<string> sN = new List<string>();
        public struct MyTimestamp
        {
            private TimeSpan time;
            public TimeSpan Time { get => time; set { StrVal = ConvertStr(value); time = value; } }
            private string strVal;
            public string StrVal { get => strVal; set => strVal = value; }

            private string ConvertStr(TimeSpan t)
            {
                if (!t.ToString().Contains(".")) return t.ToString();
                return t.ToString().TrimEnd('0');
            }

            public void Reset(Mpv.NET.Player.MpvPlayer p, bool start)
            {
                if (p != null)
                {
                    if (start)
                    {
                        Time = TimeSpan.FromSeconds(0);
                    }
                    else
                    {
                        Time = p.Duration;
                    }
                }
            }

            public string UpdateTime(string t_string)
            {
                TimeSpan ts;
                if (!TimeSpan.TryParse(t_string, out ts))
                {
                    // throw new Exception("Invalid time format.");
                    return ""; // Don't do anything
                }
                time = ts;
                strVal = t_string;
                return t_string;
            }

            public MyTimestamp(TimeSpan time) : this()
            {
                Time = time;
                StrVal = ConvertStr(time);
            }
        }

        public MyTimestamp startTimestamp;
        public MyTimestamp endTimestamp;


        public MpvDisplay Display
        {
            get { return (MpvDisplay)GetValue(DisplayProperty); }
            set { SetValue(DisplayProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Display.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DisplayProperty =
            DependencyProperty.Register("Display", typeof(MpvDisplay), typeof(MpvPlayer), new PropertyMetadata(null));

        static MpvPlayer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MpvPlayer), new FrameworkPropertyMetadata(typeof(MpvPlayer)));
        }

        public MpvPlayer()
        {
            _locker = new Locker();
        }

        public void Dispose()
        {
            if (_player != null)
            {
                _player.PositionChanged -= _player_PositionChanged;
                _player.Stop();
                _player.Dispose();
                _player = null;
            }
        }

        public override void OnApplyTemplate()
        {
            var playpause = GetTemplateChild("PART_PlayPause") as Button;
            playpause.Click += Playpause_Click;

            var stop = GetTemplateChild("PART_Stop") as Button;
            stop.Click += Stop_Click;

            var jump_left = GetTemplateChild("PART_JumpStart") as Button;
            jump_left.PreviewMouseDown += Jump_right_PreviewMouseDown;

            var jump_right = GetTemplateChild("PART_JumpEnd") as ToggleButton;
            jump_right.PreviewMouseDown += Jump_right_PreviewMouseDown;

            var jump_left_m = GetTemplateChild("PART_JumpMarkerStart") as Button;
            jump_left_m.Click += Jump_Click_Marker;

            var jump_right_m = GetTemplateChild("PART_JumpMarkerEnd") as Button;
            jump_right_m.Click += Jump_Click_Marker;

            var copyClip = GetTemplateChild("PART_CopyCurTimestampToClipb") as Button;
            copyClip.Click += CopyClip_Click;

            var volume = GetTemplateChild("PART_Volume") as Slider;
            volume.ValueChanged += Volume_ValueChanged;
            //volume.MouseWheel += Volume_MouseWheel;

            var seek = GetTemplateChild("PART_Seek") as Slider;
            seek.ValueChanged += Seek_ValueChanged;
            seek.ApplyTemplate();
            Thumb thumb = (seek.Template.FindName("PART_Track", seek) as Track).Thumb;
            thumb.MouseEnter += new MouseEventHandler(thumb_MouseEnter);
            seek.IsEnabled = false;

            Loaded += MpvPlayer_Loaded;

        }

        private void thumb_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_player == null) return;
            if (_player.Duration == TimeSpan.FromSeconds(0)) return;
            if (e.LeftButton == MouseButtonState.Pressed && e.MouseDevice.Captured == null)
            {
                MouseButtonEventArgs args = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left);
                args.RoutedEvent = MouseLeftButtonDownEvent;
                (sender as Thumb).RaiseEvent(args);
            }
        }

        private void Jump_right_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var btn = (sender as Button);
                if (btn == null)
                {
                    var btn2 = (sender as ToggleButton);
                    if (btn2.Name == "PART_JumpEnd")
                    {
                        if (btn2.IsChecked == true)
                        { // was clicked while checked
                            maxPossible = null;
                        }
                        else
                        { // was click while unchecked
                            maxPossible = endTimestamp.Time;
                            _player.Position = endTimestamp.Time;
                        }
                    }
                    return;
                }

                if (btn.Name == "PART_JumpStart")
                {
                    _player.Position = startTimestamp.Time;
                    if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed) PlayIfPaused();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Exception at jumping (check your timestamps)", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public delegate void JumpMarkerClickedDelegate(object sender, string jumpType, string timeStr);
        static public event JumpMarkerClickedDelegate GetJumpTime;
        private void Jump_Click_Marker(object sender, RoutedEventArgs e)
        {
            var btn = (sender as Button);
            if (btn.Name == "PART_JumpMarkerStart")
            {
                var str = _player.Position.ToString().TrimEnd('0');
                if (!_player.Position.ToString().Contains(".")) str = _player.Position.ToString();
                GetJumpTime?.Invoke(this, "Start", str);
            }
            if (btn.Name == "PART_JumpMarkerEnd")
            {
                var str = _player.Position.ToString().TrimEnd('0');
                if (!_player.Position.ToString().Contains(".")) str = _player.Position.ToString();
                GetJumpTime?.Invoke(this, "End", str);
            }
        }


        private void CopyClip_Click(object sender, RoutedEventArgs e)
        {
            if (_player.IsMediaLoaded)
            {
                var str = _player.Position.ToString().TrimEnd('0');
                if (!_player.Position.ToString().Contains(".")) str = _player.Position.ToString();
                System.Windows.Forms.Clipboard.SetText(str);

            }
            else
            {
                MessageBox.Show("No media loaded!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Volume_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            int _chg = 5;
            int change = e.Delta > 0 ? _chg : -_chg;
            ChangeVolume(change);
        }

        public void MoveBySeconds(int m)
        {
            if (_player == null) return;
            var newTime = _player.Position + TimeSpan.FromSeconds(m);
            if (newTime.TotalMilliseconds > _player.Duration.TotalMilliseconds) return;
            if (newTime.TotalMilliseconds < TimeSpan.FromSeconds(0).TotalMilliseconds) return;
            _player.Position = newTime;
        }

        public void ChangeVolume(int change)
        {
            if (_player == null) return;
            if (change > 0 && _player.Volume == 100) return;
            if (change < 0 && _player.Volume == 0) return;
            var newVol = _player.Volume + change;
            if (newVol > 100) newVol = 100;
            if (newVol < 0) newVol = 0;
            _player.Volume = newVol;
            var volume = GetTemplateChild("PART_Volume") as Slider;
            volume.Value = newVol;
        }

        private void MpvPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _player = new NET.Player.MpvPlayer(Display.DisplayHandle);
                _player.AutoPlay = false;
                _player.EnableYouTubeDl();
                _player.KeepOpen = NET.Player.KeepOpen.Yes;
                _player.MediaLoaded += _player_MediaLoaded;
                _player.MediaError += _player_MediaError;
                _player.PositionChanged += _player_PositionChanged;
            }
            catch (NET.Player.MpvPlayerException ex)
            {
                _player = null;
                Console.WriteLine(ex.Message);
                // MessageBox.Show(ex.Message, "Mpv loading exception");
            }
        }

        public delegate void MediaErrorDelegate(object sender, string stackTrace);
        static public event MediaErrorDelegate MediaError;
        private void _player_MediaError(object sender, EventArgs e)
        {
            MediaError?.Invoke(this, Environment.StackTrace);
        }

        public bool isPlaying()
        {
            if (_player == null) return false;
            return _player.IsPlaying;
        }

        public string GetPosition()
        {
            if (_player == null) return "00:00:00";
            var str = _player.Position.ToString().TrimEnd('0');
            if (!_player.Position.ToString().Contains(".")) str = _player.Position.ToString();
            return str;
        }

        public void LoadFile(string fileName)
        {
            if (_player != null)
            {
                _player?.Stop();
                _locker.PerformLockAction(() =>
                {
                    if (GetTemplateChild("PART_Seek") is Slider seek)
                    {
                        seek.Value = 0;
                    }

                    if (GetTemplateChild("PART_Volume") is Slider volume)
                        _player.Volume = Convert.ToInt32(volume.Value);
                });
                System.Threading.Thread.Sleep(100);
                _player.Load(fileName, true);
                PlayingFilePath = fileName;
                maxPossible = null;
            }
        }

        public delegate void PlayerLoadedDelegate(object sender, string type, object extra_payload = null);
        static public event PlayerLoadedDelegate PlayerLoaded;
        private void _player_MediaLoaded(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                //GetChapters();
                GetAudioAndSubtitles();
                var timMax = GetTemplateChild("PART_max") as TextBlock;
                var str = _player.Duration.ToString().TrimEnd('0');
                if (!_player.Duration.ToString().Contains(".")) str = _player.Duration.ToString();
                timMax.Text = str;
                startTimestamp = new MyTimestamp(TimeSpan.FromSeconds(0));
                endTimestamp = new MyTimestamp(_player.Duration);
                if (GetTemplateChild("PART_Seek") is Slider seek)
                    seek.Value = 0;
                _player.Position = TimeSpan.FromSeconds(0);
                PlayPause();
                System.Threading.Thread.Sleep(20);
                PlayPause();
                System.Threading.Thread.Sleep(20);
                PauseIfPlaying();
                PlayIfPaused();
                (GetTemplateChild("PART_Seek") as Slider).IsEnabled = true;
                PlayerLoaded?.Invoke(this, "Loaded", "");
            });

        }

        private void GetAudioAndSubtitles()
        {
            aid = new List<long>();
            aN = new List<string>();

            sid = new List<long>();
            sN = new List<string>();

            long tracks = _player.API.GetPropertyLong("track-list/count");
            for (long track = 0; track < tracks; ++track)
            {
                string type = _player.API.GetPropertyString($"track-list/{track}/type");
                long id = _player.API.GetPropertyLong(($"track-list/{track}/id"));

                string cc = _player.API.GetPropertyString($"track-list/{track}/"); //

                if (type == "audio")
                {
                    aid.Add(id);
                    var lang = _player.API.GetPropertyString($"track-list/{track}/lang") ?? "unknownLang";
                    var codec = _player.API.GetPropertyString($"track-list/{track}/codec") ?? "unknownCodec";
                    var title = _player.API.GetPropertyString($"track-list/{track}/title") ?? "unknownTitle";
                    var audio_channels = _player.API.GetPropertyString($"track-list/{track}/audio-channels") ?? "unknownTitle";
                    aN.Add($"[{lang}] {title} ({codec}) (ac{audio_channels})");
                }
                else if (type == "sub")
                {
                    sid.Add(id);
                    var lang = _player.API.GetPropertyString($"track-list/{track}/lang") ?? "unknownLang";
                    var title = _player.API.GetPropertyString($"track-list/{track}/title") ?? "unknownTitle";
                    sN.Add($"[{lang}] - {title}");
                }
            }

            if (GetTemplateChild("PART_Audio") is Button audio)
            {
                if (aid.Count > 0) _player.API.SetPropertyLong("aid", 1);
                CreateMenu(audio, aid, aN, (sender, e) =>
                {
                    if (_player != null
                    && sender is MenuItem item
                    && long.Parse(item.Tag.ToString()) is long id)
                    {
                        _player.API.SetPropertyLong("aid", id); // WARNING: FOR SOME REASON AUDIO IS 1 INDEX BASED!!!
                        foreach (MenuItem i in (item.Parent as ContextMenu).Items)
                        {
                            i.Icon = null;
                            i.Foreground = System.Windows.Media.Brushes.Black;
                            i.FontWeight = FontWeights.Regular;
                            if (i.Header == item.Header)
                            {
                                i.Icon = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Assets/checkmark.png", UriKind.Relative)) }; //idk why the icon doesn't work
                                i.Foreground = System.Windows.Media.Brushes.Green;
                                i.FontWeight = FontWeights.Bold;
                            }
                        }
                    }
                });

            }
            if (GetTemplateChild("PART_Subtitle") is Button subtitle)
            {
                if (sid.Count > 0) sid.Insert(0, 0);
                if (sid.Count > 0) sN.Insert(0, "No subs");
                if (sid.Count > 0) _player.API.SetPropertyLong("sid", 1); // lets use the first available subs if any exist
                CreateMenu(subtitle, sid, sN, (sender, e) =>
                {
                    if (_player != null
                    && sender is MenuItem item
                    && long.Parse(item.Tag.ToString()) is long id)
                    {
                        _player.API.SetPropertyLong("sid", id); // WARNING: FOR SOME REASON SUBS ARE 0 INDEX BASED!!!
                        foreach (MenuItem i in (item.Parent as ContextMenu).Items)
                        {
                            i.Icon = null;
                            i.Foreground = System.Windows.Media.Brushes.Black;
                            i.FontWeight = FontWeights.Regular;
                            if (i.Header == item.Header)
                            {
                                i.Icon = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("Assets/checkmark.png", UriKind.Relative)) }; //idk why the icon doesn't work
                                i.Foreground = System.Windows.Media.Brushes.Green;
                                i.FontWeight = FontWeights.Bold;
                            }
                        }

                    }
                });
            }
        }

        private void CreateMenu(Button target, List<long> ids, List<string> txts, Action<object, RoutedEventArgs> clickAction)
        {
            // exactly one click handler per button (this used to add another one on every file load)
            target.Click -= OpenButtonContextMenu;
            target.Click += OpenButtonContextMenu;
            if (ids.Count < 1)
            {
                target.ContextMenu = null; // no tracks in this file, don't keep offering the previous file's ones
                return;
            }
            ContextMenu menu = new ContextMenu();
            for (int i = 0; i < ids.Count; i++)
            {
                var menuitem = new MenuItem();
                menuitem.Name = $"_MI_{target.Name}_{ids[i]}";
                menuitem.Header = txts[i];
                menuitem.Tag = $"{ids[i]}";
                menuitem.Click += (sender, e) => clickAction(sender, e);
                if ((i == 0 && target.Name == "PART_Audio") || (i == 1 && target.Name == "PART_Subtitle" && txts.Count > 0) || (i == 0 && target.Name == "PART_Subtitle" && txts.Count == 0))
                {
                    menuitem.Foreground = System.Windows.Media.Brushes.Green;
                    menuitem.FontWeight = FontWeights.Bold;
                }

                menu.Items.Add(menuitem);
            }
            target.ContextMenu = menu;
        }

        private static void OpenButtonContextMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.ContextMenu != null) b.ContextMenu.IsOpen = true;
        }

        private void GetChapters()
        {
            if (_player == null) return;
            long chapters = _player.API.GetPropertyLong("chapters"); //set -> chapter
            ContextMenu chaptersMenu = new ContextMenu();
            for (long i = 0; i < chapters; i++)
            {
                var menuitem = new MenuItem();
                menuitem.Header = $"Chapter {i}";
                menuitem.Tag = i;
                menuitem.Click += (sender, args) =>
                {
                    if (_player != null
                    && sender is MenuItem item
                    && item.Tag is long chapter)
                    {
                        _player.API.SetPropertyLong("chapter", chapter);
                    }
                };
                chaptersMenu.Items.Add(menuitem);
            }
            if (GetTemplateChild("PART_Chapters") is Button btnChapters)
            {
                btnChapters.ContextMenu = null;
                btnChapters.ContextMenu = chaptersMenu;
                btnChapters.Click += (sender, e) =>
                {
                    btnChapters.ContextMenu.IsOpen = true;
                };
            }
        }

        public void Stop()
        {
            _player?.Stop();
            var timMax = GetTemplateChild("PART_max") as TextBlock;
            timMax.Text = "00:00:00.000";
            var timCur = GetTemplateChild("PART_cur") as TextBlock;
            timCur.Text = "00:00:00.000";
        }

        public void PlayPause()
        {
            Playpause_Click(null, null);
        }

        // _player is null when mpv-1.dll couldn't be loaded; the rest of the app should still work then
        public void PauseIfPlaying()
        {
            if (_player != null && _player.IsPlaying)
            {
                PlayPause();
            }
        }

        public void PlayIfPaused()
        {
            if (_player != null && !_player.IsPlaying)
            {
                PlayPause();
            }
        }


        private void Seek_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_locker.IsLocked && _player != null)
            {
                if (_player.Duration == TimeSpan.FromSeconds(0)) return;
                _player.Position = TimeSpan.FromSeconds(e.NewValue);
            }
        }

        private void Volume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_player != null)
            {
                _player.Volume = Convert.ToInt32(e.NewValue);
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _player?.Stop();
        }

        private void Playpause_Click(object sender, RoutedEventArgs e)
        {
            if (_player != null)
            {
                if (_player.IsPlaying)
                {
                    _player.Pause();
                }
                else
                {
                    _locker.PerformLockAction(() =>
                    {
                        if (GetTemplateChild("PART_Seek") is Slider seek)
                        {
                            seek.Maximum = _player.Duration.TotalSeconds;
                        }
                    });
                    _player.Resume();
                }
            }
        }

        private void _player_PositionChanged(object sender, NET.Player.MpvPlayerPositionChangedEventArgs e)
        {
            if (isClosing) return;
            _locker.PerformLockAction(() =>
            {
                if (GetTemplateChild("PART_Seek") is Slider seek &&
                    _player.IsMediaLoaded)
                {
                    seek.Value = e.NewPosition.TotalSeconds;
                    var timCur = GetTemplateChild("PART_cur") as TextBlock;
                    var txt = _player.Position.ToString().TrimEnd('0');
                    if (!_player.Position.ToString().Contains(".")) txt = _player.Position.ToString();
                    timCur.Text = txt.PadRight("00:00:00.000".Length, '0');
                    if (maxPossible != null)
                    {
                        if (_player.Position.TotalMilliseconds >= maxPossible.Value.TotalMilliseconds)
                        {
                            _player.Pause();
                            _player.Position = maxPossible.Value;
                        }
                    }
                }
            });
        }
    }
}
