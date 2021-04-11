using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
using System.Windows.Shapes;

namespace AmqTool
{
    public partial class editffmpegstuff : Window
    {
        public ObservableCollection<string> advExtensions = new ObservableCollection<string>();
        public List<string> commands = new List<string>();
        public editffmpegstuff()
        {
            InitializeComponent();
            setupAdvancedCbx(MainWindow.config.conf.Extensions);
            _AdvCbx.ItemsSource = advExtensions;
            _AdvCbx.SelectedIndex = 6;
            _chkbx.IsChecked = MainWindow.config.conf.WindowProps.Use2PassEnc;
        }

        private void setupAdvancedCbx(Extension[] source)
        {
            advExtensions = new ObservableCollection<string>();
            commands = new List<string>();
            int i = 0;
            foreach (var ex in source)
            {
                i = 0;
                foreach (var us in ex.Usage)
                {
                    advExtensions.Add($"{ex.Id}.{i++} [{ex.Name}] - {us.Description}");
                    commands.Add(us.Call);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Name == "Save_BTN")
            {
                var res = MessageBox.Show("Are you sure you want to save everything that you have changed?", "Saving?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    saveCommands();
                    DialogResult = true;
                }
                if (res == MessageBoxResult.No)
                {
                    DialogResult = false;
                }

            };
            if (btn.Name == "Cancel_BTN") DialogResult = false;
            if (btn.Name == "Default_BTN")
            {
                var res = MessageBox.Show("Are you sure that you want to restore all ffmpeg settings to their default original values? (This will be applied right away, regardless if 'Done' or 'Cancel' is clicked upon exit.)", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    setupAdvancedCbx(new Config("config.template").conf.Extensions);
                    saveCommands();
                    int tmp = _AdvCbx.SelectedIndex;
                    _AdvCbx.SelectedIndex = -1;
                    _AdvCbx.SelectedIndex = tmp;
                }
            }
        }

        private void saveCommands()
        {
            int i = 0;
            for (int j = 0; j < MainWindow.config.conf.Extensions.Length; j++)
            {
                for (int z = 0; z < MainWindow.config.conf.Extensions[j].Usage.Length; z++)
                {
                    MainWindow.config.conf.Extensions[j].Usage[z].Call = commands[i++];
                }
            }
            MainWindow.config.UpdateConfig();
        }

        private void _RTB_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var rtb = sender as RichTextBox;
            ContextMenu contextMenu = rtb.ContextMenu;
            contextMenu.PlacementTarget = rtb;

            // This uses HorizontalOffset and VerticalOffset properties to position the menu,
            // relative to the upper left corner of the parent element (RichTextBox in this case).
            contextMenu.Placement = PlacementMode.RelativePoint;

            // Compute horizontal and vertical offsets to place the menu relative to selection end.
            TextPointer position = rtb.Selection.End;

            if (position == null) return;

            Rect positionRect = position.GetCharacterRect(LogicalDirection.Forward);
            contextMenu.HorizontalOffset = positionRect.X;
            contextMenu.VerticalOffset = positionRect.Y;

            // Finally, mark the event has handled.
            contextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void _AdvCbx_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_AdvCbx.SelectedIndex == -1)
            {
                _AdvCbx.SelectedIndex = 0;
                return;
            }
            var cmd = commands[_AdvCbx.SelectedIndex];
            _RTB.Document.Blocks.Clear();
            _RTB.Document.Blocks.Add(new Paragraph(new Run(cmd)));
        }

        private void _RTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_AdvCbx.SelectedIndex == -1) return;
            _RTB.IsReadOnly = advExtensions[_AdvCbx.SelectedIndex].EndsWith("[Don't edit]");
            var text = new TextRange(_RTB.Document.ContentStart, _RTB.Document.ContentEnd).Text.Replace("\n", "").Replace("\r", "");
            commands[_AdvCbx.SelectedIndex] = text;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e) => MainWindow.config.conf.WindowProps.Use2PassEnc = true;
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e) => MainWindow.config.conf.WindowProps.Use2PassEnc = false;
    }
}
