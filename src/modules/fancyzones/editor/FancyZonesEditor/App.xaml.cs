// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using FancyZonesEditor.Utils;
using ManagedCommon;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Non-localizable strings
        private const string CrashReportLogFile = "FZEditorCrashLog.txt";
        private const string ErrorReportLogFile = "FZEditorErrorLog.txt";
        private const string PowerToysIssuesURL = "https://aka.ms/powerToysReportBug";

        private const string CrashReportExceptionTag = "Exception";
        private const string CrashReportSourceTag = "Source: ";
        private const string CrashReportTargetAssemblyTag = "TargetAssembly: ";
        private const string CrashReportTargetModuleTag = "TargetModule: ";
        private const string CrashReportTargetSiteTag = "TargetSite: ";
        private const string CrashReportEnvironmentTag = "Environment";
        private const string CrashReportCommandLineTag = "* Command Line: ";
        private const string CrashReportTimestampTag = "* Timestamp: ";
        private const string CrashReportOSVersionTag = "* OS Version: ";
        private const string CrashReportIntPtrLengthTag = "* IntPtr Length: ";
        private const string CrashReportx64Tag = "* x64: ";
        private const string CrashReportCLRVersionTag = "* CLR Version: ";
        private const string CrashReportAssembliesTag = "Assemblies - ";
        private const string CrashReportDynamicAssemblyTag = "dynamic assembly doesn't have location";
        private const string CrashReportLocationNullTag = "location is null or empty";

        public MainWindowSettingsModel MainWindowSettings { get; }

        public static FancyZonesEditorIO FancyZonesEditorIO { get; private set; }

        public static Overlay Overlay { get; private set; }

        public static int PowerToysPID { get; set; }

        public static bool DebugMode
        {
            get
            {
                return _debugMode;
            }
        }

        private static bool _debugMode;

        [Conditional("DEBUG")]
        private void DebugModeCheck()
        {
            _debugMode = true;
        }

        public App()
        {
            DebugModeCheck();
            FancyZonesEditorIO = new FancyZonesEditorIO();
            Overlay = new Overlay();
            MainWindowSettings = new MainWindowSettingsModel();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            RunnerHelper.WaitForPowerToysRunner(PowerToysPID, () =>
            {
                Environment.Exit(0);
            });

            FancyZonesEditorIO.ParseCommandLineArguments();
            FancyZonesEditorIO.ParseDeviceInfoData();

            MainWindowSettingsModel settings = ((App)Current).MainWindowSettings;
            settings.UpdateSelectedLayoutModel();

            Overlay.Show();
        }

        public static void ShowExceptionMessageBox(string message, Exception exception = null)
        {
            string fullMessage = FancyZonesEditor.Properties.Resources.Error_Report + PowerToysIssuesURL + " \n" + message;
            if (exception != null)
            {
                fullMessage += ": " + exception.Message;
            }

            MessageBox.Show(fullMessage, FancyZonesEditor.Properties.Resources.Error_Exception_Message_Box_Title);
        }

        public static void ShowExceptionReportMessageBox(string reportData)
        {
            var fileStream = File.OpenWrite(ErrorReportLogFile);
            var sw = new StreamWriter(fileStream);
            sw.Write(reportData);
            sw.Flush();
            fileStream.Close();

            ShowReportMessageBox(fileStream.Name);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var fileStream = File.OpenWrite(CrashReportLogFile);
            var sw = new StreamWriter(fileStream);
            sw.Write(FormatException((Exception)args.ExceptionObject));
            fileStream.Close();

            ShowReportMessageBox(fileStream.Name);
        }

        private static void ShowReportMessageBox(string fileName)
        {
            MessageBox.Show(
                FancyZonesEditor.Properties.Resources.Crash_Report_Message_Box_Text_Part1 +
                Path.GetFullPath(fileName) +
                "\n" +
                FancyZonesEditor.Properties.Resources.Crash_Report_Message_Box_Text_Part2 +
                PowerToysIssuesURL,
                FancyZonesEditor.Properties.Resources.Fancy_Zones_Editor_App_Title);
        }

        private static string FormatException(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## " + CrashReportExceptionTag);
            sb.AppendLine();
            sb.AppendLine("```");

            var exlist = new List<StringBuilder>();

            while (ex != null)
            {
                var exsb = new StringBuilder();
                exsb.Append(ex.GetType().FullName);
                exsb.Append(": ");
                exsb.AppendLine(ex.Message);
                if (ex.Source != null)
                {
                    exsb.Append("   " + CrashReportSourceTag);
                    exsb.AppendLine(ex.Source);
                }

                if (ex.TargetSite != null)
                {
                    exsb.Append("   " + CrashReportTargetAssemblyTag);
                    exsb.AppendLine(ex.TargetSite.Module.Assembly.ToString());
                    exsb.Append("   " + CrashReportTargetModuleTag);
                    exsb.AppendLine(ex.TargetSite.Module.ToString());
                    exsb.Append("   " + CrashReportTargetSiteTag);
                    exsb.AppendLine(ex.TargetSite.ToString());
                }

                exsb.AppendLine(ex.StackTrace);
                exlist.Add(exsb);

                ex = ex.InnerException;
            }

            foreach (var result in exlist.Select(o => o.ToString()).Reverse())
            {
                sb.AppendLine(result);
            }

            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## " + CrashReportEnvironmentTag);
            sb.AppendLine(CrashReportCommandLineTag + Environment.CommandLine);

            // Using InvariantCulture since this is used for a timestamp internally
            sb.AppendLine(CrashReportTimestampTag + DateTime.Now.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(CrashReportOSVersionTag + Environment.OSVersion.VersionString);
            sb.AppendLine(CrashReportIntPtrLengthTag + IntPtr.Size);
            sb.AppendLine(CrashReportx64Tag + Environment.Is64BitOperatingSystem);
            sb.AppendLine(CrashReportCLRVersionTag + Environment.Version);
            sb.AppendLine("## " + CrashReportAssembliesTag + AppDomain.CurrentDomain.FriendlyName);
            sb.AppendLine();
            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies().OrderBy(o => o.GlobalAssemblyCache ? 50 : 0))
            {
                sb.Append("* ");
                sb.Append(ass.FullName);
                sb.Append(" (");

                if (ass.IsDynamic)
                {
                    sb.Append(CrashReportDynamicAssemblyTag);
                }
                else if (string.IsNullOrEmpty(ass.Location))
                {
                    sb.Append(CrashReportLocationNullTag);
                }
                else
                {
                    sb.Append(ass.Location);
                }

                sb.AppendLine(")");
            }

            return sb.ToString();
        }
    }
}
