using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    public partial class KeybindSettings : Window
    {
        List<ObservableCollection<HotkeyProps>> hkeyList = new List<ObservableCollection<HotkeyProps>>();
        public HotkeyProps selected = null;
        public HotkeyProps newKeybind = null;
        bool neww = false;
        public KeybindSettings()
        {
            InitializeComponent();
            _SV.Visibility = Visibility.Visible;
            _SP_Changing.Visibility = Visibility.Collapsed;
            _Left_Btn.Visibility = Visibility.Collapsed;
            // ST: Home-0, Organize-1, Edit-2, Advanced-3
            // KB: Dynamic-0, Organizing-1, Edit-2, Adv-3, Home-4, Global-5
            foreach (var kb in MainWindow.SetKeybinds)
                hkeyList.Add(DictToObservable(kb));

            if (hkeyList.Count == 0)
            {
                MessageBox.Show("No keybinds set right now.");
                return;
            }
            //_Tmp_Keybinds.ItemsSource = hkeyList[0]; Disabled here for now
            //_Org_Keybinds.ItemsSource = hkeyList[1];
            //_Edi_Keybinds.ItemsSource = hkeyList[2];
            //_Adv_Keybinds.ItemsSource = hkeyList[3];
            //_Hom_Keybinds.ItemsSource = hkeyList[4];
            _Glo_Keybinds.ItemsSource = hkeyList[5];

            foreach (var listView in _Main_SP.Children.OfType<ListView>())
            {
                listView.MouseDoubleClick += listViewDoubleClick;
            }
        }

        private ObservableCollection<HotkeyProps> DictToObservable(Dictionary<string, Tuple<Key, string>> dict)
        {
            var ret = new ObservableCollection<HotkeyProps>();
            foreach (var kv in dict)
                ret.Add(new HotkeyProps(kv.Key, kv.Value.Item1.ToString(), kv.Value.Item2));
            return ret;
        }

        private void listViewDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var LV = sender as ListView;
            if (LV.SelectedIndex == -1) return;

            if (LV.Name == "_Tmp_Keybinds" && e.ChangedButton == MouseButton.Right)
            {
                var hk = (HotkeyProps)LV.SelectedItem;
                var res = MessageBox.Show($"Are you sure that you want to delete the keybind\n{hk.ActionName}", "Delete keybind", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    foreach (var d in MainWindow.SetKeybinds)
                    {
                        if (d.ContainsKey(hk.ActionName))
                        {
                            d.Remove(hk.ActionName);
                            break;
                        }
                    }
                    hkeyList[0].RemoveAt(LV.SelectedIndex);
                    MainWindow.config.conf.Keybindings.OrganizingModeDynamic = MainWindow.config.conf.Keybindings.OrganizingModeDynamic.Where(x => x.ActionName != hk.ActionName).ToArray();
                    MainWindow.config.UpdateConfig();
                }
                return;
            }

            _SV.Visibility = Visibility.Collapsed;
            _SP_Changing.Visibility = Visibility.Visible;
            _Left_Btn.Visibility = Visibility.Visible;
            _Right_Btn.Content = "Cancel";
            selected = (HotkeyProps)LV.SelectedItem;
            changeStatusText("Press desired keys to change keybind", color.normal);
            _NonMods_TB.Text = selected.Key;
            _Mods_TB.Text = selected.ModsReadable;
            _ChangingAction_Tbx.Text = selected.ActionName;
            LV.UnselectAll();
        }

        enum color { normal, okay, warning }
        private void changeStatusText(string text, color color)
        {
            if (color == color.normal) _Changing_Info_TB.Foreground = new SolidColorBrush(Colors.Gray);
            if (color == color.okay) _Changing_Info_TB.Foreground = new SolidColorBrush(Colors.Green);
            if (color == color.warning) _Changing_Info_TB.Foreground = new SolidColorBrush(Colors.Red);
            _Changing_Info_TB.Text = text;
        }

        List<Key> keysDown = new List<Key>();
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (selected == null) return;
            if (!Keyboard.IsKeyDown(Key.LeftAlt))
                if (keysDown.Contains(Key.LeftAlt))
                    keysDown.Remove(Key.LeftAlt);
            // while Alt is held WPF reports Key.System and puts the real key in SystemKey (LeftAlt itself, or e.g. S for Alt+S)
            Key K = e.Key == Key.System ? e.SystemKey : e.Key;
            if (K == Key.RightAlt) K = Key.LeftAlt;
            if (keysDown.Contains(K)) return;
            if (!isModifierKey(K))
                removeNonModifiers();
            keysDown.Add(K);
            handleKeys(true);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (selected == null) return;
            handleKeys(false, e.Key == Key.System ? e.SystemKey : e.Key);
        }

        private void removeNonModifiers()
        {
            if (keysDown.Count == 0) return;
            foreach (var k in keysDown)
            {
                if (isModifierKey(k)) continue;
                else
                {
                    keysDown.Remove(k);
                    return;
                }
            }
        }

        private bool isModifierKey(Key k) => (k == Key.LeftCtrl || k == Key.LeftShift || k == Key.LeftAlt);

        private void handleKeys(bool down, Key released = Key.F20)
        {
            if (released != Key.F20) keysDown.Remove(released);
            if (!down) return;

            int nonMods = 0;
            int mods = 0;
            foreach (var k in keysDown)
            {
                if (!isModifierKey(k)) nonMods++;
                else mods++;
            }

            if (nonMods == 0) return;
            _Mods_TB.Text = "None";

            foreach (var k in keysDown)
            {
                if (isModifierKey(k))
                {
                    var m = k.ToString().Substring(4);
                    if (_Mods_TB.Text == "None") _Mods_TB.Text = $"{m} ";
                    else _Mods_TB.Text = _Mods_TB.Text + $"{m} ";
                }
                else
                    _NonMods_TB.Text = k.ToString();
            }
            var added = parseSelected();
            if (added.Item1 == "") return;
            neww = isNew(added);
            if (!neww)
            {
                changeStatusText("This keybind already exists", color.warning);
                newKeybind = null;
                return;
            }
            else
            {
                changeStatusText("This keybind can be added", color.okay);
                newKeybind = new HotkeyProps(selected.ActionName, added.Item1, added.Item2);
                Console.WriteLine($"{newKeybind.ActionName} - {newKeybind.Key} - {newKeybind.Mods}");
            }
        }

        private Tuple<string, string> parseSelected()
        {
            var mods = new StringBuilder("000");
            string key = "";
            foreach (var k in keysDown)
            {
                if (k == Key.LeftShift) mods[0] = '1';
                if (k == Key.LeftCtrl) mods[1] = '1';
                if (k == Key.LeftAlt) mods[2] = '1';
                if (!isModifierKey(k)) key = k.ToString();
            }

            return new Tuple<string, string>(key, mods.ToString());
        }

        private bool isNew(Tuple<string, string> key_mods)
        {
            foreach (var kk in hkeyList)
                foreach (var k in kk)
                    if (k.Key == key_mods.Item1 && k.Mods == key_mods.Item2)
                        return false;
            return true;
        }

        private void _Btn_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Content.ToString() == "Close") Close();
            void Switch()
            {
                _SV.Visibility = Visibility.Visible;
                _SP_Changing.Visibility = Visibility.Collapsed;
                _Left_Btn.Visibility = Visibility.Collapsed;
                _Right_Btn.Content = "Close";
                neww = false;
                keysDown = new List<Key>();
            }
            if (btn.Content.ToString() == "Cancel")
            {
                newKeybind = null;
                _Right_Btn.Content = "Close";
                Switch();
            }
            if (btn.Content.ToString() == "Save")
            {
                if (!neww)
                {
                    MessageBox.Show("Please choose a new keybind", "Duplicate keybind", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                foreach (var kk in hkeyList)
                {
                    foreach (var k in kk)
                    {
                        if (k.Key == selected.Key && k.Mods == selected.Mods)
                        {
                            int idx = kk.IndexOf(selected);
                            updateNewKeybind(newKeybind);
                            kk[idx] = newKeybind;
                            Switch();
                            return;
                        }
                    }
                }
                if (dynamicSelecting)
                {
                    updateNewKeybind(newKeybind);
                    Switch();
                }
            }
        }

        private void updateNewKeybind(HotkeyProps newKb)
        {
            Key key;
            Enum.TryParse(newKb.Key, out key);
            foreach (var d in MainWindow.SetKeybinds)
                if (d.ContainsKey(newKb.ActionName))
                    d[newKb.ActionName] = new Tuple<Key, string>(key, newKb.Mods);
            var kb = MainWindow.config.conf.Keybindings;

            bool loopTru(HotkeyProps[] hks)
            {
                if (hks == null) return false;
                int i = 0;
                foreach (var hk in hks)
                {
                    if (hk.ActionName == newKb.ActionName)
                    {
                        hks[i] = newKb;
                        MainWindow.config.UpdateConfig();
                        return true;
                    }
                    i++;
                }
                return false;
            }

            if (loopTru(kb.AdvancedMode)) return;
            if (loopTru(kb.EditMode)) return;
            if (loopTru(kb.GlobalKeybinds)) return;
            if (loopTru(kb.HomeMode)) return;
            if (loopTru(kb.OrganizingMode)) return;
            if (loopTru(kb.OrganizingModeDynamic)) return;
            if (dynamicSelecting)
            {
                MainWindow.config.conf.Keybindings.OrganizingModeDynamic = MainWindow.config.conf.Keybindings.OrganizingModeDynamic.Concat(new HotkeyProps[] { newKb }).ToArray();
                MainWindow.SetKeybinds[0].Add(newKb.ActionName, new Tuple<Key, string>(key, newKb.Mods));
                MainWindow.config.UpdateConfig();
                hkeyList[0].Add(newKb);
                dynamicSelecting = false;
            }
        }

        private string getFolderSinglePathFromOfd()
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = MainWindow.config.conf.Folders.LastFolderPickerPath;
            dialog.Multiselect = true;
            dialog.IsFolderPicker = true;
            dialog.Multiselect = false;
            dialog.Title = "Select which folder saving to bind keybind to";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var tmp = dialog.FileName.Split('\\');
                MainWindow.config.conf.Folders.LastFolderPickerPath = string.Join("\\", tmp.Take(tmp.Length - 1));
                MainWindow.config.UpdateConfig();
                return dialog.FileName;
            }
            return "";
        }

        public bool dynamicSelecting = false;
        private void New_Dynamic_KB_Click(object sender, RoutedEventArgs e)
        {
            var path = getFolderSinglePathFromOfd();
            if (path == "") return;
            dynamicSelecting = true;
            _SV.Visibility = Visibility.Collapsed;
            _SP_Changing.Visibility = Visibility.Visible;
            _Left_Btn.Visibility = Visibility.Visible;
            _Right_Btn.Content = "Cancel";
            selected = new HotkeyProps($"Save to {path}", "", "000");
            changeStatusText("Press desired keys to assign keybind", color.normal);
            _NonMods_TB.Text = selected.Key;
            _Mods_TB.Text = selected.ModsReadable;
            _ChangingAction_Tbx.Text = selected.ActionName;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Escape)) Close();
        }
    }
}
