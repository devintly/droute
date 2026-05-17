using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Droute.Core;
using Droute.Installer.Classes;

namespace Droute.Installer.Forms
{
    public partial class FrmPatch : Form
    {
        public enum PatchAction { Install, Remove }
        public bool IsSuccessful { get; private set; }
        public DiscordManager.Branches SelectedBranch { get; set; } = DiscordManager.Branches.Stable;

        public event Action OnSuccess;

        private bool _isWorking = false;
        private readonly PatchAction _action;

        public FrmPatch(PatchAction action, DiscordManager.Branches branch)
        {
            InitializeComponent();

            _action = action;
            this.SelectedBranch = branch;

            this.Text = _action == PatchAction.Install ? "Droute: Installing Patch..." : "Droute: Removing Patch...";
            this.ControlBox = true;
        }

        private async void FrmPatch_Shown(object sender, EventArgs e)
        {
            await ExecutePatchOperation();
        }

        private async Task ExecutePatchOperation()
        {
            _isWorking = true;
            ClearJournal();
            UpdateProgress(0);

            PatchTools.OnLog += WriteJournal;
            PatchTools.OnProgressChanged += UpdateProgress;

            try
            {
                IsSuccessful = await Task.Run(() =>
                {
                    return _action == PatchAction.Install ? PatchTools.Install(SelectedBranch) : PatchTools.Remove(SelectedBranch);
                });
            }
            catch (Exception ex)
            {
                WriteJournal($"Error: {ex.Message}");
                IsSuccessful = false;
            }

            PatchTools.OnLog -= WriteJournal;
            PatchTools.OnProgressChanged -= UpdateProgress;
            _isWorking = false;

            if (IsSuccessful)
            {
                this.Text = "Droute: Operation completed!";
                WriteJournal("Done! You can now close this window.");
                OnSuccess?.Invoke();
            }
            else
            {
                this.Text = "Droute: Operation failed!";
                WriteJournal("Error! Please check the log above before closing.");
            }
        }

        private void FrmPatch_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isWorking)
            {
                var result = MessageBox.Show(
                    "The patch has not been finalized yet. Do you want to interrupt the process? You will need to install/uninstall the patch completely to work correctly.",
                    "Abort the patch?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                    e.Cancel = true;
                else
                    _isWorking = false;
            }
        }
        public void WriteJournal(string content)
        {
            if (journalListBox.InvokeRequired)
                journalListBox.Invoke(new Action(() => journalListBox.Items.Add(content)));
            else
                journalListBox.Items.Add(content);
        }

        public void UpdateProgress(int value)
        {
            if (progressBar.InvokeRequired)
                progressBar.Invoke(new Action(() => progressBar.Value = value));
            else
                progressBar.Value = value;
        }

        private void ClearJournal() => journalListBox.Items.Clear();
    }
}