using Microsoft.Win32;
using TakeOutMyBloatware.Utils;

namespace TakeOutMyBloatware.Operations
{
    public class AutoConfigSettingsAction : IActionHandler
    {
        private static readonly string[] appPermissionsToDeny = {
            "location",
            "documentsLibrary",
            "userDataTasks",
            "appDiagnostics",
            "userAccountInformation"
        };

        private readonly IUI ui;
        public AutoConfigSettingsAction(IUI ui) => this.ui = ui;

        public void Run()
        {
            ui.PrintMessage("Writing values into the Registry...");
            AutoConfigurePrivacy();
            StopCollectingSensitiveData();
            BlockSensitivePermissionsToApps();
            DisableWebAndLocationAccessToSearch();
        }

        private void AutoConfigurePrivacy()
        {
            // Account -> Sign-in options -> Privacy
            Registry.SetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                "DisableAutomaticRestartSignOn", 1
            );

            // Privacy -> General
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1);
            RegHelper.SetForCurrentAndDefaultUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0);
            RegHelper.SetForCurrentAndDefaultUser(@"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1);

            // Privacy -> Inking and typing personalization (and related policies)
            RegHelper.SetForCurrentAndDefaultUser(@"SOFTWARE\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0);
            RegHelper.SetForCurrentAndDefaultUser(@"SOFTWARE\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\TabletPC", "PreventHandwritingDataSharing", 1);

            // Privacy -> Diagnostics and feedback -> Improve inking and typing recognition
            Registry.SetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\TextInput",
                "AllowLinguisticDataCollection", 0);

            // Privacy -> Speech
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization", 0);

            // Microsoft Edge settings -> Privacy, search and services -> Personalize your web experience
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge", "PersonalizationReportingEnabled", 0);
        }

        private void StopCollectingSensitiveData()
        {
            // Privacy -> Activity history -> Send my activity history to Microsoft
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "AllowCrossDeviceClipboard", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Messaging", "AllowMessageSync", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\SettingSync", "DisableCredentialsSettingSync", 2);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\SettingSync", "DisableCredentialsSettingSyncUserOverride", 1);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\SettingSync", "DisableApplicationSettingSync", 2);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\SettingSync", "DisableApplicationSettingSyncUserOverride", 1);
        }

        private void BlockSensitivePermissionsToApps()
        {
            using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            foreach (string permission in appPermissionsToDeny)
            {
                using var permissionKey = localMachine.CreateSubKey(
                    $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{permission}"
                );
                permissionKey.SetValue("Value", "Deny");
            }
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsActivateWithVoice", 2);
        }

        private void DisableWebAndLocationAccessToSearch()
        {
            using RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search");
            key.SetValue("AllowSearchToUseLocation", 0);
            key.SetValue("DisableWebSearch", 1);
            key.SetValue("ConnectedSearchUseWeb", 0);
        }
    }
}
