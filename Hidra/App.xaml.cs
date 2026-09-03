using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Hidra.Core;
using Hidra.Core.Utilities;
using Hidra.Utilities;
using Hidra.Views;
using MaterialDesignThemes.Wpf;
using Application = System.Windows.Application;

namespace Hidra
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private Context context;
        private SingleGlobalInstance mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ApplyHidraTheme();
            AppDomain.CurrentDomain.UnhandledException += AppDomain_CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            mutex = new SingleGlobalInstance(); 
            if (mutex.HasHandle && GetProcesses().Length <= 1)
            {
                Logger.Info("Launching Hidra");

                InitializeApp();
                CheckForBlockedDll();

                context.ParseCommandLineArguments(e.Args);
                var mw = new MainWindow(context);
                mw.Show();
            }
            else
            {
                SendArgs(string.Join(";", e.Args));
                Current.Shutdown();
            }
        }

        // Night theme with blue highlights: derive a full Material Design dark palette from Hidra's
        // brand blue, then deepen the background/surfaces to a navy-black instead of stock neutral grey.
        private static void ApplyHidraTheme()
        {
            var paletteHelper = new PaletteHelper();
            var theme = Theme.Create(
                BaseTheme.Dark,
                (Color)ColorConverter.ConvertFromString("#3D7EF6"),
                (Color)ColorConverter.ConvertFromString("#63C7FF"));

            theme.Background = (Color)ColorConverter.ConvertFromString("#0B0F17");
            theme.Foreground = (Color)ColorConverter.ConvertFromString("#E7ECF5");

            paletteHelper.SetTheme(theme);
        }

        private void InitializeApp()
        {
            new ResourceLoader().Load();
            context = Context.Load();
        }

        private void CheckForBlockedDll()
        {
            if (context.GetPlugins().Count != 0) return;

            var result = MessageBox.Show("Hidra has detected blocked files which are required, do you want to unblock blocked Hidra files?", "Unblock files?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                UnblockFiles(AppContext.BaseDirectory);
            }
            catch (Exception e)
            {
                Logger.Error("Hidra failed to unblock the required files", e);
                MessageBox.Show("Hidra failed to unblock the required files", "Failed to unblock", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }

            InitializeApp();
        }

        // Windows marks files extracted from a downloaded zip (or copied over a network/USB) with a
        // "Zone.Identifier" NTFS alternate data stream, which can stop the CLR from loading them as
        // plugins. Deleting that stream is all "unblocking" a file actually does.
        private static void UnblockFiles(string rootDirectory)
        {
            foreach (var file in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
            {
                var zoneIdentifierStream = file + ":Zone.Identifier";
                if (File.Exists(zoneIdentifierStream))
                {
                    File.Delete(zoneIdentifierStream);
                }
            }
        }

        private static Process[] GetProcesses()
        {
            return Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
        }

        private void SendArgs(string args)
        {
            Logger.Info($"Hidra is already running, sending args: {{{args}}}");
            // Find the window with the name of the main form
            var processes = GetProcesses();
            processes = processes.Where(p => p.Id != Process.GetCurrentProcess().Id).ToArray();
            if (processes.Length == 0) return;

            IntPtr ptrCopyData = IntPtr.Zero;
            try
            {
                // Create the data structure and fill with data
                NativeMethods.COPYDATASTRUCT copyData = new NativeMethods.COPYDATASTRUCT
                {
                    dwData = new IntPtr(2),
                    cbData = args.Length + 1,
                    lpData = Marshal.StringToHGlobalAnsi(args)
                };
                // Just a number to identify the data type
                // One extra byte for the \0 character

                // Allocate memory for the data and copy
                ptrCopyData = Marshal.AllocCoTaskMem(Marshal.SizeOf(copyData));
                Marshal.StructureToPtr(copyData, ptrCopyData, false);

                // Look up the window by title rather than Process.MainWindowHandle: Hidra hides
                // its window to the tray instead of exiting, and a hidden window's
                // MainWindowHandle is always IntPtr.Zero, but FindWindow still finds it.
                var windowHandle = NativeMethods.FindWindow(null, "Hidra");
                if (windowHandle != IntPtr.Zero)
                {
                    NativeMethods.SendMessage(windowHandle, NativeMethods.WM_COPYDATA, IntPtr.Zero, ptrCopyData);
                }
                    
            }
            catch (Exception e)
            {
                Logger.Error("Unable to send args to existing process", e);
            }
            finally
            {
                // Free the allocated memory after the control has been returned
                if (ptrCopyData != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(ptrCopyData);
            }
        }

        public void Dispose()
        {
            mutex.Dispose();
            context?.Dispose();
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            context?.DevicesManager.UpdateDeviceCache();

            Dispose();
        }

        private static void AppDomain_CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception) e.ExceptionObject;
            Logger.Fatal(exception.Message, exception);
        }

        // WPF routes exceptions thrown on the UI thread (e.g. from a button Click handler, or an
        // async void continuation back on the UI thread) here, not through AppDomain.UnhandledException -
        // without this, those crashes terminate the process with nothing logged at all.
        private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Fatal(e.Exception.Message, e.Exception);
            MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}\n\nSee the log for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
