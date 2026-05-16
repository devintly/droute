using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Droute.Installer.Classes
{
    public static class DiscordManager
    {
        private const string PROCESS_NAME = "Discord";

        private const string FILENAME = "Discord.exe";
        private const string DIRECTORY = "Discord";

        public static string GetExecutablePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dsDir = Path.Combine(localAppData, DIRECTORY);

            if (Directory.Exists(dsDir))
            {
                var exe = Directory.GetFiles(dsDir, FILENAME, SearchOption.AllDirectories).OrderByDescending(f => f).FirstOrDefault();
                return exe;
            }

            return null;
        }

        public static void Close()
        {
            var processes = Process.GetProcessesByName(PROCESS_NAME);
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch { }
            }
        }

        public static bool Launch()
        {
            string path = GetExecutablePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
