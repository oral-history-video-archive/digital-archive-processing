using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Packaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Globalization;

namespace DocToXMLCleanup
{
    /// <summary>
    /// GUI front-end for program to convert folder of input .docx and/or .txt files into .xml files suitable for 
    /// ingesting into IDVL repository using an MDF data store.
    /// 
    /// NOTE:  updated ConvertFromDocxToTxt to read/write .txt files (the second step) in their given encoding.
    /// 
    /// </summary>
    /// <remarks>
    /// Modifications:
	///	Sept. 2020: Do NOT process *.doc files (Word 97-2003) but only *.docx (or *.docm) files from Microsoft Office 2007 or more recent.
	/// Reason: instead of using a Microsoft.Word.Interop, DocumentFormat.OpenXml is used which works only for Office 2007 and more recent.
	///
    /// Aug. 7, 2013: Do NOT allow the program to be run if there already are .xml files in the destination path.
    /// Reason: follow-up tools can get confused by leftover .xml files mixed with newly generated ones: by
    /// having a fresh output directory of .xml files, then the .xml files will represent only the latest DocToXMLCleanup run.
    /// 
    /// See TxtToXMLGeneration class for detailed comments on input requirements.</remarks>
    public partial class DocumentToXMLCleanupForm : Form
    {
        /// <summary>
        /// Use internal flag to note that work is being done....
        /// </summary>
        private bool stillProcessing = false;

        #region =================================== CONSTRUCTOR ===================================
        public DocumentToXMLCleanupForm()
        {
            InitializeComponent();
        }
        #endregion =================================== CONSTRUCTOR ===================================

        #region ================================= EVENT HANDLERS ==================================

        private void btnStart_Click(object sender, EventArgs e)
        {
            const string MY_CONFIG_SETTINGS_FILENAME = "ConfigSettings.xml";

            // Once Start button is clicked, get from user the directory to process .docx (or .docm) files from...
            DialogResult myResult = this.folderBrowserDialog1.ShowDialog();
            if (myResult != DialogResult.OK)
            {
                OutputStatusLine("");
                OutputStatusLine("Nothing to show: no folder chosen on which to run the conversion.");
                OutputStatusLine("");
            }
            else
            {
                string selectedFullPath = this.folderBrowserDialog1.SelectedPath;
                int xmlFileCount = 0;
                foreach (string docXFile in Directory.GetFiles(selectedFullPath, "*.xml"))
                {
                    // Could do more here; for now, just get a count!
                    xmlFileCount++;
                }
                if (xmlFileCount > 0)
                {
                    OutputStatusLine("");
                    OutputStatusLine("Conversion NOT done: all *.xml files must be deleted from destination folder first!");
                    OutputStatusLine("Destination folder: " + selectedFullPath);
                    OutputStatusLine("");
                }
                else
                {
                    stillProcessing = true;
                    bool continueWithPart2 = true;

                    if (ConvertFromDocxToTxt(selectedFullPath))
                    {
                        const string TREAT_ENDING_NUMERALS_AS_FRAMES = "Time as \"ss:ff\" or \"mm:ss:ff\" or \"hh:mm:ss:ff\" -- ff is frame count with 30 frames/sec., e.g., \"20:15\" is 20.5 seconds";
                        const string ALLOW_FRACTIONAL_SECONDS = "Time as \"ss.ss\" or \"mm:ss.ss\" or \"hh:mm:ss.ss\" -- ss.ss is fractional seconds, e.g., \"20.5\" is 20.5 seconds";
                        System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(ConfigSettings));
                        ConfigSettings mySettings = new ConfigSettings();
                        try
                        {
                            System.IO.StreamReader file = new System.IO.StreamReader(MY_CONFIG_SETTINGS_FILENAME);
                            mySettings = (ConfigSettings)reader.Deserialize(file);
                            OutputStatusLine("Conversion settings successfully loaded.");
                            if (mySettings.AllowFrameCountInTimeCode == 1)
                                this.lblFramesInTimecode.Text = TREAT_ENDING_NUMERALS_AS_FRAMES;
                            else
                                this.lblFramesInTimecode.Text = ALLOW_FRACTIONAL_SECONDS;
                        }
                        catch (Exception ex)
                        {
                            OutputPart2Problem("Will not do Part 2 conversion due to problems in trying to load required settings from " + MY_CONFIG_SETTINGS_FILENAME + ": " + ex.Message);
                            continueWithPart2 = false;
                        }
                        if (continueWithPart2)
                        {
                            // Clear slate for txtErrorsPart2:
                            txtErrorsPart2.Text = "";
                            TxtToXMLGeneration Part2Conversion = new TxtToXMLGeneration(mySettings, selectedFullPath,
                                this.txtStatus, this.txtErrorsPart2);
                        }
                    }
                    OutputStatusLine("The program has finished.");
                    stillProcessing = false;
                }
            }

        }

        private void DocumentToXMLCleanupForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && stillProcessing)
            {
                DialogResult endEarly = MessageBox.Show("Do you really want to end before the file conversion processing finishes?", "End Early?", MessageBoxButtons.YesNo);
                if (endEarly == DialogResult.No)
                    e.Cancel = true;
            }
        }

        private void DocumentToXMLCleanupForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            System.Windows.Forms.Application.Exit();
        }

        #endregion ================================= EVENT HANDLERS ==================================

        #region ================================ HELPER FUNCTIONS =================================
        /// <summary>
        /// Do conversion of all .docx and .docm files into .txt files 
        /// </summary>
        /// <param name="sFullPath">path for .docx and .docm files to convert</param>
        /// <returns>true on success, false on abort/failure</returns>
        /// <remarks>Formerly done with Microsoft.Office.Interop.Word.Application, now done in 2020+
        /// using DocumentFormat.OpenXml.</remarks>
        private bool ConvertFromDocxToTxt(string sFullPath)
        {
            const bool CHECK_FOR_TRANSCRIPT_ENDING_MARKER = true; // by default, give warning if MARKER_FOR_END_OF_CONTENTS not found per document
            bool requireFinalTranscriptEndingMarker; 
            bool foundFinalTranscriptEndingMarker;

            StreamReader sr;
            string sLine;
            List<String> myFilesToConvert = new List<String>();
            string sDestName;

            // Step 1: Convert .docx or .docm files into UTF-8 plain text .txt:
            foreach (string docxFile in Directory.GetFiles(sFullPath, "*.docx"))
            {
                myFilesToConvert.Add( docxFile );
            }
            foreach (string docmFile in Directory.GetFiles(sFullPath, "*.docm"))
            {
                myFilesToConvert.Add(docmFile);
            }

            try
            { // Open given Word document list (.docm, .docx) using WordprocessingDocument from DocumentFormat.OpenXml
              // NOTE: Word 97-2003 files with *.doc extension not read in nor supported as they are not saved in a format
              // understood by OpenXml - only respect Office 2007 or later.

                for (int iFile = 0; iFile < myFilesToConvert.Count; iFile++)
                {
                    OutputStatusLine("Processing " + Path.GetFileNameWithoutExtension(myFilesToConvert[iFile]) + " (.docx/.docm to .txt conversion) ...");
                    sDestName = sFullPath + System.IO.Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(myFilesToConvert[iFile]) + ".txt";

                    // Open a WordprocessingDocument as read-only.
                    using (WordprocessingDocument wordprocessingDocument =
                         WordprocessingDocument.Open(myFilesToConvert[iFile], false))
                    {
                        OutputStatusLine("Opened file " + myFilesToConvert[iFile]);
                        // Assign a reference to the existing document body.
                        Body body = wordprocessingDocument.MainDocumentPart.Document.Body;

                        // Hand off the body and destination file to a helper function.  Possibly hide some warnings from status report.
                        ConvertBodyIntoUnicodeTextFile(body, sDestName, this.checkBoxHideWarnings.Checked);
                        wordprocessingDocument.Close();
                    }
                    OutputStatusLine("");
                }
            }
            catch (Exception ex)
            {
                OutputProblem("Exception Caught: " + ex.Message);
            }

            if (CHECK_FOR_TRANSCRIPT_ENDING_MARKER)
            { // Step 2: Only if CHECK_FOR_TRANSCRIPT_ENDING_MARKER, do a check on .txt files 
              // (note: the .txt file is NOT changed with this step 2 check).
                string[] sFilesToCheck = System.IO.Directory.GetFiles(sFullPath, "*.txt");
                string sShortLabelForFilename;

                for (int iFile = 0; iFile < sFilesToCheck.Length; iFile++)
                {
                    sShortLabelForFilename = Path.GetFileNameWithoutExtension(sFilesToCheck[iFile]);
                    OutputStatusLine("Processing " + sShortLabelForFilename + " (.txt cleanup first pass) ...");
                    try
                    {
                        sr = new StreamReader(sFilesToCheck[iFile], true);
                    }
                    catch (Exception ex)
                    {
                        OutputStatusLine("");
                        OutputProblem(sShortLabelForFilename + ": Could not open text file so no action taken and stopping work: " + ex.Message);
                        return false;
                    }

                    // Read in lines, checking for two markers:
                    // if MARKER_FOR_COLLECTION_AND_SESSIONS_ONLY_AS_INPUT found, then will not look for transcript ending marker and exit early
                    // as collections/sessions list will NOT include transcripts, but if it is not found, then look for MARKER_FOR_END_OF_CONTENTS.
                    requireFinalTranscriptEndingMarker = true; // because earlier check has CHECK_FOR_TRANSCRIPT_ENDING_MARKER == true
                    foundFinalTranscriptEndingMarker = false;
                    while ((sLine = sr.ReadLine()) != null)
                    {
                        sLine = sLine.Trim();

                        if (sLine.StartsWith(TxtToXMLGeneration.MARKER_FOR_COLLECTION_AS_INPUT) ||
                            sLine.StartsWith(TxtToXMLGeneration.MARKER_FOR_SESSION_AS_INPUT))
                        { // this marks the file as holding collection or sessions info, not transcripts for stories, so turn off the check
                          // and exit this input-file check....
                            requireFinalTranscriptEndingMarker = false;
                            break;
                        }

                        if (sLine.StartsWith(TxtToXMLGeneration.MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                        { // done! found the required marker for end of transcript
                            foundFinalTranscriptEndingMarker = true;
                            break;
                        }
                    }
                    sr.Close();

                    if (!foundFinalTranscriptEndingMarker && requireFinalTranscriptEndingMarker)
                    {
                        OutputProblem(sShortLabelForFilename + ": WARNING: Final story transcript not ended with a " +
                            TxtToXMLGeneration.MARKER_FOR_END_OF_TRANSCRIPT_INPUT + " line; check the end of the .txt file to be sure it did not receive extra Word Doc contents such as header or footer text.");
                    }
                    OutputStatusLine("");
                }
            }
            // All done: return true to continue onto subsequent steps
            return true;
        }

        private void ConvertBodyIntoUnicodeTextFile(Body givenBody, string outputFilename, bool hideWarnings)
        {
            // From DocumentFormat.OpenXml documentation: The basic document structure of a WordProcessingML document 
            // consists of the document and body elements, followed by one or more block level elements such as p, 
            // which represents a paragraph. A paragraph contains one or more r elements. The r stands for run, 
            // which is a region of text with a common set of properties, such as formatting. A run contains one or 
            // more t elements. The t element contains a range of text. 

            // Note: extra warnings hidden if hideWarnings is true.

            // Some notes about the whitespace processing, which is different for 1,2,3 from an Office "Save as UTF-8 text" from a docx file:
            // (1) replace tab with space
            // (2) replace non-break whitespace with space
            // (3) ignore indent completely
            // (4) end paragraph with \n (same as Word to UTF - 8 conversion does)
            // (5) insert newline for both cr and br tags in the openXml (same as Word to UTF - 8 conversion does)

            const string newLine = "\n";
            string finalOutput = "";
            StringBuilder sbDoc = new StringBuilder(); // the text from the given document
            DocumentFormat.OpenXml.OpenXmlElementList docPieces = givenBody.ChildElements;
            for (int i = 0; i < docPieces.Count; i++)
            {
                if (docPieces[i].XmlQualifiedName.ToString().EndsWith(":p"))
                {
                    for (int j = 0; j < docPieces[i].ChildElements.Count; j++)
                    {
                        if (docPieces[i].ChildElements[j].XmlQualifiedName.ToString().EndsWith(":r"))
                        {
                            for (int k = 0; k < docPieces[i].ChildElements[j].ChildElements.Count; k++)
                            {
                                if (docPieces[i].ChildElements[j].ChildElements[k].XmlQualifiedName.ToString().EndsWith(":t"))
                                {
                                    if (docPieces[i].ChildElements[j].ChildElements[k].ChildElements.Count != 0)
                                    {
                                        if (!hideWarnings)
                                            OutputStatusLine("  Ignored doc para text for holding xml: " + docPieces[i].ChildElements[j].ChildElements[k].InnerText);
                                        /* later, see if we need to forgive anything, such as:
                                        if (docPieces[i].ChildElements[j].ChildElements[k].ChildElements.Count == 1 &&
                                            docPieces[i].ChildElements[j].ChildElements[k].ChildElements[0].XmlQualifiedName.ToString().EndsWith(":whateverMightBe") &&
                                            docPieces[i].ChildElements[j].ChildElements[k].ChildElements[0].ChildElements.Count == 0)
                                        {
                                            // forgive the presence of just a "whateverMightBe" marker
                                        }
                                        else
                                        {
                                            OutputStatusLine("  Ignored doc para text for holding xml: " + docPieces[i].ChildElements[j].ChildElements[k].InnerText);
                                        }
                                        ...not needed now because as of now, nothing should be within the t element! */
                                    }
                                    else
                                        // collect string for later UTF-8 conversion
                                        sbDoc.Append(docPieces[i].ChildElements[j].ChildElements[k].InnerText);
                                }
                                else if (docPieces[i].ChildElements[j].ChildElements[k].XmlQualifiedName.ToString().EndsWith(":cr") ||
                                    docPieces[i].ChildElements[j].ChildElements[k].XmlQualifiedName.ToString().EndsWith(":br"))
                                {
                                    // collect a newline at this point for later UTF-8 conversion
                                    sbDoc.Append(newLine);
                                }
                                else if (docPieces[i].ChildElements[j].ChildElements[k].XmlQualifiedName.ToString().EndsWith(":tab"))
                                {
                                    // collect a space at this point for later UTF-8 conversion
                                    // (NOTE: Microsoft Office 2016 export to plain text UTF-8 encoding does NOT do this - it keeps the \t tab character)
                                    sbDoc.Append(" ");
                                }
                                else if (!docPieces[i].ChildElements[j].ChildElements[k].XmlQualifiedName.ToString().EndsWith(":lastRenderedPageBreak") &&
                                    !docPieces[i].ChildElements[j].ChildElements[k].XmlQualifiedName.ToString().EndsWith(":rPr") &&
                                    !hideWarnings)
                                        OutputStatusLine("  Ignored xml piece in doc para run: " + docPieces[i].ChildElements[j].ChildElements[k].XmlQualifiedName.ToString());
                            }
                        }
                        else if (!docPieces[i].ChildElements[j].XmlQualifiedName.ToString().EndsWith(":pPr") && 
                                 !docPieces[i].ChildElements[j].XmlQualifiedName.ToString().EndsWith(":proofErr") &&
                                 !docPieces[i].ChildElements[j].XmlQualifiedName.ToString().EndsWith(":bookmarkStart") &&
                                 !docPieces[i].ChildElements[j].XmlQualifiedName.ToString().EndsWith(":bookmarkEnd") &&
                                    !hideWarnings)
                            OutputStatusLine("  Ignored xml piece in doc para: " + docPieces[i].ChildElements[j].XmlQualifiedName.ToString());
                    }
                    // Always mark end of paragraph with a newline:
                    sbDoc.Append(newLine);
                }
                // else don't even worry about other pieces at the doc para level, like sectPr - just quietly ignore them
            }

            // Remove the final newLine from the document.
            if (sbDoc.Length > 0)
            {
                sbDoc.Remove(sbDoc.Length - 1, 1);
                finalOutput = StringBuildertoUTF8String(sbDoc);

                // Another step: replace any Unicode 160 (i.e., &nbsp; or nonbreaking space) with plain old " "
                // (NOTE: Microsoft Office 2016 export to plain text UTF-8 encoding does NOT do this)
                finalOutput = finalOutput.Replace('\u00A0', ' ');
            }

            // If it already exists, it will be deleted!
            File.WriteAllText(outputFilename, finalOutput, Encoding.UTF8);
        }

        //set for UTF-8 encoding
        private static readonly Encoding Utf8Encoder = UTF8Encoding.GetEncoding("UTF-8", new EncoderReplacementFallback(string.Empty), new DecoderExceptionFallback());

        /// <summary>
        /// Converts StringBuilder and returns clean UTF-8 string, stripping characters outside the UTF-8 range
        /// </summary>
        private string StringBuildertoUTF8String(StringBuilder sb)
        {
            string strUTF8clean;
            char[] charsb = new char[sb.Length];

            //copies StringBuilder to char[]
            sb.CopyTo(0, charsb, 0, sb.Length);

            //get clean UTF-8 string from stringbuilder char[]  
            strUTF8clean = Utf8Encoder.GetString(Utf8Encoder.GetBytes(charsb));

            return strUTF8clean;
        }

        private void OutputStatusLine(string sLine)
        {
            txtStatus.Text = txtStatus.Text + sLine + System.Environment.NewLine;
            txtStatus.SelectionStart = txtStatus.Text.Length;
            txtStatus.ScrollToCaret();
            txtStatus.Refresh();
        }

        private void OutputProblem(string sLine)
        {
            txtErrors.Text = txtErrors.Text + sLine + System.Environment.NewLine;
            txtErrors.SelectionStart = txtErrors.Text.Length;
            txtErrors.ScrollToCaret();
            txtErrors.Refresh();
        }

        private void OutputPart2Problem(string sLine)
        {
            txtErrorsPart2.Text = txtErrors.Text + sLine + System.Environment.NewLine;
            txtErrorsPart2.SelectionStart = txtErrorsPart2.Text.Length;
            txtErrorsPart2.ScrollToCaret();
            txtErrorsPart2.Refresh();
        }

        #endregion

        private void chkConsiderEndColonNbrNbrAsFrames_CheckedChanged(object sender, EventArgs e)
        {
            txtStatus.Clear();
            txtErrors.Clear();
            txtErrorsPart2.Clear();
        }

    }
}
