using Droute.Core;
using System;
using System.IO;
using System.Threading;

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
            // staged files: .droute.[Guid].tmp
            string proxyStagedPath = null;
            string payloadStagedPath = null;
            string updaterHookStagedPath = null;
            string updaterConfigStagedPath = null;

            try
            {
                OnLog?.Invoke($"[ STATUS ] Installation started for Discord {branch.ToString()}...");
                OnProgressChanged?.Invoke(0);

                OnLog?.Invoke("[ STAGE ] Initializing and verifying paths...");

                // get discord root
                string branchRoot = DiscordManager.GetBranchRoot(branch);
                if (string.IsNullOrEmpty(branchRoot) || !Directory.Exists(branchRoot))
                    throw new DirectoryNotFoundException($"Branch Root directory not found");

                // get discord app direcory
                string appDirectory = DiscordManager.GetLastVersionPath(branchRoot);
                if (string.IsNullOrEmpty(appDirectory) || !Directory.Exists(appDirectory))
                    throw new DirectoryNotFoundException($"App directory not found");

                // get paths for release droute.dll and version.dll
                string proxyPath = Path.Combine(appDirectory, PatchManager.MAIN_PROXY_DLL);
                string payloadPath = Path.Combine(appDirectory, PatchManager.MAIN_PAYLOAD_DLL);

                // get path for Squirrel Updater
                string updaterPath = Path.Combine(branchRoot, "Update.exe");
                if (string.IsNullOrEmpty(updaterPath) || !File.Exists(updaterPath))
                    throw new FileNotFoundException($"Update.exe not found");

                // get paths for Droute.UpdaterHook.dll and Update.exe.config
                string updaterHookPath = Path.Combine(branchRoot, UPDATER_HOOK_DLL);
                string updaterConfigPath = Path.Combine(branchRoot, UPDATER_CONFIG);

                // waiting for droute files to be released, else throw error
                WaitForFilesAvailable(new[]
                {
                    proxyPath,
                    payloadPath,
                    updaterHookPath,
                    updaterConfigPath
                }, 5000);

                // make staging suffix
                string stagingSuffix = $".droute.{Guid.NewGuid():N}.tmp";

                // make staging paths for all droute files
                proxyStagedPath = proxyPath + stagingSuffix;
                payloadStagedPath = payloadPath + stagingSuffix;
                updaterHookStagedPath = updaterHookPath + stagingSuffix;
                updaterConfigStagedPath = updaterConfigPath + stagingSuffix;

                OnLog?.Invoke("[ OK ] All target environment paths verified successfully!");
                OnProgressChanged?.Invoke(15);

                OnLog?.Invoke("[ STAGE ] Deploying...");
                
                // duplicate version.dll to version.{stagingSuffix} in discord app folder
                OnLog?.Invoke($"[ > ] Preparing system proxy: {PatchManager.MAIN_PROXY_DLL}...");
                PatchManager.DuplicateProxy(proxyStagedPath);
                OnProgressChanged?.Invoke(30);

                // patch version.dll.{stagingSuffix} import tables
                OnLog?.Invoke($"[ > ] Applying Import Tables to: {PatchManager.MAIN_PROXY_DLL}...");
                PatchManager.ApplyPEPatch(proxyStagedPath);
                OnProgressChanged?.Invoke(45);

                // write droute.dll.{stagingSuffix} on disk
                OnLog?.Invoke($"[ > ] Preparing payload: {PatchManager.MAIN_PAYLOAD_DLL}...");
                File.WriteAllBytes(payloadStagedPath, Properties.Resources.Droute64);
                OnProgressChanged?.Invoke(60);

                OnLog?.Invoke($"[ STAGE ] Configuring Squirrel Update hooks...");

                // write Update.exe.config.{stagingSuffix}
                OnLog?.Invoke($"[ > ] Preparing configuration: {UPDATER_CONFIG}...");
                File.WriteAllText(updaterConfigStagedPath, Properties.Resources.UpdaterConfig);
                OnProgressChanged?.Invoke(75);

                // write Droute.UpdaterHook.dll.{stagingSuffix} on disk
                OnLog?.Invoke($"[ > ] Preparing UpdaterHook: {UPDATER_HOOK_DLL}...");
                File.WriteAllBytes(updaterHookStagedPath, Properties.Resources.UpdaterHook);
                OnProgressChanged?.Invoke(90);

                OnLog?.Invoke("[ STAGE ] Publishing prepared files...");

                // replace droute.dll.{stagingSuffix} to droute.dll
                PatchManager.PublishStagedFile(payloadStagedPath, payloadPath);
                payloadStagedPath = null;

                // replace Droute.UpdaterHook.dll.{stagingSuffix} to Droute.UpdaterHook.dll
                PatchManager.PublishStagedFile(updaterHookStagedPath, updaterHookPath);
                updaterHookStagedPath = null;

                // replace version.dll.{stagingSuffix} to version.dll
                PatchManager.PublishStagedFile(proxyStagedPath, proxyPath);
                proxyStagedPath = null;

                // replace Update.exe.config.{stagingSuffix} to Update.exe.config
                PatchManager.PublishStagedFile(updaterConfigStagedPath, updaterConfigPath);
                updaterConfigStagedPath = null;

                // installation completed, yapii >3
                OnLog?.Invoke($"[ STATUS ] Installation successfully completed!");
                OnProgressChanged?.Invoke(100);

                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[ FATAL ]: {ex.Message}");
                OnProgressChanged?.Invoke(100);
                return false;
            }
            finally
            {
                // delete all staged files
                PatchManager.DeleteStagedFile(proxyStagedPath);
                PatchManager.DeleteStagedFile(payloadStagedPath);
                PatchManager.DeleteStagedFile(updaterHookStagedPath);
                PatchManager.DeleteStagedFile(updaterConfigStagedPath);
            }
        }

        private static void WaitForFilesAvailable(string[] paths, int timeoutMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            Exception lastException = null;

            do
            {
                bool allAvailable = true;

                foreach (string path in paths)
                {
                    if (!File.Exists(path))
                        continue;

                    try
                    {
                        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    }
                    catch (IOException ex)
                    {
                        allAvailable = false;
                        lastException = ex;
                        break;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        allAvailable = false;
                        lastException = ex;
                        break;
                    }
                }

                if (allAvailable)
                    return;

                Thread.Sleep(100);
            }
            while (DateTime.UtcNow < deadline);

            throw new IOException("One or more Droute target files are still in use.", lastException);
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
