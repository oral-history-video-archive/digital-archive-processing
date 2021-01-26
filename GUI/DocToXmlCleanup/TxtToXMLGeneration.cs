using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using InformediaCORE.Common;
using InformediaCORE.Common.Xml;

namespace DocToXMLCleanup
{
    /// <summary>
    /// This class produces .xml files from very particularly formatted .doc or .txt documents in a given folder
    /// (if .doc files are found, they are exported as UTF-8 .txt files).
    /// 
    /// One other piece of data is necessary: the configuration file giving 
    /// bounds on min/max duration of a legal story segment, and name of
    /// a worlds-partitions-collection annotation categories xml file, i.e.,
    /// information used to add final pieces into the generated .xml and 
    /// document what other descriptors are legal for the input .txt files,
    /// e.g., perhaps the data set supports "FAVORITE FOOD."
    /// 
    /// Xml files are produced for worlds/partitions (via solely configuration file information), 
    /// each collection (i.e., interview) complete with sessions as needed (e.g., perhaps collection 
    /// recorded across multiple sessions in different locations or at different dates),
    /// each movie in a collection/session (i.e., tape), and each story segment in a movie.
    /// 
    /// May 2017 Update:
    /// There are three types of data input files:
    /// (a) collection metadata in files that have as their first line: ***COLLECTION
    /// (b) session information metadata in files that have as their first line: ***SESSIONINFO
    /// (c) movie (with segmentation) metadata in files that have as their first line: ***MOVIE
    /// 
    /// Two text boxes are passed in via constructor for reporting out to user the progress of processing steps.
    /// 
    /// Modifications:
    /// Sept. 2020: Replace Microsoft.Office.Interop with DocumentFormat.OpenXml (version 2.11.3) so tht
    ///     conversion from Word to UTF-8 txt can take place without needing to have Microsoft Word installed.
    ///     Notably, we are NOT calling CleanUpUTF8() as was done earlier.  All UTF8 characters assumed to be OK for downstream processing,
    ///     since the use of Lemur/Indri has been retired.
    /// May 31, 2017: Dramatically reduced comment blocks; see Docs/SegmentationGuide.docx for an updated document on the input data.
    /// May 25, 2017: Updated to latest schema supporting May 2017 Digital Archive support (the HTML5 version, not the Flash version).
    /// 
    /// Jan. 14, 2013: Fixed bug with float/double conversion and the 30.0/29.97 conversion factor (float constants now used, could 
    /// have also corrected by specifying that numeric literals to be interpreted as floats via 30.0f/29.97f and especially the greater
    /// or equal to comparison to 29.97f which caused the bug.
    /// 
    /// Sept. 27, 2012: Updated documentation in header a bit;
    /// updated limit for SHORT DESCRIPTION to be 1024 characters;
    /// Since we still support one file listing everything, that one file will have collection "Abstract" labeled as ***SHORT DESCRIPTION
    /// and the movie abstract as ***ABSTRACT (even though SHORT DESCRIPTION is the collection's abstract).
    /// 
    /// March 30, 2012: Updated how the support "InformediaCORE" MDF infrastructure is added: latest InformediaCORE.dll brought in.
    /// 
    /// Dec. 12, 2011: While Lemur itself can support UTF-8, the current wrapper for it supporting access by C# web services 
    /// will not; so, café must be converted to cafe, etc., and CleanUpUTF8 once again will map characters to their ASCII plain 
    /// text equivalents.
    /// 
    /// Nov. 16, 2011:  Added MapUTF8_To_ASCII as always FALSE for now (see if we can fully process café with its é, etc.).
    /// BIG CHANGE:  output XML capable of being easily ingested into MDF by Bryan Maher's MDF data store ingest tools.
    /// 
    /// Nov. 2, 2011: Added in two more optional markers:
    /// INITIAL_TIMECODE
    /// NONDROP_TIMECODE_IN_USE
    /// (see http://wiki.idvl.org/Default.aspx?Page=The-ScienceMakers-Data-Bugs-Fixes-and-Updates for context)
    /// If INITIAL_TIMECODE is not given, it is assumed as 00:00 (i.e., the surrogate video file starts at 0 seconds
    /// of the marked up timed video file, as expected for most supplied videos).
    /// If NONDROP_TIMECODE_IN_USE is not specified or its given value is 0 (really shouldn't bother but allow for it),
    /// then no NONDROP timecode, i.e., proper Drop Frame timecode use == 29.97 fps for INITIAL_TIMECODE and
    /// segment offset adjustments) will be presumed.
    /// If one or both of INITIAL_TIMECODE,NONDROP_TIMECODE_IN_USE given, then this third optional marker
    /// becomes required:
    /// PLAYBACK_FRAME_RATE
    /// </summary>
    class TxtToXMLGeneration
    {
        #region ================================== CONSTANTS ===================================

        private const int TRUE_INDICATOR_IN_CONFIG = 1; // 1 is same as "true" for an integer-based true/false flag in config settings

        private const int MAX_LENGTH_FOR_ABSTRACT = 1024; // change made Sept., 2012
        private const int MAX_LENGTH_FOR_SHORT_DESCRIPTION = 1024; // change made Sept., 2012
        private const int MAX_LENGTH_FOR_SHORT_BIOGRAPHY = 2048; // introduced May 2017

        public const string MARKER_FOR_COLLECTION_AS_INPUT = "***COLLECTION";
        public const string MARKER_FOR_SESSION_AS_INPUT = "***SESSIONINFO";
        private const string MARKER_FOR_MOVIE_AND_SEGMENTS_AS_INPUT = "***MOVIE";
        public const string MARKER_FOR_END_OF_TRANSCRIPT_INPUT = "***END";


        private const string PARTITION_NAME_SEPARATOR = "\t"; // Use tab character - will check to see that this is NOT within any partition name as well.
        private const string PARTITION_NAME_SEPARATOR_DESCRIPTION = "tab character";

        const string OCCUPATION_ALLOWING_MANY_VALUES = "Occupation"; // TODO: in refactoring code, make input specification/configuration more powerful regarding worlds/partitions and annotations;
                                                                    // until then, assumption is that open-ended annotations with no restricted vocabulary are one entry per collection, except for this one (Occupation)

        // Define the all-important marker tags (the keys) in the input file:

        /// <summary>
        /// Prefix to all input file markers, including optional ones like world names like "***Job Type", for example
        /// </summary>
        private const string LENIENT_MARKER_PREFACE = "***";

        private const string STORY_MARKER = "***STORY";
        private const string TRANSCRIPT_MARKER = "***TRANSCRIPT";
        private const string UNSPOKEN_TEXT_IN_TRANSCRIPT_OPENER = "[";
        private const string UNSPOKEN_TEXT_IN_TRANSCRIPT_CLOSER = "]";

        // Historically, a set of text markers evolved over years with The HistoryMakers for their transcriptions and metadata specification.
        // This tool is dated, and could use a fresh overhaul, but that would interrupt The HistoryMakers' manual processing efforts and take away their contextual knowledge of the process.
        // So, instead, in 2017 we will keep as much "as is" as possible, adding in new fields that are all marked optional, keeping the required fields from before.
        // One difference:  there are tags for collection, tags for session, and tags for movie/segment.  The three are no longer mixed.

        // Collection-level tags:
        private const string LASTNAME_MARKER = "***LAST NAME"; // required, see setup in ConvertTextFilesToXMLFiles()
        private const string PREFERREDNAME_MARKER = "***PREFERRED NAME"; // required
        private const string ACCESSION_MARKER = "***ACCESSION"; // required
        private const string GENDER_MARKER = "***GENDER"; // required
        private const string BIRTHDATE_MARKER = "***BIRTH DATE"; // required                
        private const string PORTRAIT_MARKER = "***PORTRAIT"; // required
        private const string SHORTDESC_MARKER = "***SHORT DESCRIPTION"; // required
        private const string FIRSTNAME_MARKER = "***FIRST NAME"; // required
        private const string SHORTBIO_MARKER = "***BIOGRAPHY"; // required
        private const string URL_MARKER = "***URL"; // required
        private const string REGION_MARKER = "***REGION"; // required

        private const string DECEASEDDATE_MARKER = "***DECEASED DATE"; // optional, see setup in ConvertTextFilesToXMLFiles()
        private const string BIRTHCITY_MARKER = "***BIRTH CITY"; // optional
        private const string BIRTHSTATE_MARKER = "***BIRTH STATE"; // optional
        private const string BIRTHCOUNTRY_MARKER = "***BIRTH COUNTRY"; // optional

        // Session-level tags:
        // Sessions must also have ACCESSION_MARKER to identify parent collection, see setup in ConvertTextFilesToXMLFiles()
        private const string SESSION_MARKER = "***SESSION"; // required for session, see setup in ConvertTextFilesToXMLFiles()
        private const string INTERVIEWER_MARKER = "***INTERVIEWER"; // required
        private const string INTERVIEWLOCATION_MARKER = "***INTERVIEW LOCATION"; // required
        private const string INTERVIEWDATE_MARKER = "***INTERVIEW DATE"; // required
        private const string VIDEOGRAPHER_MARKER = "***VIDEOGRAPHER"; // required

        private const string SPONSOR_MARKER = "***SPONSOR"; // optional for session, see setup in ConvertTextFilesToXMLFiles() 
        private const string SPONSOR_IMAGE_MARKER = "***SPONSOR IMAGE"; // optional
        private const string SPONSOR_URL_MARKER = "***SPONSOR URL"; // optional

        // Movie/segment level tags:
        // Movies must also have ACCESSION_MARKER and SESSION_MARKER to identify parent session and collection, see setup in ConvertTextFilesToXMLFiles()
        private const string TAPE_MARKER = "***TAPE"; // required for movie, see setup in ConvertTextFilesToXMLFiles()
        private const string FILENAME_MARKER = "***FILENAME"; // required for movie, see setup in ConvertTextFilesToXMLFiles()
        private const string MOVIE_ABSTRACT_MARKER = "***ABSTRACT";  // required

        private const string INITIAL_TIMECODE_MARKER = "***INITIAL TIMECODE"; // optional for movie, see setup in ConvertTextFilesToXMLFiles()
        private const string NONDROP_TIMECODE_IN_USE_MARKER = "***NONDROP TIMECODE IN USE"; // optional
        private const string PLAYBACK_FRAME_RATE_MARKER = "***PLAYBACK FRAME RATE"; // optional
        private const string TRANSCRIBER_MARKER = "***TRANSCRIBER"; // optional
        private const string TRANSCRIPTIONDATE_MARKER = "***TRANSCRIPTION DATE"; // optional
        private const string PRODUCER_MARKER = "***PRODUCER"; // optional
        private const string PRODUCTION_COMPANY_MARKER = "***PRODUCTION COMPANY"; // optional

        // Define some brief "lenient" tags for the above longer ones.
        private const string LENIENT_LASTNAME_MARKER = "***LAST";
        private const string LENIENT_PREFERREDNAME_MARKER = "***PREFE";
        private const string LENIENT_ACCESSION_MARKER = "***ACCESS";
        private const string LENIENT_FILENAME_MARKER = "***FILE";
        private const string LENIENT_GENDER_MARKER = "***GENDER";
        private const string LENIENT_BIRTHDATE_MARKER = "***BIRTH";
        private const string LENIENT_DECEASEDDATE_MARKER = "***DECEASED";

        /// <summary>
        /// Force a non-zero minimum length on segments, with 1 second being conservative 
        /// (and allowing for legal take away of one frame (0.03 for 30 fps) from segment length).
        /// </summary>
        private const int ABSOLUTE_MIN_LEGAL_SEGMENT_LENGTH_IN_SECS = 1;

        // Define names of certain movie-scope annotation types:
        private const string INITIAL_TIMECODE_ANNOTATION_NAME = "Initial Timecode";
        private const string NON_DROP_TIMECODE_IN_USE_ANNOTATION_NAME = "NonDrop Timecode In Use";
        private const string PRE_SPECIFIED_FRAME_RATE_ANNOTATION_NAME = "Prespecified Frame Rate";
        private const string TRANSCRIBER_ANNOTATION_NAME = "Transcriber";
        private const string TRANSCRIPTION_DATE_ANNOTATION_NAME = "Transcription Date";
        private const string PRODUCER_ANNOTATION_NAME = "Producer";
        private const string PRODUCTION_COMPANY_ANNOTATION_NAME = "Production Company";

        #endregion ================================== CONSTANTS ===================================

        #region =============================== ENUM TYPES ================================
        /// <summary>
        /// The different types of input to the processing pipeline: collection, session, or movie/segment
        /// </summary>
        public enum InputDataFileType
        {
            Collection = 1,
            Session = 2,
            MovieSegmentation = 3
        }

        #endregion =============================== ENUM TYPES ================================

        #region =============================== LOCAL VARIABLES ================================
        /// <summary>
        /// Holds nesting of partitions within worlds for the data set
        /// </summary>
        private WorldsPartitions myWorldSet;

        /// <summary>
        /// If true, multiple partitions can be input on a single line for a specified world (with semi-colon as the expected separator).
        /// </summary>
        private bool ALLOW_PARTITION_NAMES_ON_ONE_LINE = true;

        /// <summary>
        /// If true, time is given as "ss:ff" or "mm:ss:ff" or "hh:mm:ss:ff" with EXPECTED frame count given everywhere (even if 00, for proper parsing),
        /// or if it is false then fractional seconds must be given via decimal point as in ss.ss, e.g., 20.5 for 20 and a half seconds
        /// (assigned via config file for use in all processing).
        /// </summary>
        private bool myFrameCountInTimeCode = true;


        private System.Windows.Forms.TextBox myStatusTextBox;
        private System.Windows.Forms.TextBox myProblemsTextBox;

        #endregion =============================== LOCAL VARIABLES ================================

        #region ================================= CONSTRUCTOR ==================================
        /// <summary>
        /// Constructor for class
        /// </summary>
        /// <param name="configSettings">configuration settings</param>
        /// <param name="sPathToProcess">path to process</param>
        /// <param name="txtReportStatus">where to report status</param>
        /// <param name="txtReportProblems">where to report problems</param>
        public TxtToXMLGeneration(ConfigSettings configSettings, string sPathToProcess, System.Windows.Forms.TextBox txtReportStatus, 
            System.Windows.Forms.TextBox txtReportProblems)
        {
            myStatusTextBox = txtReportStatus;
            myProblemsTextBox = txtReportProblems;

            string sFullPath = System.IO.Path.GetFullPath(sPathToProcess);

            myFrameCountInTimeCode = (configSettings.AllowFrameCountInTimeCode == 1);

            // Right up front, output all that we need for the worlds/partitions annotation level based on configSettings:
            OutputWorldsPartitionsInfo(configSettings.WorldsPartitionsXMLFilename, sFullPath);

            // Right up front, output all that we need for movie-attribution items that might get filled in from movie input .doc/.txt processing:
            OutputMovieAnnotationTypes(sFullPath);

            ConvertTextFilesToXMLFiles(configSettings.MinLegalSegmentLengthInSecs, configSettings.MaxLegalSegmentLengthInSecs, configSettings.PortraitPath,
                (configSettings.UseHistoryMakersConventions == TRUE_INDICATOR_IN_CONFIG), configSettings.VideoPath, sFullPath);
        }
        #endregion ================================= CONSTRUCTOR ==================================

        #region ============================= XML EXPORT ROUTINES ==============================
        /// <summary>
        /// Output a set of movie attribution types that may be used later when processing the input file(s) for movie(s).
        /// </summary>
        /// <param name="targetPath">output path</param>
        private void OutputMovieAnnotationTypes(string targetPath)
        {
            // Create a new XML object...
            XmlAnnotationType annType = new XmlAnnotationType();
            // ...populate it...
            annType.Name = INITIAL_TIMECODE_ANNOTATION_NAME;
            annType.Scope = XmlAnnotationScope.Movie;
            annType.Description = "Initial timecode (for mapping between burned in timecodes and video surrogates)";

            // ...then serialize it to file.
            XmlUtilities.Write<XmlAnnotationType>(annType, Path.Combine(targetPath, GetSafeFilename(annType.Name) + ".annotationType.xml"));

            // Create a new XML object...
            annType = new XmlAnnotationType();

            // ...populate it...
            annType.Name = NON_DROP_TIMECODE_IN_USE_ANNOTATION_NAME;
            annType.Scope = XmlAnnotationScope.Movie;
            annType.Description = "1 indicates that 30 fps burned in timecode used; 0 indicates NTSC 29.97 fps for burned in timecode";

            // ...then serialize it to file.
            XmlUtilities.Write<XmlAnnotationType>(annType, Path.Combine(targetPath, GetSafeFilename(annType.Name) + ".annotationType.xml"));

            // Create a new XML object...
            annType = new XmlAnnotationType();

            // ...populate it...
            annType.Name = PRE_SPECIFIED_FRAME_RATE_ANNOTATION_NAME;
            annType.Scope = XmlAnnotationScope.Movie;
            annType.Description = "Given frame rate for mapping between usable segment times and some other burned in timecodes";

            // ...then serialize it to file.
            XmlUtilities.Write<XmlAnnotationType>(annType, Path.Combine(targetPath, GetSafeFilename(annType.Name) + ".annotationType.xml"));

            // Create a new XML object...
            annType = new XmlAnnotationType();

            // ...populate it...
            annType.Name = TRANSCRIBER_ANNOTATION_NAME;
            annType.Scope = XmlAnnotationScope.Movie;
            annType.Description = "Transcriber";

            // ...then serialize it to file.
            XmlUtilities.Write<XmlAnnotationType>(annType, Path.Combine(targetPath, GetSafeFilename(annType.Name) + ".annotationType.xml"));

            // Create a new XML object...
            annType = new XmlAnnotationType();

            // ...populate it...
            annType.Name = TRANSCRIPTION_DATE_ANNOTATION_NAME;
            annType.Scope = XmlAnnotationScope.Movie;
            annType.Description = "Transcription Date";

            // ...then serialize it to file.
            XmlUtilities.Write<XmlAnnotationType>(annType, Path.Combine(targetPath, GetSafeFilename(annType.Name) + ".annotationType.xml"));

            // Create a new XML object...
            annType = new XmlAnnotationType();

            // ...populate it...
            annType.Name = PRODUCER_ANNOTATION_NAME;
            annType.Scope = XmlAnnotationScope.Movie;
            annType.Description = "Producer";

            // ...then serialize it to file.
            XmlUtilities.Write<XmlAnnotationType>(annType, Path.Combine(targetPath, GetSafeFilename(annType.Name) + ".annotationType.xml"));

            // Create a new XML object...
            annType = new XmlAnnotationType();

            // ...populate it...
            annType.Name = PRODUCTION_COMPANY_ANNOTATION_NAME;
            annType.Scope = XmlAnnotationScope.Movie;
            annType.Description = "Production Company";

            // ...then serialize it to file.
            XmlUtilities.Write<XmlAnnotationType>(annType, Path.Combine(targetPath, GetSafeFilename(annType.Name) + ".annotationType.xml"));
        }

        /// <summary>
        /// Export world and annotationType XML files to cover the material given in the input worldspartitions XML file.
        /// </summary>
        /// <param name="inputFilename">XML file with very specific format matching WorldsPartitions type</param>
        /// <param name="targetPath">fully expanded path for output XML files</param>
        private void OutputWorldsPartitionsInfo(string inputFilename, string targetPath)
        {
            // Right up front, see if worldspartitionsxmlfile parses OK:
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(WorldsPartitions));
            myWorldSet = new WorldsPartitions();
            try
            {
                System.IO.StreamReader file = new System.IO.StreamReader(inputFilename);
                myWorldSet = (WorldsPartitions)reader.Deserialize(file);
            }
            catch (Exception ex)
            {
                OutputProblem("Problems in trying to load worldspartitions XML File: " + ex.Message);
                return;
            }
            // Now output the worlds/partitions for use in MDF data store:
            try
            {
                foreach (World oneWorld in myWorldSet.MemberWorlds)
                {
                    // Create a new XML object...
                    XmlWorld worldForOutput = new XmlWorld();

                    // ...populate it...
                    worldForOutput.Name = oneWorld.Name;
                    worldForOutput.Description = oneWorld.Desc;
                    List<XmlPartition> partitionsForOutput = new List<XmlPartition>();
                    foreach (Partition onePartition in oneWorld.ChildPartitions)
                    {
                        // Create a new XML object...
                        XmlPartition partitionForOutput = new XmlPartition();
                        // ...populate it...
                        if (onePartition.Name.Contains(PARTITION_NAME_SEPARATOR))
                        { // Grumble but proceed anyway....
                            OutputProblem("WARNING: Your partition name contains a " + PARTITION_NAME_SEPARATOR_DESCRIPTION + " that may ruin subsequent partition processing.  Please rename your partition.");
                        }
                        if (ALLOW_PARTITION_NAMES_ON_ONE_LINE && onePartition.Name.Contains(";"))
                        { // Grumble but proceed anyway....
                            OutputProblem("WARNING: Your partition name contains a semicolon (;) that may ruin subsequent partition processing because semicolons can be used to separate many partitions listed together in input documents.  Please rename your partition.");
                        }
                        partitionForOutput.Name = onePartition.Name;
                        partitionForOutput.Description = onePartition.Desc;
                        partitionsForOutput.Add(partitionForOutput);
                    }
                    worldForOutput.Partitions = partitionsForOutput;

                    // ...then serialize it to file.
                    XmlUtilities.Write<XmlWorld>(worldForOutput, Path.Combine(targetPath, GetSafeFilename(worldForOutput.Name) + ".world.xml"));
                }

                // Also output the open-ended (broad range of possible values) collection-scope worlds:
                foreach (CollectionScopeWorld oneCollectionScopeWorld in myWorldSet.OpenEndedCollectionScopeWorlds)
                {
                    // Create a new XML object...
                    XmlAnnotationType annType = new XmlAnnotationType();

                    // ...populate it...
                    annType.Name = oneCollectionScopeWorld.Name;
                    annType.Scope = XmlAnnotationScope.Collection;
                    annType.Description = oneCollectionScopeWorld.Desc;

                    // ...then serialize it to file.
                    XmlUtilities.Write<XmlAnnotationType>(annType, Path.Combine(targetPath, GetSafeFilename(annType.Name) + ".annotationType.xml"));
                }
            }
            catch (Exception ex)
            {
                OutputProblem("Problems in trying to export world or annotationType XML: " + ex.Message);
                return;
            }
        }

        #endregion ============================= XML EXPORT ROUTINES ==============================

        #region ========================= INPUT PARSING MAIN ROUTINES ==========================
        /// <summary>
        /// Program that processes all .txt files in this directory as specially marked up input files.
        /// 
        /// It outputs cleaned up XML files if possible for the processing queue (e.g., for follow-up ingest into an MDF data store).
        /// </summary>
        /// <param name="minLegalSegmentSizeInSeconds">minimum legal segment size in seconds (will be increased here to ABSOLUTE_MIN_LEGAL_SEGMENT_LENGTH_IN_SECS as needed)</param>
        /// <param name="maxLegalSegmentSizeInSeconds">maximum legal segment size in seconds ignored if nonpositive value)</param>
        /// <param name="portraitPathPrefix">path to collection portrait imagery, ignored if empty string</param>
        /// <param name="usingHistoryMakersConventions">if true, video filename assemblage is via different rules as used by The HistoryMakers</param>
        /// <param name="videoPathPrefix">path to movie videos, ignored if empty string</param>
        /// <param name="targetPath">fully qualified path (e.g., caller already did System.IO.Path.GetFullPath) to process</param>
        /// <remarks>
        /// Support exists for a given end time on any segment and use of hms-hms rather than hms-end for last segment 
        /// if end time given, use of given end time rather than next start time minus a bit for other segments 
        /// where end time is given.
        /// 
        /// Added automatic "downsampling" of UTF-8 to ASCII for many more "funny" characters that would
        /// throw off Lemur processing, so café gets plain e at end, naïve gets plain i, etc.
        /// 
        /// NOTE: After receiving a number of data files from The HistoryMakers, they opted to list partition names
        /// on one line separated by ; (which breaks if partition names contain ; of course!), but we now allow it
        /// if program constant ALLOW_PARTITION_NAMES_ON_ONE_LINE is true.  If ALLOW_PARTITION_NAMES_ON_ONE_LINE,
        /// then partition names are griped about if they have a ; character.
        /// 
        /// The contents of the input file is PLAIN UTF8 TEXT with *** markers on
        /// some lines to indicate structure and mark what's next, very easy for 
        /// our partners to generate.  The input file has NO parsed XML.  The movie/segmentation input file MUST follow this strict
        /// input pattern:  general tags UP front, THEN LATER ***STORY and story-related tags.
        /// </remarks>
        private void ConvertTextFilesToXMLFiles(int minLegalSegmentSizeInSeconds, int maxLegalSegmentSizeInSeconds, 
            string portraitPathPrefix, bool usingHistoryMakersConventions, string videoPathPrefix, string targetPath)
        {
            int enforcedMinLegalSegmentSizeInSeconds = minLegalSegmentSizeInSeconds;
            if (enforcedMinLegalSegmentSizeInSeconds < ABSOLUTE_MIN_LEGAL_SEGMENT_LENGTH_IN_SECS)
                enforcedMinLegalSegmentSizeInSeconds = ABSOLUTE_MIN_LEGAL_SEGMENT_LENGTH_IN_SECS;

            // NOTE: The expectedTag or optionalTag Lenient value is good enough to be a unique key on actual string values held by each tag.
            // Very few lenient tags are provided, as they MUST be exact prefixes of the full tag (so we opt to just have the full tag used).
            // NOTE: There are THREE types of input files, marked by their opening line:
            // Collection, the top level collection identifying an interview subject
            // Session, identifying a set of movies in a single recording session
            // Movie-Segment, identifying a movie and its component segments
            List<IDVLTAG> expectedCollectionTag = new List<IDVLTAG>();
            List<IDVLTAG> optionalCollectionTag = new List<IDVLTAG>();
            List<IDVLTAG> expectedSessionTag = new List<IDVLTAG>();
            List<IDVLTAG> optionalSessionTag = new List<IDVLTAG>();
            List<IDVLTAG> expectedMovieSegmentTag = new List<IDVLTAG>();
            List<IDVLTAG> optionalMovieSegmentTag = new List<IDVLTAG>();

            // TODO: NOTE: There is a hidden assumption that tags that can be on multiple lines (i.e., 3rd parameter of "true" to IDVLTAG() constructor), are ONLY for Collection.
            // So, the processing for tags in other scopes (Session and MovieSegment) will be simplified and NOT contain logic to allow for multiple line input of tag values.
            // That processing occurs only within ComputeKeyValuePairsWithinCollectionInputFile()!

            expectedCollectionTag.Add(new IDVLTAG(LASTNAME_MARKER, LENIENT_LASTNAME_MARKER));
            expectedCollectionTag.Add(new IDVLTAG(PREFERREDNAME_MARKER, LENIENT_PREFERREDNAME_MARKER));
            expectedCollectionTag.Add(new IDVLTAG(ACCESSION_MARKER, LENIENT_ACCESSION_MARKER));
            expectedCollectionTag.Add(new IDVLTAG(GENDER_MARKER, LENIENT_GENDER_MARKER));
            expectedCollectionTag.Add(new IDVLTAG(BIRTHDATE_MARKER, LENIENT_BIRTHDATE_MARKER));
            expectedCollectionTag.Add(new IDVLTAG(PORTRAIT_MARKER, PORTRAIT_MARKER));
            expectedCollectionTag.Add(new IDVLTAG(FIRSTNAME_MARKER, FIRSTNAME_MARKER));
            expectedCollectionTag.Add(new IDVLTAG(SHORTDESC_MARKER, SHORTDESC_MARKER, true));
            expectedCollectionTag.Add(new IDVLTAG(SHORTBIO_MARKER, SHORTBIO_MARKER, true));
            expectedCollectionTag.Add(new IDVLTAG(URL_MARKER, URL_MARKER));
            expectedCollectionTag.Add(new IDVLTAG(REGION_MARKER, REGION_MARKER));

            optionalCollectionTag.Add(new IDVLTAG(DECEASEDDATE_MARKER, LENIENT_DECEASEDDATE_MARKER));
            optionalCollectionTag.Add(new IDVLTAG(BIRTHCITY_MARKER, BIRTHCITY_MARKER));
            optionalCollectionTag.Add(new IDVLTAG(BIRTHSTATE_MARKER, BIRTHSTATE_MARKER));
            optionalCollectionTag.Add(new IDVLTAG(BIRTHCOUNTRY_MARKER, BIRTHCOUNTRY_MARKER));

            expectedSessionTag.Add(new IDVLTAG(ACCESSION_MARKER, LENIENT_ACCESSION_MARKER));
            expectedSessionTag.Add(new IDVLTAG(SESSION_MARKER, SESSION_MARKER));
            expectedSessionTag.Add(new IDVLTAG(INTERVIEWER_MARKER, INTERVIEWER_MARKER));
            expectedSessionTag.Add(new IDVLTAG(INTERVIEWLOCATION_MARKER, INTERVIEWLOCATION_MARKER));
            expectedSessionTag.Add(new IDVLTAG(INTERVIEWDATE_MARKER, INTERVIEWDATE_MARKER));
            expectedSessionTag.Add(new IDVLTAG(VIDEOGRAPHER_MARKER, VIDEOGRAPHER_MARKER));

            optionalSessionTag.Add(new IDVLTAG(SPONSOR_MARKER, SPONSOR_MARKER));
            optionalSessionTag.Add(new IDVLTAG(SPONSOR_IMAGE_MARKER, SPONSOR_IMAGE_MARKER));
            optionalSessionTag.Add(new IDVLTAG(SPONSOR_URL_MARKER, SPONSOR_URL_MARKER));

            expectedMovieSegmentTag.Add(new IDVLTAG(ACCESSION_MARKER, LENIENT_ACCESSION_MARKER));
            expectedMovieSegmentTag.Add(new IDVLTAG(SESSION_MARKER, SESSION_MARKER));
            expectedMovieSegmentTag.Add(new IDVLTAG(TAPE_MARKER, TAPE_MARKER));
            expectedMovieSegmentTag.Add(new IDVLTAG(FILENAME_MARKER, LENIENT_FILENAME_MARKER));
            expectedMovieSegmentTag.Add(new IDVLTAG(MOVIE_ABSTRACT_MARKER, MOVIE_ABSTRACT_MARKER));

            optionalMovieSegmentTag.Add(new IDVLTAG(INITIAL_TIMECODE_MARKER, INITIAL_TIMECODE_MARKER));
            optionalMovieSegmentTag.Add(new IDVLTAG(NONDROP_TIMECODE_IN_USE_MARKER, NONDROP_TIMECODE_IN_USE_MARKER));
            optionalMovieSegmentTag.Add(new IDVLTAG(PLAYBACK_FRAME_RATE_MARKER, PLAYBACK_FRAME_RATE_MARKER));
            optionalMovieSegmentTag.Add(new IDVLTAG(TRANSCRIBER_MARKER, TRANSCRIBER_MARKER));
            optionalMovieSegmentTag.Add(new IDVLTAG(TRANSCRIPTIONDATE_MARKER, TRANSCRIPTIONDATE_MARKER));
            optionalMovieSegmentTag.Add(new IDVLTAG(PRODUCER_MARKER, PRODUCER_MARKER));
            optionalMovieSegmentTag.Add(new IDVLTAG(PRODUCTION_COMPANY_MARKER, PRODUCTION_COMPANY_MARKER));

            string tagName;
            
            if (myWorldSet.MemberWorlds != null)
            {
                // Also, add in WORLD NAMES from myWorldSet as another optionalCollectionTag legal specifier, prefaced by LENIENT_MARKER_PREFACE,
                // e.g., ***JOB TYPE may have values Doctor, Coach, etc. with a list of values (partitions) allowed for the world
                for (int iLegalWorld = 0; iLegalWorld < myWorldSet.MemberWorlds.Count; iLegalWorld++)
                {
                    tagName = (LENIENT_MARKER_PREFACE + myWorldSet.MemberWorlds[iLegalWorld].Name).ToUpper();
                    // Many values allowed, but input range restricted according to legal partition names for this world:
                    optionalCollectionTag.Add(new IDVLTAG(tagName, tagName, true, myWorldSet.MemberWorlds[iLegalWorld].Name, true));
                }
            }

            if (myWorldSet.OpenEndedCollectionScopeWorlds != null)
            {
                // Also, add in open-ended WORLD NAMES from myWorldSet as another optionalTag legal specifier, prefaced by LENIENT_MARKER_PREFACE,
                // e.g., ***FAVORITE_FOOD with one value allowed for most open-ended worlds (e.g., spaghetti);
                // NOTE: the exception is if there is a world named OCCUPATION_ALLOWING_MANY_VALUES: if so, allow many values for that open-ended world
                string baseName;
                for (int iLegalOpenEndedWorld = 0; iLegalOpenEndedWorld < myWorldSet.OpenEndedCollectionScopeWorlds.Count; iLegalOpenEndedWorld++)
                {
                    baseName = myWorldSet.OpenEndedCollectionScopeWorlds[iLegalOpenEndedWorld].Name;
                    tagName = (LENIENT_MARKER_PREFACE + baseName).ToUpper();
                    if (baseName != OCCUPATION_ALLOWING_MANY_VALUES)
                        // Only one value allowed, but it can be any value (input range is not restricted):
                        optionalCollectionTag.Add(new IDVLTAG(tagName, tagName, false, myWorldSet.OpenEndedCollectionScopeWorlds[iLegalOpenEndedWorld].Name, false));
                    else
                        // Multiple values allowed, each can be any value (input range is not restricted):
                        optionalCollectionTag.Add(new IDVLTAG(tagName, tagName, true, baseName, false));

                }
            }

            // Loop through ALL text files in given targetPath and convert blah.txt into various xml files as appropriate. 
            string[] sFilesToConvert = System.IO.Directory.GetFiles(targetPath, "*.txt");
            InputDataFileType whichType;

            // Also, the session details will be collected HERE based on collection accession (its unique ID, i.e., key).
            // The collection details will be collected HERE based on collection accession (its unique ID, i.e., key).
            // At the conclusion of file processing, then collection information from collection (if present) and sessions will be unioned together
            // and written to appropriate collectionName.collection.xml files.
            var collectionTaggedValues = new Dictionary<string, Dictionary<string, string>>();
            var collectionSessionValues = new Dictionary<string, List<XmlSession>>();

            var inputFileTypes = new Dictionary<int, InputDataFileType>(); // Identify each of the input files' "types" up front
            for (int iFile = 0; iFile < sFilesToConvert.Length; iFile++)
            {
                whichType = ClassifyInputFile(sFilesToConvert[iFile]);
                inputFileTypes.Add(iFile, whichType);
            }

            bool continueProcessing = true;
            // Pass 1: collection files.
            for (int iFile = 0; iFile < sFilesToConvert.Length && continueProcessing; iFile++)
            {
                if (inputFileTypes[iFile] == InputDataFileType.Collection)
                {
                    continueProcessing = ProcessCollectionInputFile(sFilesToConvert[iFile], expectedCollectionTag, optionalCollectionTag, collectionTaggedValues);
                    if (!continueProcessing)
                        OutputProblem("ERROR, STOPPING PROCESSING BECAUSE OF ERROR IN COLLECTION INPUT FILE.");
                }
            }
            // Pass 2: session files.
            for (int iFile = 0; iFile < sFilesToConvert.Length && continueProcessing; iFile++)
            {
                if (inputFileTypes[iFile] == InputDataFileType.Session)
                {
                    continueProcessing = ProcessSessionInputFile(sFilesToConvert[iFile], expectedSessionTag, optionalSessionTag, collectionSessionValues);
                    if (!continueProcessing)
                        OutputProblem("ERROR, STOPPING PROCESSING BECAUSE OF ERROR IN SESSION INPUT FILE.");
                }
            } // end of looping through input text files

            if (continueProcessing)
            {
                // Output any collectionName.collection.xml files based on data in collectionTaggedValues and collectionSessionValues.
                continueProcessing = ExportCollectionXMLFiles(targetPath, collectionTaggedValues, collectionSessionValues, portraitPathPrefix, optionalCollectionTag);
            }
            // Pass 3: movie segmentation files.
            for (int iFile = 0; iFile < sFilesToConvert.Length && continueProcessing; iFile++)
            {
                if (inputFileTypes[iFile] == InputDataFileType.MovieSegmentation)
                {
                    continueProcessing = ProcessMovieInputFile(sFilesToConvert[iFile], expectedMovieSegmentTag, optionalMovieSegmentTag,
                        minLegalSegmentSizeInSeconds, maxLegalSegmentSizeInSeconds, usingHistoryMakersConventions, videoPathPrefix, targetPath);
                    if (!continueProcessing)
                        OutputProblem("ERROR, STOPPING PROCESSING BECAUSE OF ERROR IN MOVIE INPUT FILE.");
                }
            }
        }

        /// <summary>
        /// Based on opening line of input file, classify into one of 3 types.
        /// </summary>
        /// <param name="sInputFile">input file</param>
        /// <returns>InputDataFileType enum value</returns>
        private InputDataFileType ClassifyInputFile(string sInputFile)
        {
            InputDataFileType whichType = InputDataFileType.MovieSegmentation;
            StreamReader sr = null;

            try
            {
                sr = new StreamReader(sInputFile);
                string sLine = sr.ReadLine();
                if (sLine != null)
                {
                    sLine = sLine.Trim();
                    if (sLine.StartsWith(MARKER_FOR_COLLECTION_AS_INPUT))
                        whichType = InputDataFileType.Collection;
                    else if (sLine.StartsWith(MARKER_FOR_SESSION_AS_INPUT))
                        whichType = InputDataFileType.Session;
                    // else keep as InputDataFileType.MovieSegmentation
                }
                sr.Close();
            }
            catch
            {
                whichType = InputDataFileType.MovieSegmentation;
            }

            return whichType;
        }

        private bool ProcessCollectionInputFile(string sInputFile, List<IDVLTAG> expectedTag, List<IDVLTAG> optionalTag, Dictionary<string, Dictionary<string, string>> allCollectionProcessedInput)
        {
            bool allOKWithProcessing = true;
            var inputFileBaseName = System.IO.Path.GetFileNameWithoutExtension(sInputFile);
            OutputStatusLine("*** Starting collection conversion of " + inputFileBaseName);

            // Collect value data for the keys indicated by *** lines, with a REQUIREMENT that ACCESSION_MARKER (or LENIENT_ACCESSION_MARKER) exists in the file.
            // The accession value is the key for allCollectionProcessedInput.  If that key already exists, give up.  If it doesn't exist,
            // store the collected value data into allCollectionProcessedInput[accession].
            // Collected value data is populated into the given dictionary via ComputeKeyValuePairsWithinCollectionInputFile call:
            var givenTaggedValues = new Dictionary<string, string>();
            allOKWithProcessing = ComputeKeyValuePairsWithinCollectionInputFile(sInputFile, inputFileBaseName, expectedTag, optionalTag, givenTaggedValues);
            if (allOKWithProcessing)
            {
                string givenCollectionName = givenTaggedValues[LENIENT_ACCESSION_MARKER];
                if (string.IsNullOrWhiteSpace(givenCollectionName))
                {
                    OutputProblem("ERROR: Throwing away input file " + inputFileBaseName + " because legal accession value not found.");
                    allOKWithProcessing = false;
                }
                else if (allCollectionProcessedInput.ContainsKey(givenCollectionName))
                {
                    OutputProblem("ERROR: Throwing away input file " + inputFileBaseName + " because its accession value is already present in another input file in this set.");
                    allOKWithProcessing = false;
                }
                else
                    // All is good, save the data.
                    allCollectionProcessedInput.Add(givenCollectionName, givenTaggedValues);
            }
            OutputStatusLine("*** Finished work with collection " + inputFileBaseName);
            return allOKWithProcessing;
        }

        private bool ProcessSessionInputFile(string sInputFile, List<IDVLTAG> expectedTag, List<IDVLTAG> optionalTag, Dictionary<string, List<XmlSession>> allCollectionSessionValues)
        {
            bool allOKWithProcessing = true;
            string givenCollectionName;
            var inputFileBaseName = System.IO.Path.GetFileNameWithoutExtension(sInputFile);
            OutputStatusLine("*** Starting session conversion of " + inputFileBaseName);

            // Collect value data for the keys indicated by *** lines, with a REQUIREMENT that ACCESSION_MARKER (or LENIENT_ACCESSION_MARKER) exists in the file.
            // Also, there is a requirement that SESSION_MARKER start the file.  These requirements enforced by expectedTag contents.
            // The accession value is the key for allCollectionSessionValues.  The key could very well already exist, but what should not yet exist is an 
            // entry in that list with the same session number as the given one in SESSION_MARKER.  If that happens, give up.  If it doesn't exist,
            // store the collected session value data into allCollectionSessionValues.
            // Collected session value data is populated into givenSessions via ComputeKeyValuePairsWithinCollectionInputFile call:
            XmlSession givenSessionInfo = ComputeKeyValuePairsWithinSessionInputFile(sInputFile, inputFileBaseName, expectedTag, optionalTag, out givenCollectionName);
            allOKWithProcessing = (givenSessionInfo != null && givenSessionInfo.SessionOrder > 0);
            if (allOKWithProcessing)
            {
                if (string.IsNullOrWhiteSpace(givenCollectionName))
                {
                    OutputProblem("ERROR: Throwing away input file " + inputFileBaseName + " because legal accession value not found.");
                    allOKWithProcessing = false;
                }
                else
                {
                    if (allCollectionSessionValues.ContainsKey(givenCollectionName)) {
                        // If the collection is there, then make sure this session is not already there.
                        for (int i = 0; i < allCollectionSessionValues[givenCollectionName].Count && allOKWithProcessing; i++)
                        {
                            if (allCollectionSessionValues[givenCollectionName][i].SessionOrder == givenSessionInfo.SessionOrder)
                            {
                                OutputProblem("ERROR: Throwing away input file " + inputFileBaseName + " because session order " + givenSessionInfo.SessionOrder + " already used in another session data file.");
                                allOKWithProcessing = false;
                            }
                        }
                    }
                    else
                        allCollectionSessionValues.Add(givenCollectionName, new List<XmlSession>());
                    if (allOKWithProcessing)
                        // All is good, save the data.
                        allCollectionSessionValues[givenCollectionName].Add(givenSessionInfo);
                }
            }
            OutputStatusLine("*** Finished work with session " + inputFileBaseName);
            return allOKWithProcessing;
        }

        private bool ProcessMovieInputFile(string sInputFile, List<IDVLTAG> expectedTag, List<IDVLTAG> optionalTag,
            int minAllowedSegmentLengthInSeconds, int maxAllowedSegmentLengthInSeconds, bool usingHistoryMakersConventions, string videoPathPrefix, string targetPath)
        {
            bool allOKWithProcessing = true;
            var inputFileBaseName = System.IO.Path.GetFileNameWithoutExtension(sInputFile);
            OutputStatusLine("*** Starting movie conversion of " + inputFileBaseName);

            // Two passes: one to test times and collect value data for the keys indicated by *** lines, the other to output new file.
            // Collected value data is populated into these 3 lists and one dictionary via ComputeKeyValuePairsWithinMovieInputFile call:
            var givenTaggedValues = new Dictionary<string, string>();
            var segmentStartTimes = new List<string>();
            var suppliedSegmentEndTimes = new List<string>();
            var segmentEndTimes = new List<string>();
            allOKWithProcessing = ComputeKeyValuePairsWithinMovieInputFile(sInputFile, inputFileBaseName, expectedTag, optionalTag,
                givenTaggedValues, segmentStartTimes, suppliedSegmentEndTimes, segmentEndTimes, minAllowedSegmentLengthInSeconds, maxAllowedSegmentLengthInSeconds);
            if (allOKWithProcessing)
            {
                allOKWithProcessing = ExportMovieXMLFileFromKeyValuePairs(usingHistoryMakersConventions, videoPathPrefix, targetPath, sInputFile, inputFileBaseName,
                    givenTaggedValues, segmentStartTimes, suppliedSegmentEndTimes, segmentEndTimes);
            }
            OutputStatusLine("*** Finished work with movie " + inputFileBaseName);
            return allOKWithProcessing;
        }

        /// <summary>
        /// Helper function to process one collection text input file: look for required and optional tags, and fill out 
        /// dictionary of tag-value pairs.
        /// </summary>
        /// <param name="sInputFile">input filename (for accessing input data)</param>
        /// <param name="sShortFilename">short version of input filename (for diagnostic messages)</param>
        /// <param name="expectedTag">list of tags that are expected; if not found, error report given</param>
        /// <param name="optionalTag">list of optional tags (parsed if found, but not necessary in input file)</param>
        /// <param name="givenTaggedValues">holds tag (both optional and required) as key, tag's value as the value in a string-string dictionary</param>
        /// <returns>true if all parses correctly (and Dictionary gets populated correctly), false otherwise</returns>
        private bool ComputeKeyValuePairsWithinCollectionInputFile(string sInputFile, string sShortFilename, List<IDVLTAG> expectedTag, List<IDVLTAG> optionalTag,
            Dictionary<string, string> givenTaggedValues)
        {
            StreamReader sr = null;

            string sLine = "";
            string sAccumulatedValue = "";
            int iOnePartition;
            bool bGiveUpEarly = false;
            bool bNextLineAlreadyCached = false;
            int iGivenTag;
            int iNextTag;
            bool matchedOptionalTag;
            bool matchedRequiredTag;
            string matchedReadableTag = "";
            string matchedLenientTag = "";
            string matchedOwnerWorldName = "";
            bool matchedValueMayAppearOnMultipleLines = false;
            bool partitionMatchIsFromRestrictedVocabulary = false;
            List<string> partitionNames = new List<string>();
            int iLineCounter = 0;

            try
            {
                sr = new StreamReader(sInputFile);
            }
            catch (Exception ex)
            {
                this.OutputProblem("Could not open input file " + sInputFile +
                     " so no action taken: " + ex.Message);
                bGiveUpEarly = true;
            }

            if (!bGiveUpEarly)
            {
                // Read opening line, which is just the ***COLLECTION marker that has already been checked to mark data file as a collection input file type.
                sLine = sr.ReadLine();
                iLineCounter++; // Count opening line.
            }

            while (!bGiveUpEarly)
            {
                if (!bNextLineAlreadyCached)
                {
                    sLine = sr.ReadLine();
                    iLineCounter++; // Count even blank lines.
                }
                bNextLineAlreadyCached = false;
                if (sLine == null)
                    break; // done, no more input

                sLine = sLine.Trim(); // trim off all whitespace (leading and trailing)
                matchedOptionalTag = false;
                matchedRequiredTag = false;
                if ((iGivenTag = TestLineAgainstLenientList(sLine, optionalTag)) >= 0)
                { // take values from optionalTag
                    matchedOptionalTag = true;
                    matchedReadableTag = optionalTag[iGivenTag].Readable;
                    matchedLenientTag = optionalTag[iGivenTag].Lenient;
                    matchedOwnerWorldName = optionalTag[iGivenTag].OwnerWorldName;
                    matchedValueMayAppearOnMultipleLines = optionalTag[iGivenTag].MultipleLinesOKToSpecifyValue;
                    partitionMatchIsFromRestrictedVocabulary = optionalTag[iGivenTag].InputRangeRestricted;
                }
                else if ((iGivenTag = TestLineAgainstLenientList(sLine, expectedTag)) >= 0)
                { // take values from expectedTag
                    matchedRequiredTag = true;
                    matchedReadableTag = expectedTag[iGivenTag].Readable;
                    matchedLenientTag = expectedTag[iGivenTag].Lenient;
                    matchedOwnerWorldName = expectedTag[iGivenTag].OwnerWorldName;
                    matchedValueMayAppearOnMultipleLines = expectedTag[iGivenTag].MultipleLinesOKToSpecifyValue;
                    partitionMatchIsFromRestrictedVocabulary = expectedTag[iGivenTag].InputRangeRestricted;
                }
                if (matchedOptionalTag || matchedRequiredTag)
                {
                    // Read next line, which is the value (at least in part) for this tag.  Grumble if no value found.
                    sLine = sr.ReadLine();
                    if (sLine == null)
                    {
                        this.OutputProblem(sShortFilename + " Problems: required info for tag " +
                            matchedReadableTag +
                            " was not given on the next line.");
                        bGiveUpEarly = true;
                    }
                    else
                    {
                        sLine = sLine.Trim(); // trim off all whitespace (leading and trailing)
                        iLineCounter++;
                        // See if this tag can have multiple line value specification
                        if (matchedValueMayAppearOnMultipleLines)
                        { // multiple line value specification is OK, e.g., a short description across multiple lines, or many partitions (one per line) for a world
                            sAccumulatedValue = "";
                            if (matchedOwnerWorldName.Length == 0)
                                sAccumulatedValue = sLine; // first line (there may be more)
                            else
                            { // Consider this as a WORLD and now collect PARTITION values, one per input line:
                                if (partitionMatchIsFromRestrictedVocabulary)
                                {
                                    // Check data as it might not be allowed.
                                    if (!PermissivePartitionLookup(matchedOwnerWorldName, sLine, partitionNames))
                                    {
                                        // Grumble, UNLESS this is a blank line in which case just eat blank line.
                                        if (sLine.Length > 0)
                                        {
                                            this.OutputProblem(sShortFilename + " Problems: this listed item on line " + iLineCounter +
                                                " was not in the understood list of items for " + matchedReadableTag + ": " + sLine);
                                            bGiveUpEarly = true;
                                        }
                                    }
                                }
                                else
                                {
                                    // All data is allowed.
                                    partitionNames.Clear();
                                    partitionNames.Add(sLine);
                                }
                                if (!bGiveUpEarly)
                                {
                                    if (partitionNames.Count > 0)
                                    {
                                        // Assemble partitions, using PARTITION_NAME_SEPARATOR as separator for the stored partition names
                                        sAccumulatedValue = partitionNames[0];
                                        for (iOnePartition = 1; iOnePartition < partitionNames.Count; iOnePartition++)
                                            sAccumulatedValue = sAccumulatedValue + PARTITION_NAME_SEPARATOR + partitionNames[iOnePartition];
                                    }
                                }
                            }
                            while (!bGiveUpEarly && (sLine = sr.ReadLine()) != null)
                            { // Next line is not empty; check for another tag or else keep accumulating value (or adding partitions to the world)
                                // Do not worry about parsing sLine into the next optional or required tag as we will stay on sLine in next iteration
                                // of containing while loop based on logic with bNextLineAlreadyCached; just note if parsing would pass (new tag found):
                                sLine = sLine.Trim();
                                iLineCounter++;
                                if ((iNextTag = TestLineAgainstLenientList(sLine, expectedTag)) >= 0)
                                { // OK, end of multiline value.  Store it, then mark that
                                    // we have read ahead the next tag.
                                    givenTaggedValues.Add(matchedLenientTag, sAccumulatedValue);
                                    bNextLineAlreadyCached = true;
                                    break; // found end of multi-line value, and the line for the next tag that delimits its end is cached
                                }
                                else if ((iNextTag = TestLineAgainstLenientList(sLine, optionalTag)) >= 0)
                                { // OK, end of multiline value.  Store it, then mark that
                                    // we have read ahead the next tag.
                                    givenTaggedValues.Add(matchedLenientTag, sAccumulatedValue);
                                    bNextLineAlreadyCached = true;
                                    break; // found end of multi-line value, and the line for the next tag that delimits its end is cached
                                }
                                else if (sLine.StartsWith(LENIENT_MARKER_PREFACE))
                                { // NOTE: Preface (e.g., perhaps "***") is such that it is never expected within a multi-line value; it did NOT
                                    // parse OK via TestLineAgainstLenientList for both optionalTag and expectedTag, but 
                                    // rather than lump line into sAccumulatedValue exit out here so line starting with
                                    // LENIENT_MARKER_PREFACE can then be flagged as being ignored (a different sort of failure).
                                    givenTaggedValues.Add(matchedLenientTag, sAccumulatedValue);
                                    bNextLineAlreadyCached = true;
                                    break; // found end of multi-line value, and the line for the next presumed but unparseable tag that delimits its end is cached
                                }
                                else
                                {
                                    if (matchedOwnerWorldName.Length > 0)
                                    { // accumulating partitions for an owning world
                                        if (partitionMatchIsFromRestrictedVocabulary)
                                        {
                                            // Check data as it might not be allowed.
                                            if (!PermissivePartitionLookup(matchedOwnerWorldName, sLine, partitionNames))
                                            {
                                                // Grumble, UNLESS this is a blank line in which case just eat blank line.
                                                if (sLine.Length > 0)
                                                {
                                                    this.OutputProblem(sShortFilename + " Problems: this listed item on line " + iLineCounter +
                                                        " was not in the understood list of items for " + matchedReadableTag + ": " + sLine);
                                                    bGiveUpEarly = true;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // All data is allowed.
                                            partitionNames.Clear();
                                            partitionNames.Add(sLine);
                                        }
                                        if (!bGiveUpEarly)
                                        { // Add to what we have already using PARTITION_NAME_SEPARATOR as separator for the stored partition names
                                            if (partitionNames.Count > 0)
                                            {
                                                // sAccumulatedValue already has contents from prior input data line(s)
                                                for (iOnePartition = 0; iOnePartition < partitionNames.Count; iOnePartition++)
                                                    sAccumulatedValue = sAccumulatedValue + PARTITION_NAME_SEPARATOR + partitionNames[iOnePartition];
                                            }
                                        }
                                    }
                                }
                            } // end of next line not empty loop
                            if (!bGiveUpEarly && !bNextLineAlreadyCached)
                            { // Reached end of input file: keep sLine == null and set givenTaggedValues and bNextLineAlreadyCached
                                givenTaggedValues.Add(matchedLenientTag, sAccumulatedValue);
                                bNextLineAlreadyCached = true;
                            }
                        } // end of processing multi-line values for an entry
                        else
                        {
                            // save the single-line value to givenTaggedValues
                            givenTaggedValues.Add(matchedLenientTag, sLine);
                        } // end of processing a value (perhaps multi-line value) for the found expectedTag[iGivenTag] or optionalTag[iGivenTag]
                    } // end of allowable processing for collection details
                } // end of finding a legal tag expectedTag[iGivenTag] or optionalTag[iGivenTag]
            } // looping through input file until "break" on end-of-data, or bGiveUpEarly

            if (sr != null)
                sr.Close();

            if (!bGiveUpEarly)
            {
                // Make sure all required tags found.
                for (int iTag = 0; iTag < expectedTag.Count && !bGiveUpEarly; iTag++)
                {
                    if (!givenTaggedValues.ContainsKey(expectedTag[iTag].Lenient))
                    {
                        this.OutputProblem(sShortFilename + " PROBLEMS: required tag " +
                            expectedTag[iTag].Readable +
                                    " was not given on one line with its data on the next.");
                        bGiveUpEarly = true;
                    }
                }
            }
            return !bGiveUpEarly;
        }

        /// <summary>
        /// Helper function to process one session text input file: look for required and optional tags, and return single session details.
        /// </summary>
        /// <param name="sInputFile">input filename (for accessing input data)</param>
        /// <param name="sShortFilename">short version of input filename (for diagnostic messages)</param>
        /// <param name="expectedTag">list of tags that are expected; if not found, error report given</param>
        /// <param name="optionalTag">list of optional tags (parsed if found, but not necessary in input file)</param>
        /// <param name="parentCollectionIdentifier">returns the value of the parent identifier, or "" if not found</param>
        /// <returns>session details filled in (along with parentCollectionIdentifier in an out parameter), or set to SessionOrder == 0 on error</returns>
        private XmlSession ComputeKeyValuePairsWithinSessionInputFile(string sInputFile, string sShortFilename, List<IDVLTAG> expectedTag, List<IDVLTAG> optionalTag, out string parentCollectionIdentifier)
        {
            XmlSession retVal = new XmlSession();
            retVal.SessionOrder = 0; // should get filled in with positive value later in tag processing....
            parentCollectionIdentifier = ""; // should get filled in with actual value later in tag processing....

            StreamReader sr = null;

            string sLine = "";
            bool bGiveUpEarly = false;
            int iGivenTag;
            bool matchedOptionalTag;
            bool matchedRequiredTag;
            string matchedReadableTag = "";
            string matchedLenientTag = "";
            int iLineCounter = 0;
            var foundExpectedTags = new List<string>();

            try
            {
                sr = new StreamReader(sInputFile);
            }
            catch (Exception ex)
            {
                this.OutputProblem("Could not open input file " + sInputFile +
                     " so no action taken: " + ex.Message);
                bGiveUpEarly = true;
            }

            if (!bGiveUpEarly)
            {
                // Read opening line, which is just the ***SESSIONINFO marker that has already been checked to mark data file as a session input file type.
                sLine = sr.ReadLine();
                iLineCounter++; // Count opening line.
            }

            while (!bGiveUpEarly)
            {
                sLine = sr.ReadLine();
                iLineCounter++; // Count even blank lines.
                if (sLine == null)
                    break; // done, no more input

                sLine = sLine.Trim(); // trim off all whitespace (leading and trailing)
                matchedOptionalTag = false;
                matchedRequiredTag = false;
                if ((iGivenTag = TestLineAgainstLenientList(sLine, optionalTag)) >= 0)
                { // take values from optionalTag
                    matchedOptionalTag = true;
                    matchedReadableTag = optionalTag[iGivenTag].Readable;
                    matchedLenientTag = optionalTag[iGivenTag].Lenient;
                }
                else if ((iGivenTag = TestLineAgainstLenientList(sLine, expectedTag)) >= 0)
                { // take values from expectedTag
                    matchedRequiredTag = true;
                    matchedReadableTag = expectedTag[iGivenTag].Readable;
                    matchedLenientTag = expectedTag[iGivenTag].Lenient;
                }
                if (matchedOptionalTag || matchedRequiredTag)
                {
                    // Read next line, which is the value for this tag.  Grumble if no value found.
                    sLine = sr.ReadLine();
                    if (sLine == null)
                    {
                        this.OutputProblem(sShortFilename + " Problems: required info for tag " + matchedReadableTag + " was not given on the next line.");
                        bGiveUpEarly = true;
                    }
                    else
                    {
                        sLine = sLine.Trim(); // trim off all whitespace (leading and trailing)
                        iLineCounter++;
                        
                        if (matchedRequiredTag)
                            // Note we have match to required tag.
                            foundExpectedTags.Add(matchedLenientTag);

                        // Map required and optional tags to values in the session object.
                        switch (matchedLenientTag)
                        {
                            case LENIENT_ACCESSION_MARKER:
                                parentCollectionIdentifier = sLine;
                                break;
                            case SESSION_MARKER:
                                retVal.SessionOrder = int.Parse(sLine);
                                break;
                            case INTERVIEWER_MARKER:
                                retVal.Interviewer = sLine;
                                break;
                            case INTERVIEWLOCATION_MARKER:
                                retVal.Location = sLine;
                                break;
                            case INTERVIEWDATE_MARKER:
                                retVal.InterviewDate = DateTime.Parse(sLine);
                                break;
                            case VIDEOGRAPHER_MARKER:
                                retVal.Videographer = sLine;
                                break;
                            case SPONSOR_MARKER:
                                retVal.Sponsor = sLine;
                                break;
                            case SPONSOR_IMAGE_MARKER:
                                retVal.SponsorImagePath = sLine;
                                break;
                            case SPONSOR_URL_MARKER:
                                retVal.SponsorURL = sLine;
                                break;
                        } // end of tag value assignment to retVal or parentCollectionIdentifier
                    } // end of non-empty tag value processing
                } // end of processing a value for the found expectedTag[iGivenTag] or optionalTag[iGivenTag]
            } // looping through input file until "break" on end-of-data, or bGiveUpEarly

            if (sr != null)
                sr.Close();

            if (!bGiveUpEarly)
            {
                // Make sure all required tags found.
                for (int iTag = 0; iTag < expectedTag.Count && !bGiveUpEarly; iTag++)
                {
                    if (!foundExpectedTags.Contains(expectedTag[iTag].Lenient))
                    {
                        this.OutputProblem(sShortFilename + " PROBLEMS: required tag " +
                            expectedTag[iTag].Readable +
                                    " was not given on one line with its data on the next.");
                        bGiveUpEarly = true;
                    }
                }
            }

            if (bGiveUpEarly)
            {
                // Let caller know that we're giving up by setting SessionOrder to 0.
                retVal.SessionOrder = 0;
            }

            return retVal;
        }

        /// <summary>
        /// Helper function to processing one text input file: look for required and optional tags, and fill out 
        /// dictionary of tag-value pairs, and also segment start times, supplied end times, and computed end times,
        /// with error checking on story segment length controlled by given min/max limits.
        /// </summary>
        /// <param name="sInputFile">input filename (for accessing input data)</param>
        /// <param name="sShortFilename">short version of input filename (for diagnostic messages)</param>
        /// <param name="expectedTag">list of tags that are expected; if not found, error report given</param>
        /// <param name="optionalTag">list of optional tags (parsed if found, but not necessary in input file)</param>
        /// <param name="givenTaggedValues">holds tag (both optional and required) as key, tag's value as the value in a string-string dictionary</param>
        /// <param name="segmentStartTimes">segment start time list</param>
        /// <param name="suppliedSegmentEndTimes">as specified in input file, given segment end time list: may hold empty strings if end times not given in input file), used to later
        /// parse whether input file gives 2 numbers (start and end) or just one (start) for segment times</param>
        /// <param name="segmentEndTimes">computed end times for each segment</param>
        /// <param name="minAllowedSegmentLengthInSeconds">enforced minimum length on story segment in seconds (always greater than zero)</param>
        /// <param name="maxAllowedSegmentLengthInSeconds">maximum allowed length of story in seconds, or zero if no limit enforced</param>
        /// <returns>true if all parses correctly (and Dictionary and 3 segment time lists get populated correctly), false otherwise</returns>
        private bool ComputeKeyValuePairsWithinMovieInputFile(string sInputFile, string sShortFilename, List<IDVLTAG> expectedTag, List<IDVLTAG> optionalTag,
            Dictionary<string, string> givenTaggedValues,
            List<string> segmentStartTimes, List<string> suppliedSegmentEndTimes, List<string> segmentEndTimes,
            int minAllowedSegmentLengthInSeconds, int maxAllowedSegmentLengthInSeconds)
        {
            StreamReader sr = null;

            string sLine = "";
            string sCurrentSegStartTime = "";
            string sOptionalCurrentSegEndTime = "";
            string sPriorSegEndTime = "";
            string sErrMsg = "";
            double dCurrentTimeAsSeconds = 0.0;
            double dPriorTimeAsSeconds = 0.0;
            double dGivenEndTimeAsSeconds = 0.0;
            bool bGiveUpEarly = false;
            int iGivenTag;
            bool startedProcessingStoryItems = false;  // set to true once first ***STORY input line encountered
            bool matchedOptionalTag;
            bool matchedRequiredTag;
            string matchedReadableTag = "";
            string matchedLenientTag = "";
            int iLineCounter = 0;
            string sCleanHHMSSFFTime;
            bool foundEndNowMarker = false; // set to true iff input file lists a MARKER_FOR_END_OF_TRANSCRIPT_INPUT line

            try
            {
                sr = new StreamReader(sInputFile);
            }
            catch (Exception ex)
            {
                this.OutputProblem("Could not open input file " + sInputFile +
                     " so no action taken: " + ex.Message);
                bGiveUpEarly = true;
            }

            if (!bGiveUpEarly)
            {
                // Read opening line, which is just the ***MOVIE marker that is presumed to mark data file as a movie input file type.
                sLine = sr.ReadLine().Trim();
                iLineCounter++; // Count opening line.
                if (!sLine.StartsWith(MARKER_FOR_MOVIE_AND_SEGMENTS_AS_INPUT))
                    OutputProblem("WARNING First line ignored; first line of movie input file should be " + MARKER_FOR_MOVIE_AND_SEGMENTS_AS_INPUT);
            }

            while (!bGiveUpEarly && !foundEndNowMarker)
            {
                sLine = sr.ReadLine();
                iLineCounter++; // Count even blank lines.

                if (sLine == null || sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                    break; // done, no more input

                sLine = sLine.Trim(); // trim off all whitespace (leading and trailing)
                matchedOptionalTag = false;
                matchedRequiredTag = false;
                if (!startedProcessingStoryItems && (iGivenTag = TestLineAgainstLenientList(sLine, optionalTag)) >= 0)
                { // take values from optionalTag
                    matchedOptionalTag = true;
                    matchedReadableTag = optionalTag[iGivenTag].Readable;
                    matchedLenientTag = optionalTag[iGivenTag].Lenient;
                }
                else if (!startedProcessingStoryItems && (iGivenTag = TestLineAgainstLenientList(sLine, expectedTag)) >= 0)
                { // take values from expectedTag
                    matchedRequiredTag = true;
                    matchedReadableTag = expectedTag[iGivenTag].Readable;
                    matchedLenientTag = expectedTag[iGivenTag].Lenient;
                }
                if (matchedOptionalTag || matchedRequiredTag)
                {
                    // Read next line, which is the value for this tag.  Grumble if no value found.
                    sLine = sr.ReadLine();
                    if (sLine == null)
                    {
                        this.OutputProblem(sShortFilename + " Problems: required info for tag " +
                            matchedReadableTag +
                            " was not given on the next line.");
                        bGiveUpEarly = true;
                    }
                    else
                    {
                        sLine = sLine.Trim(); // trim off all whitespace (leading and trailing)
                        iLineCounter++;
                        givenTaggedValues.Add(matchedLenientTag, sLine);
                    } // end of processing a value for the found expectedTag[iGivenTag] or optionalTag[iGivenTag]
                } // end of finding a legal tag expectedTag[iGivenTag] or optionalTag[iGivenTag]
                else
                { // allow parsing to repeated story blocks at end of input text
                    if (sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                        foundEndNowMarker = true; // do not output the ***END as part of the transcript; do not really consider it until we are processing story transcripts
                    else if (sLine.Length > 0)
                    {
                        if (sLine.StartsWith(STORY_MARKER))
                        {
                            if (!startedProcessingStoryItems)
                                // Also, note that we have started ***STORY processing in the input file at large
                                startedProcessingStoryItems = true;

                            // Required story starting time is on the next line.
                            sCurrentSegStartTime = sr.ReadLine();
                            iLineCounter++;
                            if (sCurrentSegStartTime == null)
                            {
                                this.OutputProblem(sShortFilename + " PROBLEMS: segment time expected but not found on line " +
                                    iLineCounter);
                                bGiveUpEarly = true;
                            }
                            else
                            {
                                // See if there is an end time listed for this segment.  If so, use it.
                                sOptionalCurrentSegEndTime = sr.ReadLine();
                                sOptionalCurrentSegEndTime = sOptionalCurrentSegEndTime.Trim();
                                iLineCounter++;
                                if (sOptionalCurrentSegEndTime == null)
                                {
                                    this.OutputProblem(sShortFilename + " PROBLEMS: segment ended early without a title or transcript on line " +
                                        iLineCounter);
                                    bGiveUpEarly = true;
                                }

                                if (!bGiveUpEarly)
                                {
                                    sCurrentSegStartTime = sCurrentSegStartTime.Trim();
                                    dCurrentTimeAsSeconds = TimeAsSeconds(sCurrentSegStartTime);
                                    if (dCurrentTimeAsSeconds < 0)
                                    {
                                        this.OutputProblem(sShortFilename + " PROBLEMS: segment time not understood on line " +
                                            iLineCounter + ", time " + sCurrentSegStartTime);
                                        bGiveUpEarly = true;
                                    }
                                    else
                                    {
                                        // One of four cases:
                                        // (1) sOptionalCurrentSegEndTime is a time, title is on next line and "***TRANSCRIPT" marker line after that;
                                        // (2) title is in sOptionalCurrentSegEndTime rather than a time, with "***TRANSCRIPT" on next line;
                                        // (3) input is malformed: we have non-parsing time in sOptionalCurrentSegEndTime followed by title and "***TRANSCRIPT"; 
                                        // (4) no "***TRANSCRIPT" within two lines
                                        //
                                        // In all success cases, we read past both title and ***TRANSCRIPT line.
                                        sLine = sr.ReadLine();
                                        iLineCounter++;
                                        if (sLine == null || sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                                        { // Case (4): error
                                            this.OutputProblem(sShortFilename + " Line " + iLineCounter + ": Expected " + TRANSCRIPT_MARKER + " marker not found.");
                                            bGiveUpEarly = true;
                                        }
                                        else if (sLine.StartsWith(TRANSCRIPT_MARKER))
                                            // Case (2), so dismiss sOptionalCurrentSegEndTime as the title
                                            sOptionalCurrentSegEndTime = "";
                                        else
                                        {
                                            // Need to read one more line and it better be "***TRANSCRIPT"
                                            sLine = sr.ReadLine();
                                            iLineCounter++;
                                            if (sLine == null || !sLine.StartsWith(TRANSCRIPT_MARKER))
                                            { // Case (4): error
                                                this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": Expected " + TRANSCRIPT_MARKER + " marker not found.");
                                                bGiveUpEarly = true;
                                            }
                                        }
                                        if (!bGiveUpEarly)
                                        {
                                            if (sOptionalCurrentSegEndTime.Length > 0)
                                            {
                                                dGivenEndTimeAsSeconds = TimeAsSeconds(sOptionalCurrentSegEndTime);
                                                if (dGivenEndTimeAsSeconds < 0)
                                                { // Case (3): error
                                                    this.OutputProblem(sShortFilename + " PROBLEMS: segment time not understood on line " +
                                                        iLineCounter + ", time " + sOptionalCurrentSegEndTime);
                                                    bGiveUpEarly = true;
                                                }
                                                else if (dGivenEndTimeAsSeconds <= dCurrentTimeAsSeconds)
                                                { // Case (3): error
                                                    this.OutputProblem(sShortFilename + " PROBLEMS: segment time would cause segment duration to be at or less than zero on line " +
                                                        iLineCounter + ", time " + sOptionalCurrentSegEndTime);
                                                    bGiveUpEarly = true;
                                                }
                                                else
                                                { // Case (1) success
                                                    if (PaddedTimeAsHHMMSSFF(sOptionalCurrentSegEndTime, out sCleanHHMSSFFTime, out sErrMsg))
                                                    {
                                                        suppliedSegmentEndTimes.Add(sCleanHHMSSFFTime);
                                                    }
                                                    else
                                                    {
                                                        this.OutputProblem(sShortFilename + " PROBLEMS: Before line " + iLineCounter + ": Segment 'end time' " + sOptionalCurrentSegEndTime + " caused processing error: " + sErrMsg);
                                                        bGiveUpEarly = true;
                                                    }
                                                }
                                            }
                                            else
                                            { // remainder of Case (2): no end time given
                                                suppliedSegmentEndTimes.Add(""); // marks that supplied end time for this segment is empty 
                                            }
                                        }

                                        if (segmentStartTimes.Count > 0)
                                        {   // There is a prior segment (look to segmentStartTimes in "if" test because it is updated 
                                            // AFTER this check, and so will have count == 0 on first iteration while suppliedSegmentEndTimes
                                            // already has an item from above work).  No errors below because
                                            // suppliedSegmentEndTimes.Count will be greater than segmentStartTimes.Count
                                            if (suppliedSegmentEndTimes[segmentStartTimes.Count - 1].Length == 0)
                                            { // Prior segment does not have a supplied end time, so set its 
                                                // end to just before this current time.
                                                if (dCurrentTimeAsSeconds < dPriorTimeAsSeconds)
                                                {
                                                    // Log warning, and give up.
                                                    this.OutputProblem(sShortFilename + " PROBLEMS: segment timing is not greater than prior segment at line " +
                                                        iLineCounter + ", time " + sCurrentSegStartTime);
                                                    bGiveUpEarly = true;
                                                }
                                                else if (dCurrentTimeAsSeconds < dPriorTimeAsSeconds + minAllowedSegmentLengthInSeconds)
                                                {
                                                    // Log warning, and give up.
                                                    this.OutputProblem(sShortFilename + " PROBLEMS: segment timing is too close; prior segment will be less than " +
                                                        minAllowedSegmentLengthInSeconds + " secs. long, at line " + iLineCounter +
                                                        ", time " + sCurrentSegStartTime);
                                                    bGiveUpEarly = true;
                                                }
                                                else if (maxAllowedSegmentLengthInSeconds > 0 && dCurrentTimeAsSeconds > dPriorTimeAsSeconds + maxAllowedSegmentLengthInSeconds)
                                                {
                                                    // Log warning, and give up.
                                                    this.OutputProblem(sShortFilename + " PROBLEMS: segment timing is too far apart; prior segment will be more than " +
                                                        maxAllowedSegmentLengthInSeconds + " secs. long, TOO LONG, at line " + iLineCounter +
                                                        ", time " + sCurrentSegStartTime);
                                                    bGiveUpEarly = true;
                                                }
                                                else
                                                {
                                                    // Last time of prior segment == 1 less than start time of this segment.
                                                    if (TimeDecrementByOneFrame(sCurrentSegStartTime, out sPriorSegEndTime, out sErrMsg))
                                                    {
                                                        segmentEndTimes.Add(sPriorSegEndTime);
                                                    }
                                                    else
                                                    {
                                                        this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": Segment time processing error: " + sErrMsg);
                                                        bGiveUpEarly = true;
                                                    }
                                                }
                                            }
                                            else
                                            { // Use what is given in slot segmentStartTimes.Count - 1 without concern 
                                                // as we tested it before suppliedSegmentEndTimes assignment.
                                                segmentEndTimes.Add(suppliedSegmentEndTimes[segmentStartTimes.Count - 1]);
                                            }
                                        }
                                        if (!bGiveUpEarly)
                                        {
                                            dPriorTimeAsSeconds = dCurrentTimeAsSeconds;
                                            if (PaddedTimeAsHHMMSSFF(sCurrentSegStartTime, out sCleanHHMSSFFTime, out sErrMsg))
                                            {
                                                segmentStartTimes.Add(sCleanHHMSSFFTime);
                                            }
                                            else
                                            {
                                                this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": Segment time processing error: " + sErrMsg);
                                                bGiveUpEarly = true;
                                            }
                                        } // end processing of confirmed legal story segment data
                                    } // end processing of segment data
                                } // found segment data, too
                            } // found segment time for story
                        } // processing ***STORY marker
                        else if (!bGiveUpEarly && !startedProcessingStoryItems)
                        {
                            this.OutputProblem(sShortFilename + " WARNING, Line Ignored: " + sLine);
                            // Continue processing rest of input file.
                        }
                    } // non-empty line length
                } // only OK clause with startedProcessingStoryItems
            } // looping through input file until "break" on end-of-data, or bGiveUpEarly

            if (sr != null)
                sr.Close();

            if (!bGiveUpEarly)
            {
                // Make sure all required tags found.
                for (int iTag = 0; iTag < expectedTag.Count && !bGiveUpEarly; iTag++)
                {
                    if (!givenTaggedValues.ContainsKey(expectedTag[iTag].Lenient))
                    {
                        this.OutputProblem(sShortFilename + " PROBLEMS: required tag " +
                            expectedTag[iTag].Readable +
                                    " was not given on one line with its data on the next.");
                        bGiveUpEarly = true;
                    }
                }
            }
            return !bGiveUpEarly;
        }

        /// <summary>
        /// Given two sets of data keyed by collection IDs, use the data to output collection files.
        /// </summary>
        /// <param name="targetPath">target path for output</param>
        /// <param name="allCollectionData">Tags and values indexed by collection ID</param>
        /// <param name="allCollectionSessionValues">A list of sessions indexed by collection ID</param>
        /// <param name="portraitPathPrefix">path to collection portrait imagery, ignored if empty string</param>
        /// <param name="optionalTag">optional tags used to fill out givenTaggedValues, passed in here so that the optional worlds can be parsed to see if any partitions specified,
        /// e.g., if there is a job type world, were any jobs specified?</param>
        /// <returns>true if export worked, false otherwise</returns>
        private bool ExportCollectionXMLFiles(string targetPath, Dictionary<string, Dictionary<string, string>> allCollectionData, Dictionary<string, List<XmlSession>> allCollectionSessionValues, string portraitPathPrefix, List<IDVLTAG> optionalTag)
        {
            bool bGiveUpEarly = false;
            string fieldVal;
            Dictionary<string, string> tagList;

            foreach (string collectionName in allCollectionData.Keys)
            {
                tagList = allCollectionData[collectionName];

                // Create a new collection object.
                XmlCollection collection = new XmlCollection();
                // Populate it with results from the givenTaggedValues dictionary.
                collection.Accession = collectionName;
                collection.LastName = tagList[LENIENT_LASTNAME_MARKER];
                collection.PreferredName = tagList[LENIENT_PREFERREDNAME_MARKER];
                collection.Gender = this.GenderFromString(tagList[LENIENT_GENDER_MARKER].ToLower());
                try
                {
                    collection.BirthDate = DateTime.Parse(tagList[LENIENT_BIRTHDATE_MARKER]);
                }
                catch
                {
                    OutputProblem("Collection " + collectionName + " WARNING: Could not interpret value for " + BIRTHDATE_MARKER +
                        " as a full mm/dd/yyyy date, so setting it to null; given value was " + tagList[LENIENT_BIRTHDATE_MARKER]);
                    collection.BirthDate = null;
                }

                if (portraitPathPrefix != null && portraitPathPrefix.Length > 0)
                    collection.PortraitPath = portraitPathPrefix + System.IO.Path.DirectorySeparatorChar + tagList[PORTRAIT_MARKER];
                else
                    collection.PortraitPath = tagList[PORTRAIT_MARKER];

                if (tagList.ContainsKey(SHORTDESC_MARKER))
                {
                    fieldVal = tagList[SHORTDESC_MARKER];
                    if (fieldVal.Length > MAX_LENGTH_FOR_SHORT_DESCRIPTION)
                    {
                        this.OutputProblem("Collection " + collectionName + " WARNING: Given short description has " + fieldVal.Length + " chars., only " + MAX_LENGTH_FOR_SHORT_DESCRIPTION + " allowed.");
                        fieldVal = fieldVal.Substring(0, MAX_LENGTH_FOR_SHORT_DESCRIPTION);
                    }
                    collection.DescriptionShort = fieldVal;
                }
                if (tagList.ContainsKey(SHORTBIO_MARKER))
                {
                    fieldVal = tagList[SHORTBIO_MARKER];
                    if (fieldVal.Length > MAX_LENGTH_FOR_SHORT_BIOGRAPHY)
                    {
                        this.OutputProblem("Collection " + collectionName + " WARNING: Given short biography has " + fieldVal.Length + " chars., only " + MAX_LENGTH_FOR_SHORT_BIOGRAPHY + " allowed.");
                        fieldVal = fieldVal.Substring(0, MAX_LENGTH_FOR_SHORT_BIOGRAPHY);
                    }
                    collection.BiographyShort = fieldVal;
                }
                if (tagList.ContainsKey(LENIENT_DECEASEDDATE_MARKER)) {
                    try
                    {
                        collection.DeceasedDate = DateTime.Parse(tagList[LENIENT_DECEASEDDATE_MARKER]);
                    }
                    catch
                    {
                        OutputProblem("Collection " + collectionName + " WARNING: Could not interpret value for " + DECEASEDDATE_MARKER +
                            " as a full mm/dd/yyyy date, so setting it to null; given value was " + tagList[LENIENT_DECEASEDDATE_MARKER]);
                        collection.DeceasedDate = null;
                    }
                }
                if (tagList.ContainsKey(FIRSTNAME_MARKER))
                {
                    collection.FirstName = tagList[FIRSTNAME_MARKER];
                }
                if (tagList.ContainsKey(URL_MARKER))
                {
                    collection.WebsiteURL = tagList[URL_MARKER];
                }
                if (tagList.ContainsKey(REGION_MARKER))
                {
                    collection.Region = tagList[REGION_MARKER];
                }
                if (tagList.ContainsKey(BIRTHCITY_MARKER))
                {
                    collection.BirthCity = tagList[BIRTHCITY_MARKER];
                }
                if (tagList.ContainsKey(BIRTHSTATE_MARKER))
                {
                    collection.BirthState = tagList[BIRTHSTATE_MARKER];
                }
                if (tagList.ContainsKey(BIRTHCOUNTRY_MARKER))
                {
                    collection.BirthCountry = tagList[BIRTHCOUNTRY_MARKER];
                }

                if (allCollectionSessionValues.ContainsKey(collectionName))
                    // Session data has been given for this collection.
                    collection.Sessions = allCollectionSessionValues[collectionName];

                // Collection Annotations are open-ended items, such as: 
                // ***FAVORITE FOOD
                // Lasagna
                collection.Annotations = GetCollectionAnnotations(tagList, optionalTag);

                // Collection Partitions are closed range items with many possible per world, such as: 
                // ***JOB TYPE
                // Manager
                // Teacher
                collection.Partitions = GetCollectionPartitions(tagList, optionalTag);

                try
                {
                    // Serialize the results to file.
                    XmlUtilities.Write<XmlCollection>(collection, Path.Combine(targetPath, GetSafeFilename(collection.Accession) + ".collection.xml"));
                }
                catch
                {
                    this.OutputProblem("WARNING: probably multiple specifications for collection, instead of just one, and so did not (re)produce " + GetSafeFilename(collection.Accession) + ".collection.xml.");
                }
            }
            foreach (string collectionName in allCollectionSessionValues.Keys)
            {
                if (!allCollectionData.ContainsKey(collectionName))
                {
                    // Process segment information that does not have any accompanying collection information, aside from the given collection ID (accession) that is its key.
                    // Create a new collection object.
                    XmlCollection collection = new XmlCollection();
                    // Populate it with just the sessions (and its accession key).
                    collection.Accession = collectionName;
                    collection.Sessions = allCollectionSessionValues[collectionName];
                    try
                    {
                        // Serialize the results to file.
                        XmlUtilities.Write<XmlCollection>(collection, Path.Combine(targetPath, GetSafeFilename(collection.Accession) + ".collection.xml"));
                    }
                    catch
                    {
                        this.OutputProblem("WARNING: probably multiple specifications for collection, instead of just one, and so did not (re)produce " + GetSafeFilename(collection.Accession) + ".collection.xml.");
                    }
                }
            }
            return !bGiveUpEarly;
        }

        /// <summary>
        /// Given a dictionary of tag-value entries and lists of segment times, output XML details for the movie (and its segments) being described.
        /// </summary>
        /// <param name="usingHistoryMakersConventions">if true, video filename assemblage is via different rules as used by The HistoryMakers</param>
        /// <param name="videoPathPrefix">path to movie videos, ignored if empty string</param>
        /// <param name="targetPath">target path for output</param>
        /// <param name="givenInputFilename">input file to process</param>
        /// <param name="sShortFilename">short version of input filename (for diagnostic messages)</param>
        /// <param name="givenTaggedValues">most important list of tags and their values</param>
        /// <param name="segmentStartTimes">story start times</param>
        /// <param name="suppliedSegmentEndTimes">input file-specified story end times, or empty string if no end time given for an entry (defaulting to end just before next segment starts)</param>
        /// <param name="segmentEndTimes">story end times</param>
        /// <returns>true on successful XML output, false otherwise</returns>
        private bool ExportMovieXMLFileFromKeyValuePairs(bool usingHistoryMakersConventions, string videoPathPrefix, string targetPath, string givenInputFilename, 
            string sShortFilename, Dictionary<string, string> givenTaggedValues, 
            List<string> segmentStartTimes, List<string> suppliedSegmentEndTimes, List<string> segmentEndTimes)
        {
            const string VIDEO_FILENAME_EXTENSION = ".flv";

            bool bGiveUpEarly = false;

            // In this pass, output XML (earlier pass did segment time arithmetic checks and required tag presence checks on input file).

            string givenCollectionName = "";
            int sessionOrder = 0;
            int tapeOrder = 0;

            try
            {
                // NOTE: need to have collection name and valid session and tape orders (numbers)
                givenCollectionName = givenTaggedValues[LENIENT_ACCESSION_MARKER];
                sessionOrder = int.Parse(givenTaggedValues[SESSION_MARKER]);
                tapeOrder = int.Parse(givenTaggedValues[TAPE_MARKER]);
            }
            catch
            {
                bGiveUpEarly = true;
            }
            if (!bGiveUpEarly)
            {
                // Make sure we have valid session and tape orders.
                if (sessionOrder <= 0 || tapeOrder <= 0)
                    bGiveUpEarly = true;
            }

            if (bGiveUpEarly)
            {
                OutputProblem(sShortFilename + "ERROR: could not get accession, session order >= 0 and tapeOrder >= 0 from data, so this movie data IGNORED.");
                return false; // let caller know that all is not good....
            }

            // Output movie-segments info
            string fieldVal;

            // Check on funny burned-in timecode to actual offset time computation adjustments for this movie
            string sInitialTimecode = ""; // defaults to empty string (not specified nor needed)
            Single fGivenFrameRate = 0; // defaults to 0 (not given)
            bool isNonDropTimecode = false; // defaults to false (timecode assumed to be drop time code for 29.97 fps playback rate)
            bool timingAdjustmentNecessary = false;
            double initialTimeOffset = 0;
            double correctiveFPSFactor = 1;
            bool fixTheFrameRateIssue = false; // set to true depending on both fGivenFrameRate and isNonDropTimecode
            try
            { // Handle optional items for timecode and frame rate adjustments on segment offset values
                if (givenTaggedValues.ContainsKey(INITIAL_TIMECODE_MARKER))
                {
                    sInitialTimecode = givenTaggedValues[INITIAL_TIMECODE_MARKER];
                }
                if (givenTaggedValues.ContainsKey(NONDROP_TIMECODE_IN_USE_MARKER))
                {
                    isNonDropTimecode = (givenTaggedValues[NONDROP_TIMECODE_IN_USE_MARKER] == "1");
                }
                if (givenTaggedValues.ContainsKey(PLAYBACK_FRAME_RATE_MARKER))
                {
                    fGivenFrameRate = Single.Parse(givenTaggedValues[PLAYBACK_FRAME_RATE_MARKER]);
                }
            }
            catch (Exception ex)
            {
                this.OutputProblem(sShortFilename + " Problems in dealing with optional initial timecode, nondrop timecode in use, and/or frame rate values: " + ex.Message);
                bGiveUpEarly = true;
            }

            if (!bGiveUpEarly && (sInitialTimecode != "" || isNonDropTimecode))
            {
                if (fGivenFrameRate == 0)
                { // NOTE:  if either INITIAL_TIMECODE_MARKER or NONDROP_TIMECODE_IN_USE_MARKER
                    // (or both) given, then MUST also use PLAYBACK_FRAME_RATE_MARKER):

                    this.OutputProblem(sShortFilename + " Missing REQUIRED frame rate " + PLAYBACK_FRAME_RATE_MARKER +
                        " marker (required since you specified a timecode or nondrop timecode in use marker).");
                    bGiveUpEarly = true;
                }
                else
                { // Determine if we really need the given offset and/or nondrop timecode information
                    // to modify the segment start and end offsets.

                    const float NTSC_FRAME_RATE = 29.97f;
                    const float NTSC_FRAME_RATE_CLOSE_UPPER_BOUND = 29.98f;
                    const float FRAME_RATE_OF_30 = 30.0f;

                    if ((fGivenFrameRate >= NTSC_FRAME_RATE && fGivenFrameRate <= NTSC_FRAME_RATE_CLOSE_UPPER_BOUND) && isNonDropTimecode)
                    { // Have video at 29.97 (or close enough) NTSC playback rate, but timecodes at 30 fps
                        // So, if initial offset timecode were 1:00:00:00 (1 hour), it would really be at 30*60*60=108,000 frames
                        // which at 29.97 fps is at 1 hour and 3.6 seconds into the video, not 1 hour into the video.
                        // Correct all segment offset values given from reading the timecode at 30 fps which should have been
                        // timecode at 29.97 fps by multiplying the value first by correctiveFPSFactor.
                        // ALSO, correct any initial time offset by also multiplying it by this corrective FPS factor.
                        correctiveFPSFactor = FRAME_RATE_OF_30 / NTSC_FRAME_RATE; // e.g., a given time of 1:00:00:00 is not really 1 hour in, but 1 hr 3.6 seconds in
                        fixTheFrameRateIssue = true;
                    }
                    else if (fGivenFrameRate == FRAME_RATE_OF_30 && !isNonDropTimecode)
                    { // Very unlikely: Have video at 30 fps but timecode is using drop frame encoding for 29.97
                        // At timecode 1:00:00:00 (1 hour) we only are at frame 107,892 at 29.97 drop frame encoding.
                        // This corresponds to 3596.4 seconds into the video, not 3600 seconds.
                        correctiveFPSFactor = NTSC_FRAME_RATE / FRAME_RATE_OF_30;
                        fixTheFrameRateIssue = true;
                    }
                    // else fixTheFrameRateIssue remains false as the given timecode marking (30 fps or 29.97 fps) presumed
                    // to match the given frame rate (NOTE: within ParseHHMMSSFFIntoSeconds we assume 30 fps rather than support
                    // 25 fps or 60 fps or other frame rates as well when interpreting hh:mm:ss:ff -- this annoying ambivalence
                    // regarding the meaning of frames in this format is why the segment offsets can be restricted to fractional
                    // seconds, not frames, e.g., just hh:mm:ss.ss (decimal point and fractional seconds) depending on configuration value.

                    if (sInitialTimecode.Length > 0)
                        initialTimeOffset = ParseHHMMSSFFIntoSeconds(sInitialTimecode, fixTheFrameRateIssue, correctiveFPSFactor);
                    else
                        initialTimeOffset = 0;

                    if (initialTimeOffset != 0 || fixTheFrameRateIssue)
                        timingAdjustmentNecessary = true;
                }
            }

            if (!bGiveUpEarly)
            {
                string givenMovieFilename = givenTaggedValues[LENIENT_FILENAME_MARKER];
                        
                // Create an XmlMovie object.
                XmlMovie movie = new XmlMovie();

                List<XmlAnnotation> myMovieAnnotations = new List<XmlAnnotation>();
                movie.Collection = givenCollectionName;
                movie.SessionNumber = sessionOrder;
                movie.TapeNumber = tapeOrder;

                // Accept movie name however it is given:
                movie.Name = givenMovieFilename;

                if (givenTaggedValues.ContainsKey(MOVIE_ABSTRACT_MARKER))
                {
                    fieldVal = givenTaggedValues[MOVIE_ABSTRACT_MARKER];
                    if (fieldVal.Length > MAX_LENGTH_FOR_ABSTRACT)
                    {
                        this.OutputProblem(sShortFilename + " WARNING: Given abstract has " + fieldVal.Length + " chars., only " + MAX_LENGTH_FOR_ABSTRACT + " allowed.");
                        fieldVal = fieldVal.Substring(0, MAX_LENGTH_FOR_ABSTRACT);
                    }
                    movie.Abstract = fieldVal;
                }

                if (videoPathPrefix != null && videoPathPrefix.Length > 0)
                { // Modify given name in one of two ways:
                    // If usingHistoryMakersConventions, then givenCollectionName MUST be of form foo.bar, e.g., A2012.112,
                    // and the name will be: video path/foo/bar/movieFilename (splitting collection name to before/after the ".")
                    // (NOTE: if period "." not found in collection name, behavior reverts to just video path/collectionName/movieFilename)
                    // Otherwise, name is of form:
                    // video path/collectionName/movieFilename
                    if (usingHistoryMakersConventions)
                    {
                        int breakSpotInCollectionName = givenCollectionName.IndexOf(".");

                        if (breakSpotInCollectionName > 0 && breakSpotInCollectionName < givenCollectionName.Length - 1) {
                            // There is at least 1 character to work with on each side of the discovered "."
                            movie.Path = videoPathPrefix + System.IO.Path.DirectorySeparatorChar + givenCollectionName.Substring(0, breakSpotInCollectionName) +
                                System.IO.Path.DirectorySeparatorChar + givenCollectionName.Substring(breakSpotInCollectionName + 1) +
                                System.IO.Path.DirectorySeparatorChar + givenMovieFilename;
                        }
                        else // just using collection name as is:
                            movie.Path = videoPathPrefix + System.IO.Path.DirectorySeparatorChar + givenCollectionName
                                + System.IO.Path.DirectorySeparatorChar + givenMovieFilename;
                    }
                    else
                        movie.Path = videoPathPrefix + System.IO.Path.DirectorySeparatorChar + givenCollectionName
                            + System.IO.Path.DirectorySeparatorChar + givenMovieFilename;
                }
                else // no expansion of what is specified: use it as is!
                    movie.Path = givenMovieFilename;

                // NOTE: Every movie path checked and forced to end with VIDEO_FILENAME_EXTENSION
                if (!movie.Path.ToLower().EndsWith(VIDEO_FILENAME_EXTENSION))
                    movie.Path += VIDEO_FILENAME_EXTENSION;

                movie.Segments = GetMovieSegments(givenInputFilename, sShortFilename, segmentStartTimes, suppliedSegmentEndTimes, segmentEndTimes,
                    timingAdjustmentNecessary, initialTimeOffset, fixTheFrameRateIssue, correctiveFPSFactor);

                if (timingAdjustmentNecessary)
                { // write out one or two of sInitialTimecode, isNonDropTimecode and always fGivenFrameRate
                    if (sInitialTimecode != "")
                    {
                        XmlAnnotation oneAnn = new XmlAnnotation();
                        oneAnn.Type = INITIAL_TIMECODE_ANNOTATION_NAME;
                        oneAnn.Value = sInitialTimecode;
                        myMovieAnnotations.Add(oneAnn);
                    }
                    if (fixTheFrameRateIssue)
                    {
                        // only when fixTheFrameRateIssue is true do we care to make note of isNonDropTimecode
                        if (isNonDropTimecode)
                        {
                            // burned in timecode assumes 30 fps
                            XmlAnnotation oneAnnT = new XmlAnnotation();
                            oneAnnT.Type = NON_DROP_TIMECODE_IN_USE_ANNOTATION_NAME;
                            oneAnnT.Value = "1";
                            myMovieAnnotations.Add(oneAnnT);
                        }
                        else
                        {
                            // burned in timecode assumes 29.97 fps
                            XmlAnnotation oneAnn = new XmlAnnotation();
                            oneAnn.Type = NON_DROP_TIMECODE_IN_USE_ANNOTATION_NAME;
                            oneAnn.Value = "0";
                            myMovieAnnotations.Add(oneAnn);
                        }
                    }
                    // have given frame rate too, whenever timingAdjustmentNecessary
                    XmlAnnotation oneAnnFPS = new XmlAnnotation();
                    oneAnnFPS.Type = PRE_SPECIFIED_FRAME_RATE_ANNOTATION_NAME;
                    oneAnnFPS.Value = fGivenFrameRate.ToString();
                    myMovieAnnotations.Add(oneAnnFPS);
                }
                if (givenTaggedValues.ContainsKey(TRANSCRIBER_MARKER) && givenTaggedValues[TRANSCRIBER_MARKER].Length > 0)
                {
                    XmlAnnotation oneAnnFPS = new XmlAnnotation();
                    oneAnnFPS.Type = TRANSCRIBER_ANNOTATION_NAME;
                    oneAnnFPS.Value = givenTaggedValues[TRANSCRIBER_MARKER];
                    myMovieAnnotations.Add(oneAnnFPS);
                }
                if (givenTaggedValues.ContainsKey(TRANSCRIPTIONDATE_MARKER))
                {
                    XmlAnnotation oneAnnFPS = new XmlAnnotation();
                    oneAnnFPS.Type = TRANSCRIPTION_DATE_ANNOTATION_NAME;
                    oneAnnFPS.Value = givenTaggedValues[TRANSCRIPTIONDATE_MARKER];
                    myMovieAnnotations.Add(oneAnnFPS);
                }
                if (givenTaggedValues.ContainsKey(PRODUCER_MARKER) && givenTaggedValues[PRODUCER_MARKER].Length > 0)
                {
                    XmlAnnotation oneAnnFPS = new XmlAnnotation();
                    oneAnnFPS.Type = PRODUCER_ANNOTATION_NAME;
                    oneAnnFPS.Value = givenTaggedValues[PRODUCER_MARKER];
                    myMovieAnnotations.Add(oneAnnFPS);
                }
                if (givenTaggedValues.ContainsKey(PRODUCTION_COMPANY_MARKER) && givenTaggedValues[PRODUCTION_COMPANY_MARKER].Length > 0)
                {
                    XmlAnnotation oneAnnFPS = new XmlAnnotation();
                    oneAnnFPS.Type = PRODUCTION_COMPANY_ANNOTATION_NAME;
                    oneAnnFPS.Value = PRODUCTION_COMPANY_MARKER;
                    myMovieAnnotations.Add(oneAnnFPS);
                }
                movie.Annotations = myMovieAnnotations;

                // Serialize movie to file.
                XmlUtilities.Write<XmlMovie>(movie, Path.Combine(targetPath, GetSafeFilename(movie.Name) + ".movie.xml"));
            }

            return !bGiveUpEarly;
        }

        /// <summary>
        /// Given an input file and segment timing, process input file into component segments and add them to the return list. 
        /// </summary>
        /// <param name="givenInputFilename">input filename to process</param>
        /// <param name="sShortFilename">used in error reporting, short version of input filename</param>
        /// <param name="segmentStartTimes">story start times</param>
        /// <param name="suppliedSegmentEndTimes">input file-specified story end times, or empty string if no end time given for an entry (defaulting to end just before next segment starts)</param>
        /// <param name="segmentEndTimes">story end times</param>
        /// <param name="timingAdjustmentNecessary">if true, given times will need to be adjusted</param>
        /// <param name="initialTimeOffset">initial time offset for adjustment</param>
        /// <param name="fixTheFrameRateIssue">if true, then make use of final parameter</param>
        /// <param name="correctiveFPSFactor">frame rate multiplier to adjust given time (before initial offset adjustment) from 29.97 to 30 fps or vice versa</param>
        /// <returns>A list of XmlSegment objects</returns>
        private List<XmlSegment> GetMovieSegments(string givenInputFilename, string sShortFilename, List<string> segmentStartTimes,
            List<string> suppliedSegmentEndTimes, List<string> segmentEndTimes,
            bool timingAdjustmentNecessary, double initialTimeOffset, bool fixTheFrameRateIssue, double correctiveFPSFactor)
        {
            const double ZERO_TIME = 0.0;

            string sLine = "";
            int iLineCounter = 0;
            StreamReader sr = null;

            StringBuilder sbPara;
            bool bBlankParasOK;

            int transcriptUnspokenMarkerOpenCount;
            int iNextOpen, iNextClose;

            bool bGiveUpEarly = false;
            int iOutputSegmentCounter = 0;

            string segStartTime, segEndTime;
            XmlTimeFormatSpecifier segTimingFormat;

            // Initialize the return structure.
            List<XmlSegment> segments = new List<XmlSegment>();

            try
            {
                sr = new StreamReader(givenInputFilename);
                while ((sLine = sr.ReadLine()) != null)
                {
                    iLineCounter++; // Count even blank lines.
                    // RETIRED use of CleanUpUTF8 in 2020: sLine = CleanUpUTF8(sShortFilename, sLine.Trim(), iLineCounter); // trim off all whitespace (leading and trailing)
                    sLine = sLine.Trim();
                    if (sLine.Length > 0)
                    {
                        if (sLine.StartsWith(STORY_MARKER))
                        {
                            // IMPORTANT landmark: found FIRST story: process stories as segments from this point forward...

                            // Stay in this clause for remainder of document, looping through these steps:
                            // get time (required start and optional end; pass by them here as they were processed earlier)
                            // get title
                            // get transcript marker
                            // process transcript until END OR ***STORY OR ***END encountered.

                            while (!bGiveUpEarly)
                            {
                                if (iOutputSegmentCounter >= segmentStartTimes.Count)
                                {
                                    this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": PROBLEM, second pass segment count differs from the first.");
                                    bGiveUpEarly = true;
                                }
                                else
                                {
                                    // Create a XmlSegment to hold the segment information.
                                    XmlSegment segment = new XmlSegment();

                                    // Two cases: use times as specified in input document for !timingAdjustmentNecessary, or else
                                    // manipulate based on INITIAL_TIMECODE and/or NONDROP_TIMECODE_IN_USE and given
                                    // frame rate to turn the timecode-video informed offset to a true offset
                                    // into the watermarked surrogate video (for when timingAdjustmentNecessary)
                                    if (!timingAdjustmentNecessary)
                                    {
                                        segStartTime = AdjustedTime(segmentStartTimes[iOutputSegmentCounter]);
                                        // If this is final segment, output perhaps hms-end as TimeFormat (end then set to end of video),
                                        // else output hms-hms with both start and end time given:
                                        if (iOutputSegmentCounter == segmentStartTimes.Count - 1)
                                        {
                                            // If suppliedSegmentEndTimes has a non-empty entry for the last segment, use it.
                                            if (suppliedSegmentEndTimes.Count == segmentStartTimes.Count &&
                                                suppliedSegmentEndTimes[suppliedSegmentEndTimes.Count - 1].Length > 0)
                                            {
                                                segTimingFormat = XmlTimeFormatSpecifier.HMSHMS;
                                                segEndTime = AdjustedTime(suppliedSegmentEndTimes[iOutputSegmentCounter]);
                                            }
                                            else
                                            { // no end time given for final segment, so go to "end" of the video file:
                                                segTimingFormat = XmlTimeFormatSpecifier.HMSEND;
                                                segEndTime = SecondsToHHMMSSWithFractionalSeconds(ZERO_TIME);
                                            }
                                        }
                                        else // time format is hms-hms with 2 given times
                                        {
                                            segTimingFormat = XmlTimeFormatSpecifier.HMSHMS;
                                            segEndTime = AdjustedTime(segmentEndTimes[iOutputSegmentCounter]);
                                        }
                                    }
                                    else
                                    { // replace all time references with AdjustedTime() call...
                                        segStartTime = AdjustedTime(segmentStartTimes[iOutputSegmentCounter], initialTimeOffset,
                                                        fixTheFrameRateIssue, correctiveFPSFactor);
                                        // If this is final segment, output perhaps hms-end as TimeFormat (end then set to end of video),
                                        // else output hms-hms with both start and end time given:
                                        if (iOutputSegmentCounter == segmentStartTimes.Count - 1)
                                        {
                                            // If suppliedSegmentEndTimes has a non-empty entry for the last segment, use it.
                                            if (suppliedSegmentEndTimes.Count == segmentStartTimes.Count &&
                                                suppliedSegmentEndTimes[suppliedSegmentEndTimes.Count - 1].Length > 0)
                                            {
                                                segTimingFormat = XmlTimeFormatSpecifier.HMSHMS;
                                                segEndTime = AdjustedTime(suppliedSegmentEndTimes[iOutputSegmentCounter], initialTimeOffset,
                                                        fixTheFrameRateIssue, correctiveFPSFactor);
                                            }
                                            else
                                            { // no end time given for final segment, so go to "end" of the video file:
                                                segTimingFormat = XmlTimeFormatSpecifier.HMSEND;
                                                segEndTime = SecondsToHHMMSSWithFractionalSeconds(ZERO_TIME);
                                            }
                                        }
                                        else // time format is hms-hms with 2 given times
                                        {
                                            segTimingFormat = XmlTimeFormatSpecifier.HMSHMS;
                                            segEndTime = AdjustedTime(segmentEndTimes[iOutputSegmentCounter], initialTimeOffset,
                                                        fixTheFrameRateIssue, correctiveFPSFactor);
                                        }
                                    }
                                    segment.StartTime = DateTime.Parse(segStartTime);
                                    segment.EndTime = DateTime.Parse(segEndTime);
                                    segment.TimeFormat = segTimingFormat;

                                    // Now continue with additional processing of the input file, looking for this
                                    // pattern:
                                    // time (ignored; parsed already in first pass and discarded now)
                                    // optional second time (ignored; parsed already in first pass and discarded now)
                                    // title (required)
                                    // ***TRANSCRIPT (required)
                                    // paragraph one of output
                                    // blank line
                                    // more paragraphs delimited by blank line, ...

                                    sLine = sr.ReadLine();
                                    iLineCounter++;
                                    if (sLine == null || sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                                    {
                                        this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": PROBLEM, second pass segment time info line not found.");
                                        bGiveUpEarly = true;
                                    }
                                    else
                                    {
                                        // Make use of suppliedSegmentEndTimes[] to decide if an optional second time item was given
                                        if (suppliedSegmentEndTimes.Count > iOutputSegmentCounter &&
                                            suppliedSegmentEndTimes[iOutputSegmentCounter].Length > 0)
                                        {
                                            // Optional second time (ending time) given, so move past it to get to true title line.
                                            sLine = sr.ReadLine();
                                        }
                                        sLine = sr.ReadLine();
                                        iLineCounter++;
                                        if (sLine == null || sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                                        {
                                            this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": PROBLEM, second pass segment title line not found.");
                                            bGiveUpEarly = true;
                                        }
                                        else
                                        {
                                            // RETIRED use of CleanUpUTF8 in 2020: sLine = CleanUpUTF8(sShortFilename, sLine.Trim(), iLineCounter); 
                                            sLine = sLine.Trim();
                                            if (sLine.Length == 0)
                                            {
                                                this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": PROBLEM, nonEmpty second pass segment title line not found.");
                                                bGiveUpEarly = true;
                                            }
                                            else
                                                segment.Title = sLine;
                                        }
                                    }

                                    iOutputSegmentCounter++;

                                    if (!bGiveUpEarly)
                                    { // ***TRANSCRIPT (required)
                                        sLine = sr.ReadLine();
                                        iLineCounter++;
                                        if (sLine == null || sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                                        {
                                            this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": PROBLEM, second pass segment transcript " + TRANSCRIPT_MARKER + " line not found.");
                                            bGiveUpEarly = true;
                                        }
                                    }

                                    List<string> transcriptParagraphs = new List<string>();

                                    bBlankParasOK = false; // allow blank paragraphs in the middle of transcript text, but not at top or bottom...
                                    transcriptUnspokenMarkerOpenCount = 0; // keep track of [ and ] for balancing these special markers
                                    sbPara = new StringBuilder();
                                    while (!bGiveUpEarly)
                                    { // Keep appending into sbPara until one of the following:
                                        // all whitespace line (ends paragraph)
                                        // ***STORY (ends paragraph AND story; we stay in story processing loop)
                                        // end of file (ends paragraph AND story AND breaks out of story loop)

                                        // Complain if another TRANSCRIPT_MARKER is seen (should NEVER occur within transcript; indicates a malformed input file)
                                        sLine = sr.ReadLine();
                                        iLineCounter++;
                                        if (sLine != null && !sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                                        {
                                            // RETIRED use of CleanUpUTF8 in 2020: sLine = CleanUpUTF8(sShortFilename, sLine.Trim(), iLineCounter); 
                                            sLine = sLine.Trim();
                                            if (sLine.Contains(TRANSCRIPT_MARKER))
                                            {
                                                this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": Giving up EARLY, *inside* the story transcript there is this funny marker: " + TRANSCRIPT_MARKER);
                                                // Problem noted, but will be kept as is with giving up early also set....
                                                bGiveUpEarly = true;
                                            }
                                            else
                                            { // balance UNSPOKEN_TEXT_IN_TRANSCRIPT_OPENER and UNSPOKEN_TEXT_IN_TRANSCRIPT_CLOSER
                                                iNextOpen = 0;
                                                iNextClose = 0;
                                                while (!bGiveUpEarly && (iNextOpen >= 0 || iNextClose >= 0))
                                                {
                                                    if (iNextOpen >= 0)
                                                        iNextOpen = sLine.IndexOf(UNSPOKEN_TEXT_IN_TRANSCRIPT_OPENER, iNextOpen);
                                                    if (iNextClose >= 0)
                                                    {
                                                        if (iNextOpen >= 0 && iNextOpen < iNextClose)
                                                        { // found ] at iNextClose-1 and now have [ before that which is bad: 2 [[ occurred before 1 ]
                                                            // Problems: encountered two [ without intervening ]
                                                            this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": Giving up EARLY, story transcript had a second opening marker of unspoken text ( " + UNSPOKEN_TEXT_IN_TRANSCRIPT_OPENER + " ) without an intervening closing marker " + UNSPOKEN_TEXT_IN_TRANSCRIPT_CLOSER);
                                                            // Problem noted, but will be kept as is with giving up early also set....
                                                            bGiveUpEarly = true;
                                                        }
                                                        else
                                                            iNextClose = sLine.IndexOf(UNSPOKEN_TEXT_IN_TRANSCRIPT_CLOSER, iNextClose);
                                                    }
                                                    if (!bGiveUpEarly)
                                                    {
                                                        // Do not allow nested [ [] ] so never let transcriptUnspokenMarkerOpenCount be greater than 1.
                                                        if (iNextOpen > iNextClose && iNextClose >= 0)
                                                        { // found both ] and [ in that order...
                                                            if (transcriptUnspokenMarkerOpenCount == 0)
                                                            {
                                                                // Problems: encountered ] first
                                                                this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": Giving up EARLY, story transcript had this special closing marker of unspoken text first ( " + UNSPOKEN_TEXT_IN_TRANSCRIPT_CLOSER + " ) before the opening marker " + UNSPOKEN_TEXT_IN_TRANSCRIPT_OPENER);
                                                                // Problem noted, but will be kept as is with giving up early also set....
                                                                bGiveUpEarly = true;
                                                            }
                                                            // Otherwise by other tests transcriptUnspokenMarkerOpenCount == 1.  It 
                                                            // gets "reset" to 0 via iNextClose, then set to 1 via iNextOpen so all is good.
                                                        }
                                                        else // found one of [ and ] or just [ or neither
                                                        {
                                                            if (iNextOpen >= 0)
                                                            {
                                                                transcriptUnspokenMarkerOpenCount++;
                                                                if (transcriptUnspokenMarkerOpenCount > 1)
                                                                {
                                                                    // Problems: encountered two [ without intervening ]
                                                                    this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter + ": Giving up EARLY, story transcript had a second opening marker of unspoken text ( " + UNSPOKEN_TEXT_IN_TRANSCRIPT_OPENER + " ) without an intervening closing marker " + UNSPOKEN_TEXT_IN_TRANSCRIPT_CLOSER);
                                                                    // Problem noted, but will be kept as is with giving up early also set....
                                                                    bGiveUpEarly = true;
                                                                }
                                                                if (iNextClose >= 0)
                                                                    transcriptUnspokenMarkerOpenCount--; // set it right back to 0 as we have [ and ] on same line
                                                            }
                                                        }
                                                        if (iNextOpen >= 0)
                                                            iNextOpen++; // move past [ to process any other [ on this same line
                                                        if (iNextClose >= 0)
                                                            iNextClose++; // move past ] to process any other ] on this same line
                                                    }
                                                }
                                            }
                                        }


                                        if (sLine == null || sLine.StartsWith(STORY_MARKER) || sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                                        {
                                            if (sbPara.Length > 0)
                                                transcriptParagraphs.Add(sbPara.ToString());
                                            // else suppress ending blank line (just 1 suppressed) at end of story
                                            break; // end of story
                                        }
                                        else
                                        {
                                            if (sLine.Length == 0)
                                            { // End prior paragraph.
                                                if (bBlankParasOK || sbPara.Length > 0)
                                                {
                                                    transcriptParagraphs.Add(sbPara.ToString());
                                                    if (sbPara.Length > 0)
                                                    {
                                                        // Be ready to continue new paragraph:
                                                        sbPara = new StringBuilder();
                                                        bBlankParasOK = true; // allow blank line output after 
                                                        // we get some transcript contents for story
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (sbPara.Length == 0)
                                                    sbPara.Append(sLine);
                                                else // tack on next line with single space separator
                                                    sbPara.Append(" " + sLine);
                                            }
                                        }
                                    } // end of while loop collecting transcript
                                    segment.Transcript = transcriptParagraphs;
                                    // Append the segment to the results list.
                                    segments.Add(segment);

                                    if (sLine == null || sLine.StartsWith(MARKER_FOR_END_OF_TRANSCRIPT_INPUT))
                                        break; // done with all story processing if we hit end of the input file or the END marker

                                } // end of segment count check being as expected
                            } // end of while loop through story section

                            break; // Done with output; can exit input-file processing....

                        } // end of ***STORY section where segments are listed
                    } // end of non-empty input line
                } // end of while loop through opening input file lines
            }
            catch (Exception ex)
            {
                this.OutputProblem(sShortFilename + " Problems in processing segment list: " + ex.Message);
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            // Return the results, complete or not.
            return segments;
        }

        #endregion ========================= INPUT PARSING MAIN ROUTINES ==========================

        #region ============================== HELPER FUNCTIONS ================================

        /// <summary>
        /// Returns true iff given session is complete (either null, or fully filled in).
        /// </summary>
        /// <param name="givenSession">given session</param>
        /// <returns>true iff session has a nonempty location, interviewer, "non-empty" date and a positive value session order (or else is null)</returns>
        private bool SessionDetailsComplete(XmlSession givenSession)
        {
            return (givenSession == null || (givenSession.SessionOrder > 0 && givenSession.Interviewer.Length > 0 &&
                givenSession.InterviewDate != DateTime.MinValue && givenSession.Location.Length > 0));
        }

        /// <summary>
        /// Returns an "empty" session (InterviewDate set to DateTime.MinValue).
        /// </summary>
        /// <returns>xmlSession instance corresponding to "empty"</returns>
        private XmlSession EmptySession()
        {
            XmlSession retObj = new XmlSession();
            retObj.SessionOrder = 0;
            retObj.Location = "";
            retObj.Interviewer = "";
            retObj.InterviewDate = DateTime.MinValue;

            return retObj;
        }

        /// <summary>
        /// Convert time given as hh:mm:ss:ff or just mm:ss:ff or just ss:ff into a (possibly fractional) seconds count,
        /// assuming 30 fps when converting ff into fractional seconds.  
        /// </summary>
        /// <param name="givenHHMMSSFF">time in hh:mm:ss:ff or just mm:ss:ff or just ss:ff or just ff format</param>
        /// <remarks>A bit of automatic time code correcting is done, e.g., ff is pushed into [0, 29] range.</remarks>
        private double ParseHHMMSSFFIntoSeconds(string givenHHMMSSFF)
        {
            const int PRESUMED_FRAME_RESOLUTION_FOR_FF = 30; // divide given ff value by 30 to get fractional seconds, e.g., 15 as ff == 0.5 seconds

            string[] pieces = givenHHMMSSFF.Split(':');
            int hours = 0, minutes = 0, seconds = 0, frames = 0;
            double temp;
            
            if (pieces.Length == 1)
                // ff
                frames = Int32.Parse(pieces[0]);
            else if (pieces.Length == 2)
            { // ss:ff
                seconds = Int32.Parse(pieces[0]);
                frames = Int32.Parse(pieces[1]);
            }
            else if (pieces.Length == 3)
            { // mm:ss:ff
                minutes = Int32.Parse(pieces[0]);
                seconds = Int32.Parse(pieces[1]);
                frames = Int32.Parse(pieces[2]);
            }
            else if (pieces.Length == 4)
            { // hh:mm:ss:ff
                hours = Int32.Parse(pieces[0]);  
                minutes = Int32.Parse(pieces[1]);
                seconds = Int32.Parse(pieces[2]);
                frames = Int32.Parse(pieces[3]);
            }


            if (frames >= PRESUMED_FRAME_RESOLUTION_FOR_FF)
                frames = PRESUMED_FRAME_RESOLUTION_FOR_FF - 1;
            else if (frames < 0)
                frames = 0;
            
            if (hours < 0)
                hours = 0;
            // NOTE: do not enforce a maximum with hours 
            
            if (minutes < 0)
                minutes = 0;
            else if (minutes > 59)
                minutes = 59;
            
            if (seconds < 0)
                seconds = 0;
            else if (seconds > 59)
                seconds = 59;

            temp = (hours * 3600) + (minutes * 60) + seconds + (frames / (double)PRESUMED_FRAME_RESOLUTION_FOR_FF);

            return temp;
        }

        /// <summary>
        /// Convert time given as hh:mm:ss:ff or just mm:ss:ff or just ss:ff into a (possibly fractional) seconds count,
        /// using givenFrameRate.  Used only for a specific time offset specified once per movie file (i.e., NOT used in segment time offsets).  
        /// </summary>
        /// <param name="givenHHMMSSFF">time in hh:mm:ss:ff or just mm:ss:ff or just ss:ff format</param>
        /// <param name="fixFrameRateIssue">if true, make use of givenFrameRateCorrectingFactor, else ignore it</param>
        /// <param name="givenFrameRateCorrectingFactor">typically, 30/29.97 (to correct Non Drop Frame Time Code back to 29.97 fps)</param>
        /// <returns>seconds equivalent of given time</returns>
        private double ParseHHMMSSFFIntoSeconds(string givenHHMMSSFF, bool fixFrameRateIssue, double givenFrameRateCorrectingFactor)
        {
            double temp = ParseHHMMSSFFIntoSeconds(givenHHMMSSFF);
            if (fixFrameRateIssue)
                return givenFrameRateCorrectingFactor * temp;
            else
                return temp;
        }

        /// <summary>
        /// Adjust given time by converting it back into HH:MM:SS.FF format (it could be in a frame rate format).
        /// </summary>
        /// <param name="givenTimeAsString">given time as HH:MM:SS.FF (fractional seconds) OR HH:MM:SS:FF (required frames with 30 fps math used to make fractional seconds)</param>
        /// <returns>string in HH:MM:SS.S format, i.e., fractional seconds format</returns>
        private string AdjustedTime(string givenTimeAsString)
        {
            double givenTime = TimeAsSeconds(givenTimeAsString);
            return SecondsToHHMMSSWithFractionalSeconds(givenTime);
        }

        /// <summary>
        /// Adjust given time by possibly fixing it via a frame rate multiplier and then
        /// decrementing the initial offset, converting it back into HH:MM:SS.FF format.
        /// This hopefully will not be done often; hence it is not optimized to take place
        /// earlier in the input processing tree when we deal with givenTime as a double and
        /// not a string (i.e., we are converting into double via TimeAsSeconds more than once).
        /// </summary>
        /// <param name="givenTimeAsString">given time as HH:MM:SS.FF (fractional seconds) OR HH:MM:SS:FF (required frames with 30 fps math used to make fractional seconds)</param>
        /// <param name="initialOffset">given offset to chop off of given time (0 for none)</param>
        /// <param name="fixFrameRate">if true, then make use of final parameter</param>
        /// <param name="frameRateFixMultiplier">frame rate multiplier to adjust given time (before initial offset adjustment) from 29.97 to 30 fps or vice versa</param>
        /// <returns>string in HH:MM:SS.FF format with correct time adjustments made to it</returns>
        private string AdjustedTime(string givenTimeAsString, double initialOffset, bool fixFrameRate, double frameRateFixMultiplier)
        {
            double givenTime = TimeAsSeconds(givenTimeAsString);
            if (fixFrameRate)
                givenTime = givenTime * frameRateFixMultiplier;

            givenTime = givenTime - initialOffset;
            return SecondsToHHMMSSWithFractionalSeconds(givenTime);
        }

        /// <summary>
        /// Loose mapping of anything starting with "f" or "0" to female, "m" or "1" to male, "b" or "2" to both, with default to female on
        /// unknown other input strings.
        /// </summary>
        /// <param name="givenGenderIndicator">string indicating gender choice</param>
        /// <returns>one of XmlGenderType enumerators </returns>
        private XmlGenderType GenderFromString(string givenGenderIndicator)
        {
            XmlGenderType returnVal = XmlGenderType.Female; // default to Female
            if (givenGenderIndicator.Length > 0)
            {
                switch (givenGenderIndicator.Substring(0, 1))
                {
                    case "m":
                    case "1":
                        { returnVal = XmlGenderType.Male; break; }
                }
            }
            return returnVal;
        }

        /// <summary>
        /// Returns index of list item matching start of given line, or -1 if no match found.
        /// </summary>
        /// <param name="sGivenLine">given line</param>
        /// <param name="LenientList">list to match</param>
        /// <returns>index into LenientList if line start matches an item, -1 on no match</returns>
        private int TestLineAgainstLenientList(string sGivenLine, List<IDVLTAG> templateList)
        {
            int returnVal = -1;
            for (int iTag = 0; iTag < templateList.Count; iTag++)
            {
                if (sGivenLine.StartsWith(templateList[iTag].Lenient))
                {
                    returnVal = iTag;
                    break;
                }
            }
            return returnVal;
        }

        /// <summary>
        /// Given a dictionary of tag-value entries and a list of optional tags encoding which tags are for open-ended worlds (i.e., 
        /// collection annotations), return the list of collection annotation values.
        /// </summary>
        /// <param name="givenTaggedValues">most important list of tags and their values</param>
        /// <param name="optionalTag">optional tags used to fill out givenTaggedValues, passed in here so that the optional worlds can be parsed to see if any open-ended worlds specified,
        /// e.g., if there is a favorite food world, was a favorite food specified?</param>
        /// <returns>a list of XmlAnnotation objects, one per open-ended filled in world for the collection</returns>
        private List<XmlAnnotation> GetCollectionAnnotations(Dictionary<string, string> givenTaggedValues, List<IDVLTAG> optionalTag)
        {
            // Initialize the return structure.
            List<XmlAnnotation> annotations = new List<XmlAnnotation>();

            Dictionary<string, string>.KeyCollection.Enumerator myKeysEnumerator;
            string oneKey;

            for (int iTag = 0; iTag < optionalTag.Count; iTag++)
            {
                myKeysEnumerator = givenTaggedValues.Keys.GetEnumerator();
                while ((myKeysEnumerator.MoveNext()) && (myKeysEnumerator.Current != null))
                {
                    oneKey = myKeysEnumerator.Current;
                    if (oneKey == optionalTag[iTag].Lenient && optionalTag[iTag].OwnerWorldName.Length > 0 &&
                        !optionalTag[iTag].InputRangeRestricted)
                    {
                        if (optionalTag[iTag].OwnerWorldName != OCCUPATION_ALLOWING_MANY_VALUES)
                        {
                            // tag key is a world with non-restricted input (i.e., no partitions and hence open-ended), so put into the return list 
                            // this particular collection annotation
                            // Create a new annotation...
                            XmlAnnotation annotation = new XmlAnnotation();
                            //...populate it...
                            annotation.Type = optionalTag[iTag].OwnerWorldName;
                            annotation.Value = givenTaggedValues[optionalTag[iTag].Lenient];
                            // Add annotation to the list.
                            annotations.Add(annotation);
                        }
                        else
                        {
                            // SPECIAL CASE for OCCUPATION_ALLOWING_MANY_VALUES: make multiple annotations, one per value 
                            string[] splitters = new string[1];
                            splitters[0] = PARTITION_NAME_SEPARATOR;
                            string[] individualOccupations = givenTaggedValues[optionalTag[iTag].Lenient].Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                            for (int iJob = 0; iJob < individualOccupations.Length; iJob++)
                            {
                                // Create a new annotation...
                                XmlAnnotation annotation = new XmlAnnotation();
                                //...populate it...
                                annotation.Type = optionalTag[iTag].OwnerWorldName;
                                annotation.Value = individualOccupations[iJob];
                                // Add annotation to the list.
                                annotations.Add(annotation);
                            }
                        }
                    }
                }
                myKeysEnumerator.Dispose();
            } 
            
            return annotations;
        }

        /// <summary>
        /// Given a dictionary of tag-value entries and a list of optional tags encoding which tags are for partitions, return the list of partition values.
        /// </summary>
        /// <param name="givenTaggedValues">most important list of tags and their values</param>
        /// <param name="optionalTag">optional tags used to fill out givenTaggedValues, passed in here so that the optional worlds can be parsed to see if any partitions specified,
        /// e.g., if there is a job type world, were any jobs specified?</param>
        /// <returns></returns>
        private List<string> GetCollectionPartitions(Dictionary<string, string> givenTaggedValues, List<IDVLTAG> optionalTag)
        {
            // Initialize the return structure.
            List<string> partitions = new List<string>();

            Dictionary<string, string>.KeyCollection.Enumerator myKeysEnumerator;
            string oneKey;
            string[] splitters = new string[1];
            splitters[0] = PARTITION_NAME_SEPARATOR;
            string[] partitionNames;

            for (int iTag = 0; iTag < optionalTag.Count; iTag++)
            {
                myKeysEnumerator = givenTaggedValues.Keys.GetEnumerator();
                while ((myKeysEnumerator.MoveNext()) && (myKeysEnumerator.Current != null))
                {
                    oneKey = myKeysEnumerator.Current;
                    if (oneKey == optionalTag[iTag].Lenient && optionalTag[iTag].OwnerWorldName.Length > 0 &&
                        optionalTag[iTag].InputRangeRestricted)
                    { // tag key is a world with restricted input (i.e., with partitions), so put into the return list 
                      // all the given partitions for that world (found in tab-separated list as the value for the tag)
                        partitionNames = givenTaggedValues[optionalTag[iTag].Lenient].Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                        for (int iPartition = 0; iPartition < partitionNames.Length; iPartition++)
                        {
                            partitions.Add(partitionNames[iPartition]);
                        }
                    }
                }
                myKeysEnumerator.Dispose();
            }
            return partitions;
        }

        /// <summary>
        /// Return time as seconds, where time is in hh:mm:ss.ff format or else in hh:mm:ss:ff (or simply ff or ss:ff or mm:ss:ff) depending
        /// on configuration variable
        /// </summary>
        /// <param name="sTime">time in hh:mm:ss.ff format if !myFrameCountInTimeCode, else time in ff or ss:ff or mm:ss:ff or hh:mm:ss:ff format with 30 fps assumed to turn ff into fractional seconds</param>
        /// <returns>time as seconds, or -1 on error</returns>
        private double TimeAsSeconds(string sTime)
        {
            const double ERROR_VALUE = -1.0;

            double dRetVal = ERROR_VALUE;
            bool bParseStatus = true;

            if (myFrameCountInTimeCode) 
            { // time given as hh:mm:ss:ff format (ff always must be present as frames assuming 30 fps so values of 00 to 29 make sense)
                if (!sTime.Contains("."))
                    dRetVal = ParseHHMMSSFFIntoSeconds(sTime);
                // else fractional seconds or something attempted, so give up with dRetVal remaining at ERROR_VALUE
            }
            else
            { // time given as hh:mm:ss.ff format
                try
                {
                    int nHours = 0;
                    int nMins = 0;
                    double dSecs = 0.0;
                    int iColon1, iColon2, iPeriod;
                    iPeriod = sTime.IndexOf(".");
                    iColon1 = sTime.IndexOf(":");
                    if (iColon1 < 0)
                    {
                        // Just left with ss or ss.ff
                        // Leave nHours = 0 and nMins = 0
                        dSecs = Double.Parse(sTime);
                    }
                    else
                    { // Have at least one colon; see if we have 2

                        iColon2 = sTime.IndexOf(":", iColon1 + 1);
                        // First, take care of bogus input:
                        if (iPeriod >= 0 && (iPeriod < iColon1 || iPeriod < iColon2))
                        {
                            // strange, give up on .hh:mm:ss or whatever (period must come after colons) 
                            bParseStatus = false;
                        }
                        else
                        {
                            if (iColon2 < 0)
                            { // Have mm:ss or mm:ss.ff
                                // Leave nHours = 0
                                if (iColon1 == 0)
                                    // Not sure if Informedia processing will handle
                                    // :ss.ff as 0:ss.ff but assume so for now...
                                    nMins = 0;
                                else
                                    nMins = Int32.Parse(sTime.Substring(0, iColon1));
                                if (iPeriod > iColon1)
                                {
                                    if (iPeriod == iColon1 + 1)
                                    {
                                        // strange, give up on mm:.ff 
                                        bParseStatus = false;
                                    }
                                    else
                                        dSecs = Double.Parse(sTime.Substring(iColon1 + 1));
                                }
                                else // no fraction, so nFraction stays = 0
                                    dSecs = Double.Parse(sTime.Substring(iColon1 + 1));
                            }
                            else if (iColon2 == iColon1 + 1)
                            { // reject hh::ss
                                bParseStatus = false;
                            }
                            else
                            { // Have hh:mm:ss or hh:mm:ss.ff
                                if (iColon1 == 0)
                                    // Not sure if Informedia processing will handle
                                    // :mm:ss.ff as 0:mm:ss.ff but assume so for now...
                                    nHours = 0;
                                else
                                    nHours = Int32.Parse(sTime.Substring(0, iColon1));
                                nMins = Int32.Parse(sTime.Substring(iColon1 + 1, iColon2 - iColon1 - 1));
                                if (iPeriod > iColon2)
                                {
                                    if (iPeriod == iColon2 + 1)
                                    {
                                        // strange, give up on hh:mm:.ff 
                                        bParseStatus = false;
                                    }
                                    else
                                        dSecs = Double.Parse(sTime.Substring(iColon2 + 1));
                                }
                                else // no fraction, so nFraction stays = 0
                                    dSecs = Double.Parse(sTime.Substring(iColon2 + 1));
                            }
                        }
                    }
                    // NOTE:  Allowing 2 digit minutes, i.e., up to 99:59 rather than 
                    // stopping at 59:59
                    if (bParseStatus)
                    {
                        if ((nMins > 99 || nMins < 0) || (dSecs >= 60 || dSecs < 0))
                        {
                            bParseStatus = false;
                        }
                    }
                    if (bParseStatus)
                        dRetVal = (nHours * 3600) + (nMins * 60) + dSecs;
                    else
                        dRetVal = ERROR_VALUE;
                }
                catch
                {
                    dRetVal = ERROR_VALUE;
                }
            }
            return dRetVal;
        }

        /// <summary>
        /// Given input of a string of form ss, mm:ss, hh:mm:ss, or hh:mm:ss all 
        /// with optional .ff at end (hundredths of seconds), IF config parameter "myFrameCountInTimeCode" is false, 
        /// else input is in ff or ss:ff or mm:ss:ff or hh:mm:ss:ff with frame count ALWAYS given for "myFrameCountInTimeCode" as true,
        /// return a string that is the equivalent of one frame (assuming 30 frames per second, 0.03 less) than the given
        /// time.  For most cases, the input will have no .ff or a .00, e.g., 3.00
        /// in which case .97 will be the fractional answer, e.g., 2.97.  For hh:mm:ss:ff, the answer is an ff or ss:ff one frame earlier than given frame.
        /// </summary>
        /// <param name="sTime">string version of time in hh:mm:ss.ff OR hh:mm:ss:ff format with hh, mm, etc. optional</param>
        /// <param name="sTimeDecrementedByOne">set to string version of time minus 1, in hh:mm:ss.ff format or hh:mm:ss:ff format as per myFrameCountInTimeCode</param>
        /// <param name="sErrorMsg">error message, if given string will not parse</param>
        /// <returns>true on successful parse, false otherwise</returns>
        private bool TimeDecrementByOneFrame(string sTime, out string sTimeDecrementedByOne, out string sErrorMsg)
        {
            sTimeDecrementedByOne = "";
            sErrorMsg = "";
            bool bParseStatus = true; // assume success

            if (myFrameCountInTimeCode)
            { // sTime is in ff or ss:ff or mm:ss:ff or hh:mm:ss:ff format
                string[] timePieces = sTime.Split(':');
                int nHours = 0;
                int nMins = 0;
                int nSecs = 0;
                int nFrames = 0;
                if (timePieces.Length > 4)
                {
                    bParseStatus = false;
                    sErrorMsg = "Too many colons to parse as hh:mm:ss:ff in given time " + sTime;
                }
                else
                {
                    if (timePieces.Length == 4)
                    { // hh:mm:ss:ff
                        nHours = Int32.Parse(timePieces[0]);
                        nMins = Int32.Parse(timePieces[1]);
                        nSecs = Int32.Parse(timePieces[2]);
                        nFrames = Int32.Parse(timePieces[3]);
                    }
                    else if (timePieces.Length == 3)
                    { // mm:ss:ff
                        nMins = Int32.Parse(timePieces[0]);
                        nSecs = Int32.Parse(timePieces[1]);
                        nFrames = Int32.Parse(timePieces[2]);
                    }
                    else if (timePieces.Length == 2)
                    { // ss:ff
                        nSecs = Int32.Parse(timePieces[0]);
                        nFrames = Int32.Parse(timePieces[1]);
                    }
                    else
                    { // ff
                        if (sTime.Length > 0)
                            nFrames = Int32.Parse(sTime);
                        else
                        {
                            bParseStatus = false;
                            sErrorMsg = "Given time cannot have one frame subtracted from it: " + sTime;
                        }
                    }
                }
                if (bParseStatus)
                { // compute one less than given time
                    if (nFrames > 0)
                        nFrames--; // easy case
                    else if (nSecs > 0)
                    {
                        nSecs--;
                        nFrames = 29;
                    }
                    else if (nMins > 0)
                    {
                        nMins--;
                        nSecs = 59;
                        nFrames = 29;
                    }
                    else if (nHours > 0)
                    {
                        nHours--;
                        nMins = 59;
                        nSecs = 59;
                        nFrames = 29;
                    }
                    else
                    {
                        bParseStatus = false;
                        sErrorMsg = "Given time cannot have one frame subtracted from it: " + sTime;
                    }
                }
                if (bParseStatus)
                    sTimeDecrementedByOne = nHours.ToString("00") + ":" + nMins.ToString("00") + ":" + nSecs.ToString("00") + ":" + nFrames.ToString("00");
            }
            else
            {

                const double dOneFrameDecrement = 0.03; // ~1/30 of a second

                try
                {
                    int nHours = 0;
                    int nMins = 0;
                    double dSecs = 0.0;
                    int iColon1, iColon2, iPeriod;
                    iPeriod = sTime.IndexOf(".");
                    iColon1 = sTime.IndexOf(":");
                    if (iColon1 < 0)
                    {
                        // Just left with ss or ss.ff
                        // Leave nHours = 0 and nMins = 0
                        dSecs = Double.Parse(sTime);
                    }
                    else
                    { // Have at least one colon; see if we have 2

                        iColon2 = sTime.IndexOf(":", iColon1 + 1);
                        // First, take care of bogus input:
                        if (iPeriod >= 0 && (iPeriod < iColon1 || iPeriod < iColon2))
                        {
                            // strange, give up on .hh:mm:ss or whatever (period must come after colons) 
                            bParseStatus = false;
                            sErrorMsg = "Expected period after colon(s) in given time " + sTime;
                        }
                        else
                        {
                            if (iColon2 < 0)
                            { // Have mm:ss or mm:ss.ff
                                // Leave nHours = 0
                                if (iColon1 == 0)
                                    // Not sure if Informedia processing will handle
                                    // :ss.ff as 0:ss.ff but assume so for now...
                                    nMins = 0;
                                else
                                    nMins = Int32.Parse(sTime.Substring(0, iColon1));
                                if (iPeriod > iColon1)
                                {
                                    if (iPeriod == iColon1 + 1)
                                    {
                                        // strange, give up on mm:.ff 
                                        bParseStatus = false;
                                        sErrorMsg = "Expected seconds after colon and before period in given time " + sTime;
                                    }
                                    else
                                        dSecs = Double.Parse(sTime.Substring(iColon1 + 1));
                                }
                                else // no fraction, so nFraction stays = 0
                                    dSecs = Double.Parse(sTime.Substring(iColon1 + 1));
                            }
                            else if (iColon2 == iColon1 + 1)
                            { // reject hh::ss
                                bParseStatus = false;
                                sErrorMsg = "Expected a number for minutes between colons in given time " + sTime;
                            }
                            else
                            { // Have hh:mm:ss or hh:mm:ss.ff
                                if (iColon1 == 0)
                                    // Not sure if Informedia processing will handle
                                    // :mm:ss.ff as 0:mm:ss.ff but assume so for now...
                                    nHours = 0;
                                else
                                    nHours = Int32.Parse(sTime.Substring(0, iColon1));
                                nMins = Int32.Parse(sTime.Substring(iColon1 + 1, iColon2 - iColon1 - 1));
                                if (iPeriod > iColon2)
                                {
                                    if (iPeriod == iColon2 + 1)
                                    {
                                        // strange, give up on hh:mm:.ff 
                                        bParseStatus = false;
                                        sErrorMsg = "Expected seconds after colon and before period in given time " + sTime;
                                    }
                                    else
                                        dSecs = Double.Parse(sTime.Substring(iColon2 + 1));
                                }
                                else // no fraction, so nFraction stays = 0
                                    dSecs = Double.Parse(sTime.Substring(iColon2 + 1));
                            }
                        }
                    }
                    if (bParseStatus)
                    {
                        // NOTE:  Allowing 2 digit minutes, i.e., up to 99:59 rather than 
                        // stopping at 59:59
                        if ((nMins > 99 || nMins < 0) || (dSecs >= 60 || dSecs < 0))
                        {
                            bParseStatus = false;
                            sErrorMsg = "Problems parsing time " + sTime;
                        }
                        if (dSecs > dOneFrameDecrement)
                        { // Easy: drop seconds by the frame and we are done
                            dSecs = dSecs - dOneFrameDecrement;
                        }
                        else
                        {
                            if (nMins > 0)
                            { // Drop minutes by 1, give seconds 60, take away dOneFrameDecrement
                                nMins = nMins - 1;
                                dSecs = dSecs + 60 - dOneFrameDecrement;
                            }
                            else if (nHours > 0)
                            { // Give nMinutes 59, seconds 60:
                                nHours = nHours - 1;
                                nMins = 59;
                                dSecs = dSecs + 60 - dOneFrameDecrement;
                            }
                            else
                            {
                                bParseStatus = false;
                                sErrorMsg = "Problems parsing time, as it is too small to be a start time for a later segment (must be at least a second long): " + sTime;
                            }
                        }
                        if (bParseStatus)
                        { // OK, have decremented time, represent it as string.

                            // One last change:  modify mm:ss to 1:mm:ss if mm is in [60-99] range
                            if (nMins >= 60 && nMins <= 99)
                            {
                                nHours++;
                                nMins -= 60;
                            }
                            sTimeDecrementedByOne = nHours.ToString("00") + ":" +
                                  nMins.ToString("00") + ":" +
                                  dSecs.ToString("00.##");
                        }
                    }
                }
                catch (Exception ex)
                {
                    bParseStatus = false;
                    sErrorMsg = "Problems parsing time " + sTime + ", " + ex.Message;
                }
            }

            return bParseStatus;
        }

        /// <summary>
        /// Returns true if at least one name corresponds to the given candidate partition name
        /// (or name string with semi-colon separators if ALLOW_PARTITION_NAMES_ON_ONE_LINE) 
        /// within the world with the given name.
        /// </summary>
        /// <param name="worldName">world name</param>
        /// <param name="candidatePerhapsMixedCase">possible partition name or list of names like "name1;name2"</param>
        /// <param name="returnedPartitionNames">OK'd partition names</param>
        /// <returns>true if found name(s) for given partition name(s) based on myWorldSet, false if not found</returns>
        private bool PermissivePartitionLookup(string worldName, string candidatePerhapsMixedCase, List<string> returnedPartitionNames)
        {
            bool foundPartitionNameMatch = false;
            returnedPartitionNames.Clear();
            if (myWorldSet.MemberWorlds != null)
            {
                for (int iWorld = 0; iWorld < myWorldSet.MemberWorlds.Count; iWorld++)
                {
                    if (myWorldSet.MemberWorlds[iWorld].Name== worldName)
                    { // This is the world whose partitions will be tested:
                        if (myWorldSet.MemberWorlds[iWorld].ChildPartitions != null)
                        {
                            string sVariantForm = candidatePerhapsMixedCase.Replace(" and ", " & "); // soften needed match in this manner

                            if (ALLOW_PARTITION_NAMES_ON_ONE_LINE)
                            {
                                char[] splitters = { ';' };
                                string[] sCandidates = candidatePerhapsMixedCase.Split(splitters);
                                for (int iCandidate = 0; iCandidate < sCandidates.Length; iCandidate++)
                                {
                                    for (int iPartition = 0; iPartition < myWorldSet.MemberWorlds[iWorld].ChildPartitions.Count; iPartition++)
                                        if (sCandidates[iCandidate]
                                            .StartsWith(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name, StringComparison.InvariantCultureIgnoreCase) ||
                                            sVariantForm.StartsWith(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            returnedPartitionNames.Add(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name);
                                            foundPartitionNameMatch = true;
                                            break; // found partition, so exit loop
                                        }
                                }
                            }
                            else
                            {
                                for (int iPartition = 0; iPartition < myWorldSet.MemberWorlds[iWorld].ChildPartitions.Count; iPartition++)
                                    if (candidatePerhapsMixedCase.StartsWith(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name, StringComparison.InvariantCultureIgnoreCase) ||
                                        sVariantForm.StartsWith(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        returnedPartitionNames.Add(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name);
                                        foundPartitionNameMatch = true;
                                        break; // found partition, so exit loop
                                    }
                            }
                            if (!foundPartitionNameMatch)
                            { // Try one last time ignoring commas in both target and source
                                sVariantForm = sVariantForm.Replace(",", "");
                                candidatePerhapsMixedCase = candidatePerhapsMixedCase.Replace(",", "");
                                for (int iPartition = 0; iPartition < myWorldSet.MemberWorlds[iWorld].ChildPartitions.Count; iPartition++)
                                    if (candidatePerhapsMixedCase.StartsWith(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name.Replace(",", ""), StringComparison.InvariantCultureIgnoreCase) ||
                                        sVariantForm.StartsWith(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name.Replace(",", ""), StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        returnedPartitionNames.Add(myWorldSet.MemberWorlds[iWorld].ChildPartitions[iPartition].Name);
                                        foundPartitionNameMatch = true;
                                        break; // found partition, so exit loop
                                    }
                            }
                        }
                        break; // found the world to be checked, so exit loop
                    }
                }
            }
            return foundPartitionNameMatch;
        }

        /// <summary>
        /// Given input of a string of form ss, mm:ss, hh:mm:ss, or hh:mm:ss all 
        /// with optional .ff at end (hundredths of seconds) IF config parameter "myFrameCountInTimeCode" is false, 
        /// else input is in ff or ss:ff or mm:ss:ff or hh:mm:ss:ff with frame count ALWAYS given for "myFrameCountInTimeCode" as true, 
        /// return a string that is the same time, hh:mm:ss.ff format for !myFrameCountInTimeCode, hh:mm:ss:ff for myFrameCountInTimeCode.
        /// </summary>
        /// <param name="sTime">string version of time in hh:mm:ss.ff OR hh:mm:ss:ff format with hh, mm, etc. optional</param>
        /// <param name="sPaddedTime">set to string version of time in strict hh:mm:ss.ff format</param>
        /// <param name="sErrorMsg">error message, if given string will not parse</param>
        /// <returns>true on successful parse, false otherwise</returns>
        private bool PaddedTimeAsHHMMSSFF(string sTime, out string sPaddedTime, out string sErrorMsg)
        {
            bool bParseStatus = true; // assume success
            sPaddedTime = "";
            sErrorMsg = "";

            try
            {
                int nHours = 0;
                int nMins = 0;
                int nSecs = 0; // used only if myFrameCountInTimeCode is true 
                int nFrames = 0; // used only if myFrameCountInTimeCode is true 
                double dSecs = 0.0;
                int iColon1, iColon2, iPeriod;

                if (myFrameCountInTimeCode)
                { // input is in ff or ss:ff or mm:ss:ff or hh:mm:ss:ff format
                    string[] timePieces = sTime.Split(':');
                    if (timePieces.Length > 4)
                    {
                        bParseStatus = false;
                        sErrorMsg = "Too many colons to parse as hh:mm:ss:ff in given time " + sTime;
                    }
                    else
                    {
                        if (timePieces.Length == 4)
                        { // hh:mm:ss:ff
                            nHours = Int32.Parse(timePieces[0]);
                            nMins = Int32.Parse(timePieces[1]);
                            nSecs = Int32.Parse(timePieces[2]);
                            nFrames = Int32.Parse(timePieces[3]);
                        }
                        else if (timePieces.Length == 3)
                        { // mm:ss:ff
                            nMins = Int32.Parse(timePieces[0]);
                            nSecs = Int32.Parse(timePieces[1]);
                            nFrames = Int32.Parse(timePieces[2]);
                        }
                        else if (timePieces.Length == 2)
                        { // ss:ff
                            nSecs = Int32.Parse(timePieces[0]);
                            nFrames = Int32.Parse(timePieces[1]);
                        }
                        else
                        { // ff
                            if (sTime.Length > 0)
                                nFrames = Int32.Parse(sTime);
                            else // keep frames as "00" for empty specification
                                nFrames = 0;
                        }
                    }
                }
                else
                { // parse time with fractional seconds as an option
                    iPeriod = sTime.IndexOf(".");
                    iColon1 = sTime.IndexOf(":");
                    if (iColon1 < 0)
                    {
                        // Just left with ss or ss.ff
                        // Leave nHours = 0 and nMins = 0
                        dSecs = Double.Parse(sTime);
                    }
                    else
                    { // Have at least one colon; see if we have 2

                        iColon2 = sTime.IndexOf(":", iColon1 + 1);
                        // First, take care of bogus input:
                        if (iPeriod >= 0 && (iPeriod < iColon1 || iPeriod < iColon2))
                        {
                            // strange, give up on .hh:mm:ss or whatever (period must come after colons) 
                            bParseStatus = false;
                            sErrorMsg = "Expected period after colon(s) in given time " + sTime;
                        }
                        else
                        {
                            if (iColon2 < 0)
                            { // Have mm:ss or mm:ss.ff
                                // Leave nHours = 0
                                if (iColon1 == 0)
                                    // Not sure if Informedia processing will handle
                                    // :ss.ff as 0:ss.ff but assume so for now...
                                    nMins = 0;
                                else
                                    nMins = Int32.Parse(sTime.Substring(0, iColon1));
                                if (iPeriod > iColon1)
                                {
                                    if (iPeriod == iColon1 + 1)
                                    {
                                        // strange, give up on mm:.ff 
                                        bParseStatus = false;
                                        sErrorMsg = "Expected seconds after colon and before period in given time " + sTime;
                                    }
                                    else
                                        dSecs = Double.Parse(sTime.Substring(iColon1 + 1));
                                }
                                else // no fraction, so nFraction stays = 0
                                    dSecs = Double.Parse(sTime.Substring(iColon1 + 1));
                            }
                            else if (iColon2 == iColon1 + 1)
                            { // reject hh::ss
                                bParseStatus = false;
                                sErrorMsg = "Expected a number for minutes between colons in given time " + sTime;
                            }
                            else
                            { // Have hh:mm:ss or hh:mm:ss.ff
                                if (iColon1 == 0)
                                    // Not sure if Informedia processing will handle
                                    // :mm:ss.ff as 0:mm:ss.ff but assume so for now...
                                    nHours = 0;
                                else
                                    nHours = Int32.Parse(sTime.Substring(0, iColon1));
                                nMins = Int32.Parse(sTime.Substring(iColon1 + 1, iColon2 - iColon1 - 1));
                                if (iPeriod > iColon2)
                                {
                                    if (iPeriod == iColon2 + 1)
                                    {
                                        // strange, give up on hh:mm:.ff 
                                        bParseStatus = false;
                                        sErrorMsg = "Expected seconds after colon and before period in given time " + sTime;
                                    }
                                    else
                                        dSecs = Double.Parse(sTime.Substring(iColon2 + 1));
                                }
                                else // no fraction, so nFraction stays = 0
                                    dSecs = Double.Parse(sTime.Substring(iColon2 + 1));
                            }
                        }
                    }
                }
                if (bParseStatus)
                {
                    // NOTE:  Allowing 2 digit minutes, i.e., up to 99:59 rather than 
                    // stopping at 59:59
                    if ((nMins > 99 || nMins < 0) || (dSecs >= 60 || dSecs < 0))
                    {
                        bParseStatus = false;
                        sErrorMsg = "Problems parsing time " + sTime;
                    }
                    if (bParseStatus)
                    { // OK, represent it as string.
                        // One last change:  modify mm:ss to 1:mm:ss if mm is in [60-99] range
                        if (nMins >= 60 && nMins <= 99)
                        {
                            nHours++;
                            nMins -= 60;
                        }
                        if (myFrameCountInTimeCode)
                            sPaddedTime = nHours.ToString("00") + ":" +
                                    nMins.ToString("00") + ":" + nSecs.ToString("00") + ":" +
                                    nFrames.ToString("00");
                        else // use fractional seconds
                            sPaddedTime = nHours.ToString("00") + ":" +
                                    nMins.ToString("00") + ":" +
                                    dSecs.ToString("00.##");
                    }
                }
            }
            catch (Exception ex)
            {
                bParseStatus = false;
                sErrorMsg = "Problems parsing time " + sTime + ", " + ex.Message;
            }
            return bParseStatus;
        }

        /// <summary>
        /// Convert a given time in seconds into string of hh:mm:ss.ff format (ff fractional seconds, not frames) that is the same time.
        /// </summary>
        /// <param name="timeInSeconds">given time in seconds</param>
        /// <returns>same time in hh:mm:ss.ff format, or 00:00:00.00 on nonsense input (less than 0).</returns>
        private string SecondsToHHMMSSWithFractionalSeconds(double timeInSeconds)
        {
            string sReturnVal = "00:00:00.00";
            if (timeInSeconds > 0)
            {
                int timeAsIntegralSeconds = (int) Math.Floor(timeInSeconds);
                int nVal;
                string sFractionalSecs = "";
                int nHours = timeAsIntegralSeconds / 3600;
                nVal = timeAsIntegralSeconds % 3600;
                int nMinutes = nVal / 60;
                int nSeconds = nVal % 60;
                int nHundredthsSecs = (int)(Math.Floor(timeInSeconds * 100));
                nHundredthsSecs = nHundredthsSecs - (nHours * 360000) - (nMinutes * 6000) - (nSeconds * 100);
                if (nHundredthsSecs > 0)
                    // preserve precision by reporting fractional seconds
                    sFractionalSecs = "." + nHundredthsSecs.ToString("00");
                sReturnVal = nHours.ToString("00") + ":" +
                             nMinutes.ToString("00") + ":" +
                             nSeconds.ToString("00") + sFractionalSecs;
            }
            return sReturnVal;
        }

/*        /// <summary>
        /// Returns given string with all problem characters changed to simpler ones where possible.
        /// Look for problem characters (using http://www.alanwood.net/unicode/general_punctuation.html
        /// for help) -- if we don't have a replacement, gripe and keep as is.
        /// For now, always replace characters in range [8192, 8250] and a few more up to 8292
        /// using the referenced web site for help in the mapping.
        /// </summary>
        /// <param name="sShortFilename">used only in error messaging</param>
        /// <param name="sGiven">given string</param>
        /// <param name="iLineCounter">line counter, used in error reporting to Console</param>
        /// <returns>cleaned up version, perhaps different character length, of given string</returns>
        /// Retired in 2020... */
        //private string CleanUpUTF8(string sShortFilename, string sGiven, int iLineCounter)
        //{
        //    char testChar, cleanChar;
        //    int iChar;
        //    int unicodeValue;
        //    StringBuilder sb = new StringBuilder();

        //    for (iChar = 0; iChar < sGiven.Length; iChar++)
        //    {
        //        testChar = sGiven[iChar];
        //        unicodeValue = System.Convert.ToInt32(testChar);
        //        if (unicodeValue > 127)
        //        {   // Note:  more could be added here; see table at http://www.alanwood.net/demos/ansi.html for
        //            // example to see ý as UTF-8 253 which we could preserve as y instead to allow
        //            // Lemur indexing;  leave off vague translations and see if we tag too many
        //            // down the road as SICK/unknown-untranslated characters....
        //            if (unicodeValue == 201)
        //            { // É becomes E
        //                cleanChar = 'E';
        //            }
        //            else if (unicodeValue >= 224 && unicodeValue <= 229)
        //            { // ä or other variants, e.g., ã, â
        //                cleanChar = 'a';
        //            }
        //            else if (unicodeValue == 231)
        //            { // ç as in façade
        //                cleanChar = 'c';
        //            }
        //            else if (unicodeValue >= 232 && unicodeValue <= 235)
        //            { // é as in Café, ë as in Zoë, or other e variants
        //                cleanChar = 'e';
        //            }
        //            else if (unicodeValue >= 236 && unicodeValue <= 239)
        //            { // ï with dots over it as in naïve or other i variants
        //                cleanChar = 'i';
        //            }
        //            else if (unicodeValue == 241)
        //            { // ñ 
        //                cleanChar = 'n';
        //            }
        //            else if (unicodeValue >= 242 && unicodeValue <= 246)
        //            { // ö and other o variants
        //                cleanChar = 'o';
        //            }
        //            else if (unicodeValue >= 249 && unicodeValue <= 252)
        //            { // ü and other u variants
        //                cleanChar = 'u';
        //            }
        //            else if (unicodeValue >= 8192 && unicodeValue <= 8292)
        //            {
        //                if (unicodeValue >= 8192 && unicodeValue <= 8207)
        //                    cleanChar = ' '; // replace with space
        //                else if (unicodeValue >= 8208 && unicodeValue <= 8209)
        //                    cleanChar = '-'; // replace with hyphen
        //                else if (unicodeValue >= 8210 && unicodeValue <= 8213)
        //                {
        //                    sb.Append('-');
        //                    cleanChar = '-'; // replace long dash with two hyphens
        //                }
        //                else if (unicodeValue == 8214)
        //                    cleanChar = '|'; // replace with vertical bar
        //                else if (unicodeValue == 8215)
        //                    cleanChar = '_'; // replace with underline / low line
        //                else if (unicodeValue >= 8216 && unicodeValue <= 8219)
        //                    cleanChar = '\''; // replace with single quote
        //                else if (unicodeValue >= 8220 && unicodeValue <= 8223)
        //                    cleanChar = '\"'; // replace with double quote
        //                else if (unicodeValue >= 8224 && unicodeValue <= 8227)
        //                    cleanChar = ' '; // replace with space
        //                else if (unicodeValue == 8228 || unicodeValue == 8231)
        //                    cleanChar = '-'; // replace with hyphen
        //                else if (unicodeValue == 8229)
        //                {
        //                    sb.Append('.');
        //                    cleanChar = '.'; // replace with two periods
        //                }
        //                else if (unicodeValue == 8230)
        //                { // ellipsis ...
        //                    sb.Append('.');
        //                    sb.Append('.');
        //                    cleanChar = '.'; // replace with three periods
        //                }
        //                else if (unicodeValue >= 8232 && unicodeValue <= 8239)
        //                    cleanChar = ' '; // replace with space
        //                else if (unicodeValue == 8240 || unicodeValue == 8241)
        //                    cleanChar = '%';
        //                else if (unicodeValue == 8242)
        //                { // prime
        //                    cleanChar = '\'';
        //                }
        //                else if (unicodeValue == 8243)
        //                { // double prime
        //                    sb.Append('\'');
        //                    cleanChar = '\''; // replace with two '
        //                }
        //                else if (unicodeValue == 8244)
        //                { // triple prime ...
        //                    sb.Append('\'');
        //                    sb.Append('\'');
        //                    cleanChar = '\''; // replace with three '
        //                }
        //                else if (unicodeValue == 8245)
        //                { // reverse prime
        //                    cleanChar = '`';
        //                }
        //                else if (unicodeValue == 8246)
        //                { // reverse double prime
        //                    sb.Append('`');
        //                    cleanChar = '`'; // replace with two `
        //                }
        //                else if (unicodeValue == 8247)
        //                { // reverse triple prime ...
        //                    sb.Append('`');
        //                    sb.Append('`');
        //                    cleanChar = '`'; // replace with three `
        //                }
        //                else if (unicodeValue == 8248)
        //                { // caret
        //                    cleanChar = '^';
        //                }
        //                else if (unicodeValue == 8249 || unicodeValue == 8250)
        //                { // single quotation mark (left and right)
        //                    cleanChar = '\'';
        //                }
        //                else if (unicodeValue == 8252)
        //                {
        //                    sb.Append('!');
        //                    cleanChar = '!'; // replace with two !
        //                }
        //                else if (unicodeValue == 8254)
        //                { // overline
        //                    cleanChar = '-';
        //                }
        //                else if (unicodeValue == 8259)
        //                { // hyphen bullet
        //                    cleanChar = '-';
        //                }
        //                else if (unicodeValue == 8260)
        //                { // fraction slash
        //                    cleanChar = '/';
        //                }
        //                else if (unicodeValue == 8263)
        //                { // ?? 
        //                    sb.Append('?');
        //                    cleanChar = '?';
        //                }
        //                else if (unicodeValue == 8264)
        //                { // ?!
        //                    sb.Append('?');
        //                    cleanChar = '!';
        //                }
        //                else if (unicodeValue == 8265)
        //                { // !? 
        //                    sb.Append('!');
        //                    cleanChar = '?';
        //                }
        //                else
        //                {
        //                    this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter +
        //                      ": Possible SICK character, unicode " + unicodeValue + ", " + testChar);
        //                    cleanChar = testChar;
        //                }
        //            }
        //            // GRRRR this won't work.  Word has LOTS of chars mapping to this
        //            // Square Box UNLESS you do the smart thing and follow this procedure:
        //            // BEFORE RUNNING THIS PROGRAM on an XML from WORD, FIRST SAVE THAT XML FROM 
        //            // WORD INTO .TXT WITH UTF8 as the ENCODING TYPE.  THEN PROCESS THAT CLEANER TXT FILE.
        //            // else if (unicodeValue == 65533)
        //            //{ // ’ 
        //            //    cleanChar = '\''; 
        //            //}
        //            else
        //            {
        //                this.OutputProblem(sShortFilename + " PROBLEMS: Line " + iLineCounter +
        //                  ": Possible SICK character, unicode " + unicodeValue + ", " + testChar);
        //                cleanChar = testChar;
        //            }
        //        }
        //        else
        //            cleanChar = testChar;

        //        sb.Append(cleanChar);
        //    }
        //    if (sb.Length <= 0)
        //        return "";
        //    else
        //        return (sb.ToString());
        //}

        /// <summary>
        /// Strips filesystem unfriendly characters from the given string.
        /// </summary>
        /// <param name="filename">The string which needs to be cleaned</param>
        /// <returns>Filename friendly string</returns>
        private string GetSafeFilename(string filename)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^a-zA-Z0-9_]");
            return regex.Replace(filename, "_");
        }
        
        private void OutputStatusLine(string sLine)
        {
            myStatusTextBox.Text = myStatusTextBox.Text + sLine + System.Environment.NewLine;
            myStatusTextBox.SelectionStart = myStatusTextBox.Text.Length;
            myStatusTextBox.ScrollToCaret();
            myStatusTextBox.Refresh();
        }

        private void OutputProblem(string sLine)
        {
            myProblemsTextBox.Text = myProblemsTextBox.Text + sLine + System.Environment.NewLine;
            myProblemsTextBox.SelectionStart = myProblemsTextBox.Text.Length;
            myProblemsTextBox.ScrollToCaret();
            myProblemsTextBox.Refresh();
        }

        #endregion ============================== HELPER FUNCTIONS ================================
    }

    /// <summary>
    /// Holds information on readable full tag and short lenient parsing tag (that front-end needed to parse uniquely).
    /// </summary>
    public class IDVLTAG
    {
        public string Readable;
        public string Lenient;

        /// <summary>
        /// The "value" for tag can be given across multiple input file lines if true; if false, value MUST be given on single input file line
        /// </summary>
        public bool MultipleLinesOKToSpecifyValue;

        /// <summary>
        /// Often ignored (defaulting to ""), set to a valid world Name only if the tag is a "world" name to line up with children partitions.
        /// </summary>
        public string OwnerWorldName;

        /// <summary>
        /// Only consulted if OwnerWorldName is non-empty, this indicates if the input range of values must be within some other specification,
        /// or if any value is OK.  In first release, if this is true, range will be partition names for the world and tag associated with 
        /// worlds and partitions; if false and OwnerWorldName is non-empty, tag associated with open-ended collection annotations (like FAVORITE FOOD)
        /// and value is open-ended.
        /// </summary>
        public bool InputRangeRestricted;

        public IDVLTAG(string givenReadable, string givenLenient, bool givenMultipleLinesForValueOK = false,
            string givenOwnerWorldName = "", bool givenInputRangeRestricted = false)
        {
            Readable = givenReadable;
            Lenient = givenLenient;
            MultipleLinesOKToSpecifyValue = givenMultipleLinesForValueOK;
            OwnerWorldName = givenOwnerWorldName;
            InputRangeRestricted = givenInputRangeRestricted;
        }
    }
}
