﻿using Microsoft.Win32;
using TakeOutMyBloatware.Utils;

namespace TakeOutMyBloatware.Operations
{
    public class DisableMSDefenderAction : IActionHandler
    {
        private static readonly string[] defenderServices = {
            "wscsvc",
            "Sense",
            "SgrmBroker",
            "SgrmAgent"
        };

        private static readonly string[] defenderScheduledTasks = {
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Verification"
        };

        private readonly IUI ui;
        private readonly RemoveServicesAction serviceRemover;

        public bool IsRebootRecommended { get; private set; }

        public DisableMSDefenderAction(IUI ui, RemoveServicesAction serviceRemover)
        {
            this.ui = ui;
            this.serviceRemover = serviceRemover;
        }

        public void Run()
        {
            DowngradeAntimalwarePlatform();
            EditWindowsRegistryKeys();
            RemoveDefenderServices();
            DisableDefenderScheduledTasks();
        }

        // DisableAntiSpyware policy is not honored anymore on Defender antimalware platform version 4.18.2007.8+
        // This workaround will last until Windows ships with a lower version of that platform pre-installed
        private void DowngradeAntimalwarePlatform()
        {
            ui.PrintHeading("Downgrading Defender antimalware platform...");
            var exitCode = OS.RunProcessBlockingWithOutput(
                $@"{OS.GetProgramFilesFolder()}\Windows Defender\MpCmdRun.exe", "-resetplatform", ui);

            if (exitCode.IsNotSuccessful())
            {
                ui.PrintWarning(
                    "Antimalware platform downgrade failed. This is likely happened because you have already disabled Windows Defender.\n" +
                    "If this is not your case, you can proceed anyway but be aware that Defender will not be disabled fully " +
                    "if the antimalware platform has been updated to version 4.18.2007.8 or higher through Windows Update.");
                ui.ThrowIfUserDenies("Do you want to continue?");
            }
            IsRebootRecommended = true;
        }

        private void EditWindowsRegistryKeys()
        {
            ui.PrintHeading("Editing keys in Windows Registry...");

            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1);
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet"))
            {
                key.SetValue("SpynetReporting", 0);
                key.SetValue("SubmitSamplesConsent", 2);
            }
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\MRT"))
            {
                key.SetValue("DontReportInfectionInformation", 1);
                key.SetValue("DontOfferThroughWUAU", 1);
            }

            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 0);
            Registry.SetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\smartscreen.exe",
                "Debugger", @"%windir%\System32\taskkill.exe"
            );
            // Turn off SmartScreen for Microsoft Store apps
            RegHelper.SetForCurrentAndDefaultUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation", 0);
            // Turn off SmartScreen for Microsoft Edge
            RegHelper.SetForCurrentAndDefaultUser(@"Software\Microsoft\Edge\SmartScreenEnabled", valueName: null, 0);
            RegHelper.SetForCurrentAndDefaultUser(@"Software\Microsoft\Edge\SmartScreenPuaEnabled", valueName: null, 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\MicrosoftEdge\PhishingFilter", "EnabledV9", 0);

            using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            localMachine.DeleteSubKeyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "SecurityHealth");
            localMachine.DeleteSubKeyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "SecurityHealth");

            using RegistryKey notificationSettings = localMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance"
            );
            notificationSettings.SetValue("Enabled", 0);
        }

        private void RemoveDefenderServices()
        {
            ui.PrintHeading("Removing Windows Defender services...");
            serviceRemover.BackupAndRemove(defenderServices);
        }

        private void DisableDefenderScheduledTasks()
        {
            ui.PrintHeading("Disabling Windows Defender scheduled tasks...");
            new DisableTasksAction(defenderScheduledTasks, ui).Run();
        }
    }
}
