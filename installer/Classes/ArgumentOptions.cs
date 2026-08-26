using CommandLine;

namespace Droute.Installer.Classes
{
    internal class ArgumentOptions
    {
        [Option('i', "install", Required = false, HelpText = "Install Droute patch.")]
        public bool Install { get; set; }

        [Option('u', "uninstall", Required = false, HelpText = "Remove Droute patch.")]
        public bool Uninstall { get; set; }

        [Option("branch", Required = false, Default = "stable", HelpText = "Discord branch: `stable`, `canary`, `ptb`. Default: stable.")]
        public string Branch { get; set; } = "stable";

        [Option("host", Required = false, HelpText = "Proxy address.")]
        public string Host { get; set; }

        [Option("port", Required = false, HelpText = "Proxy port.")]
        public int? Port { get; set; }

        [Option("user", Required = false, HelpText = "Proxy user.")]
        public string User { get; set; }

        [Option("password", Required = false, HelpText = "Proxy password.")]
        public string Password { get; set; }

        [Option("path", Required = false, HelpText = "Custom directory to place version.dll and droute.dll.")]
        public string InstallPath { get; set; }

        [Option("portable", Required = false, HelpText = "Skip Update.exe patching and store settings in droute.ini next to droute.dll instead of the registry.")]
        public bool Portable { get; set; }
    }
}
