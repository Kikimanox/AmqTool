using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// <summary>
    /// Interaction logic for MultiInputWindow.xaml
    /// </summary>
    public partial class MultiInputWindow : Window
    {
        public delegate void CheckTheseDelegate(List<VolumeCheckerFromRTB> check);
        public static event CheckTheseDelegate CheckThese;
        int paragraphIndex;
        int indexInParagraph;
        public List<VolumeCheckerFromRTB> toCheck = new List<VolumeCheckerFromRTB>();

        public string examples = "EXAMPLE 1:\nhttps://some.link1.com/asdf.mp3\nhttps://some.link2.com/asdf.webm\n\nEXAMPLE 2:\nhttps://l1.com/aaa.webm::saveFileName\nhttps://l2.com/bbb.mp4::NoExtensionNeededBtw\nhttps://l3.com/f/a.webm::no_space_Please\nhttps://just.check.dontsaveme/asd.flac\n\nEXAMPLE 3:\nYou can also input the clipboard result of the get links user script in amq expand library.\nSource: TODO_ADD_LINK\n\nBlue colored lines will only be checked\nOrange colored lines will be checked and downloaded";

        public class VolumeCheckerFromRTB
        {
            public string CatboxUrl { get; set; } = "";
            public string SaveFileName { get; set; } = "";
            public string AnnId { get; set; } = "";
        }

        public MultiInputWindow()
        {
            InitializeComponent();
            _RTB.Document.Blocks.Add(new Paragraph(new Run(examples)));
            _RTB.TextChanged += _RTB_TextChanged;
            _RTB.GotFocus += _RTB_GotFocus;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn.Name == "Save_BTN")
            {
                if (toCheck.Count == 0)
                {
                    MessageBox.Show($"No valid input in the rich text box.\nSome examples of what to do here:\n\n{examples}", "Please try again", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                CheckThese?.Invoke(toCheck); DialogResult = true;
            };
            if (btn.Name == "Cancel_BTN") DialogResult = false;
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

        private void _RTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            _RTB.TextChanged -= _RTB_TextChanged;
            var text = new TextRange(_RTB.Document.ContentStart, _RTB.Document.ContentEnd).Text;
            if (text.Contains("<e>") && text.Contains("</e>") && text.Contains("Links for:"))
            {
            }
            else
            {
                FixRTB_NoScript(text);
            }

            _RTB.TextChanged += _RTB_TextChanged;
        }

        private void _RTB_GotFocus(object sender, RoutedEventArgs e)
        {
            var text = new TextRange(_RTB.Document.ContentStart, _RTB.Document.ContentEnd).Text;
            if (text.Replace("\r", string.Empty).TrimEnd('\n') == examples)
            {
                _RTB.TextChanged -= _RTB_TextChanged;
                _RTB.Document.Blocks.Clear();
                _RTB.TextChanged += _RTB_TextChanged;
            }
        }

        private void FixRTB_NoScript(string txt)
        {
            SaveCaretState();
            _RTB.Document.Blocks.Clear();
            toCheck.Clear();

            var lines = txt.TrimEnd('\n').Replace("\r", string.Empty).Split('\n');

            foreach (var l in lines)
            {
                Paragraph para = new Paragraph();
                var words = l.Split(' ');
                bool wasValidUrl = false;
                bool first = true;
                foreach (var w in words)
                {
                    if (l.StartsWith("http") && w.StartsWith("http") && w.Contains("::") && !w.EndsWith("::"))
                    {
                        var s = w.Split(new string[] { "::" }, StringSplitOptions.None);
                        var uri = MainWindow.checkIfActuallyUrl(s.First());
                        var isInvalid = PathHelper.PossiblyInvalid(s[1]);

                        if (uri != "" || isInvalid == "")
                        {
                            var ext = uri.Split('.').Last();
                            var path = $"{MainWindow.config.conf.Folders.VolumeCheckerPath}\\{s[1]}.{ext}";
                            if (!File.Exists(path))
                            {
                                para.Inlines.Add(new Run(first ? w : $" {w}") { Foreground = Brushes.Orange });
                            }
                            else
                                para.Inlines.Add(new Run(first ? w : $" {w}"));
                        }
                        else
                            para.Inlines.Add(new Run(first ? w : $" {w}"));
                        wasValidUrl = uri != "";
                    }
                    else if (l.StartsWith("http") && w.StartsWith("http") && !w.EndsWith("::"))
                    {
                        var s = w.Split(new string[] { "::" }, StringSplitOptions.None);
                        var uri = MainWindow.checkIfActuallyUrl(s.First());
                        if (uri != "")
                            para.Inlines.Add(new Run(first ? w : $" {w}") { Foreground = Brushes.DeepSkyBlue });
                        else
                            para.Inlines.Add(new Run(first ? w : $" {w}"));
                        wasValidUrl = uri != "";
                    }

                    else
                    {
                        para.Inlines.Add(new Run(first ? w : $" {w}"));
                    }
                }
                _RTB.Document.Blocks.Add(para);

                if (wasValidUrl)
                {
                    if (!l.Contains("::"))
                    {
                        toCheck.Add(new VolumeCheckerFromRTB() { CatboxUrl = l.Split(' ').FirstOrDefault() });
                    }
                    else
                    {
                        var s = l.Split(new string[] { "::" }, StringSplitOptions.None);
                        toCheck.Add(new VolumeCheckerFromRTB() { CatboxUrl = s.First(), SaveFileName = s[1].Split(' ').First() });
                    }
                }
            }

            // unique
            toCheck = toCheck.GroupBy(o => new { o.CatboxUrl, o.SaveFileName }).Select(o => o.FirstOrDefault()).ToList();

            //_RTB.Document.Blocks.Add(new Paragraph(new Run()));            
            var text = new TextRange(_RTB.Document.ContentStart, _RTB.Document.ContentEnd).Text;
            RestoreCaretState();
        }

        public static void AppendText(RichTextBox box, string text, string color)
        {
            BrushConverter bc = new BrushConverter();
            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            tr.Text = text;
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty,
                    bc.ConvertFromString(color));
            }
            catch (FormatException) { }
        }

        private void tmpFnc()
        {
            //_RTB.Document.Blocks.Clear();
            //_RTB.VerticalOffset = ;
            //_RTB.AppendText(txt);
            //_RTB.AppendText("\u2028");
            // _RTB.Document.Blocks.Add(new Paragraph(new Run("Text")));
            // Bold myBold = new Bold(new Run("edit me!"));
            DialogResult = true;
        }

        public void SaveCaretState()
        {
            try
            {
                //enumerate and get the paragraph index
                paragraphIndex = -1;
                foreach (var p in _RTB.Document.Blocks)
                {
                    paragraphIndex++;
                    if (p == _RTB.CaretPosition.Paragraph)
                        break;
                }
                if (_RTB.CaretPosition.Paragraph == null)
                {
                    paragraphIndex = 0;
                    indexInParagraph = -1;
                    return; // when selecting all + delete
                }
                //get index relative to the start of the paragraph:
                indexInParagraph = _RTB.CaretPosition.Paragraph.ElementStart.GetOffsetToPosition(_RTB.CaretPosition);

            }
            catch { }
        }

        public void RestoreCaretState()
        {
            try
            {
                if (indexInParagraph != -1)
                {
                    // you might need to insure some conditions here (paragraph should exist and ...)
                    Paragraph para = _RTB.Document.Blocks.ElementAt(paragraphIndex) as Paragraph;
                    _RTB.CaretPosition = para.ElementStart.GetPositionAtOffset(indexInParagraph);
                }
                else
                {
                    _RTB.Document.Blocks.Clear();
                    Paragraph para = _RTB.Document.Blocks.Last() as Paragraph;
                    _RTB.CaretPosition = para.ElementEnd.GetPositionAtOffset(0);
                }

            }
            catch { }
        }

    }
}
