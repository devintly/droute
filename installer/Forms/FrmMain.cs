using Droute.Core;
using Droute.Installer.Classes;
using Droute.Installer.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Droute.Installer.Forms
{
    public partial class FrmMain : Form
    {
        private Config _cfg = null;

        public FrmMain()
        {
            InitializeComponent();

            _cfg = new Config();
        }

        private void authCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            authPanel.Enabled = authCheckBox.Checked;

            if (!authCheckBox.Checked)
            {
                userTextBox.Text = "";
                passwordTextBox.Text = "";
            }
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            hostTextBox.Text = _cfg.Host;
            portNumeric.Value = _cfg.Port;
            userTextBox.Text = _cfg.User;
            passwordTextBox.Text = _cfg.Password;

            if (string.IsNullOrEmpty(_cfg.User) && string.IsNullOrEmpty(_cfg.Password))
                authCheckBox.Checked = false;

            autoRestartPatchCheckbox.Checked = Settings.Default.AutoRestartPatch;
            autoRestartConfigCheckbox.Checked = Settings.Default.AutoRestartConfig;

            var versionInfo = new Version(Application.ProductVersion);
            versionLabel.Text = $"v. {versionInfo.Major}.{versionInfo.Minor}.{versionInfo.Build}";
        }

        private void applyCfgButton_Click(object sender, EventArgs e)
        {
            ApplyConfig();
            if (Settings.Default.AutoRestartConfig)
            {
                DiscordManager.Close(DiscordManager.Branches.Stable); 
                DiscordManager.Launch(DiscordManager.Branches.Stable);
            }
        }

        private void discordActionsCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.AutoRestartPatch = autoRestartPatchCheckbox.Checked;
            Settings.Default.AutoRestartConfig = autoRestartConfigCheckbox.Checked;
            Settings.Default.Save();
        }

        private void repoLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/snowluwu/droute");
        }

        private void licenseLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/snowluwu/droute/blob/master/LICENSE.txt");
        }

        private void inspiredLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/runetfreedom/force-proxy");
        }

        private void openLogsButton_Click(object sender, EventArgs e)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string path = Path.Combine(localAppData, "Temp", "droute.log");

            try 
            { 
                Process.Start(path); 
            }
            catch (Exception ex) 
            { 
                Trace.WriteLine($"error during open log file: {ex.ToString()}"); 
            }
        }

        private void installPatchButton_Click(object sender, EventArgs e)
        {
            ApplyConfig();
            HandlePatchAction(FrmPatch.PatchAction.Install);
        }
        private void removePatchButton_Click(object sender, EventArgs e) 
            => HandlePatchAction(FrmPatch.PatchAction.Remove);

        private void ApplyConfig()
        {
            _cfg.Host = hostTextBox.Text;
            _cfg.Port = (int)portNumeric.Value;
            _cfg.User = userTextBox.Text;
            _cfg.Password = passwordTextBox.Text;
            _cfg.Apply();
        }

        private void HandlePatchAction(FrmPatch.PatchAction action)
        {
            if (Settings.Default.AutoRestartPatch)
                DiscordManager.Close(DiscordManager.Branches.Stable);

            using (var frm = new FrmPatch(action))
            {
                frm.OnSuccess += () =>
                {
                    if (Settings.Default.AutoRestartPatch && action == FrmPatch.PatchAction.Install)
                    {
                        this.BeginInvoke(new Action(() => DiscordManager.Launch(DiscordManager.Branches.Stable)));
                    }
                };

                frm.ShowDialog();
            }
        }
    }
}
