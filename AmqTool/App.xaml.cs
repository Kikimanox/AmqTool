using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
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
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Unhandled exception occurred: \n" + e.Exception.Message + "\n\nStack Trace:\n\n" + e.Exception.StackTrace, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }


}

