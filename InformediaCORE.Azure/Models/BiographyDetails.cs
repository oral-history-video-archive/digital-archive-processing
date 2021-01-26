using System;
using System.Collections.Generic;
using System.Linq;
using InformediaCORE.Common.Database;

namespace InformediaCORE.Azure.Models
{
    /// <summary>
    /// Details regarding a single biographical collection including it's interview
    /// sessions, video tapes, and processed story segments.
    /// </summary>
    public class BiographyDetails
    {
        /// <summary>
        /// Document key. Analogous to CollectionID converted to string
        /// as required by Azure Search
        /// </summary>
        public string BiographyID { get; set; }

        /// <summary>
        /// Accession number
        /// </summary>
        public string Accession { get; set; }

        /// <summary>
        /// THM's DescriptionShort
        /// </summary>
        public string DescriptionShort { get; set; }

        /// <summary>
        /// THM's BiographyShort
        /// </summary>
        public string BiographyShort { get; set; }

        /// <summary>
        /// First name of interview subject
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Last name of interview subject
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Interview subject's full preferred name
        /// </summary>
        public string PreferredName { get; set; }

        /// <summary>
        /// Gender as M or F
        /// </summary>
        public string Gender { get; set; }

        /// <summary>
        /// Link back to the related bio page on main THM web site.
        /// </summary>
        public string WebsiteURL { get; set; }

        /// <summary>
        /// Where the subject currently lives
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Birth city
        /// </summary>
        public string BirthCity { get; set; }

        /// <summary>
        /// Birth state
        /// </summary>
        public string BirthState { get; set; }

        /// <summary>
        /// Birth country
        /// </summary>
        public string BirthCountry { get; set; }

        /// <summary>
        /// Date of birth, may be null
        /// </summary>
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// 2 digit birth day
        /// </summary>
        public int? BirthDay { get; set; }

        /// <summary>
        /// 2 digit birth month
        /// </summary>
        public int? BirthMonth { get; set; }

        /// <summary>
        /// 4 digit birth year
        /// </summary>
        public int? BirthYear { get; set; }

        /// <summary>
        /// Date of death, may be null
        /// </summary>
        public DateTime? DeceasedDate { get; set; }

        /// <summary>
        /// True if biography is part of the ScienceMakers corpus.
        /// </summary>
        public bool IsScienceMaker { get; set; }

        /// <summary>
        /// List of "Maker" category codes
        /// </summary>
        public string[] MakerCategories { get; set; }

        /// <summary>
        /// List of JobType classification codes
        /// </summary>
        public string[] OccupationTypes { get; set; }

        /// <summary>
        /// List of occupations held by the subject
        /// </summary>
        public string[] Occupations { get; set; }

        /// <summary>
        /// Answers to "People Magazine-ish" type questions.
        /// </summary>
        public FavoritesSet Favorites { get; set; }

        /// <summary>
        /// List of Sessions related to this Biography
        /// </summary>
        public List<BiographySession> Sessions { get; set; }

        /// <summary>
        /// Empty constructor allows serialization
        /// </summary>
        public BiographyDetails() { }

        /// <summary>
        /// Instantiates an instance of the BiographyDetails class with information from the given collection.
        /// </summary>
        /// <param name="collection">A database collection object.</param>
        public BiographyDetails(Collection collection)
        {
            int? birthDay = null, birthMonth = null, birthYear = null;
            if (collection.BirthDate != null)
            {
                birthDay = ((DateTime)collection.BirthDate).Day;
                birthMonth = ((DateTime)collection.BirthDate).Month;
                birthYear = ((DateTime)collection.BirthDate).Year;
            }

            BiographyID = collection.CollectionID.ToString();
            Accession = collection.Accession;
            DescriptionShort = collection.DescriptionShort;
            BiographyShort = collection.BiographyShort;
            FirstName = collection.FirstName;
            LastName = collection.LastName;
            PreferredName = collection.PreferredName;
            Gender = collection.Gender.ToString();
            WebsiteURL = collection.WebsiteURL;
            Region = collection.Region;
            BirthCity = collection.BirthCity;
            BirthState = collection.BirthState;
            BirthCountry = collection.BirthCountry;
            BirthDate = collection.BirthDate;
            BirthDay = birthDay;
            BirthMonth = birthMonth;
            BirthYear = birthYear;            
            DeceasedDate = collection.DeceasedDate;
        }
    }

    /// <summary>
    /// Details regarding a single interview session.
    /// </summary>
    public class BiographySession
    {
        /// <summary>
        /// 1-based ordering of the session within the biographical collection.
        /// </summary>
        public int? SessionOrder { get; set; }

        /// <summary>
        /// Date of interview session
        /// </summary>
        public DateTime? InterviewDate { get; set; }

        /// <summary>
        /// Name of person conducting the interview
        /// </summary>
        public string Interviewer { get; set; }

        /// <summary>
        /// Name of person filming the interview
        /// </summary>
        public string Videographer { get; set; }

        /// <summary>
        /// Location of interview
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// List of Tapes in this session
        /// </summary>
        public List<BiographyTape> Tapes { get; set; }

        /// <summary>
        /// Empty constructor allows serialization
        /// </summary>
        public BiographySession() { }

        /// <summary>
        /// Instantiates an instance of the BiographySession class with information from the given session.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="tapes"></param>
        public BiographySession(Session session, List<BiographyTape> tapes)
        {
            SessionOrder = session.SessionOrder;
            InterviewDate = session.InterviewDate;
            Interviewer = session.Interviewer;
            Videographer = session.Videographer;
            Location = session.Location;
            Tapes = tapes;
        }
    }

    /// <summary>
    /// Details regarding a specific video tape within a biographical collection.
    /// </summary>
    public class BiographyTape
    {
        /// <summary>
        /// A brief description about the contents of this tape
        /// </summary>
        public string Abstract { get; set; }

        /// <summary>
        /// List of Stories in this session
        /// </summary>
        public List<BiographyStory> Stories { get; set; }

        /// <summary>
        /// 1-based ordering of the tape within the interview session.
        /// </summary>
        public int? TapeOrder { get; set; }

        /// <summary>
        /// Empty constructor allows serialization
        /// </summary>
        public BiographyTape() { }

        /// <summary>
        /// Instantiates an instance of the BiographyTape class with information from the given movie.
        /// </summary>
        /// <param name="movie">A database movie.</param>
        /// <param name="stories">A list of BiographyStories to append this this instance.</param>
        public BiographyTape(Movie movie, List<BiographyStory> stories)
        {
            Abstract = movie.Abstract;
            Stories = stories;
            TapeOrder = movie.Tape;
        }
    }

    /// <summary>
    /// Details regarding a specific story within the biographical collection.
    /// </summary>
    public class BiographyStory
    {
        /// <summary>
        /// Duration of segment in milliseconds
        /// </summary>
        public int? Duration { get; set; }

        /// <summary>
        /// Document key. Analogous to database SegmentID converted to a 
        /// string as required by Azure Search
        /// </summary>
        public string StoryID { get; set; }

        /// <summary>
        /// 1-based order of the story within the tape.
        /// </summary>
        public int? StoryOrder { get; set; }

        /// <summary>
        /// Story title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// A list of US states detected by NER processing.
        /// </summary>
        public string[] EntityStates { get; set; }

        /// <summary>
        /// Empty constructor allows serialization
        /// </summary>
        public BiographyStory() { }

        /// <summary>
        /// Instantiates an instance of the BiographyStory class with information from the given segement.
        /// </summary>
        /// <param name="segment">A database segment.</param>
        public BiographyStory(Segment segment)
        {
            Duration = segment.Duration;
            StoryID = segment.SegmentID.ToString();
            StoryOrder = segment.SegmentOrder;            
            Title = segment.Title;
            EntityStates = segment.NamedEntities.Where(e => e.Type == NamedEntityType.State).Select(e => e.Value).ToArray();
        }
    }

}
