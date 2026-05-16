using Droute.Core;
using HarmonyLib;
using System;
using System.Reflection;
using System.Windows.Forms;

namespace Droute.UpdaterHook
{
    public class Bootstrapper : AppDomainManager
    {
        private static readonly Harmony HarmonyInstance = new Harmony("com.snowluwu.droute.updatehook");

        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            Logger.Info("AppDomainManager initialized, waiting for Update.exe");
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly.GetName().Name.Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                Logger.Info("target assembly loaded!");

                Type type = args.LoadedAssembly.GetType("Squirrel.Update.Program");
                if (type == null)
                {
                    Logger.Error("failed to find Squirrel.Update.Program type");
                    return;
                }

                MethodInfo baseMethod = type.GetMethod("ProcessStart",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (baseMethod == null)
                {
                    Logger.Error("failed to find ProcessStart() info");
                    return;
                }

                MethodInfo prefixMethod = typeof(Bootstrapper).GetMethod(nameof(MyProcessStart),
                    BindingFlags.Static | BindingFlags.Public);
                if (prefixMethod == null)
                {
                    Logger.Error("failed to find internal MyProcessStart() info");
                    return;
                }

                HarmonyInstance.Patch(baseMethod, prefix: new HarmonyMethod(prefixMethod));
                Logger.Info("hooks successfully installed for Squirrel.Update.Program.ProcessStart()");
            }
        }

        internal static bool MyProcessStart(object __instance, string exeName, string arguments, bool shouldWait)
        {
            MessageBox.Show($"yap! \nexeName: {exeName}\n args: {arguments}");
            return true;
        }
    }
}