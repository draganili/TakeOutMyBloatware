using System;
using System.Diagnostics;
using System.Security.Principal;
using TakeOutMyBloatware.Utils;

namespace TakeOutMyBloatware
{
    static class Program
    {
        public const string Minimum_support_version_WIN10 = "2009";

        private static void Main()
        {
            using var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);
            Console.Title = "TakeOutMyBloatware Windows 10 Assistant by DraganIli";
            AdminChecker();
            IsSupportedOSChecker();
            CheckExitEventHandler();

            var configuration = LoadConfigurationFromFileOrDefault();
            var rebootFlag = new RebootHandler();
            var menu = new CLI(CreateMenuActions(configuration, rebootFlag), rebootFlag);
            menu.RunUntilKilled();
        }

        private static CLIActionList[] CreateMenuActions(Configuration configuration, RebootHandler rebootFlag)
        {
            return new CLIActionList[] {
                new About(),
                new RWF(configuration),
                new ARSA(),
                new UWPR(configuration),
                new RME(),
                new ROD(),
                new RMS(configuration),
                new ACSFMP(),
                new DT(),
                new DBAWD(),
                new DAU(),
                new DMST(configuration),
                new DWER(),
                new DSCCFR(),
                new ContactDev(),
                new ExitApp(rebootFlag)
            };
        }

        private static void AdminChecker()
        {
            if (!Program.HasAdministratorRights())
            {
                CLIHelpers.WriteLine("ERROR: This application requires administrator rights. Please execute the tool with Admin rights.", ConsoleColor.Red);
                Console.ReadKey();
                Environment.Exit(-1);
            }
        }

        private static bool HasAdministratorRights()
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void IsSupportedOSChecker()
        {
            if (OS.IsWindows10() && IsWindows10VersionSupported(OS.WindowsReleaseId))
                return;

            CLIHelpers.WriteLine("ERROR: Your Windows version is not supported: --\n", ConsoleColor.Red);
            if (!OS.IsWindows10())
                Console.WriteLine("This program was designed to work only on Windows 10.");
            else
            {
                Console.WriteLine(
                                        "You are running an older version of Windows 10 which is not supported by this version of the program.\n" +
                                        "You should update your system and match the following\n" +
                                        $"Windows 10 version: ({OS.WindowsReleaseId})"
                );
            }

            Console.WriteLine(
                                    "\nYou can still continue using this program, but BE AWARE that some features might work badly or not at all\n" +
                                    "and could even have unintended effects on your system (including corruptions or instability)."
            );

            Console.WriteLine("\nPress enter to continue, or another key to quit.");
            if (Console.ReadKey().Key != ConsoleKey.Enter)
                Environment.Exit(-1);
        }

        private static bool IsWindows10VersionSupported(string? windows10Version)
        {
            return string.Compare(windows10Version, Minimum_support_version_WIN10) >= 0;
        }

        private static Configuration LoadConfigurationFromFileOrDefault()
        {
            try
            {
                return Configuration.LoadOrCreateFile();
            }
            catch (ConfigurationException exc)
            {
                PrintConfigurationErrorMessage(exc);
                return Configuration.Default;
            }
        }

        private static void PrintConfigurationErrorMessage(ConfigurationException exc)
        {
            string errorMessage = "";
            if (exc is ConfigurationLoadException)
                errorMessage = $"An error occurred while loading settings file: {exc.Message}\n" +
                                "Default settings have been loaded instead.\n";
            else if (exc is ConfigurationWriteException)
                errorMessage = $"Couldn't write default configuration to settings file: {exc.Message}\n";

            CLIHelpers.WriteLine(errorMessage, ConsoleColor.DarkYellow);
            Console.WriteLine("Press a key to continue to the main menu.");
            Console.ReadKey();
        }

        private static void CheckExitEventHandler()
        {
#if !DEBUG
            bool cancelKeyPressedOnce = false;
            Console.CancelKeyPress += (sender, args) => {
                if (!cancelKeyPressedOnce)
                {
                    CLIHelpers.WriteLine("Press Ctrl+C again to terminate the program.", ConsoleColor.Red);
                    cancelKeyPressedOnce = true;
                    args.Cancel = true;
                }
                else
                    Process.GetCurrentProcess().KillChildProcesses();
            };
#endif

            AppDomain.CurrentDomain.ProcessExit += (sender, args) => Process.GetCurrentProcess().KillChildProcesses();
        }
    }
}
