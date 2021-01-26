using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace InformediaCORE.Azure.Models
{
    /// Azure Search document representing data primarily from the
    /// processing database's Segments table.
    [SerializePropertyNamesAsCamelCase]
    public class Story
    {
        /// <summary>
        /// Document key. Analogous to database SegmentID converted to a 
        /// string as required by Azure Search
        /// </summary>
        [Key, IsSearchable]
        public string StoryID { get; set; }

        /// <summary>
        /// Story title
        /// </summary>
        [IsSearchable, Analyzer(AnalyzerName.AsString.EnMicrosoft)]
        public string Title { get; set; }
        /// <summary>
        /// Transcription of story
        /// </summary>
        [IsSearchable, Analyzer(AnalyzerName.AsString.EnMicrosoft)]
        public string Transcript { get; set; }

        /// <summary>
        /// Database id of parent collection (biography)
        /// </summary>
        [IsFilterable, IsSortable, IsSearchable]
        public string BiographyID { get; set; }

        /// <summary>
        /// Filterable, Facetable
        /// </summary>
        [IsFilterable, IsFacetable]
        public int? BirthYear { get; set; }

        /// <summary>
        /// US States mentioned in story
        /// </summary>
        [IsFilterable, IsFacetable]
        public string[] EntityStates { get; set; }

        /// <summary>
        /// Countries mentioned in story
        /// </summary>
        [IsFilterable, IsFacetable]
        public string[] EntityCountries { get; set; }

        /// <summary>
        /// Organizations mentioned in story
        /// </summary>
        [IsFilterable, IsFacetable]
        public string[] EntityOrganizations { get; set; }

        /// <summary>
        /// Years mentioned in story
        /// </summary>
        [IsFilterable, IsFacetable]
        public int[] EntityYears { get; set; }

        /// <summary>
        /// Decades mentioned in story
        /// </summary>
        [IsFilterable, IsFacetable]
        public int[] EntityDecades { get; set; }

        /// <summary>
        /// Filterable, Facetable
        /// </summary>
        [IsFilterable, IsFacetable]
        public string Gender { get; set; }

        /// <summary>
        /// From Sessions table, may be null
        /// </summary>
        [IsFilterable, IsSortable, IsFacetable]
        public DateTime? InterviewDate { get; set; }

        /// <summary>
        /// HistoryMaker Category Facet - stored as a lookup identifier
        /// </summary>
        [IsFilterable, IsFacetable]
        public string[] MakerCategories { get; set; }

        /// <summary>
        /// Occupation Type Facet - stored as a lookup identifier
        /// </summary>
        [IsFilterable, IsFacetable]
        public string[] OccupationTypes { get; set; }

        /// <summary>
        /// Subjective annotations added by The HistoryMakers
        /// </summary>
        [IsFilterable, IsFacetable]
        public string[] Tags { get; set; }

        /// <summary>
        /// Duration of segment in milliseconds
        /// </summary>
        public int? Duration { get; set; }

        /// <summary>
        /// 1-based ordering of the session within the biographical collection.
        /// </summary>
        public int? SessionOrder { get; set; }

        /// <summary>
        /// 1-based ordering of the tape within the interview session.
        /// </summary>
        public int? TapeOrder { get; set; }

        /// <summary>
        /// 1-based story ordering across all sessions
        /// </summary>
        public int? StoryOrder { get; set; }

        /// <summary>
        /// Filter for counting/selecting only ScienceMaker stories.
        /// </summary>
        [IsFilterable]
        public bool IsScienceMaker { get; set; }

        /// <summary>
        /// Filter for counting/selecting only tagged stories.
        /// </summary>
        [IsFilterable]
        public bool IsTagged { get; set; }
    }
}