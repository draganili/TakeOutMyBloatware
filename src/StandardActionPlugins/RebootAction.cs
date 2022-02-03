using TakeOutMyBloatware.Utils;
using static TakeOutMyBloatware.Operations.IUI;

namespace TakeOutMyBloatware.Operations
{
    class RebootAction : IActionHandler
    {
        private readonly IUI ui;
        private readonly RebootHandler rebootFlag;

        public RebootAction(IUI ui, RebootHandler rebootFlag)
        {
            this.ui = ui;
            this.rebootFlag = rebootFlag;
        }

        public void Run()
        {
            if (rebootFlag.IsRebootRecommended)
            {
                ui.PrintWarning("You have executed one or more operations that require a system reboot to take full effect.");
                var choice = ui.AskUserConsent("Do you want to reboot now?");
                if (choice == UserChoice.Yes)
                    OS.RebootPC();
            }
        }
    }
}
