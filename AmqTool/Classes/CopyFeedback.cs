using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AmqTool
{
    /// <summary>
    /// Copies text to the clipboard and shows a small "Copied" bubble above the clicked control,
    /// so the user can see that something actually happened.
    /// </summary>
    public static class CopyFeedback
    {
        static Popup popup;
        static Border bubble;
        static TextBlock label;
        static DispatcherTimer hideTimer;

        /// <summary>Copies <paramref name="text"/> and shows "Copied" (or why it failed) above <paramref name="anchor"/>.</summary>
        /// <returns>true if the text is on the clipboard</returns>
        public static bool Copy(string text, FrameworkElement anchor, string what = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                Show(anchor, "Nothing to copy", false);
                return false;
            }

            // the clipboard can be locked by another app for a moment (CLIPBRD_E_CANT_OPEN), try a few times
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    Show(anchor, what == null ? "Copied" : $"Copied {what}", true);
                    return true;
                }
                catch (COMException) { Thread.Sleep(50); }
                catch (ExternalException) { Thread.Sleep(50); }
            }
            Show(anchor, "Copy failed, clipboard is busy", false);
            return false;
        }

        /// <summary>Shows a short bubble above <paramref name="anchor"/> that fades out by itself.</summary>
        public static void Show(FrameworkElement anchor, string message, bool ok = true)
        {
            if (anchor == null || !anchor.IsLoaded) return;
            Init();

            hideTimer.Stop();
            bubble.BeginAnimation(UIElement.OpacityProperty, null);
            popup.IsOpen = false; // reopen so it moves to the new anchor

            label.Text = message;
            bubble.Background = new SolidColorBrush(ok ? Color.FromRgb(0x2e, 0x7d, 0x32) : Color.FromRgb(0xc6, 0x28, 0x28));
            popup.PlacementTarget = anchor;
            bubble.Opacity = 1;
            popup.IsOpen = true;

            hideTimer.Interval = TimeSpan.FromMilliseconds(ok ? 1100 : 2200);
            hideTimer.Start();
        }

        static void Init()
        {
            if (popup != null) return;

            label = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
            };
            bubble = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 4),
                Margin = new Thickness(0, 0, 0, 4),
                Child = label,
                IsHitTestVisible = false,
            };
            popup = new Popup
            {
                Child = bubble,
                AllowsTransparency = true,
                Placement = PlacementMode.Top,
                StaysOpen = true,
                IsHitTestVisible = false,
                Focusable = false,
                PopupAnimation = PopupAnimation.None,
            };
            hideTimer = new DispatcherTimer();
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
                fade.Completed += (s2, e2) =>
                {
                    // only close if nothing new was shown in the meantime
                    if (!hideTimer.IsEnabled) popup.IsOpen = false;
                };
                bubble.BeginAnimation(UIElement.OpacityProperty, fade);
            };
        }
    }
}
