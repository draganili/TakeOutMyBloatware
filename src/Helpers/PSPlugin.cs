using Microsoft.PowerShell;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using TakeOutMyBloatware.Operations;

namespace TakeOutMyBloatware.Utils
{
    public static class PSPlugin
    {
        public static PowerShell CreateWithImportedModules(params string[] modules)
        {
            Environment.SetEnvironmentVariable("PSModuleAutoLoadingPreference", "None");
            Environment.SetEnvironmentVariable("POWERSHELL_TELEMETRY_OPTOUT", "true");
            var sessionState = InitialSessionState.CreateDefault2();
            sessionState.ThreadOptions = PSThreadOptions.UseCurrentThread;
            sessionState.ExecutionPolicy = ExecutionPolicy.Unrestricted;
            var powerShell = PowerShell.Create(sessionState);
            powerShell.ImportModules(modules);
            return powerShell;
        }

        private static void ImportModules(this PowerShell powerShell, string[] modules)
        {

            foreach (var module in modules)
                powerShell.Run($"Import-Module {module} -SkipEditionCheck");
        }

        public static dynamic[] Run(this PowerShell powerShell, string script)
        {

            powerShell.Streams.ClearStreams();

            powerShell.AddScript(script);
            Collection<PSObject> results = powerShell.Invoke();


            powerShell.Commands.Clear();

            return UnwrapCommandResults(results);
        }

        private static dynamic[] UnwrapCommandResults(Collection<PSObject> results)
        {
            return results.Select(psObject => psObject.BaseObject).ToArray();
        }

        public static PowerShell WithOutput(this PowerShell powerShell, IMessagePrinter printer)
        {
            powerShell.Streams.Information.DataAdded +=
                (stream, eventArgs) => printer.PrintMessage(GetMessageToPrint<InformationRecord>(stream!, eventArgs));
            powerShell.Streams.Error.DataAdded +=
                (stream, eventArgs) => printer.PrintError(GetMessageToPrint<ErrorRecord>(stream!, eventArgs));
            powerShell.Streams.Warning.DataAdded +=
                (stream, eventArgs) => printer.PrintWarning(GetMessageToPrint<WarningRecord>(stream!, eventArgs));
            return powerShell;
        }

        private static string GetMessageToPrint<TRecord>(object psStream, DataAddedEventArgs eventArgs)
        {
            var powerShellCollection = (PSDataCollection<TRecord>)psStream;
            return powerShellCollection[eventArgs.Index]!.ToString()!;
        }
    }
}
