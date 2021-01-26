using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Common.Media;

namespace InformediaCORE.DatabaseEditor
{
    public partial class CollectionEditor : Form
    {
        #region ====================               Declarations              ====================
        /// <summary>
        /// Persistent application title.
        /// </summary>
        private const string APPLICATION_TITLE = "Database Editor";

        /// <summary>
        /// Separates the fields within the application title.
        /// </summary>
        private const string TITLE_SEPARATOR = " - ";

        /// <summary>
        /// The literal value of an empty date input box.
        /// </summary>
        private const string EMPTY_DATE_STRING = "__/__/____";

        /// <summary>
        /// Default background color for controls.
        /// </summary>
        private static readonly Color DEFAULT_BACKGROUND_COLOR = SystemColors.Window;
        
        /// <summary>
        /// Background color to use when the data of a control has changed.
        /// </summary>
        private static readonly Color DATA_CHANGED_COLOR = Color.LemonChiffon;

        /// <summary>
        /// The minimum allowable date value which can be entered.
        /// </summary>
        private static readonly DateTime MIN_DATE = new DateTime(1800, 1, 1);

        /// <summary>
        /// The maximum allowable date value which can be entered.
        /// </summary>
        private static readonly DateTime MAX_DATE = DateTime.Now;

        /// <summary>
        /// The persistent DataAccessExtended instance used to access the database.
        /// </summary>
        private DataAccessCollectionEditor dataAccess;

        /// <summary>
        /// The database Collection instance currently active in the editor.
        /// </summary>
        private Collection collection;

        /// <summary>
        /// Flag indicating if portrait has been changed.
        /// </summary>
        private bool portraitChanged;

        /// <summary>
        /// Backing storage for Dirty property.
        /// </summary>
        private bool _dirty;

        /// <summary>
        /// Flag indicating the editor content has been altered and requires updating.
        /// </summary>
        private bool Dirty 
        { 
            get
            {
                return this._dirty;
            }
            set
            {
                this._dirty = value;
                commit.Enabled = value;
                statusLabel.Text = (this.Dirty) ? "Changes have been made, commit required." : string.Empty;
                this.UpdateTitle();
            }
        }
        #endregion =================               Declarations              ====================

        #region ====================               Form Methods              ====================
        /// <summary>
        /// Constructor
        /// </summary>
        public CollectionEditor()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles the Load event on the parent form.
        /// </summary>
        private void CollectionEditor_Load(object sender, EventArgs e)
        {
            this.ClearForm();
            this.dataAccess = new DataAccessCollectionEditor();
            this.LoadAccessionNumbers();
            this.UpdateTitle();
        }

        /// <summary>
        /// Handles the Closing event on the parent form.
        /// </summary>
        private void CollectionEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If user does NOT confirm action, then cancel.
            e.Cancel = !ConfirmAction();
        }
        #endregion =================               Form Methods              ====================

        #region ====================             Private Methods             ====================
        /// <summary>
        /// Clears all fields on the form.
        /// </summary>
        private void ClearForm()
        {
            loadImage.Enabled = false;
            saveImage.Enabled = false;
            collectionID.Enabled = false;
            lastName.Enabled = false;
            preferredName.Enabled = false;
            birthDate.Enabled = false;
            deceasedDate.Enabled = false;
            shortDescription.Enabled = false;
            gender.Enabled = false;

            accessionList.Text = string.Empty;
            collectionID.Text = string.Empty;
            lastName.Text = string.Empty;
            preferredName.Text = string.Empty;
            birthDate.Text = string.Empty;
            deceasedDate.Text = string.Empty;
            shortDescription.Text = string.Empty;
            gender.Text = string.Empty;
            portrait.Image = null;

            lastName.BackColor = DEFAULT_BACKGROUND_COLOR;
            preferredName.BackColor = DEFAULT_BACKGROUND_COLOR;
            birthDate.BackColor = DEFAULT_BACKGROUND_COLOR;
            deceasedDate.BackColor = DEFAULT_BACKGROUND_COLOR;
            shortDescription.BackColor = DEFAULT_BACKGROUND_COLOR;
            gender.BackColor = DEFAULT_BACKGROUND_COLOR;
            portraitPanel.BackColor = SystemColors.Control;

            this.portraitChanged = false;
            this.Dirty = false;

            statusLabel.Text = "Ready";
        }

        /// <summary>
        /// Loads form fields with data from the active collection.
        /// </summary>
        private void LoadForm()
        {
            // Always clear form to prevent any confusion.
            this.ClearForm();

            // If collecion is null bail out, nothing to do here.
            if (this.collection == null) return;

            // Load collection data into appropriate fields            
            collectionID.Text = this.collection.CollectionID.ToString();
            lastName.Text = this.collection.LastName;
            preferredName.Text = this.collection.PreferredName;
            birthDate.Text = GetFormattedDate(this.collection.BirthDate);
            deceasedDate.Text = GetFormattedDate(this.collection.DeceasedDate);
            shortDescription.Text = this.collection.DescriptionShort;
            gender.Text = this.collection.Gender.ToString();
            portrait.Image = MediaTools.LinqBinaryToImage(this.collection.Portrait);                

            // Re-enable editor
            loadImage.Enabled = true;
            saveImage.Enabled = true;
            collectionID.Enabled = true;
            lastName.Enabled = true;
            preferredName.Enabled = true;
            birthDate.Enabled = true;
            deceasedDate.Enabled = true;
            shortDescription.Enabled = true;
            gender.Enabled = true;

            statusLabel.Text = "Collection " + collection.Accession + " loaded.";
            this.UpdateTitle();
        }

        /// <summary>
        /// Formats a nullable DateTime.
        /// </summary>
        /// <param name="date">The nullable DateTime instance to format.</param>
        /// <returns>A string in the form of MM/DD/YYYY for valid dates; the empty string otherwise.</returns>
        private string GetFormattedDate(DateTime? date)
        {
            return (date == null) ? string.Empty : ((DateTime)date).ToString("MM/dd/yyyy");
        }

        /// <summary>
        /// Parses the given string as date.
        /// </summary>
        /// <param name="valueToParse">The string to parse.</param>
        /// <param name="defaultValue">The value to return in the event of a parsing failure.</param>
        /// <returns>A DateTime repsenting the given string on success; defaultValue otherwise.</returns>
        private DateTime? ParseDateString(string valueToParse, DateTime? defaultValue)
        {
            DateTime date;
            if (DateTime.TryParse(valueToParse, out date))
            {
                return date;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Initializes the form with the list of available collections accession numbers from the database.
        /// </summary>
        private void LoadAccessionNumbers()
        {
            string[] accessionNumbers = this.dataAccess.GetCollectionAccessionNumbers();

            foreach (string accession in accessionNumbers)
            {
                accessionList.Items.Add(accession);
            }
        }

        /// <summary>
        /// Requests user confirmation to requested action.
        /// </summary>
        /// <returns></returns>
        private bool ConfirmAction()
        {
            if (this.Dirty)
            {
                DialogResult result = MessageBox.Show(
                    "You have unsaved changes, if you continue your changes will be lost. Do want to continue?",
                    "Confirmation Required",
                    MessageBoxButtons.OKCancel);

                return (result == DialogResult.OK);
            }
            else
            {
                // Confirmation not required
                return true;
            }

        }

        /// <summary>
        /// Detects if any changes have been made to the data.
        /// </summary>
        private void DetectChanges()
        {
            if (collection == null)
            {
                this.Dirty = false;
                return;
            }

            this.Dirty =
            (
                this.collection.BirthDate != ParseDateString(birthDate.Text, null) ||
                this.collection.DeceasedDate != ParseDateString(deceasedDate.Text, null) ||
                this.collection.Gender != gender.Text[0] ||
                this.collection.LastName != lastName.Text ||
                this.collection.PreferredName != preferredName.Text ||
                this.collection.DescriptionShort != shortDescription.Text ||
                this.portraitChanged
            );
        }

        /// <summary>
        /// Returns the file extension without the "."
        /// </summary>
        /// <remarks>
        /// This function duplicates a function in InformediaCORE.Processing.XmlImporter
        /// but due to access restrictions was not available here.
        /// </remarks>
        /// <param name="filename">The fully or partially qualified file name to </param>
        /// <returns></returns>
        private string GetFileType(string filename)
        {
            string extension = Path.GetExtension(filename).ToLower();

            if (extension.Length > 1)
                extension = extension.Substring(1);

            return extension;
        }


        /// <summary>
        /// Updates the title to reflect the current application state.
        /// </summary>
        private void UpdateTitle()
        {
            this.Text = String.Concat(
                (this.Dirty) ? "*" : string.Empty,
                (this.collection == null) ? string.Empty : this.collection.Accession + TITLE_SEPARATOR,
                APPLICATION_TITLE
            );
        }
        #endregion =================             Private Methods             ====================

        #region ====================        Component Event Handlers         ====================
        /// <summary>
        /// Handles the Validating event on the the birthDate textbox.
        /// </summary>
        private void birthDate_Validating(object sender, CancelEventArgs e)
        {
            var birthdate = ParseDateString(birthDate.Text, DateTime.MinValue);
            var deathdate = ParseDateString(deceasedDate.Text, DateTime.MaxValue);

            bool valid = (birthDate.Text == EMPTY_DATE_STRING) ||
                         (MIN_DATE <= birthdate && birthdate < deathdate);

            if (!valid)
            {
                e.Cancel = true;
                birthDate.BackColor = Color.Red;
                statusLabel.Text = String.Format("Birth date must be empty or between {0:MM/dd/yyyy} and {1:MM/dd/yyyy}.", MIN_DATE, deathdate);
            }
        }

        /// <summary>
        /// Handles the Validated event on the birthDate textbox.
        /// </summary>
        private void birthDate_Validated(object sender, EventArgs e)
        {
            birthDate.BackColor =
                (this.collection.BirthDate == ParseDateString(birthDate.Text, null)) ? DEFAULT_BACKGROUND_COLOR : DATA_CHANGED_COLOR;
            DetectChanges();
        }

        /// <summary>
        /// Handles the Validating event for the deceasedDate textbox.
        /// </summary>
        private void deceasedDate_Validating(object sender, CancelEventArgs e)
        {
            var birthdate = ParseDateString(birthDate.Text, DateTime.MinValue);
            var deathdate = ParseDateString(deceasedDate.Text, DateTime.MaxValue);

            bool valid = (deceasedDate.Text == EMPTY_DATE_STRING) ||
                         (birthdate <= deathdate && deathdate <= MAX_DATE);

            if (!valid)
            {
                e.Cancel = true;
                deceasedDate.BackColor = Color.Red;
                statusLabel.Text = String.Format("Deceased date must be empty or between {0:MM/dd/yyyy} and {1:MM/dd/yyyy}.", birthdate, MAX_DATE);
            }
        }

        /// <summary>
        /// Handles the Validated event on the deceasedDate textbox.
        /// </summary>
        private void deceasedDate_Validated(object sender, EventArgs e)
        {

            deceasedDate.BackColor =
                (this.collection.DeceasedDate == ParseDateString(deceasedDate.Text, null)) ? DEFAULT_BACKGROUND_COLOR : DATA_CHANGED_COLOR;
            DetectChanges();
        }

        /// <summary>
        /// Handles the Validating event for the gender combobox.
        /// </summary>
        private void gender_Validating(object sender, CancelEventArgs e)
        {
            gender.Text = gender.Text.ToUpper();
            if (gender.Text != "M" && gender.Text != "F")
            {
                e.Cancel = true;
                gender.BackColor = Color.Red;
                statusLabel.Text = "Gender value must be one of: M or F";
            }
        }

        /// <summary>
        /// Handles the Validated event for the gender combobox.
        /// </summary>
        private void gender_Validated(object sender, EventArgs e)
        {
            gender.BackColor = (this.collection.Gender == gender.Text[0]) ? DEFAULT_BACKGROUND_COLOR : DATA_CHANGED_COLOR;            
            DetectChanges();
        }

        /// <summary>
        /// Handles the Validating event for all textboxes.
        /// </summary>
        private void textBox_Validating(object sender, CancelEventArgs e)
        {
            // Mike did tests and determined that unicode characters are perfectly acceptible
            // in these fields so no validation is required here.
        }

        /// <summary>
        /// Handles the Validated event for the lastName textbox.
        /// </summary>
        private void lastName_Validated(object sender, EventArgs e)
        {
            lastName.BackColor = 
                (this.collection.LastName == lastName.Text) ? DEFAULT_BACKGROUND_COLOR : DATA_CHANGED_COLOR;
            DetectChanges();
        }

        /// <summary>
        /// Handles the Validated event for the prefferedName textbox.
        /// </summary>
        private void preferredName_Validated(object sender, EventArgs e)
        {
            preferredName.BackColor = 
                (this.collection.PreferredName == preferredName.Text) ? DEFAULT_BACKGROUND_COLOR : DATA_CHANGED_COLOR;
            DetectChanges();
        }

        /// <summary>
        /// Handles the Validated event for the shortDescription textbox.
        /// </summary>
        private void shortDescription_Validated(object sender, EventArgs e)
        {
            shortDescription.BackColor =
                (this.collection.DescriptionShort == shortDescription.Text) ? DEFAULT_BACKGROUND_COLOR : DATA_CHANGED_COLOR;
            DetectChanges();
        }

        /// <summary>
        /// Handles the SelectedIndexChange event on the accessionList combobox.
        /// </summary>
        private void accessionList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.ConfirmAction())
            {
                var selectedIndex = accessionList.SelectedIndex;
                var accession = accessionList.Items[selectedIndex] as string;

                this.collection = this.dataAccess.GetCollection(accession);

                this.LoadForm();
            }
        }

        /// <summary>
        /// Handles the Click event on the loadImage button.
        /// </summary>
        private void loadImage_Click(object sender, EventArgs e)
        {
            // Present file dialog
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Filter = "Image Files (*.png; *.jpg; *.gif)|*.png;*.jpg;*.gif";
            openFileDialog.Title = "Select Portrait Image";

            // Load selected image...
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string ext = GetFileType(openFileDialog.FileName);

                if (ext == "gif" || ext == "jpg" || ext == "png")
                { 
                    // ...into PictureBox...
                    portrait.Load(openFileDialog.FileName);

                    // The portrait has been touched, it could be the same but it could be different
                    // and attempts to do bitwise comparisons failed due to compression artifacts.
                    this.portraitChanged = true;

                    portraitPanel.BackColor = DATA_CHANGED_COLOR;

                    DetectChanges();
                }
                else
                {
                    MessageBox.Show(
                        "The file you selected is not supported. Please select a .gif, .jpg, or .png file.",
                        "Format not supported",
                        MessageBoxButtons.OK
                        );
                }
            }
        }

        /// <summary>
        /// Handles Click event on saveImage button.
        /// </summary>
        private void saveImage_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowser = new FolderBrowserDialog();

            if (folderBrowser.ShowDialog() == DialogResult.OK)
            {
                if (portrait.Image.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Gif))
                {
                    portrait.Image.Save(
                        Path.Combine(folderBrowser.SelectedPath, this.collection.Accession + ".gif"), 
                        System.Drawing.Imaging.ImageFormat.Gif);
                }
                else if (portrait.Image.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg))
                {
                    portrait.Image.Save(
                        Path.Combine(folderBrowser.SelectedPath, this.collection.Accession + ".jpg"),
                        System.Drawing.Imaging.ImageFormat.Jpeg);
                }
                else
                {
                    // All other formats default to PNG for web compatibility
                    portrait.Image.Save(
                        Path.Combine(folderBrowser.SelectedPath, this.collection.Accession + ".png"),
                        System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// Handles the Click event on the commit button.
        /// </summary>
        private void commit_Click(object sender, EventArgs e)
        {
            var birthdate = ParseDateString(birthDate.Text, null);
            if (this.collection.BirthDate != birthdate)
                    this.collection.BirthDate = birthdate;
                
            var deathdate = ParseDateString(deceasedDate.Text, null);
            if (this.collection.DeceasedDate != deathdate)
                this.collection.DeceasedDate = deathdate;

            if (this.collection.Gender != gender.Text[0])
                this.collection.Gender = gender.Text[0];

            if (this.collection.LastName != lastName.Text)
                this.collection.LastName = lastName.Text;

            if (this.collection.PreferredName != preferredName.Text)
                this.collection.PreferredName = preferredName.Text;

            if (this.collection.DescriptionShort != shortDescription.Text)
                this.collection.DescriptionShort = shortDescription.Text;

            if (this.portraitChanged)
            {
                this.collection.FileType = GetFileType(portrait.ImageLocation);
                this.collection.Portrait = MediaTools.ImageToLinqBinary(portrait.Image);
            }

            this.dataAccess.UpdateCollection(this.collection);
            this.Dirty = false;
            this.UpdateTitle();
        }        
        #endregion =================        Component Event Handlers         ====================
    }
}
