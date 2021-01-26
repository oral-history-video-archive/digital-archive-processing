namespace InformediaCORE.DatabaseEditor
{
    partial class CollectionEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CollectionEditor));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.collectionID = new System.Windows.Forms.TextBox();
            this.accessionList = new System.Windows.Forms.ComboBox();
            this.lastName = new System.Windows.Forms.TextBox();
            this.preferredName = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.loadImage = new System.Windows.Forms.Button();
            this.commit = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            this.gender = new System.Windows.Forms.ComboBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.saveImage = new System.Windows.Forms.Button();
            this.shortDescription = new System.Windows.Forms.TextBox();
            this.birthDate = new System.Windows.Forms.MaskedTextBox();
            this.deceasedDate = new System.Windows.Forms.MaskedTextBox();
            this.portraitPanel = new System.Windows.Forms.Panel();
            this.portrait = new System.Windows.Forms.PictureBox();
            this.statusStrip.SuspendLayout();
            this.portraitPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.portrait)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(677, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "CollectionID:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(45, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 16);
            this.label2.TabIndex = 1;
            this.label2.Text = "Accession:";
            // 
            // collectionID
            // 
            this.collectionID.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.collectionID.Location = new System.Drawing.Point(766, 6);
            this.collectionID.Name = "collectionID";
            this.collectionID.ReadOnly = true;
            this.collectionID.Size = new System.Drawing.Size(76, 22);
            this.collectionID.TabIndex = 2;
            this.collectionID.TabStop = false;
            this.collectionID.Text = "00000000";
            this.collectionID.WordWrap = false;
            // 
            // accessionList
            // 
            this.accessionList.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.accessionList.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.accessionList.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.accessionList.FormattingEnabled = true;
            this.accessionList.Location = new System.Drawing.Point(125, 6);
            this.accessionList.Name = "accessionList";
            this.accessionList.Size = new System.Drawing.Size(525, 24);
            this.accessionList.TabIndex = 1;
            this.accessionList.Text = "A1993.001";
            this.accessionList.SelectedIndexChanged += new System.EventHandler(this.accessionList_SelectedIndexChanged);
            // 
            // lastName
            // 
            this.lastName.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lastName.Location = new System.Drawing.Point(125, 48);
            this.lastName.MaxLength = 64;
            this.lastName.Name = "lastName";
            this.lastName.Size = new System.Drawing.Size(525, 22);
            this.lastName.TabIndex = 2;
            this.lastName.Tag = "";
            this.lastName.Text = "Last Name";
            this.lastName.WordWrap = false;
            this.lastName.Validating += new System.ComponentModel.CancelEventHandler(this.textBox_Validating);
            this.lastName.Validated += new System.EventHandler(this.lastName_Validated);
            // 
            // preferredName
            // 
            this.preferredName.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.preferredName.Location = new System.Drawing.Point(125, 90);
            this.preferredName.MaxLength = 128;
            this.preferredName.Name = "preferredName";
            this.preferredName.Size = new System.Drawing.Size(525, 22);
            this.preferredName.TabIndex = 3;
            this.preferredName.Tag = "";
            this.preferredName.Text = "Preferred Name";
            this.preferredName.WordWrap = false;
            this.preferredName.Validating += new System.ComponentModel.CancelEventHandler(this.textBox_Validating);
            this.preferredName.Validated += new System.EventHandler(this.preferredName_Validated);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(46, 51);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(73, 16);
            this.label3.TabIndex = 6;
            this.label3.Text = "LastName:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(12, 93);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(104, 16);
            this.label4.TabIndex = 7;
            this.label4.Text = "PreferredName:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(694, 51);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(66, 16);
            this.label5.TabIndex = 8;
            this.label5.Text = "BirthDate:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(656, 93);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(104, 16);
            this.label6.TabIndex = 9;
            this.label6.Text = "DeceasedDate:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(6, 131);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(110, 16);
            this.label7.TabIndex = 12;
            this.label7.Text = "DescriptionShort:";
            // 
            // loadImage
            // 
            this.loadImage.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.loadImage.Location = new System.Drawing.Point(345, 346);
            this.loadImage.Name = "loadImage";
            this.loadImage.Size = new System.Drawing.Size(102, 35);
            this.loadImage.TabIndex = 7;
            this.loadImage.Text = "Load Image";
            this.loadImage.UseVisualStyleBackColor = true;
            this.loadImage.Click += new System.EventHandler(this.loadImage_Click);
            // 
            // commit
            // 
            this.commit.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.commit.Location = new System.Drawing.Point(707, 524);
            this.commit.Name = "commit";
            this.commit.Size = new System.Drawing.Size(135, 35);
            this.commit.TabIndex = 9;
            this.commit.Text = "Commit Changes";
            this.commit.UseVisualStyleBackColor = true;
            this.commit.Click += new System.EventHandler(this.commit_Click);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(714, 341);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(56, 16);
            this.label8.TabIndex = 15;
            this.label8.Text = "Gender:";
            // 
            // gender
            // 
            this.gender.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gender.FormattingEnabled = true;
            this.gender.Items.AddRange(new object[] {
            "M",
            "F"});
            this.gender.Location = new System.Drawing.Point(776, 338);
            this.gender.MaxLength = 1;
            this.gender.Name = "gender";
            this.gender.Size = new System.Drawing.Size(66, 24);
            this.gender.TabIndex = 8;
            this.gender.Validating += new System.ComponentModel.CancelEventHandler(this.gender_Validating);
            this.gender.Validated += new System.EventHandler(this.gender_Validated);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 569);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(854, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 16;
            this.statusStrip.Text = "why isn\'t this showing up?";
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(118, 17);
            this.statusLabel.Text = "toolStripStatusLabel1";
            // 
            // saveImage
            // 
            this.saveImage.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.saveImage.Location = new System.Drawing.Point(344, 387);
            this.saveImage.Name = "saveImage";
            this.saveImage.Size = new System.Drawing.Size(102, 35);
            this.saveImage.TabIndex = 17;
            this.saveImage.Text = "Save Image";
            this.saveImage.UseVisualStyleBackColor = true;
            this.saveImage.Click += new System.EventHandler(this.saveImage_Click);
            // 
            // shortDescription
            // 
            this.shortDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.shortDescription.Location = new System.Drawing.Point(125, 131);
            this.shortDescription.MaxLength = 1024;
            this.shortDescription.Multiline = true;
            this.shortDescription.Name = "shortDescription";
            this.shortDescription.Size = new System.Drawing.Size(717, 191);
            this.shortDescription.TabIndex = 6;
            this.shortDescription.Tag = "";
            this.shortDescription.Text = resources.GetString("shortDescription.Text");
            this.shortDescription.Validating += new System.ComponentModel.CancelEventHandler(this.textBox_Validating);
            this.shortDescription.Validated += new System.EventHandler(this.shortDescription_Validated);
            // 
            // birthDate
            // 
            this.birthDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.birthDate.Location = new System.Drawing.Point(766, 48);
            this.birthDate.Mask = "00/00/0000";
            this.birthDate.Name = "birthDate";
            this.birthDate.Size = new System.Drawing.Size(76, 22);
            this.birthDate.TabIndex = 18;
            this.birthDate.Text = "01011900";
            this.birthDate.TextMaskFormat = System.Windows.Forms.MaskFormat.IncludePromptAndLiterals;
            this.birthDate.ValidatingType = typeof(System.DateTime);
            this.birthDate.Validating += new System.ComponentModel.CancelEventHandler(this.birthDate_Validating);
            this.birthDate.Validated += new System.EventHandler(this.birthDate_Validated);
            // 
            // deceasedDate
            // 
            this.deceasedDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.deceasedDate.Location = new System.Drawing.Point(766, 90);
            this.deceasedDate.Mask = "00/00/0000";
            this.deceasedDate.Name = "deceasedDate";
            this.deceasedDate.Size = new System.Drawing.Size(76, 23);
            this.deceasedDate.TabIndex = 19;
            this.deceasedDate.Text = "01011999";
            this.deceasedDate.TextMaskFormat = System.Windows.Forms.MaskFormat.IncludePromptAndLiterals;
            this.deceasedDate.ValidatingType = typeof(System.DateTime);
            this.deceasedDate.Validating += new System.ComponentModel.CancelEventHandler(this.deceasedDate_Validating);
            this.deceasedDate.Validated += new System.EventHandler(this.deceasedDate_Validated);
            // 
            // portraitPanel
            // 
            this.portraitPanel.Controls.Add(this.portrait);
            this.portraitPanel.Location = new System.Drawing.Point(125, 343);
            this.portraitPanel.Name = "portraitPanel";
            this.portraitPanel.Size = new System.Drawing.Size(216, 216);
            this.portraitPanel.TabIndex = 20;
            // 
            // portrait
            // 
            this.portrait.BackColor = System.Drawing.SystemColors.ControlDark;
            this.portrait.Location = new System.Drawing.Point(3, 3);
            this.portrait.Name = "portrait";
            this.portrait.Size = new System.Drawing.Size(210, 210);
            this.portrait.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.portrait.TabIndex = 15;
            this.portrait.TabStop = false;
            // 
            // CollectionEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(854, 591);
            this.Controls.Add(this.portraitPanel);
            this.Controls.Add(this.deceasedDate);
            this.Controls.Add(this.birthDate);
            this.Controls.Add(this.saveImage);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.gender);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.commit);
            this.Controls.Add(this.loadImage);
            this.Controls.Add(this.shortDescription);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.preferredName);
            this.Controls.Add(this.lastName);
            this.Controls.Add(this.accessionList);
            this.Controls.Add(this.collectionID);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "CollectionEditor";
            this.Text = "Collection Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CollectionEditor_FormClosing);
            this.Load += new System.EventHandler(this.CollectionEditor_Load);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.portraitPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.portrait)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox collectionID;
        private System.Windows.Forms.ComboBox accessionList;
        private System.Windows.Forms.TextBox lastName;
        private System.Windows.Forms.TextBox preferredName;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button loadImage;
        private System.Windows.Forms.Button commit;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox gender;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.Button saveImage;
        private System.Windows.Forms.TextBox shortDescription;
        private System.Windows.Forms.MaskedTextBox birthDate;
        private System.Windows.Forms.MaskedTextBox deceasedDate;
        private System.Windows.Forms.Panel portraitPanel;
        private System.Windows.Forms.PictureBox portrait;
    }
}

