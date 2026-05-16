namespace Droute.Installer.Forms
{
    partial class FrmPatch
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmPatch));
            this.journalListBox = new System.Windows.Forms.ListBox();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();
            // 
            // journalListBox
            // 
            this.journalListBox.FormattingEnabled = true;
            this.journalListBox.Location = new System.Drawing.Point(12, 10);
            this.journalListBox.Name = "journalListBox";
            this.journalListBox.ScrollAlwaysVisible = true;
            this.journalListBox.Size = new System.Drawing.Size(334, 134);
            this.journalListBox.TabIndex = 1;
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(12, 149);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(334, 24);
            this.progressBar.TabIndex = 2;
            // 
            // FrmPatch
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(358, 181);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.journalListBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "FrmPatch";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Droute: Applying Patch";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmPatch_FormClosing);
            this.Shown += new System.EventHandler(this.FrmPatch_Shown);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ListBox journalListBox;
        private System.Windows.Forms.ProgressBar progressBar;
    }
}