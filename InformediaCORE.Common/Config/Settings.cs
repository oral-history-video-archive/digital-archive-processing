using System.Collections.Generic;
using System.Security.Policy;
using InformediaCORE.Common.Xml;

namespace InformediaCORE.Common.Config
{
    /// <summary>
    /// Global configuration class for the entire processing stack.
    /// </summary>
    /// <remarks>
    /// Based on Singleton pattern as presented in MSDN documentation.
    /// See http://msdn.microsoft.com/en-us/library/ff650316.aspx.
    /// </remarks>
    public class Settings
    {
        /// <summary>
        /// The connection string specifying the SQL Server database to use for processing.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The root of the output directory for generated content.
        /// </summary>
        public string BuildPath { get; set; }

        /// <summary>
        /// Email settings used to send notifications to the processing team.
        /// </summary>
        public EmailSettings Email { get; set; }

        /// <summary>
        /// Paths to external tools / APIs
        /// </summary>
        public ExternalToolSettings ExternalTools { get; set; }

        /// <summary>
        /// Settings pertaining to the forced alignment data formatter.
        /// </summary>
        public AlignmentFormatterSettings AlignmentFormatter { get; set; }

        /// <summary>
        /// Settings pertaining to the text captioning task.
        /// </summary>
        public CaptioningTaskSettings CaptioningTask { get; set; }

        /// <summary>
        /// Settings pertaining to the named entity resolution task.
        /// </summary>
        public EntityResolutionTaskSettings EntityResolutionTask { get; set; }

        /// <summary>
        /// Settings pertaining to the video transcoding task.
        /// </summary>
        public TranscodingTaskSettings TranscodingTask { get; set; }

        /// <summary>
        /// Settings pertaining to the procesing (review) instance of the Digital Archive.
        /// </summary>
        public AzureSettings Processing { get; set; }

        /// <summary>
        /// Settings pertaining to the production (public) instance of the Digital Archive.
        /// </summary>
        public AzureSettings Production { get; set; }

        /// <summary>
        /// Returns the single instance of the Settings class.
        /// </summary>
        public static Settings Current { get; } = XmlUtilities.Read<Settings>("informediaCORE.config");

        /// <summary>
        /// Prevents external callers from initializing their own version of the class.
        /// </summary>
        private Settings() {}
    }


    /// <summary>
    /// Azure Digital Archive Settings
    /// </summary>
    public class AzureSettings
    {
        /// <summary>
        /// The name of the Azure Search service used by the configured instance of the Digital Archive.
        /// </summary>
        public string AzureSearchServiceName { get; set; }

        /// <summary>
        /// The Azure Search API Key corresponding to AzureSearchServiceName.
        /// </summary>
        public string AzureSearchApiKey { get; set; }

        /// <summary>
        /// The name of the Azure Storage account used by the configured instance of the Digital Archive.
        /// </summary>
        public string AzureStorageAccountName { get; set; }

        /// <summary>
        /// The API key corresponding to AzureStorageAccountName.
        /// </summary>
        public string AzureStorageAccountKey { get; set; }

        /// <summary>
        /// The URL used to review content.
        /// </summary>
        public string BiographyDetailsUrl { get; set; }

        /// <summary>
        /// If true, completed sessions will be automatically published to the test site.
        /// </summary>
        public bool? AutoPublish { get; set; }

        /// <summary>
        /// If true, segment tags will be imported from Firebase during AutoPublish process.
        /// </summary>
        public bool? AutoPublishTagImport { get; set; }
    }
    
    
    /// <summary>
    /// Email server settings
    /// </summary>
    public class EmailSettings
    {
        /// <summary>
        /// The SendGrid-verified "from" address for notification emails sent by this system.
        /// </summary>
        /// <remarks>
        /// This must be a REAL email address and it MUST be a verified sender.
        /// SEE: https://sendgrid.com/docs/for-developers/sending-email/sender-identity/
        /// </remarks>
        public string SenderAddress { get; set; }

        /// <summary>
        /// The list of email addresses to receive notification emails.
        /// </summary>
        [System.Xml.Serialization.XmlArrayItemAttribute("Address")]
        public List<string> Recipients { get; set; }

        /// <summary>
        /// The SendGrid API key.
        /// </summary>
        public string SendGridApiKey { get; set; }

        /// <summary>
        /// Determines what types of email messages are sent. Default is All.
        /// </summary>
        public EmailMessageLevel MessageLevel { get; set; } = EmailMessageLevel.All;
    }

    /// <summary>
    /// Defines the possible values for the MessageLevel setting
    /// </summary>
    public enum EmailMessageLevel
    {
        All,
        ErrorsOnly,
        None
    }


    /// <summary>
    /// Paths to external tools / APIs
    /// </summary>
    public class ExternalToolSettings
    {
        /// <summary>
        /// Fully qualified path to where PowerShell Core 6+ binaries are installed.
        /// </summary>
        public string PowerShellPath { get; set; }

        /// <summary>
        /// Fully qualified path to directory containing the FFmpeg binaries
        /// </summary>
        public string FFmpegPath { get; set; }

        /// <summary>
        /// Fully qualified path to the directory where Python and Spacy are installed.
        /// </summary>
        public string SpacyPath { get; set; }

        /// <summary>
        /// Fully qualified path to the directory with the Java binaries are installed.
        /// </summary>
        public string JavaPath { get; set; }

        /// <summary>
        /// Fully qualified path to the directory where the Stanford NER JAR files are installed.
        /// </summary>
        public string SNERPath { get; set; }

        /// <summary>
        /// Root URL to the inferential tag Firebase database.
        /// </summary>
        public string FirebaseURL { get; set; }
    }


    public class AlignmentFormatterSettings
    {
        /// <summary>
        /// Large numbers of unaligned words at the end of the transcript
        /// are a strong indication of segmentation issues.  Unaligned word
        /// blocks at the tail end of the transcript will be deleted if they
        /// exceed this threshold.
        /// </summary>
        public int MaxUnalignedTrailingWordsAllowed { get; set; }
    }


    /// <summary>
    /// Settings applicable to the CaptioningTask.
    /// </summary>
    public class CaptioningTaskSettings
    {
        /// <summary>
        /// Reverse the speaker order if the ratio of characters spoken
        /// by Speaker1 compared to Speaker2 exceeds this value.
        /// </summary>
        public double Speaker1ToSpeaker2CharRatio { get; set; }

        /// <summary>
        /// Maximum number of characters allow per cue.
        /// </summary>
        public int MaxCueLength { get; set; }

        /// <summary>
        /// Target number of characters per cue.
        /// </summary>
        public int TargetLength { get; set; }

        /// <summary>
        /// Minimum number of milliseconds a cue should be on screen.
        /// </summary>
        public int MinCueDuration { get; set; }

        /// <summary>
        /// Maximum number of milliseconds a cue should be on screen.
        /// </summary>
        public int MaxCueDuration { get; set; }

        /// <summary>
        /// Target duration for each cue in milliseconds.
        /// </summary>
        public double TargetDuration { get; set; }

        /// <summary>
        /// Maximum number of lines per cue.
        /// </summary>
        public int MaxCueLineCount { get; set; }
    }


    /// <summary>
    /// Settings applicable to the EntityResolutionTask
    /// </summary>
    public class EntityResolutionTaskSettings
    {
        /// <summary>
        /// Path to the data files containing things like USGS state codes, et cetera.
        /// </summary>
        public string DataPath { get; set; }
    }


    /// <summary>
    /// Settings applicable to the TranscodingTask.
    /// </summary>
    public class TranscodingTaskSettings
    {
        /// <summary>
        /// Dertermines the maximum allowable difference between the actual duration of a segmented video 
        /// and the expected duration based on the given start and end times.  An error will be raised if
        /// the difference exceeds this value.
        /// </summary>
        public int MaximumAllowableDeltaMS { get; set; }

        /// <summary>
        /// Maps input resolutions to output resolutions.
        /// </summary>
        public List<ResolutionMapping> ResolutionMappings { get; set; }
    }


    /// <summary>
    /// Defines the mapping of input (source) to output (target) resolution.
    /// </summary>
    public class ResolutionMapping
    {
        /// <summary>
        /// Resolution of source (input) video.
        /// </summary>
        public Resolution Source { get; set; }

        /// <summary>
        /// Resolution of taget (output) video.
        /// </summary>
        public Resolution Target { get; set; }
    }


    /// <summary>
    /// Defines the width and heigth of a video/image.
    /// </summary>
    public class Resolution
    {
        /// <summary>
        /// Width in pixels.
        /// </summary>
        [System.Xml.Serialization.XmlAttribute]
        public int Width  { get; set; }

        /// <summary>
        /// Height in pixels.
        /// </summary>
        [System.Xml.Serialization.XmlAttribute]
        public int Height { get; set; }
    }
}
