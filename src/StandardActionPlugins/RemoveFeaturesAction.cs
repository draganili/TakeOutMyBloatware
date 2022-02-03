using System.Linq;
using System.Management.Automation;
using TakeOutMyBloatware.Utils;

namespace TakeOutMyBloatware.Operations
{
    public class RemoveFeaturesAction : IActionHandler
    {
        private readonly string[] featuresToRemove;
        private readonly IUI ui;

        private /*lateinit*/ PowerShell powerShell;

        public bool IsRebootRecommended { get; private set; }

#nullable disable warnings
        public RemoveFeaturesAction(string[] featuresToRemove, IUI ui)
        {
            this.featuresToRemove = featuresToRemove;
            this.ui = ui;
        }
#nullable restore warnings

        public void Run()
        {
            using (powerShell = PSPlugin.CreateWithImportedModules("Dism").WithOutput(ui))
            {
                foreach (string capabilityName in featuresToRemove)
                    RemoveCapabilitiesWhoseNameStartsWith(capabilityName);
            }
        }

        private void RemoveCapabilitiesWhoseNameStartsWith(string capabilityName)
        {
            var capabilities = powerShell.Run($"Get-WindowsCapability -Online -Name {capabilityName}*");
            if (capabilities.Length == 0)
            {
                ui.PrintWarning($"No features found with name {capabilityName}.");
                return;
            }

            foreach (var capability in capabilities)
                RemoveCapability(capability);
        }

        private void RemoveCapability(dynamic capability)
        {
            if (capability.State.ToString() != "Installed")
            {
                ui.PrintMessage($"Feature {capability.Name} is not installed.");
                return;
            }

            ui.PrintMessage($"Removing feature {capability.Name}...");
            var result = powerShell.Run($"Remove-WindowsCapability -Online -Name {capability.Name}").First();
            if (result.RestartNeeded)
                IsRebootRecommended = true;

            if (capability.Name.StartsWith("Hello.Face"))
                new DisableTasksAction(new[] { @"\Microsoft\Windows\HelloFace\FODCleanupTask" }, ui).Run();
        }
    }
}
