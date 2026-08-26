using CommandLine;
using Droute.Core;
using Droute.Installer.Classes;
using Droute.Installer.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DrCore = Droute.Core.Droute;

namespace Droute.Installer
{
    internal static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        private static bool createdNew;
        private static Mutex mtx;

        [STAThread]
        static void Main(string[] args)
        {
            mtx = new Mutex(true, "snowluwu.droute", out createdNew);

            // droute is already running
            if (!createdNew)
                return;

            if (args.Length > 0)
            {
                if (AttachConsole(-1)) // ATTACH_PARENT_PROCESS
                {
                    Console.Out.Flush();
                }

                #region [ ASCII ART ]

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(@"
    ____  ____  ____  __  ______________
   / __ \/ __ \/ __ \/ / / /_  __/ ____/
  / / / / /_/ / / / / / / / / / / __/   
 / /_/ / _, _/ /_/ / /_/ / / / / /___   
/_____/_/ |_|\____/\____/ /_/ /_____/

");

                #endregion

                #region [ About Droute ]

                var versionInfo = new Version(Application.ProductVersion);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"> CLI Mode (v. {versionInfo.Major}.{versionInfo.Minor}.{versionInfo.Build})");

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("> by snowluwu <3");
                Console.WriteLine();

                #endregion

                Parser.Default.ParseArguments<ArgumentOptions>(NormalizeCliArguments(args))
                    .WithParsed(CliActions)
                    .WithNotParsed(OnCliError);

                Console.ResetColor();
                SendKeys.SendWait("{ENTER}");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FrmMain());
        }

        private static void CliActions(ArgumentOptions opts)
        {
            if (opts.Install && opts.Uninstall)
            {
                CliLogger.WriteError("Use either `-install` or `-uninstall`, not both.");
                Environment.ExitCode = 2;
                return;
            }

            if (!Enum.TryParse(opts.Branch, true, out DiscordManager.Branches branch) ||
                !Enum.IsDefined(typeof(DiscordManager.Branches), branch))
            {
                CliLogger.WriteError("Invalid branch. Expected `stable`, `canary` or `ptb`.");
                Environment.ExitCode = 2;
                return;
            }

            using (var logger = new CliLogger())
            {
                try
                {
                    bool configChanged = ApplyOptionalConfig(opts, branch);

                    if (!opts.Install && !opts.Uninstall)
                    {
                        if (!configChanged)
                        {
                            CliLogger.WriteError("No action or proxy settings were specified.");
                            Environment.ExitCode = 2;
                        }
                        else
                        {
                            Environment.ExitCode = 0;
                        }

                        return;
                    }

                    bool success = opts.Install
                        ? PatchTools.Install(branch, opts.InstallPath, opts.Portable)
                        : PatchTools.Remove(branch, opts.InstallPath, opts.Portable);

                    Environment.ExitCode = success ? 0 : 1;
                }
                catch (Exception ex)
                {
                    CliLogger.WriteError(ex.Message);
                    Environment.ExitCode = 1;
                }
            }
        }

        private static void OnCliError(IEnumerable<Error> errors)
        {
            bool helpRequested = errors.Any(error =>
                error is HelpRequestedError || error is VersionRequestedError);

            Environment.ExitCode = helpRequested ? 0 : 2;
        }

        private static string[] NormalizeCliArguments(string[] args)
        {
            var normalized = new string[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-install", StringComparison.OrdinalIgnoreCase))
                    normalized[i] = "--install";
                else if (string.Equals(args[i], "-uninstall", StringComparison.OrdinalIgnoreCase))
                    normalized[i] = "--uninstall";
                else
                    normalized[i] = args[i];
            }

            return normalized;
        }

        private static bool ApplyOptionalConfig(ArgumentOptions opts, DiscordManager.Branches branch)
        {
            if (opts.Portable && opts.Uninstall)
                return false;

            bool changed = opts.Host != null || opts.Port.HasValue || opts.User != null || opts.Password != null;
            if (!opts.Portable)
            {
                if (!changed)
                    return false;

                var config = new Config();
                ApplyProxyOptions(opts, config);
                config.Apply();
                CliLogger.WriteOk("Proxy configuration updated.");
                return true;
            }

            if (!changed && !opts.Install)
                return false;

            bool createDirectory = opts.Install || !string.IsNullOrWhiteSpace(opts.InstallPath);
            string directory = DrCore.ResolveInstallDirectory(branch, opts.InstallPath, createIfMissing: createDirectory);
            var portableConfig = new Config(DrCore.GetConfigIniPath(directory));
            ApplyProxyOptions(opts, portableConfig);
            portableConfig.Apply();
            CliLogger.WriteOk($"Portable configuration written to {portableConfig.IniPath}.");
            return true;
        }

        private static void ApplyProxyOptions(ArgumentOptions opts, Config config)
        {
            if (opts.Host != null)
                config.Host = opts.Host;

            if (opts.Port.HasValue)
                config.Port = opts.Port.Value;

            if (opts.User != null)
                config.User = opts.User;

            if (opts.Password != null)
                config.Password = opts.Password;
        }
    }
}
