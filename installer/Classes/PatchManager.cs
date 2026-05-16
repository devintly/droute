using PeNet;
using System;
using System.IO;

namespace Droute.Installer.Classes
{
    internal class PatchManager
    {
        private const string PROXY_DLL = "version.dll";
        private const string PAYLOAD_DLL = "droute.dll";

        public static event Action<string> OnLog;
        public static event Action<int> OnProgressChanged;

        public static bool Install()
        {
            try
            {
                OnLog?.Invoke("Locating Discord directory...");
                string folder = GetDiscordFolder();
                OnProgressChanged?.Invoke(20);

                OnLog?.Invoke("Copying version.dll...");
                PrepareProxyDll(folder);
                OnProgressChanged?.Invoke(40);

                OnLog?.Invoke("Writing droute.dll...");
                PreparePayloadDll(folder);
                OnProgressChanged?.Invoke(60);

                OnLog?.Invoke("Patching version.dll...");
                ApplyPePatch(Path.Combine(folder, PROXY_DLL));
                OnProgressChanged?.Invoke(90);

                OnLog?.Invoke("Patch installed successfully!");
                OnProgressChanged?.Invoke(100);
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"ERROR: {ex.Message}");
                return false;
            }
        }

        public static bool Remove()
        {
            try
            {
                OnLog?.Invoke("Locating Discord directory...");
                string folder = GetDiscordFolder();
                OnProgressChanged?.Invoke(30);

                OnLog?.Invoke("Removing version.dll...");
                DeleteFile(Path.Combine(folder, PROXY_DLL));
                OnProgressChanged?.Invoke(60);

                OnLog?.Invoke("Removing droute.dll...");
                DeleteFile(Path.Combine(folder, PAYLOAD_DLL));
                OnProgressChanged?.Invoke(100);

                OnLog?.Invoke("Patch removed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"ERROR: {ex.Message}");
                return false;
            }
        }

        private static string GetDiscordFolder()
        {
            string path = DiscordManager.GetExecutablePath();
            if (string.IsNullOrEmpty(path)) throw new Exception("Discord path not found in registry.");
            return Path.GetDirectoryName(path);
        }

        private static void PrepareProxyDll(string targetFolder)
        {
            string source = Path.Combine(Environment.SystemDirectory, PROXY_DLL);
            string destination = Path.Combine(targetFolder, PROXY_DLL);
            File.Copy(source, destination, true);
        }

        private static void PreparePayloadDll(string targetFolder)
        {
            string destination = Path.Combine(targetFolder, PAYLOAD_DLL);
            File.WriteAllBytes(destination, Properties.Resources.Droute64);
        }

        private static void ApplyPePatch(string filePath)
        {
            var peFile = new PeFile(filePath);
            peFile.AddImport(PAYLOAD_DLL, "DllMain");
            File.WriteAllBytes(filePath, peFile.RawFile.ToArray());
        }

        private static void DeleteFile(string filePath)
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }
}