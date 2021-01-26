using System;
using System.Collections.Generic;

namespace InformediaCORE.Azure.Models
{
    /// <summary>
    /// Details regarding a specific story including information from the parent 
    /// biographical collection needed for proper citation.
    /// </summary>
    public class StoryDetails
    {
        /// <summary>
        /// Document key. Analogous to database SegmentID converted to a 
        /// string as required by Azure Search
        /// </summary>
        public string StoryID { get; set; }

        /// <summary>
        /// Additional information necessary to generate the citation.
        /// </summary>
        public StoryCitation Citation { get; set; }

        /// <summary>
        /// Video aspect ratio in form of 4:3 or 16:9.
        /// </summary>
        public string AspectRatio { get; set; }

        /// <summary>
        /// Duration of segment in milliseconds
        /// </summary>
        public int? Duration { get; set; }

        /// <summary>
        /// Answers to "People Magazine-ish" type questions.
        /// </summary>
        public FavoritesSet Favorites { get; set; }

        /// <summary>
        /// True if story is part of the ScienceMakers corpus.
        /// </summary>
        public bool IsScienceMaker { get; set; }

        /// <summary>
        /// List of "maker" category codes
        /// </summary>
        public string[] MakerCategories { get; set; }

        /// <summary>
        /// Location of matching query terms within the transcript.
        /// </summary>
        public List<MatchTerm> MatchTerms { get; set; }

        /// <summary>
        /// ID of the next story in the interview.
        /// </summary>
        public int? NextStory { get; set; }

        /// <summary>
        /// List of occupations
        /// </summary>
        public string[] Occupations { get; set; }

        /// <summary>
        /// List of JobType codes
        /// </summary>
        public string[] OccupationTypes { get; set; }

        /// <summary>
        /// ID of the previously story in the interview.
        /// </summary>
        public int? PrevStory { get; set; }

        /// <summary>
        /// Location of story within parent tape.
        /// </summary>
        public int? StartTime { get; set; }

        /// <summary>
        /// 1-based story ordering across all sessions
        /// </summary>
        public int? StoryOrder { get; set; }

        /// <summary>
        /// Highly subjective tags added to only a small subset of the
        /// The HistoryMaker corpus.
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// Timing data required to align transcript text to video.
        /// </summary>
        public List<TimingPair> TimingPairs { get; set; }

        /// <summary>
        /// Story title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Transcription of story
        /// </summary>
        public string Transcript { get; set; }
    }

    /// <summary>
    /// Additional attributes necessary to cite the story.
    /// </summary>
    public class StoryCitation
    {
        /// <summary>
        /// Short description for parent biography.
        /// </summary>
        public string DescriptionShort { get; set; }

        /// <summary>
        /// Accession number of parent biography.
        /// </summary>
        public string Accession { get; set; }

        /// <summary>
        /// Document key of the parent biography.
        /// </summary>
        public string BiographyID { get; set; }

        /// <summary>
        /// Facetable, sortable
        /// </summary>
        public int? BirthYear { get; set; }

        /// <summary>
        /// Facetable, sortable
        /// </summary>
        public string Gender { get; set; }

        /// <summary>
        /// From Sessions table, may be null
        /// </summary>
        public DateTime? InterviewDate { get; set; }

        /// <summary>
        /// From Sessions table
        /// </summary>
        public string Interviewer { get; set; }

        /// <summary>
        /// From Sessions table
        /// </summary>
        public string Videographer { get; set; }

        /// <summary>
        /// From Sessions table {get; set; }
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Subject's preferred full name for display
        /// </summary>
        public string PreferredName { get; set; }

        /// <summary>
        /// 1-based session ordering.
        /// </summary>
        public int? SessionOrder { get; set; }

        /// <summary>
        /// 1-based tape ordering.
        /// </summary>
        public int? TapeOrder { get; set; }
    }

    /// <summary>
    /// A list of transcript regions which matched one or more of the given query terms.
    /// </summary>
    public class MatchTerm
    {
        /// <summary>
        /// The term's starting character offset as given by the the Azure Search Language Analyzer.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// The term's ending offset as given by the Azure Search Language Analzyer.
        /// </summary>
        public int EndOffset { get; set; }
    }

    /// <summary>
    /// Data which aligns the transcript text to the spoken audio track.
    /// </summary>
    public class TimingPair
    {
        /// <summary>
        /// The transcript text position as a character offset.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// The video time in milliseconds
        /// </summary>
        public int Time { get; set; }
    }
}
