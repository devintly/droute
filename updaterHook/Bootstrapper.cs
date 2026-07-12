using Droute.Core;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;

namespace Droute.UpdaterHook
{
    public class Bootstrapper : AppDomainManager
    {
        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                Logger.Debug("domain initialized, subscribing to assembly load events");
            }
            catch (Exception ex) 
            {
                Logger.Error($"failed to initialize domain manager: {ex.Message}");
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                if (args?.LoadedAssembly == null) return;

                string name = args.LoadedAssembly.GetName()?.Name;
                if (string.IsNullOrEmpty(name)) return;

                if (name.Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
                {
                    AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                    Logger.Info("Update.exe loaded, preparing patches");

                    Type type = args.LoadedAssembly.GetType("Squirrel.Update.Program");
                    if (type == null)
                    {
                        Logger.Error("type \"Squirrel.Update.Program\" not found in assembly");
                        return;
                    }

                    MethodInfo baseMethod = type.GetMethod("ProcessStart",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (baseMethod == null)
                    {
                        Logger.Error("target method \"ProcessStart\" not found");
                        return;
                    }

                    MethodInfo prefixMethod = typeof(Bootstrapper).GetMethod(nameof(MyProcessStart),
                        BindingFlags.Static | BindingFlags.Public);
                    if (prefixMethod == null)
                    {
                        Logger.Error("detour method \"MyProcessStart]\" not found in hook assembly");
                        return;
                    }

                    Logger.Debug("applying harmony prefix patch for ProcessStart");

                    var harmony = new Harmony("Droute.UpdaterHook");
                    harmony.Patch(baseMethod, prefix: new HarmonyMethod(prefixMethod));

                    Logger.Info("hooks successfully installed");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"exception during assembly load interception: {ex.Message}");
            }
        }

        public static bool MyProcessStart(object __instance, string exeName, string arguments, bool shouldWait)
        {
            Logger.Trace($"ProcessStart triggered: exeName=\"{exeName}\", args=\"{arguments}\", wait={shouldWait}");

            // staged paths: .droute.[Guid].tmp
            string proxyStagedPath = null;
            string drouteStagedPath = null;

            try
            {
                string branchRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(branchRoot))
                {
                    Logger.Error("AppContext.BaseDirectory returned null or empty");
                    return true;
                }

                Logger.Debug($"resolving last version path from: {branchRoot}");
                string appDirectory = DiscordManager.GetLastVersionPath(branchRoot);

                if (string.IsNullOrEmpty(appDirectory) || !Directory.Exists(appDirectory))
                {
                    Logger.Error($"resolved app directory is invalid or missing: {appDirectory}");
                    return true;
                }

                Logger.Info($"target app directory: {appDirectory}");

                string proxyPath = Path.Combine(appDirectory, PatchManager.MAIN_PROXY_DLL);
                string droutePath = Path.Combine(appDirectory, "droute.dll");

                // make staged paths
                string stagingSuffix = $".droute.{Guid.NewGuid():N}.tmp";
                proxyStagedPath = proxyPath + stagingSuffix;
                drouteStagedPath = droutePath + stagingSuffix;

                if (File.Exists(proxyPath) && File.Exists(droutePath)) 
                {
                    Logger.Info("patch already been applied, skip installation.");
                    return true;
                }

                Logger.Info($"duplicating {PatchManager.MAIN_PROXY_DLL} to: {proxyPath}");

                // duplicate version.dll to version.{stagingSuffix}
                PatchManager.DuplicateProxy(proxyStagedPath);

                // apply patch for version.dll.{stagingSuffix}
                Logger.Info("applying PE patch...");
                PatchManager.ApplyPEPatch(proxyStagedPath);

                // write main payload to droute.dll.{stagingSuffix}
                Logger.Info("preparing droute.dll payload...");
                File.WriteAllBytes(drouteStagedPath, Properties.Resources.Droute64);

                // publish droute.dll (remove staging suffix)
                Logger.Info("publishing prepared patch files...");
                PatchManager.PublishStagedFile(drouteStagedPath, droutePath);
                drouteStagedPath = null;

                // publish version.dll
                PatchManager.PublishStagedFile(proxyStagedPath, proxyPath);
                proxyStagedPath = null;

                Logger.Debug("patching completed successfully!");
            }
            catch (Exception ex)
            {
                Logger.Error($"unexpected exception in MyProcessStart: {ex.Message}");
                Logger.Trace($"stack trace: {ex.StackTrace}");
            }
            finally
            {
                PatchManager.DeleteStagedFile(proxyStagedPath);
                PatchManager.DeleteStagedFile(drouteStagedPath);
            }

            return true;
        }
    }
}
