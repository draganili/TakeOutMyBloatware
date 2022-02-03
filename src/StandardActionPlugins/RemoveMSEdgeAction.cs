﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using TakeOutMyBloatware.Utils;
using static System.Environment;
using Version = System.Version;

namespace TakeOutMyBloatware.Operations
{
    public class RemoveMSEdgeAction : IActionHandler
    {
        private readonly IUI ui;
        private readonly IActionHandler legacyEdgeRemover;

        public RemoveMSEdgeAction(IUI ui, IActionHandler legacyEdgeRemover)
        {
            this.ui = ui;
            this.legacyEdgeRemover = legacyEdgeRemover;
        }

        public void Run()
        {
            UninstallEdgeChromiumIfPresent();
            legacyEdgeRemover.Run();
        }

        private void UninstallEdgeChromiumIfPresent()
        {
            ui.PrintHeading("Removing Edge Chromium...");
            string? installerPath = RetrieveEdgeChromiumInstallerPath();
            if (installerPath == null)
            {
                ui.PrintMessage("Edge Chromium is not installed.");
                return;
            }

            ui.PrintMessage("Running uninstaller...");
            OS.RunProcessBlocking(installerPath, "--uninstall --force-uninstall --system-level");
            // Since part of the uninstallation happens in another process launched by the installer, we wait
            // for a reasonable amount of time to let this process do its work before continuing
            Thread.Sleep(TimeSpan.FromSeconds(5));

            RemoveEdgeLeftovers();
        }

        private string? RetrieveEdgeChromiumInstallerPath()
        {
            string edgeChromiumBaseFolder = $@"{GetFolderPath(SpecialFolder.ProgramFilesX86)}\Microsoft\Edge\Application";
            if (!Directory.Exists(edgeChromiumBaseFolder))
                return null;

            Version? edgeChromiumVersion = null;
            string? folderOfSpecificEdgeVersion = Directory.EnumerateDirectories(edgeChromiumBaseFolder)
                .FirstOrDefault(folder => Version.TryParse(new DirectoryInfo(folder).Name, out edgeChromiumVersion));

            if (folderOfSpecificEdgeVersion != null)
            {
                string installerPath = $@"{folderOfSpecificEdgeVersion}\Installer\setup.exe";
                if (File.Exists(installerPath))
                {
                    ui.PrintMessage($"Detected Edge Chromium version {edgeChromiumVersion}.");
                    return installerPath;
                }
            }
            return null;
        }

        private void RemoveEdgeLeftovers()
        {
            ui.PrintMessage("Removing leftover data for the current user...");
            OS.TryDeleteDirectoryIfExists($@"{GetFolderPath(SpecialFolder.UserProfile)}\MicrosoftEdgeBackups", ui);
            OS.TryDeleteDirectoryIfExists($@"{GetFolderPath(SpecialFolder.LocalApplicationData)}\MicrosoftEdge", ui);
            OS.TryDeleteDirectoryIfExists($@"{GetFolderPath(SpecialFolder.LocalApplicationData)}\Microsoft\Edge", ui);
            var desktopShortcut = new FileInfo($@"{GetFolderPath(SpecialFolder.DesktopDirectory)}\Microsoft Edge.lnk");
            if (desktopShortcut.Exists)
                desktopShortcut.Delete();
        }
    }
}
