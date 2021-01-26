using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace InformediaCORE.Azure.Models
{
    /// <summary>
    /// Azure Search document representing data primarily from the processing database's Collections table.
    /// </summary>
    [SerializePropertyNamesAsCamelCase]
    public class Biography
    {
        /// <summary>
        /// Document key. Analogous to CollectionID converted to string as required by Azure Search
        /// </summary>
        [Key]
        public string BiographyID { get; set; }

        /// <summary>
        /// The HistoryMakers' accession identifier
        /// </summary>
        [IsFilterable, IsSearchable]
        public string Accession { get; set; }

        /// <summary>
        /// The HistoryMakers' short description
        /// </summary>
        [IsSearchable, Analyzer(AnalyzerName.AsString.EnMicrosoft)]
        public string DescriptionShort { get; set; }

        /// <summary>
        /// The HistoryMakers' short biography
        /// </summary>
        [IsSearchable, Analyzer(AnalyzerName.AsString.EnMicrosoft)]
        public string BiographyShort { get; set; }

        /// <summary>
        /// Subject's US state of birth
        /// </summary>
        [IsFilterable, IsFacetable]
        public string BirthState { get; set; }

        /// <summary>
        /// Late addition to the search index, it's possible that we can do all date 
        /// faceting with just this field and eliminate the need for the separate 
        /// Day, Month, and Year fields below.
        /// See: https://msdn.microsoft.com/en-us/library/hh169248(v=nav.90).aspx
        /// </summary>
        [IsFilterable, IsSortable]
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// 2 digit birth day - used for faceting by day of the month
        /// </summary>
        [IsFilterable, IsSortable]
        public int? BirthDay { get; set; }

        /// <summary>
        /// 2 digit birth month - used for faceting by month
        /// </summary>
        [IsFilterable, IsSortable]
        public int? BirthMonth { get; set; }

        /// <summary>
        /// 4 digit birth year - used for faceting by year
        /// </summary>
        [IsFilterable, IsSortable, IsFacetable]
        public int? BirthYear { get; set; }

        /// <summary>
        /// Date of death, may be null
        /// </summary>
        [IsFilterable]
        public DateTime? DeceasedDate { get; set; }

        /// <summary>
        /// Facet
        /// </summary>
        [IsFilterable, IsFacetable]
        public string Gender { get; set; }

        /// <summary>
        /// Subject's first name
        /// </summary>
        [IsFilterable, IsSearchable, Analyzer(AnalyzerName.AsString.EnMicrosoft)]
        public string FirstName { get; set; }

        /// <summary>
        /// Subject's last name
        /// </summary>
        [IsFilterable, IsSortable, IsSearchable, Analyzer(AnalyzerName.AsString.EnMicrosoft)]
        public string LastName { get; set; }

        /// <summary>
        /// Used to facet by first letter of last name.
        /// </summary>
        [IsFilterable, IsFacetable]
        public string LastInitial { get; set; }

        /// <summary>
        /// Subject's preferred full name for display
        /// </summary>
        [IsSearchable]
        public string PreferredName { get; set; }

        /// <summary>
        /// Facet - stored as a lookup identifier
        /// </summary>
        [IsFilterable, IsFacetable]
        public string[] MakerCategories { get; set; }

        /// <summary>
        /// Facet - stored as a lookup identifier
        /// </summary>
        [IsFilterable, IsFacetable]
        public string[] OccupationTypes { get; set; }

        /// <summary>
        /// Filter for counting/selecting only ScienceMaker biographies.
        /// </summary>
        [IsFilterable]
        public bool IsScienceMaker { get; set; }

        /// <summary>
        /// Filter for counting/selecting only tagged biographies
        /// </summary>
        [IsFilterable]
        public bool IsTagged { get; set; }
    }
}
