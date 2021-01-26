using System;
using System.Collections.Generic;
using System.Linq;
using InformediaCORE.Azure.Models;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Firebase;

namespace InformediaCORE.Azure
{
    /// <summary>
    /// Encapsulates the data required to publish a database collection to Azure.
    /// </summary>
    public abstract class AbstractPackage : IDisposable
    {
        #region ====================         PROTECTED  DECLARATIONS         ====================
        /// <summary>
        /// The database context used by all internal methods.
        /// </summary>
        protected IDVLDataContext _context;

        /// <summary>
        /// Caches the interview subject's answers to regarding their favorite things.
        /// </summary>
        protected FavoritesSet _favorites;

        /// <summary>
        /// Caches the collection's MakerCategories
        /// </summary>
        protected List<string> _categories;

        /// <summary>
        /// Caches the collection's JobTypes
        /// </summary>
        protected List<string> _jobTypes;

        /// <summary>
        /// Caches the collection's Occupation annotations
        /// </summary>
        protected List<string> _occupations;

        /// <summary>
        /// Indicates if the known collection is a science maker or not.
        /// </summary>
        protected bool _isScienceMaker;

        /// <summary>
        /// The collection to be published.
        /// </summary>
        protected Collection _collection;

        /// <summary>
        /// The set of Sessions to be published
        /// </summary>
        protected List<Session> _sessions;

        /// <summary>
        /// The list of segments to be published.
        /// </summary>
        protected readonly List<Segment> _segments = new List<Segment>();
        #endregion =================         PROTECTED  DECLARATIONS         ====================

        #region ====================          PROTECTED  PROPERTIES          ====================
        /// <summary>
        /// Get the 4-digit birth year for the known collection.
        /// </summary>
        protected int? BirthYear => _collection?.BirthDate?.Year;

        /// <summary>
        /// Get the 1's based birth month for the known collection.
        /// </summary>
        protected int? BirthMonth => _collection?.BirthDate?.Month;

        /// <summary>
        /// Get the calendar birth day for the known collection.
        /// </summary>
        protected int? BirthDay => _collection?.BirthDate?.Day;

        /// <summary>
        /// Get the gender for the known collection.
        /// </summary>
        protected string Gender => _collection?.Gender.ToString();
        #endregion =================          PROTECTED  PROPERTIES          ====================

        #region ====================            PUBLIC PROPERTIES            ====================
        /// <summary>
        /// Gets the searchable Biography document representing the loaded collection.
        /// </summary>
        public Biography Biography { get; protected set; }

        /// <summary>
        /// Gets the BiographyDetails document representing the loaded collection.
        /// </summary>
        public BiographyDetails BiographyDetails { get; protected set; }

        /// <summary>
        /// Gets a list of searchable Story documents belonging to the loaded collection.
        /// </summary>
        public List<Story> Stories { get; protected set; }

        /// <summary>
        /// Gets a list of StoryDetails documents belonging to the loaded collection.
        /// </summary>
        public List<StoryDetails> StoryDetails { get; protected set; }

        /// <summary>
        /// Gets the Accession identifier for the loaded collection.
        /// </summary>
        public string Accession
        {
            get { return _collection.Accession; }
        }

        /// <summary>
        /// Gets the current publishing Phase for the loaded collection.
        /// </summary>
        public PublishingPhase Phase
        {
            get { return (PublishingPhase)_collection.Phase; }
        }
        #endregion =================            PUBLIC PROPERTIES            ====================

        #region ====================      IMPLEMENTATION OF IDISPOSABLE      ====================
        /****************************************************************************************
         * Implementation of IDisposable Interface which ensures that the IDVLDataContext is
         * disposed of properly.  This relatively simple pattern is taken from:
         * 
         * How to Implement IDisposable and Finalizers: 3 Easy Rules
         * http://blog.stephencleary.com/2009/08/how-to-implement-idisposable-and.html
         * 
         * Specifically: The Second Rule of Implementing IDisposable and Finalizers
         * http://blog.stephencleary.com/2009/08/second-rule-of-implementing-idisposable.html
         ****************************************************************************************/
        /// <summary>
        /// Public implementation of Dispose pattern callable by consumers. 
        /// </summary>
        public void Dispose()
        {
            if (_context != null) _context.Dispose();
        }
        #endregion =================      IMPLEMENTATION OF IDISPOSABLE      ====================

        #region ====================            PROTECTED METHODS            ====================
        /// <summary>
        /// Retrieves the segment corresponding to the given StoryID
        /// </summary>
        /// <param name="storyID">A story identifier.</param>
        /// <returns>A database segment on success; null otherwise.</returns>
        protected Segment GetSegment(string storyID)
        {
            if (_segments == null)
            {
                return null;
            }

            var segmentID = int.Parse(storyID);

            var segment = (from s in _segments
                           where s.SegmentID == segmentID
                           select s).FirstOrDefault();

            return segment;
        }

        /// <summary>
        /// Returns the fist letter of the given name.
        /// </summary>
        /// <param name="name">A person's name.</param>
        /// <returns>An uppercase letter on success; empty string otherwise</returns>
        protected string GetInitial(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return name.Substring(0, 1).ToUpper();
        }
        #endregion =================            PROTECTED METHODS            ====================

        #region ====================            DATABASE  METHODS            ====================
        /// <summary>
        /// Returns a list of publishing phases suitable for (re)publishing to the given target.
        /// </summary>
        /// <param name="target">The digital archive which this content will be published to.</param>
        /// <returns></returns>
        protected List<char> AllowedPhases(DigitalArchiveSpecifier target)
        {
            if (target == DigitalArchiveSpecifier.Processing)
            {
                return new List<char> { 'R', 'P' };
            }
            else
            {
                return new List<char> { 'P' };
            }
        }

        /// <summary>
        /// Caches the interviewee's "favorites" set.
        /// </summary>
        protected void LoadCollectionAnnotations()
        {
            // Cache AnnotationType identifiers.
            var annotationTypes = (from t in _context.AnnotationTypes select t);
            var favoriteColor = annotationTypes.Where(t => t.Name == "Favorite Color").Select(t => t.TypeID).Single();
            var favoriteFood = annotationTypes.Where(t => t.Name == "Favorite Food").Select(t => t.TypeID).Single();
            var favoriteQuote = annotationTypes.Where(t => t.Name == "Favorite Quote").Select(t => t.TypeID).Single();
            var favoriteTimeOfYear = annotationTypes.Where(t => t.Name == "Favorite Time of Year").Select(t => t.TypeID).Single();
            var favoriteVacationSpot = annotationTypes.Where(t => t.Name == "Favorite Vacation Spot").Select(t => t.TypeID).Single();
            var occupationID = annotationTypes.Where(t => t.Name == "Occupation").Select(t => t.TypeID).Single();

            _favorites = new FavoritesSet();
            _occupations = new List<string>();

            var annotations = (from a in _context.Annotations
                               where a.CollectionID == _collection.CollectionID
                               select a);

            foreach (var annotation in annotations)
            {
                if (annotation.TypeID == favoriteColor)
                    _favorites.Color = annotation.Value;
                else if (annotation.TypeID == favoriteFood)
                    _favorites.Food = annotation.Value;
                else if (annotation.TypeID == favoriteQuote)
                    _favorites.Quote = annotation.Value;
                else if (annotation.TypeID == favoriteTimeOfYear)
                    _favorites.TimeOfYear = annotation.Value;
                else if (annotation.TypeID == favoriteVacationSpot)
                    _favorites.VacationSpot = annotation.Value;
                else if (annotation.TypeID == occupationID)
                    _occupations.Add(annotation.Value);
            }
        }

        /// <summary>
        /// Caches the "Maker" categories and Job Types (a.k.a. occupations) for the collection.
        /// </summary>
        protected void LoadMakerCategoriesAndJobTypes()
        {
            // Cache numeric identifiers for JobType and MakerCategory from Worlds table
            var worlds = (from w in _context.Worlds select w);
            var jobTypeID = worlds.Where(w => w.Name == "Job Type").Single().WorldID;
            var categoryID = worlds.Where(w => w.Name == "Maker Group").Single().WorldID;

            // Cache numeric identifiers from Partitions table
            var partitions = (from p in _context.Partitions select p).OrderBy(p => p.PartitionID).ToList();

            var memberPartitions = (from pm in _context.PartitionMembers
                                    where pm.CollectionID == _collection.CollectionID
                                    select pm).OrderBy(p => p.PartitionID);

            _categories = new List<string>();
            _jobTypes = new List<string>();

            foreach (var memberPartition in memberPartitions)
            {
                var partition = (from p in partitions
                                 where p.PartitionID == memberPartition.PartitionID
                                 select p).FirstOrDefault();

                if (partition?.WorldID == jobTypeID)
                {
                    _jobTypes.Add(partition.PartitionID.ToString());
                }
                else if (partition?.WorldID == categoryID)
                {
                    _categories.Add(partition.PartitionID.ToString());
                }
                else
                {
                    Logger.Warning("Unknown WorldID: {0}", partition?.WorldID);
                }
            }

            var scienceMakerPartionID = (
                from p in _context.Partitions
                where p.Name == "ScienceMaker"
                select p.PartitionID).Single().ToString();

            // Cache boolean indicating if person is a ScienceMaker
            _isScienceMaker = _categories.Contains(scienceMakerPartionID);
        }

        /// <summary>
        /// Get the list of THM*NEW annoations related to the given segment.
        /// </summary>
        /// <returns>A list of subject identifiers in the form 00_11_22.</returns>
        protected List<string> GetTheHistoryMakerTags(int segmentID)
        {
            var tagTypeID = _context.AnnotationTypes
                .Where(t => t.Name == "THM*NEW")
                .Select(t => t.TypeID).Single();

            var tags = _context.Annotations
                 .Where(a => a.SegmentID == segmentID && a.TypeID == tagTypeID)
                 .Select(a => a.Value.Substring(0, 8))
                 .ToHashSet();  // HashSet eliminates duplicates

            return tags.ToList();
        }
        #endregion =================            DATABASE  METHODS            ====================

        #region ====================             CLASS FACTORIES             ====================
        /// <summary>
        /// Instantiate a Biography instance from the given database Collection.
        /// </summary>
        /// <param name="collection">The source Collection from the processing database.</param>
        /// <returns>An index-ready Biography instance.</returns>
        /// <remarks>Must be called AFTER all stories have initiazed.</remarks>
        protected Biography CreateBiography(Collection collection)
        {
            var biography = new Biography
            {
                BiographyID = collection.CollectionID.ToString(),
                Accession = collection.Accession,
                DescriptionShort = collection.DescriptionShort,
                BiographyShort = collection.BiographyShort,
                BirthState = collection.BirthState,
                BirthDate = collection.BirthDate,
                BirthDay = BirthDay,
                BirthMonth = BirthMonth,
                BirthYear = BirthYear,
                DeceasedDate = collection.DeceasedDate,
                Gender = Gender,
                FirstName = collection.FirstName,
                LastName = collection.LastName,
                LastInitial = GetInitial(collection.LastName),
                PreferredName = collection.PreferredName,
                MakerCategories = _categories.ToArray(),
                OccupationTypes = _jobTypes.ToArray(),
                IsScienceMaker = _isScienceMaker,
                IsTagged = Stories.Any(s => s.IsTagged)
            };

            return biography;
        }

        /// <summary>
        /// Instantiate a Story instance from the given database Segment.
        /// </summary>
        /// <param name="segment">The source Segment from the proecessing database.</param>
        /// <returns>An index-ready Story instance.</returns>
        protected Story CreateStory(Segment segment)
        {
            var session = segment.Movie.Session;
            var tags = GetTheHistoryMakerTags(segment.SegmentID).ToArray();

            var entities = segment.NamedEntities;

            var entityStates = entities
                .Where(e => e.Type == NamedEntityType.State)
                .Select(e => e.Value);

            var entityCountries = entities
                .Where(e => e.Type == NamedEntityType.Country)
                .Select(e => e.Value);

            var entityOrganizations = entities
                .Where(e => e.Type == NamedEntityType.Organization)
                .Select(e => e.Value);

            // Following solution in StackOverflow article
            // https://stackoverflow.com/questions/4961675/select-parsed-int-if-string-was-parseable-to-int
            var entityYears = entities
                .Where(e => e.Type == NamedEntityType.Year)
                .Select(e => { bool success = int.TryParse(e.Value, out int number); return new { number, success }; })
                .Where(pair => pair.success)
                .Select(pair => pair.number);

            var entityDecades = entities
                .Where(e => e.Type == NamedEntityType.Decade)
                .Select(e => { bool success = int.TryParse(e.Value, out int number); return new { number, success }; })
                .Where(pair => pair.success)
                .Select(pair => pair.number);

            return new Story
            {
                StoryID = segment.SegmentID.ToString(),
                Title = segment.Title,
                Transcript = segment.TranscriptText,
                BiographyID = _collection.CollectionID.ToString(),
                BirthYear = BirthYear,
                EntityStates = entityStates.ToArray(),
                EntityCountries = entityCountries.ToArray(),
                EntityOrganizations = entityOrganizations.ToArray(),
                EntityYears = entityYears.ToArray(),
                EntityDecades = entityDecades.ToArray(),
                Gender = Gender,
                InterviewDate = session.InterviewDate,
                MakerCategories = _categories.ToArray(),
                OccupationTypes = _jobTypes.ToArray(),
                Tags = tags,
                Duration = segment.Duration ?? -1,
                SessionOrder = session.SessionOrder,
                TapeOrder = segment.Movie.Tape,
                StoryOrder = segment.SegmentOrder ?? -1,
                IsScienceMaker = _isScienceMaker,
                IsTagged = tags.Any()
            };
        }

        /// <summary>
        /// Instantiate a StoryDetails instance from the given database Segment.
        /// </summary>
        /// <param name="segment">The source Segment from the processing database.</param>
        /// <returns>A fully populated StoryDetails instance.</returns>
        protected StoryDetails CreateStoryDetails(Segment segment)
        {
            var movie = segment.Movie;
            var session = movie.Session;

            var citation = new StoryCitation
            {
                DescriptionShort = _collection.DescriptionShort,
                Accession = _collection.Accession,
                BiographyID = _collection.CollectionID.ToString(),
                BirthYear = BirthYear,
                Gender = Gender,
                InterviewDate = session.InterviewDate,
                Interviewer = session.Interviewer,
                Videographer = session.Videographer,
                Location = session.Location,
                PreferredName = _collection.PreferredName,
                SessionOrder = session.SessionOrder,
                TapeOrder = movie.Tape
            };

            var tsync = new TranscriptSync(segment.TranscriptSync);
            var timing = new List<TimingPair>();

            foreach (var pair in tsync.SyncPairs)
            {
                timing.Add(new TimingPair { Offset = pair.offset, Time = pair.time });
            }

            var storyDetails = new StoryDetails
            {
                StoryID = segment.SegmentID.ToString(),
                Citation = citation,
                AspectRatio = DetermineAspectRatio(segment.Width ?? 0, segment.Height ?? 0),
                Duration = segment.Duration,
                Favorites = _favorites,
                IsScienceMaker = _isScienceMaker,
                MakerCategories = _categories.ToArray(),
                MatchTerms = null,  // To be filled at time of delivery.
                NextStory = segment.NextSegmentID,
                Occupations = _occupations.ToArray(),
                OccupationTypes = _jobTypes.ToArray(),
                PrevStory = segment.PrevSegmentID,
                StartTime = segment.StartTime,
                StoryOrder = segment.SegmentOrder,
                Tags = GetTheHistoryMakerTags(segment.SegmentID).ToArray(),
                TimingPairs = timing,
                Title = segment.Title,
                Transcript = segment.TranscriptText
            };

            return storyDetails;
        }
        #endregion =================             CLASS FACTORIES             ====================

        #region ====================            HELPER  FUNCTIONS            ====================
        /// <summary>
        /// Determines the aspect ratio for the given input resolution.
        /// </summary>
        /// <param name="width">Video width in pixels.</param>
        /// <param name="height">Video height in pixels.</param>
        /// <returns>Either "4:3" or "16:9" for proper resolutions; null otherwise.</returns>
        protected string DetermineAspectRatio(int width, int height)
        {
            string aspectRatio = GetRatioAsString(width, height);

            if (aspectRatio == null)
            {
                // Encountered one of the 34K videos with wonky aspect ratios.
                // See if there is a mapping in the config file.
                ResolutionMapping resolutionMapping = null;

                foreach (var rm in Settings.Current.TranscodingTask.ResolutionMappings)
                {
                    if (width == rm.Source.Width && height == rm.Source.Height)
                    {
                        resolutionMapping = rm;
                        break;
                    }
                }

                if (resolutionMapping != null)
                {
                    aspectRatio = GetRatioAsString(resolutionMapping.Target.Width, resolutionMapping.Target.Height);
                }
            }

            return aspectRatio;
        }

        /// <summary>
        /// Gets the aspect ratio as a formatted string.
        /// </summary>
        /// <param name="width">Video width in pixels</param>
        /// <param name="height">Video height in pixels.</param>
        /// <returns>Either "4:3" or "16:9" for proper resolutions; null otherwise.</returns>
        protected string GetRatioAsString(float width, float height)
        {
            var pixelAspect = Math.Truncate(width / height * 100);

            switch (pixelAspect)
            {
                case 133f:
                    return "4:3";
                case 177f:
                    return "16:9";
                default:
                    return null;
            }
        }
        #endregion =================            HELPER  FUNCTIONS            ====================
    }
}
