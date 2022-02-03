using Microsoft.Win32;

namespace TakeOutMyBloatware.Operations
{
    public class DisableUpdatesAction : IActionHandler
    {
        private readonly IUI ui;
        public DisableUpdatesAction(IUI ui) => this.ui = ui;

        public void Run()
        {
            ui.PrintMessage("Writing values into the Registry...");
            DisableAutomaticWindowsUpdates();
            DisableAutomaticStoreUpdates();
            DisableAutomaticSpeechModelUpdates();
        }

        private void DisableAutomaticWindowsUpdates()
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 1);
        }

        private void DisableAutomaticStoreUpdates()
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2);
        }

        private void DisableAutomaticSpeechModelUpdates()
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Speech", "AllowSpeechModelUpdate", 0);
        }
    }
}
