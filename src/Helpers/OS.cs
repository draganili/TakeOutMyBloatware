﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using TakeOutMyBloatware.Operations;

namespace TakeOutMyBloatware.Utils
{
    static class OS
    {
        private static readonly Lazy<string?> windowsReleaseId = new Lazy<string?>(() =>
            (string?)Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", defaultValue: null));

        public static string? WindowsReleaseId => windowsReleaseId.Value;

        public static void StopServiceAndItsDependents(string name)
        {
            using var service = new ServiceController(name);
            foreach (var dependent in service.DependentServices)
                StopServiceAndItsDependents(dependent.ServiceName);

            if (service.Status != ServiceControllerStatus.Stopped)
            {
                if (service.Status != ServiceControllerStatus.StopPending)
                    service.Stop(stopDependentServices: false);
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
            }
        }

        public static void RebootPC()
        {
            RunProcessBlocking(SystemExecutablePath("shutdown"), "/r /t 5");
        }

        public static string GetProgramFilesFolder()
        {
            // See docs.microsoft.com/en-us/windows/win32/winprog64/wow64-implementation-details#environment-variables
            if (Environment.Is64BitOperatingSystem)
                return Environment.GetEnvironmentVariable("ProgramW6432")!;
            else
                return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        }

        public static void ExecuteWindowsPromptCommand(string command, IMessagePrinter printer)
        {
            Debug.WriteLine($"Command executed: {command}");
            RunProcessBlockingWithOutput(SystemExecutablePath("cmd"), $@"/c ""{command}""", printer);
        }

        public static string SystemExecutablePath(string executableName)
        {
            // SpecialFolder.SystemX86 returns SysWOW64 folder on 64-bit systems
            string systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
            return $@"{systemFolder}\{executableName}.exe";
        }

        public static void CloseExplorer()
        {

            IntPtr trayWindow = FindWindow("Shell_TrayWnd", null);
            if (trayWindow != IntPtr.Zero)
            {
                PostMessage(trayWindow, 0x5B4, IntPtr.Zero, IntPtr.Zero);
                Thread.Sleep(TimeSpan.FromSeconds(3)); // wait for the process to gracefully exit
            }
            KillProcess("explorer");
        }

        public static void StartExplorer()
        {

            string windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            Process.Start($@"{windowsFolder}\explorer.exe");
        }

        public static void KillProcess(string processName)
        {
            foreach (var processToKill in Process.GetProcessesByName(processName))
            {
                processToKill.Kill();
                processToKill.WaitForExit();
            }
        }

        public static void KillChildProcesses(this Process process)
        {
            var searcher = new ManagementObjectSearcher(
                $"Select * From Win32_Process Where ParentProcessID={process.Id}"
            );
            foreach (var managementObject in searcher.Get())
            {
                using var child = Process.GetProcessById(Convert.ToInt32(managementObject["ProcessID"]));
                child.KillChildProcesses();
                child.Kill();
            }
        }

        public static ExitCode RunProcessBlocking(string name, string args)
        {
            using var process = CreateProcessInstance(name, args);
            process.Start();
            process.WaitForExit();
            return new ExitCode(process.ExitCode);
        }

        public static ExitCode RunProcessBlockingWithOutput(string name, string args, IMessagePrinter printer)
        {
            using var process = CreateProcessInstance(name, args);
            process.OutputDataReceived += (_, evt) =>
            {
                if (!string.IsNullOrEmpty(evt.Data))
                    printer.PrintMessage(evt.Data);
            };
            process.ErrorDataReceived += (_, evt) =>
            {
                if (!string.IsNullOrEmpty(evt.Data))
                    printer.PrintError(evt.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return new ExitCode(process.ExitCode);
        }

        private static Process CreateProcessInstance(string name, string args)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
        }

        public static void TryDeleteDirectoryIfExists(string path, IMessagePrinter printer)
        {
            try
            {
                DeleteDirectoryIfExists(path);
            }
            catch (Exception exc)
            {
                printer.PrintError($"An error occurred when deleting folder {path}: {exc.Message}");
            }
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            var directoryToDelete = new DirectoryInfo(path);
            if (directoryToDelete.Exists)
            {

                directoryToDelete.Attributes = FileAttributes.Directory;
                directoryToDelete.Delete(recursive: true);
            }
        }

        public static bool IsWindows10()
        {
            var windowsVersion = Environment.OSVersion.Version;
            return windowsVersion.Major == 10 && windowsVersion.Build < 21996;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? className, string? windowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
    }
}