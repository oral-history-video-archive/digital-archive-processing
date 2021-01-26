namespace DocToXMLCleanup
{
    partial class DocumentToXMLCleanupForm
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
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.txtErrors = new System.Windows.Forms.TextBox();
            this.txtStatus = new System.Windows.Forms.TextBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblIssuesToFix = new System.Windows.Forms.Label();
            this.lblIssues2 = new System.Windows.Forms.Label();
            this.txtErrorsPart2 = new System.Windows.Forms.TextBox();
            this.lblFramesInTimecode = new System.Windows.Forms.Label();
            this.checkBoxHideWarnings = new System.Windows.Forms.CheckBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // folderBrowserDialog1
            // 
            this.folderBrowserDialog1.Description = "Select the folder holding the .docx and .doc files to convert";
            this.folderBrowserDialog1.ShowNewFolderButton = false;
            // 
            // txtErrors
            // 
            this.txtErrors.ForeColor = System.Drawing.SystemColors.WindowText;
            this.txtErrors.Location = new System.Drawing.Point(0, 201);
            this.txtErrors.Multiline = true;
            this.txtErrors.Name = "txtErrors";
            this.txtErrors.ReadOnly = true;
            this.txtErrors.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtErrors.Size = new System.Drawing.Size(584, 95);
            this.txtErrors.TabIndex = 0;
            // 
            // txtStatus
            // 
            this.txtStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtStatus.Location = new System.Drawing.Point(0, 55);
            this.txtStatus.Multiline = true;
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.ReadOnly = true;
            this.txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtStatus.Size = new System.Drawing.Size(614, 121);
            this.txtStatus.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStatus.Location = new System.Drawing.Point(8, 32);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(62, 20);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "Status";
            // 
            // lblIssuesToFix
            // 
            this.lblIssuesToFix.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblIssuesToFix.AutoSize = true;
            this.lblIssuesToFix.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblIssuesToFix.Location = new System.Drawing.Point(8, 179);
            this.lblIssuesToFix.Name = "lblIssuesToFix";
            this.lblIssuesToFix.Size = new System.Drawing.Size(467, 20);
            this.lblIssuesToFix.TabIndex = 3;
            this.lblIssuesToFix.Text = "Issues to Review and Fix as Needed, Part 1 (.docx to .txt)";
            // 
            // lblIssues2
            // 
            this.lblIssues2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblIssues2.AutoSize = true;
            this.lblIssues2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblIssues2.Location = new System.Drawing.Point(8, 302);
            this.lblIssues2.Name = "lblIssues2";
            this.lblIssues2.Size = new System.Drawing.Size(456, 20);
            this.lblIssues2.TabIndex = 4;
            this.lblIssues2.Text = "Issues to Review and Fix as Needed, Part 2 (.txt to .xml)";
            // 
            // txtErrorsPart2
            // 
            this.txtErrorsPart2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtErrorsPart2.ForeColor = System.Drawing.SystemColors.WindowText;
            this.txtErrorsPart2.Location = new System.Drawing.Point(0, 327);
            this.txtErrorsPart2.Multiline = true;
            this.txtErrorsPart2.Name = "txtErrorsPart2";
            this.txtErrorsPart2.ReadOnly = true;
            this.txtErrorsPart2.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtErrorsPart2.Size = new System.Drawing.Size(614, 116);
            this.txtErrorsPart2.TabIndex = 5;
            this.txtErrorsPart2.Text = "...This part not started yet - must make .txt files from .docx files first.";
            // 
            // lblFramesInTimecode
            // 
            this.lblFramesInTimecode.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFramesInTimecode.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblFramesInTimecode.Location = new System.Drawing.Point(9, 3);
            this.lblFramesInTimecode.Name = "lblFramesInTimecode";
            this.lblFramesInTimecode.Size = new System.Drawing.Size(593, 23);
            this.lblFramesInTimecode.TabIndex = 6;
            this.lblFramesInTimecode.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // checkBoxHideWarnings
            // 
            this.checkBoxHideWarnings.AutoSize = true;
            this.checkBoxHideWarnings.Location = new System.Drawing.Point(123, 34);
            this.checkBoxHideWarnings.Name = "checkBoxHideWarnings";
            this.checkBoxHideWarnings.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.checkBoxHideWarnings.Size = new System.Drawing.Size(203, 17);
            this.checkBoxHideWarnings.TabIndex = 7;
            this.checkBoxHideWarnings.Text = "Hide docx warnings from status report";
            this.checkBoxHideWarnings.UseVisualStyleBackColor = true;
            // 
            // btnStart
            // 
            this.btnStart.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStart.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnStart.ForeColor = System.Drawing.Color.Black;
            this.btnStart.Location = new System.Drawing.Point(413, 26);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(110, 26);
            this.btnStart.TabIndex = 8;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // DocumentToXMLCleanupForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(614, 442);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.checkBoxHideWarnings);
            this.Controls.Add(this.txtErrorsPart2);
            this.Controls.Add(this.lblIssues2);
            this.Controls.Add(this.lblIssuesToFix);
            this.Controls.Add(this.txtStatus);
            this.Controls.Add(this.txtErrors);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblFramesInTimecode);
            this.Name = "DocumentToXMLCleanupForm";
            this.Text = "Clean up .docx/.txt files to .xml files for Digital Archive publishing, Sept. 202" +
    "0+";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DocumentToXMLCleanupForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.DocumentToXMLCleanupForm_FormClosed);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.TextBox txtErrors;
        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblIssuesToFix;
        private System.Windows.Forms.Label lblIssues2;
        private System.Windows.Forms.TextBox txtErrorsPart2;
        private System.Windows.Forms.Label lblFramesInTimecode;
        private System.Windows.Forms.CheckBox checkBoxHideWarnings;
        private System.Windows.Forms.Button btnStart;
    }
}

