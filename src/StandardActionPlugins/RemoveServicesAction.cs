﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TakeOutMyBloatware.Utils;

namespace TakeOutMyBloatware.Operations
{
    public class ServiceRemovalOperation : IActionHandler
    {
        private readonly string[] servicesToRemove;
        private readonly IUI ui;
        private readonly RemoveServicesAction serviceRemover;

        public bool IsRebootRecommended => serviceRemover.IsRebootRecommended;

        public ServiceRemovalOperation(string[] servicesToRemove, IUI ui, RemoveServicesAction serviceRemover)
        {
            this.servicesToRemove = servicesToRemove;
            this.ui = ui;
            this.serviceRemover = serviceRemover;
        }

        public void Run()
        {
            ui.PrintHeading("Backing up services...");
            string[] actualBackuppedServices = serviceRemover.PerformBackup(servicesToRemove);

            if (actualBackuppedServices.Length > 0)
            {
                ui.PrintHeading("Removing services...");
                serviceRemover.PerformRemoval(actualBackuppedServices);
            }
        }
    }

    /*
     *  Performs backup (export of registry keys) and removal of those services whose name starts with the given service names.
     *  This is made in order to include services that end with a random code.
     */
    public class RemoveServicesAction
    {
        private readonly DirectoryInfo backupDirectory;
        private readonly IUI ui;

        private const int SC_EXIT_CODE_MARKED_FOR_DELETION = 1072;

        public bool IsRebootRecommended { get; private set; }

        public RemoveServicesAction(IUI ui) : this(ui, DateTime.Now) { }
        public RemoveServicesAction(IUI ui, DateTime now)
        {
            this.ui = ui;
            backupDirectory = new DirectoryInfo($"servicesBackup_{now:yyyy-MM-dd_HH-mm-ss}");
        }

        public void BackupAndRemove(params string[] servicesToRemove)
        {
            IsRebootRecommended = false;
            string[] actualBackuppedServices = PerformBackup(servicesToRemove);
            PerformRemoval(actualBackuppedServices);
        }

        public string[] PerformBackup(string[] servicesToBackup)
        {
            string[] existingServices = FindExistingServicesWithNames(servicesToBackup);
            foreach (string service in existingServices)
                BackupService(service);
            return existingServices;
        }

        private string[] FindExistingServicesWithNames(string[] servicesNames)
        {
            string[] allExistingServices = GetAllServicesNames();
            List<string> allMatchingServices = new List<string>();
            foreach (string serviceName in servicesNames)
            {
                var matchingServices = allExistingServices.Where(name => name.StartsWith(serviceName)).ToArray();
                if (matchingServices.Length == 0)
                    ui.PrintMessage($"No services found with name {serviceName}.");
                else
                    allMatchingServices.AddRange(matchingServices);
            }

            return allMatchingServices.ToArray();
        }

        private string[] GetAllServicesNames()
        {
            using RegistryKey servicesKey = Registry.LocalMachine.OpenSubKeyWritable(@"SYSTEM\CurrentControlSet\Services");
            return servicesKey.GetSubKeyNames();
        }

        private void BackupService(string service)
        {
            EnsureBackupDirectoryExists();
            var regExportExitCode = OS.RunProcessBlocking(
                OS.SystemExecutablePath("reg"),
                $@"export ""HKLM\SYSTEM\CurrentControlSet\Services\{service}"" ""{backupDirectory.FullName}\{service}.reg"""
            );
            if (regExportExitCode.IsSuccessful())
                ui.PrintMessage($"Service {service} backed up.");
            else
                throw new Exception($"Could not backup service {service}.");
        }

        private void EnsureBackupDirectoryExists()
        {
            if (!backupDirectory.Exists)
                backupDirectory.Create();
        }

        public void PerformRemoval(string[] backuppedServices)
        {
            foreach (string service in backuppedServices)
                RemoveService(service);
        }

        private void RemoveService(string service)
        {
            var scExitCode = OS.RunProcessBlocking(OS.SystemExecutablePath("sc"), $"delete \"{service}\"");
            if (IsScRemovalSuccessful(scExitCode))
            {
                PrintSuccessMessage(scExitCode, service);
                if (scExitCode == SC_EXIT_CODE_MARKED_FOR_DELETION)
                    IsRebootRecommended = true;
            }
            else
            {
                // Unstoppable (but not protected) system services are not removable with SC,
                // but can be removed by deleting their Registry keys
                Debug.WriteLine($"SC removal failed with exit code {scExitCode} for service {service}.");
                DeleteServiceRegistryKey(service);
            }
        }

        private bool IsScRemovalSuccessful(ExitCode exitCode)
        {
            return exitCode.IsSuccessful() ||
                   exitCode == SC_EXIT_CODE_MARKED_FOR_DELETION;
        }

        private void PrintSuccessMessage(ExitCode scExitCode, string service)
        {
            if (scExitCode == SC_EXIT_CODE_MARKED_FOR_DELETION)
                ui.PrintMessage($"Service {service} will be removed after reboot.");
            else
                ui.PrintMessage($"Service {service} removed successfully.");
        }

        private void DeleteServiceRegistryKey(string service)
        {
            try
            {
                using var allServicesKey = Registry.LocalMachine.OpenSubKeyWritable(@"SYSTEM\CurrentControlSet\Services");
                allServicesKey.DeleteSubKeyTree(service);
                ui.PrintMessage($"Service {service} removed, but it will continue to run until the next restart.");
                IsRebootRecommended = true;
            }
            catch (Exception exc)
            {
                ui.PrintError($"Service {service} removal failed: couldn't delete its registry keys ({exc.Message}).");
            }
        }
    }
}
