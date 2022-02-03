﻿using Microsoft.Win32;
using System.IO;
using TakeOutMyBloatware.Utils;
using Env = System.Environment;

namespace TakeOutMyBloatware.Operations
{
    public class RemoveOneDriveAction : IActionHandler
    {
        private readonly IUI ui;

        public RemoveOneDriveAction(IUI ui) => this.ui = ui;

        public void Run()
        {
            DisableOneDrive();
            OS.KillProcess("onedrive");
            RunOneDriveUninstaller();
            RemoveOneDriveLeftovers();
            DisableAutomaticSetupForNewUsers();
        }

        private void DisableOneDrive()
        {
            ui.PrintMessage("Disabling OneDrive via registry edits...");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1);
            using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey key = localMachine.CreateSubKey(@"SOFTWARE\Microsoft\OneDrive");
            key.SetValue("PreventNetworkTrafficPreUserSignIn", 1);
        }

        private void RunOneDriveUninstaller()
        {
            ui.PrintMessage("Executing OneDrive uninstaller...");
            string setupPath = RetrieveOneDriveSetupPath();
            var uninstallationExitCode = OS.RunProcessBlockingWithOutput(setupPath, "/uninstall", ui);
            if (uninstallationExitCode.IsNotSuccessful())
            {
                ui.PrintError("Uninstallation failed due to an unknown error.");
                ui.ThrowIfUserDenies("Do you still want to continue the process by removing all leftover OneDrive " +
                                     "files (including its application files for the current user) and registry keys?");
            }
        }

        private string RetrieveOneDriveSetupPath()
        {
            if (Env.Is64BitOperatingSystem)
                return $@"{Env.GetFolderPath(Env.SpecialFolder.Windows)}\SysWOW64\OneDriveSetup.exe";
            else
                return $@"{Env.GetFolderPath(Env.SpecialFolder.Windows)}\System32\OneDriveSetup.exe";
        }

        private void RemoveOneDriveLeftovers()
        {
            ui.PrintMessage("Removing OneDrive leftovers...");
            OS.CloseExplorer();
            RemoveResidualFiles();
            RemoveResidualRegistryKeys();
            OS.StartExplorer();
        }

        private void RemoveResidualFiles()
        {
            OS.TryDeleteDirectoryIfExists(@"C:\OneDriveTemp", ui);
            OS.TryDeleteDirectoryIfExists($@"{Env.GetFolderPath(Env.SpecialFolder.LocalApplicationData)}\OneDrive", ui);
            OS.TryDeleteDirectoryIfExists($@"{Env.GetFolderPath(Env.SpecialFolder.LocalApplicationData)}\Microsoft\OneDrive", ui);
            OS.TryDeleteDirectoryIfExists($@"{Env.GetFolderPath(Env.SpecialFolder.UserProfile)}\OneDrive", ui);
            var menuShortcut = new FileInfo($@"{Env.GetFolderPath(Env.SpecialFolder.StartMenu)}\Programs\OneDrive.lnk");
            if (menuShortcut.Exists)
                menuShortcut.Delete();
        }

        private void RemoveResidualRegistryKeys()
        {
            using RegistryKey classesRoot = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64);
            using RegistryKey key = classesRoot.OpenSubKeyWritable(@"CLSID");
            key.DeleteSubKeyTree("{018D5C66-4533-4307-9B53-224DE2ED1FE6}", throwOnMissingSubKey: false);
        }

        // Borrowed from github.com/W4RH4WK/Debloat-Windows-10/blob/master/scripts/remove-onedrive.ps1
        private void DisableAutomaticSetupForNewUsers()
        {
            ui.PrintMessage("Disabling automatic OneDrive setup for new users...");
            RegHelper.DefaultUser.DeleteSubKeyValue(@"Software\Microsoft\Windows\CurrentVersion\Run", "OneDriveSetup");
        }
    }
}
