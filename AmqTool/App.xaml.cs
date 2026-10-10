using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace AmqTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App() : base()
        {
            // config.json, jobs.json, the tmp folder and mpv-1.dll are all opened with relative paths,
            // so always run from the exe's folder no matter how we got launched (pinned taskbar icon, updater, cmd...)
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            // Numbers end up in ffmpeg arguments and file names ("volume=+7.5dB"), so always format them with a dot,
            // also on PCs set to a comma decimal language (German, Slovenian...)
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        static DateTime lastErrorAt = DateTime.MinValue;
        static int errorsInARow;

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var now = DateTime.Now;
            errorsInARow = (now - lastErrorAt).TotalSeconds < 10 ? errorsInARow + 1 : 1;
            lastErrorAt = now;

            MessageBox.Show("Unhandled exception occurred: \n" + e.Exception.Message + "\n\nStack Trace:\n\n" + e.Exception.StackTrace, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Without Handled WPF closes the app right after this box. Once the main window is up an error is usually
            // just one action that broke, so keep running. During startup, or when errors keep coming, close like before.
            e.Handled = MainWindow != null && MainWindow.IsLoaded && errorsInARow <= 3;
        }

    }


}

