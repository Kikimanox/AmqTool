using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;

namespace Mpv.Net.Wpf
{
    public class MpvDisplay : Control
    {
        static MpvDisplay()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MpvDisplay), new FrameworkPropertyMetadata(typeof(MpvDisplay)));
        }

        public delegate void MpvDisplayDelegate(object sender, string type, object extra_payload = null);
        static public event MpvDisplayDelegate GetDataFromMpvDisplay;

        public override void OnApplyTemplate()
        {
            if (GetTemplateChild("PART_Host") is WindowsFormsHost host
                && host.Child is System.Windows.Forms.Panel panel)
            {
                panel.BackColor = System.Drawing.Color.Black;
                panel.MouseWheel += Panel_MouseWheel;
                panel.AllowDrop = true;
                panel.DragDrop += Panel_DragDrop;
                panel.DragEnter += Panel_DragEnter;
                panel.MouseDown += Panel_MouseDown;
                panel.MouseDoubleClick += Panel_MouseDoubleClick;
            }
        }

        private void Panel_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                GetDataFromMpvDisplay?.Invoke(this, "DoubleClick");
        }

        private void Panel_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            e.Effect = System.Windows.Forms.DragDropEffects.Copy;
        }

        private void Panel_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                GetDataFromMpvDisplay?.Invoke(this, "DropFile", files);
            }
            if (e.Data.GetDataPresent(DataFormats.Text))
            {
                var txt = e.Data.GetData(DataFormats.Text);
                GetDataFromMpvDisplay?.Invoke(this, "DropText", txt);
            }
        }

        private void Panel_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Delta > 0) GetDataFromMpvDisplay?.Invoke(this, "MouseWheelUp");
            if (e.Delta < 0) GetDataFromMpvDisplay?.Invoke(this, "MouseWheelDown");
        }

        private void Panel_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right) GetDataFromMpvDisplay?.Invoke(this, "MouseDownR");
        }

        public IntPtr DisplayHandle
        {
            get
            {
                if (GetTemplateChild("PART_Host") is WindowsFormsHost host
                    && host.Child is System.Windows.Forms.Panel panel)
                {
                    return panel.Handle;
                }
                return IntPtr.Zero;
            }
        }
    }
}
