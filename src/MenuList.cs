using System;
using System.Linq;
using TakeOutMyBloatware.Operations;

namespace TakeOutMyBloatware
{
    abstract class CLIActionList
    {
        public abstract string Plugin { get; }
        public virtual bool ShouldQuit => false;
        public abstract string ProvideDescription();
        public abstract IActionHandler CreateNewAction(IUI ui);
    }

    class ARSA : CLIActionList
    {
        public override string Plugin => "Allow removal of system applications";
        public override string ProvideDescription()
        {
            return
                    @"This action will basically edit an internal database to allow the removal of system UWP apps such as legacy Edge and
                    Security Center via PowerShell (used by this tool) and in Settings app.
                    It is recommended to create a system restore point before proceeding.

                    It is generally safe to remove only those system apps that can be found in Start menu.
                    Certain ""hidden"" apps are there to provide critical OS functionality, and therefore uninstalling them may lead
                    to an unstable or unusable system: BE CAREFUL.

                    Remember also that any system app may be reinstalled after any Windows cumulative update.
                    Before starting, make sure that Microsoft Store is not installing/updating apps in the background.";
        }
        public override IActionHandler CreateNewAction(IUI ui) => new SystemAppsRemovalEnabler(ui);
    }

    class UWPR : CLIActionList
    {
        private readonly Configuration configuration;

        public UWPR(Configuration configuration) => this.configuration = configuration;

        public override string Plugin => "Remove UWP apps from the system";
        public override string ProvideDescription()
        {
            string impactedUsers = configuration.UWPAppsRemovalMode == UWPAppRemovalMode.CurrentUser
                ? "the current user"
                : "all present and future users";
            string explanation = $"The following groups of UWP apps will be removed for {impactedUsers}:";
            foreach (UWPAppGroup app in configuration.UWPAppsToRemove)
                explanation += $"\n  {app}";

            if (configuration.UWPAppsRemovalMode == UWPAppRemovalMode.AllUsers)
                explanation += "\n\nServices, components and scheduled tasks used specifically by those apps will also " +
                               "be disabled or removed,\ntogether with any leftover data.";

            if (configuration.UWPAppsToRemove.Contains(UWPAppGroup.Xbox))
                explanation += "\n\nIn order to fully remove Xbox apps, you need to make system apps removable first.";

            return explanation;
        }
        public override IActionHandler CreateNewAction(IUI ui)
            => new RemoveUWPAppAction(configuration.UWPAppsToRemove, configuration.UWPAppsRemovalMode, ui, new RemoveServicesAction(ui));
    }

    class DBAWD : CLIActionList
    {
        public override string Plugin => "Disable built-in antivirus (Windows Defender)";
        public override string ProvideDescription()
        {
            return
                    @"IMPORTANT: Before starting, disable Tamper protection in Windows Security app under Virus & threat protection settings.

                    Windows Defender antimalware engine and SmartScreen feature will be disabled via Group Policies, and services
                    related to those features will be removed.
                    Furthermore, Windows Security app will be prevented from running automatically at system start-up.
                    Windows Defender Firewall will continue to work as intended.

                    Be aware that SmartScreen for Microsoft Edge and Store apps will be disabled only for the currently logged in user
                    and for new users created after running this procedure.";
        }

        public override IActionHandler CreateNewAction(IUI ui)
        {
            return new DisableMSDefenderAction(ui, new RemoveServicesAction(ui));
        }
    }

    class RME : CLIActionList
    {
        public override string Plugin => "Remove Microsoft Edge (IE) from the system";
        public override string ProvideDescription()
        {
            return
                    @"Both Edge Chromium and legacy Edge browser will be uninstalled from the system.
                    In order to be able to uninstall the latter (which may appear in Start menu once you uninstall the former),
                    you need to make system apps removable.
                    Take note that both browsers may be reinstalled after any Windows cumulative update.
                    Make sure that Edge Chromium is not updating itself before proceeding.";
        }
        public override IActionHandler CreateNewAction(IUI ui)
        {
            return new RemoveMSEdgeAction(ui,
                new RemoveUWPAppAction(
                    new[] { UWPAppGroup.EdgeUWP },
                    UWPAppRemovalMode.AllUsers,
                    ui, new RemoveServicesAction(ui)
                )
            );
        }
    }

    class ROD : CLIActionList
    {
        public override string Plugin => "Remove OneDrive from the system";
        public override string ProvideDescription()
        {
            return "OneDrive will be disabled using Group Policies and then uninstalled for the current user.\n" +
                   "Futhermore, it will be prevented from being installed when a new user logs in for the first time.";
        }
        public override IActionHandler CreateNewAction(IUI ui) => new RemoveOneDriveAction(ui);
    }

    class RMS : CLIActionList
    {
        private readonly Configuration configuration;

        public RMS(Configuration configuration) => this.configuration = configuration;

        public override string Plugin => "Remove misc. services from the system";
        public override string ProvideDescription()
        {
            string explanation = "The services starting with the following names will be removed:\n";
            foreach (string service in configuration.ServicesToRemove)
                explanation += $"  {service}\n";
            return explanation + "Services will be backed up in the same folder as this program executable.";
        }
        public override IActionHandler CreateNewAction(IUI ui)
            => new ServiceRemovalOperation(configuration.ServicesToRemove, ui, new RemoveServicesAction(ui));
    }

    class RWF : CLIActionList
    {
        private readonly Configuration configuration;

        public RWF(Configuration configuration) => this.configuration = configuration;

        public override string Plugin => "Remove Windows features";
        public override string ProvideDescription()
        {
            string explanation = "The following features on demand will be removed:";
            foreach (string feature in configuration.WindowsFeaturesToRemove)
                explanation += $"\n  {feature}";
            return explanation;
        }
        public override IActionHandler CreateNewAction(IUI ui)
            => new RemoveFeaturesAction(configuration.WindowsFeaturesToRemove, ui);
    }

    class ACSFMP : CLIActionList
    {
        public override string Plugin => "Optimize my system for more privacy";
        public override string ProvideDescription()
        {
            return
                    @"Several default settings and policies will be changed to make Windows more respectful of user's privacy.
                    These changes consist essentially of:
                      - adjusting various options under Privacy section of Settings app (disable advertising ID, app launch tracking etc.)
                      - preventing input data (inking/typing information, speech) from being sent to Microsoft to improve their services
                      - preventing Edge from sending browsing history, favorites and other data to Microsoft in order to personalize ads,
                        news and other services for your Microsoft account
                      - denying access to sensitive data (location, documents, activities, account details, diagnostic info) to
                        all UWP apps by default
                      - disabling voice activation for voice assistants (so that they can't always be listening)
                      - disabling cloud synchronization of sensitive data (user activities, clipboard, text messages, passwords
                        and app data)
                      - disabling web search in bottom search bar

                    Whereas almost all of these settings are applied for all users, some of them will only be changed for the current
                    user and for new users created after running this procedure.";
        }
        public override IActionHandler CreateNewAction(IUI ui) => new AutoConfigSettingsAction(ui);
    }

    class DT : CLIActionList
    {
        public override string Plugin => "Disable telemetry in the system";
        public override string ProvideDescription()
        {
            return
                    @"This procedure will disable scheduled tasks, services and features that are responsible for collecting and
                    reporting data to Microsoft, including Compatibility Telemetry, Device Census, Customer Experience Improvement
                    Program and Compatibility Assistant.";
        }
        public override IActionHandler CreateNewAction(IUI ui)
            => new DisableTelemetryAction(ui, new RemoveServicesAction(ui));
    }

    class DAU : CLIActionList
    {
        public override string Plugin => "Disable automatic updates in the system";
        public override string ProvideDescription()
        {
            return "Automatic updates for Windows, Store apps and speech models will be disabled using Group Policies.\n" +
                   "At least Windows 10 Pro edition is required to disable automatic Windows updates.";
        }
        public override IActionHandler CreateNewAction(IUI ui) => new DisableUpdatesAction(ui);
    }

    class DMST : CLIActionList
    {
        private readonly Configuration configuration;

        public DMST(Configuration configuration) => this.configuration = configuration;

        public override string Plugin => "Disable misc. scheduled tasks";
        public override string ProvideDescription()
        {
            string explanation = "The following scheduled tasks will be disabled:";
            foreach (string task in configuration.ScheduledTasksToDisable)
                explanation += $"\n  {task}";
            return explanation;
        }
        public override IActionHandler CreateNewAction(IUI ui)
            => new DisableTasksAction(configuration.ScheduledTasksToDisable, ui);
    }

    class DWER : CLIActionList
    {
        public override string Plugin => "Disable Windows Error Reporting in the system";
        public override string ProvideDescription()
        {
            return
                    @"Windows Error Reporting will disabled by editing Group Policies, as well as by removing its services (after
                    backing them up).";
        }
        public override IActionHandler CreateNewAction(IUI ui)
            => new DisableErrReportingAction(ui, new RemoveServicesAction(ui));
    }

    class DSCCFR : CLIActionList
    {
        public override string Plugin => "Disable suggestions, cloud content and feedback requests from the system";
        public override string ProvideDescription()
        {
            return
                    @"Feedback notifications and requests, apps suggestions, tips and cloud-based content (including Spotlight dynamic
                    backgrounds and News and Interests) will be turned off by setting Group Policies accordingly and by disabling some
                    related scheduled tasks.

                    Be aware that some of these features will be disabled only for the currently logged in user and for new users
                    created after running this procedure.";
        }
        public override IActionHandler CreateNewAction(IUI ui) => new DisableSuggestionAction(ui);
    }

    class ContactDev : CLIActionList
    {
        public override string Plugin => "Contact the developer";
        public override string ProvideDescription()
        {
            return
                    @"After choosing continue, your browser will navigate to the github account of the developer, where you can contact him, contribute to the project or suggest
                    a new feature.";
        }
        public override IActionHandler CreateNewAction(IUI ui)
            => new WebBrowserAction("https://github.com/draganili");
    }

    class About : CLIActionList
    {
        public override string Plugin => "More about the tool";
        public override string ProvideDescription()
        {
            Version programVersion = GetType().Assembly.GetName().Version!;
            return
                    $@"TakeOutMyBloatware {programVersion.Major}.{programVersion.Minor} for Windows 10 version {Program.Minimum_support_version_WIN10} or higher
                    Developed by Dragan Ilievski";
        }
        public override IActionHandler CreateNewAction(IUI ui) => new LicensePrinter(ui);
    }

    class ExitApp : CLIActionList
    {
        private readonly RebootHandler rebootFlag;

        public ExitApp(RebootHandler rebootFlag) => this.rebootFlag = rebootFlag;

        public override string Plugin => "Exit!";
        public override bool ShouldQuit => true;
        public override string ProvideDescription() => "Are you sure? Fine. I'm breaking up with you. It's not me, it's you :)";
        public override IActionHandler CreateNewAction(IUI ui) => new RebootAction(ui, rebootFlag);
    }
}
