﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using TakeOutMyBloatware.Utils;
using Env = System.Environment;

namespace TakeOutMyBloatware.Operations
{
    public enum UWPAppRemovalMode
    {
        CurrentUser,
        AllUsers
    }

    enum UWPAppRemovalOutcome
    {
        Success,
        NotInstalled,
        Failure
    }

    public enum UWPAppGroup
    {
        AlarmsAndClock,
        Bing,
        Calculator,
        Camera,
        CommunicationsApps,
        Cortana,
        EdgeUWP,
        HelpAndFeedback,
        Maps,
        Messaging,
        MixedReality,
        Mobile,
        OfficeHub,
        OneNote,
        Paint3D,
        Photos,
        Skype,
        SnipAndSketch,
        SolitaireCollection,
        SoundRecorder,
        StickyNotes,
        Store,
        Xbox,
        Zune
    }

    public class RemoveUWPAppAction : IActionHandler
    {

        private static readonly Dictionary<UWPAppGroup, string[]> appNamesForGroup = new Dictionary<UWPAppGroup, string[]> {
            { UWPAppGroup.AlarmsAndClock, new[] { "Microsoft.WindowsAlarms" } },
            { UWPAppGroup.Bing, new[] {
                "Microsoft.BingNews",
                "Microsoft.BingWeather",
                "Microsoft.BingFinance",
                "Microsoft.BingSports"
            } },
            { UWPAppGroup.Calculator, new[] { "Microsoft.WindowsCalculator" } },
            { UWPAppGroup.Camera, new[] { "Microsoft.WindowsCamera" } },
            { UWPAppGroup.CommunicationsApps, new[] { "microsoft.windowscommunicationsapps", "Microsoft.People" } },
            { UWPAppGroup.Cortana, new[] { "Microsoft.549981C3F5F10" } },
            { UWPAppGroup.EdgeUWP, new[] { "Microsoft.MicrosoftEdge", "Microsoft.MicrosoftEdgeDevToolsClient" } },
            { UWPAppGroup.HelpAndFeedback, new[] {
                "Microsoft.WindowsFeedbackHub",
                "Microsoft.GetHelp",
                "Microsoft.Getstarted"
            } },
            { UWPAppGroup.Maps, new[] { "Microsoft.WindowsMaps" } },
            { UWPAppGroup.Messaging, new[] { "Microsoft.Messaging" } },
            { UWPAppGroup.MixedReality, new[] {
                "Microsoft.Microsoft3DViewer",
                "Microsoft.Print3D",
                "Microsoft.MixedReality.Portal"
            } },
            { UWPAppGroup.Mobile, new[] { "Microsoft.YourPhone", "Microsoft.OneConnect" } },
            { UWPAppGroup.OfficeHub, new[] { "Microsoft.MicrosoftOfficeHub" } },
            { UWPAppGroup.OneNote, new[] { "Microsoft.Office.OneNote" } },
            { UWPAppGroup.Paint3D, new[] { "Microsoft.MSPaint" } },
            { UWPAppGroup.Photos, new[] { "Microsoft.Windows.Photos" } },
            { UWPAppGroup.Skype, new[] { "Microsoft.SkypeApp" } },
            { UWPAppGroup.SnipAndSketch, new[] { "Microsoft.ScreenSketch" } },
            { UWPAppGroup.SolitaireCollection, new[] { "Microsoft.MicrosoftSolitaireCollection" } },
            { UWPAppGroup.SoundRecorder, new[] { "Microsoft.WindowsSoundRecorder" } },
            { UWPAppGroup.StickyNotes, new[] { "Microsoft.MicrosoftStickyNotes" } },
            { UWPAppGroup.Store, new[] {
                "Microsoft.WindowsStore",
                "Microsoft.StorePurchaseApp",
                "Microsoft.Services.Store.Engagement",
            } },
            { UWPAppGroup.Xbox, new[] {
                "Microsoft.XboxGameCallableUI",
                "Microsoft.XboxSpeechToTextOverlay",
                "Microsoft.XboxApp",
                "Microsoft.XboxGameOverlay",
                "Microsoft.XboxGamingOverlay",
                "Microsoft.XboxIdentityProvider",
                "Microsoft.Xbox.TCUI"
            } },
            { UWPAppGroup.Zune, new[] { "Microsoft.ZuneMusic", "Microsoft.ZuneVideo" } }
        };

        private readonly Dictionary<UWPAppGroup, Action> postUninstallOperationsForGroup;
        private readonly UWPAppGroup[] appsToRemove;
        private readonly UWPAppRemovalMode removalMode;
        private readonly IUI ui;
        private readonly RemoveServicesAction serviceRemover;

        private /*lateinit*/ PowerShell powerShell;
        private int removedApps = 0;

        public bool IsRebootRecommended { get; private set; }

#nullable disable warnings
        public RemoveUWPAppAction(UWPAppGroup[] appsToRemove, UWPAppRemovalMode removalMode, IUI ui, RemoveServicesAction serviceRemover)
        {
            this.appsToRemove = appsToRemove;
            this.removalMode = removalMode;
            this.ui = ui;
            this.serviceRemover = serviceRemover;

            postUninstallOperationsForGroup = new Dictionary<UWPAppGroup, Action> {
                { UWPAppGroup.CommunicationsApps, RemoveOneSyncServiceFeature },
                { UWPAppGroup.Cortana, HideCortanaFromTaskBar },
                { UWPAppGroup.Maps, RemoveMapsServicesAndTasks },
                { UWPAppGroup.Messaging, RemoveMessagingService },
                { UWPAppGroup.Paint3D, RemovePaint3DContextMenuEntries },
                { UWPAppGroup.Photos, RestoreWindowsPhotoViewer },
                { UWPAppGroup.MixedReality, RemoveMixedRealityAppsLeftovers },
                { UWPAppGroup.Xbox, RemoveXboxServicesAndTasks },
                { UWPAppGroup.Store, DisableStoreFeaturesAndServices }
            };
        }
#nullable restore warnings

        public void Run()
        {
            using (powerShell = PSPlugin.CreateWithImportedModules("AppX", "Dism").WithOutput(ui))
            {
                foreach (UWPAppGroup appGroup in appsToRemove)
                    UninstallAppsOfGroup(appGroup);
            }

            if (removedApps > 0)
                RestartExplorer();
        }

        private void UninstallAppsOfGroup(UWPAppGroup appGroup)
        {
            string[] appsInGroup = appNamesForGroup[appGroup];
            ui.PrintHeading($"Removing {appGroup} {(appsInGroup.Length == 1 ? "app" : "apps")}...");
            bool noErrorsEncountered = true;
            foreach (string appName in appsInGroup)
            {

                if (removalMode == UWPAppRemovalMode.AllUsers)
                {
                    var removalOutcome = UninstallAppProvisionedPackage(appName);
                    if (removalOutcome == UWPAppRemovalOutcome.Failure)
                        noErrorsEncountered = false;
                }

                var appRemovalOutcome = UninstallApp(appName);
                if (appRemovalOutcome == UWPAppRemovalOutcome.Success)
                    removedApps++;
                else if (appRemovalOutcome == UWPAppRemovalOutcome.Failure)
                    noErrorsEncountered = false;
            }
            if (removalMode == UWPAppRemovalMode.AllUsers && noErrorsEncountered)
                TryPerformPostUninstallOperations(appGroup);
        }

        private UWPAppRemovalOutcome UninstallAppProvisionedPackage(string appName)
        {
            var provisionedPackage = powerShell.Run("Get-AppxProvisionedPackage -Online")
                .FirstOrDefault(package => package.DisplayName == appName);
            if (provisionedPackage == null)
                return UWPAppRemovalOutcome.NotInstalled;

            ui.PrintMessage($"Removing provisioned package for app {appName}...");
            powerShell.Run(
                $"Remove-AppxProvisionedPackage -Online -PackageName \"{provisionedPackage.PackageName}\""
            );
            return powerShell.Streams.Error.Count == 0 ? UWPAppRemovalOutcome.Success : UWPAppRemovalOutcome.Failure;
        }

        private UWPAppRemovalOutcome UninstallApp(string appName)
        {
            var packages = powerShell.Run(GetAppxPackageCommand(appName));
            if (packages.Length == 0)
            {
                ui.PrintMessage($"App {appName} is not installed.");
                return UWPAppRemovalOutcome.NotInstalled;
            }

            ui.PrintMessage($"Uninstalling app {appName}...");
            foreach (var package in packages)
            {
                string command = RemoveAppxPackageCommand(package.PackageFullName);
                powerShell.Run(command);
            }
            return powerShell.Streams.Error.Count == 0 ? UWPAppRemovalOutcome.Success : UWPAppRemovalOutcome.Failure;
        }

        private string GetAppxPackageCommand(string appName)
        {
            string command = "Get-AppxPackage ";
            if (removalMode == UWPAppRemovalMode.AllUsers)
                command += "-AllUsers ";
            return command + $"-Name \"{appName}\"";
        }

        private string RemoveAppxPackageCommand(string fullPackageName)
        {
            string command = "Remove-AppxPackage ";
            if (removalMode == UWPAppRemovalMode.AllUsers)
                command += "-AllUsers ";
            return command + $"-Package \"{fullPackageName}\"";
        }

        private void RestartExplorer()
        {
            ui.PrintHeading("Restarting Explorer to avoid stale app entries in Start menu...");
            OS.CloseExplorer();
            OS.StartExplorer();
        }

        private void TryPerformPostUninstallOperations(UWPAppGroup appGroup)
        {
            try
            {
                if (postUninstallOperationsForGroup.ContainsKey(appGroup))
                {
                    ui.PrintEmptySpace();
                    postUninstallOperationsForGroup[appGroup]();
                    IsRebootRecommended = true;
                }
            }
            catch (Exception exc)
            {
                ui.PrintError($"An error occurred while performing post-uninstall/cleanup operations: {exc.Message}");
            }
        }

        private void HideCortanaFromTaskBar()
        {
            ui.PrintMessage("Hiding Cortana from the taskbar of current and default user...");
            RegHelper.SetForCurrentAndDefaultUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCortanaButton", 0);
        }

        private void RemoveMapsServicesAndTasks()
        {
            new DisableTasksAction(new[] {
                @"\Microsoft\Windows\Maps\MapsUpdateTask",
                @"\Microsoft\Windows\Maps\MapsToastTask"
            }, ui).Run();
            serviceRemover.BackupAndRemove("MapsBroker", "lfsvc");
        }

        private void RemoveXboxServicesAndTasks()
        {
            new DisableTasksAction(new[] { @"Microsoft\XblGameSave\XblGameSaveTask" }, ui).Run();
            serviceRemover.BackupAndRemove("XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc");
            ui.PrintMessage("Disabling Xbox Game Bar...");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
        }

        private void RemoveMessagingService()
        {
            serviceRemover.BackupAndRemove("MessagingService");
        }

        private void RemovePaint3DContextMenuEntries()
        {
            ui.PrintMessage("Removing Paint 3D context menu entries...");
            OS.ExecuteWindowsPromptCommand(
                @"echo off & for /f ""tokens=1* delims="" %I in " +
                 @"(' reg query ""HKEY_CLASSES_ROOT\SystemFileAssociations"" /s /k /f ""3D Edit"" ^| find /i ""3D Edit"" ') " +
                @"do (reg delete ""%I"" /f )",
                ui
            );
        }

        private void RemoveMixedRealityAppsLeftovers()
        {
            Remove3DObjectsFolder();
            Remove3DPrintContextMenuEntries();
        }

        private void Remove3DObjectsFolder()
        {
            ui.PrintMessage("Removing 3D Objects folder...");
            using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey key = localMachine.OpenSubKeyWritable(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace"
            );
            key.DeleteSubKeyTree("{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}", throwOnMissingSubKey: false);

            OS.TryDeleteDirectoryIfExists($@"{Env.GetFolderPath(Env.SpecialFolder.UserProfile)}\3D Objects", ui);
        }

        private void Remove3DPrintContextMenuEntries()
        {
            ui.PrintMessage("Removing 3D Print context menu entries...");
            OS.ExecuteWindowsPromptCommand(
                @"echo off & for /f ""tokens=1* delims="" %I in " +
                @"(' reg query ""HKEY_CLASSES_ROOT\SystemFileAssociations"" /s /k /f ""3D Print"" ^| find /i ""3D Print"" ') " +
                @"do (reg delete ""%I"" /f )",
                ui
            );
        }

        private void RestoreWindowsPhotoViewer()
        {
            ui.PrintMessage("Setting file association with legacy photo viewer for BMP, GIF, JPEG, PNG and TIFF pictures...");

            const string PHOTO_VIEWER_SHELL_COMMAND =
                @"%SystemRoot%\System32\rundll32.exe ""%ProgramFiles%\Windows Photo Viewer\PhotoViewer.dll"", ImageView_Fullscreen %1";
            const string PHOTO_VIEWER_CLSID = "{FFE2A43C-56B9-4bf5-9A79-CC6D4285608A}";

            Registry.SetValue(@"HKEY_CLASSES_ROOT\Applications\photoviewer.dll\shell\open", "MuiVerb", "@photoviewer.dll,-3043");
            Registry.SetValue(
                @"HKEY_CLASSES_ROOT\Applications\photoviewer.dll\shell\open\command", valueName: null,
                PHOTO_VIEWER_SHELL_COMMAND, RegistryValueKind.ExpandString
            );
            Registry.SetValue(@"HKEY_CLASSES_ROOT\Applications\photoviewer.dll\shell\open\DropTarget", "Clsid", PHOTO_VIEWER_CLSID);

            string[] imageTypes = { "Paint.Picture", "giffile", "jpegfile", "pngfile" };
            foreach (string type in imageTypes)
            {
                Registry.SetValue(
                    $@"HKEY_CLASSES_ROOT\{type}\shell\open\command", valueName: null,
                    PHOTO_VIEWER_SHELL_COMMAND, RegistryValueKind.ExpandString
                );
                Registry.SetValue($@"HKEY_CLASSES_ROOT\{type}\shell\open\DropTarget", "Clsid", PHOTO_VIEWER_CLSID);
            }
        }

        private void RemoveOneSyncServiceFeature()
        {
            new RemoveFeaturesAction(new[] { "OneCoreUAP.OneSync" }, ui).Run();
        }

        private void DisableStoreFeaturesAndServices()
        {
            ui.PrintMessage("Disabling Microsoft Store features...");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore", "RemoveWindowsStore", 1);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\PushToInstall", "DisablePushToInstall", 1);
            RegHelper.SetForCurrentAndDefaultUser(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                "SilentInstalledAppsEnabled", 0
            );

            serviceRemover.BackupAndRemove("PushToInstall");
        }
    }
}
