using Microsoft.Win32;

namespace TakeOutMyBloatware.Operations
{
    public class DisableErrReportingAction : IActionHandler
    {
        private static readonly string[] errorReportingServices = { "WerSvc", "wercplsupport" };
        private static readonly string[] errorReportingScheduledTasks = {
            @"\Microsoft\Windows\Windows Error Reporting\QueueReporting"
        };

        private readonly IUI ui;
        private readonly RemoveServicesAction serviceRemover;

        public bool IsRebootRecommended { get; private set; }

        public DisableErrReportingAction(IUI ui, RemoveServicesAction serviceRemover)
        {
            this.ui = ui;
            this.serviceRemover = serviceRemover;
        }

        public void Run()
        {
            DisableErrorReporting();
            RemoveErrorReportingServices();
            DisableErrorReportingScheduledTasks();
        }

        private void DisableErrorReporting()
        {
            ui.PrintHeading("Writing values into the Registry...");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\PCHealth\ErrorReporting", "DoReport", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);
        }

        private void RemoveErrorReportingServices()
        {
            ui.PrintHeading("Backing up and removing error reporting services...");
            serviceRemover.BackupAndRemove(errorReportingServices);
            IsRebootRecommended = serviceRemover.IsRebootRecommended;
        }

        private void DisableErrorReportingScheduledTasks()
        {
            ui.PrintHeading("Disabling error reporting scheduled tasks...");
            new DisableTasksAction(errorReportingScheduledTasks, ui).Run();
        }
    }
}
