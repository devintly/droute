using Droute.Core;
using System;
using System.IO;

namespace Droute.Installer.Classes
{
    internal class PatchTools
    {
        public static event Action<string> OnLog;
        public static event Action<int> OnProgressChanged;

        private const string UPDATER_HOOK_DLL = "Droute.UpdaterHook.dll";
        private const string UPDATER_CONFIG = "Update.exe.config";

        public static bool Install(DiscordManager.Branches branch)
        {
            try
            {
                OnLog?.Invoke($"[ STATUS ] Instalation started for Discord {branch.ToString()}...");
                OnProgressChanged?.Invoke(0);

                OnLog?.Invoke("[ STAGE ] Initializing and verifying paths...");

                string branchRoot = DiscordManager.GetBranchRoot(branch);
                if (string.IsNullOrEmpty(branchRoot) || !Directory.Exists(branchRoot))
                    throw new DirectoryNotFoundException($"Branch Root directory not found");

                string appDirectory = DiscordManager.GetLastVersionPath(branchRoot);
                if (string.IsNullOrEmpty(appDirectory) || !Directory.Exists(appDirectory))
                    throw new DirectoryNotFoundException($"App directory not found");

                string proxyPath = Path.Combine(appDirectory, PatchManager.MAIN_PROXY_DLL);
                string payloadPath = Path.Combine(appDirectory, PatchManager.MAIN_PAYLOAD_DLL);

                string updaterPath = Path.Combine(branchRoot, "Update.exe");
                if (string.IsNullOrEmpty(updaterPath) || !File.Exists(updaterPath))
                    throw new FileNotFoundException($"Update.exe not found");

                string updaterHookPath = Path.Combine(branchRoot, UPDATER_HOOK_DLL);
                string updaterConfigPath = Path.Combine(branchRoot, UPDATER_CONFIG);

                OnLog?.Invoke("[ OK ] All target environment paths verified successfully!");
                OnProgressChanged?.Invoke(15);

                OnLog?.Invoke("[ STAGE ] Deploying...");

                OnLog?.Invoke($"[ > ] Cloning system proxy to: {PatchManager.MAIN_PROXY_DLL}...");
                PatchManager.DuplicateProxy(proxyPath);
                OnProgressChanged?.Invoke(30);

                OnLog?.Invoke($"[ > ] Applying Import Tables to: {PatchManager.MAIN_PROXY_DLL}...");
                PatchManager.ApplyPEPatch(proxyPath);
                OnProgressChanged?.Invoke(45);

                OnLog?.Invoke($"[ > ] Deploying payload: {PatchManager.MAIN_PAYLOAD_DLL}...");
                File.WriteAllBytes(payloadPath, Properties.Resources.Droute64);
                OnProgressChanged?.Invoke(60);

                OnLog?.Invoke($"[ STAGE ] Configuring Squirrel Update hooks...");

                OnLog?.Invoke($"[ > ] Creating configuration: {UPDATER_CONFIG}...");
                File.WriteAllText(updaterConfigPath, Properties.Resources.UpdaterConfig);
                OnProgressChanged?.Invoke(75);

                OnLog?.Invoke($"[ > ] Extracting UpdaterHook: {UPDATER_HOOK_DLL}...");
                File.WriteAllBytes(updaterHookPath, Properties.Resources.UpdaterHook);
                OnProgressChanged?.Invoke(90);

                OnLog?.Invoke($"[ STATUS ] Instalation successfully completed!");
                OnProgressChanged?.Invoke(100);

                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[ FATAL ]: {ex.Message}");
                OnProgressChanged?.Invoke(100);
                return false;
            }
        }

        public static bool Remove(DiscordManager.Branches branch)
        {
            try
            {
                OnLog?.Invoke($"[ STATUS ] Removal started for Discord {branch.ToString()}...");
                OnProgressChanged?.Invoke(0);

                string branchRoot = DiscordManager.GetBranchRoot(branch);
                if (string.IsNullOrEmpty(branchRoot) || !Directory.Exists(branchRoot))
                {
                    OnLog?.Invoke("[ WARN ] Branch root directory not found. Nothing to remove.");
                    OnProgressChanged?.Invoke(100);
                    return true;
                }

                string updaterHookPath = Path.Combine(branchRoot, UPDATER_HOOK_DLL);
                string updaterConfigPath = Path.Combine(branchRoot, UPDATER_CONFIG);

                OnLog?.Invoke("[ STAGE ] Cleaning up Squirrel Update hooks...");

                if (File.Exists(updaterHookPath))
                {
                    OnLog?.Invoke($"[ < ] Removing: {UPDATER_HOOK_DLL}...");
                    File.Delete(updaterHookPath);
                }
                OnProgressChanged?.Invoke(30);

                if (File.Exists(updaterConfigPath))
                {
                    OnLog?.Invoke($"[ < ] Removing: {UPDATER_CONFIG}...");
                    File.Delete(updaterConfigPath);
                }
                OnProgressChanged?.Invoke(50);

                string appDirectory = DiscordManager.GetLastVersionPath(branchRoot);
                if (!string.IsNullOrEmpty(appDirectory) && Directory.Exists(appDirectory))
                {
                    string proxyPath = Path.Combine(appDirectory, PatchManager.MAIN_PROXY_DLL);
                    string payloadPath = Path.Combine(appDirectory, PatchManager.MAIN_PAYLOAD_DLL);

                    OnLog?.Invoke("[ STAGE ] Cleaning up...");

                    if (File.Exists(proxyPath))
                    {
                        OnLog?.Invoke($"[ < ] Deleting modified proxy: {PatchManager.MAIN_PROXY_DLL}...");
                        File.Delete(proxyPath);
                    }
                    OnProgressChanged?.Invoke(75);

                    if (File.Exists(payloadPath))
                    {
                        OnLog?.Invoke($"[ < ] Deleting payload: {PatchManager.MAIN_PAYLOAD_DLL}...");
                        File.Delete(payloadPath);
                    }
                }
                else
                {
                    OnLog?.Invoke("[ INFO ] Application version directory not found, skipping core payload cleanup.");
                }

                OnLog?.Invoke($"[ STATUS ] Removal completed successfully for Discord {branch}!");
                OnProgressChanged?.Invoke(100);

                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[ FATAL ] Removal failed: {ex.Message}");
                return false;
            }
        }
    }
}
