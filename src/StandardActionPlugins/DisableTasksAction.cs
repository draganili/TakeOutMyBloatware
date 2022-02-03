using TakeOutMyBloatware.Utils;

namespace TakeOutMyBloatware.Operations
{
    public class DisableTasksAction : IActionHandler
    {
        private readonly string[] scheduledTasksToDisable;
        private readonly IUI ui;

        public DisableTasksAction(string[] scheduledTasksToDisable, IUI ui)
        {
            this.ui = ui;
            this.scheduledTasksToDisable = scheduledTasksToDisable;
        }

        public void Run()
        {
            foreach (string task in scheduledTasksToDisable)
            {
                OS.RunProcessBlockingWithOutput(
                    OS.SystemExecutablePath("schtasks"), $@"/Change /TN ""{task}"" /disable",
                    ui
                );
            }
        }
    }
}
