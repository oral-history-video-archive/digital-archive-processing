using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using InformediaCORE.Common.Config;

namespace InformediaCORE.Common.Database
{
    #region ===== Public Structs
    /// <summary>
    /// Biography and Story counts required to populate client home view.
    /// </summary>
    public class CorpusMetrics
    {
        public DateTime LastUpdated { get; set; }
        public CorpusCounts HistoryMakers { get; set; }
        public CorpusCounts ScienceMakers { get; set; }
    }

    /// <summary>
    /// Counts for a specific corpus or subcorpus.
    /// </summary>
    public class CorpusCounts
    {
        public ScopeCounts Biographies { get; set; }
        public ScopeCounts Stories { get; set; }
    }

    /// <summary>
    /// Counts related to Biographies or Stories.
    /// </summary>
    public class ScopeCounts
    {
        public int All { get; set; }
        public int Tagged { get; set; }
    }
    #endregion == Public Structs

    public class DataAccess
    {
        /// <summary>
        /// Constant represents null for integer identity fields.
        /// </summary>
        public const int NullID = 0;

        /// <summary>
        /// Constant represents that process has been run, Y=yes.
        /// </summary>
        public const char Yes = 'Y';

        /// <summary>
        /// Constant represents that process has not been run, N=no.
        /// </summary>
        public const char No = 'N';

        /// <summary>
        /// The connection string used by this instance of the DataAccess class.
        /// </summary>
        private readonly string _connectionString;

        #region ====================               Constructor               ====================
        /// <summary>
        /// Initializes an instance of the DataAccess class using the connection 
        /// string specified in the configuration file.
        /// </summary>
        public DataAccess() : this(Settings.Current.ConnectionString) { }

        /// <summary>
        /// Intitializes an instance of the DataAccess class using the connection
        /// string specified.
        /// </summary>
        /// <param name="connectionString"></param>
        public DataAccess(string connectionString)
        {        
            _connectionString = connectionString;
        }
        #endregion =================               Constructor               ====================

        #region ====================        Annotations Table Methods        ====================
        /// <summary>
        /// Performs an insert or update (upsert) for the given annotation annotation.
        /// </summary>
        /// <param name="collectionID">Numeric id of the owning collection.</param>
        /// <param name="typeID">Numerica annotation type id.</param>
        /// <param name="value">The annotation value.</param>
        /// <returns>The id of newly inserted annotation on success; NullID otherwise.</returns>
        public int UpsertCollectionAnnotation(int collectionID, int typeID, string value)
        {
            var annotationID = NullID;

            using (var context = GetTrackingDataContext())
            {
                try
                {
                    var annotation = (from a in context.Annotations
                                      where a.CollectionID == collectionID && a.TypeID == typeID
                                      select a).FirstOrDefault();

                    if (annotation == null)
                    {
                        annotation = new Annotation
                        {
                            CollectionID = collectionID,
                            TypeID = typeID
                        };
                        context.Annotations.InsertOnSubmit(annotation);
                    }

                    annotation.Value = value;
                    context.SubmitChanges();
                    annotationID = annotation.AnnotationID;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                }
            }

            return annotationID;
        }

        /// <summary>
        /// Inserts an Annotation into the database.
        /// </summary>
        /// <param name="annotation">The Annotation object to insert.</param>
        /// <returns>The ID of the Annotation inserted on success, NULL_ID on failure.</returns>
        public int InsertAnnotation(Annotation annotation)
        {
            using (var context = GetTrackingDataContext())
            {
                try
                {
                    context.Annotations.InsertOnSubmit(annotation);
                    context.SubmitChanges();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    annotation.AnnotationID = NullID;
                }
            }

            return annotation.AnnotationID;
        }

        #endregion =================        Annotations Table Methods        ====================

        #region ====================      AnnotationTypes Table Methods      ====================

        /// <summary>
        /// Returns the annotation-type ID of the annotation-type with the specified name.
        /// </summary>
        /// <param name="annotationTypeName">The unique name identifying the desired annotation-type.</param>
        /// <returns>A valid annotation-type ID on success, NULL_ID on failure.</returns>
        public int GetAnnotationTypeID(string annotationTypeName)
        {
            int annotationTypeID;

            using (var context = GetTrackingDataContext())
            {
                try
                {
                    annotationTypeID = (from at in context.AnnotationTypes
                                        where at.Name == annotationTypeName
                                        select at.TypeID).Single();
                }
                catch (SqlException ex)
                {
                    Logger.Exception(ex);
                    annotationTypeID = NullID;
                }
            }

            return annotationTypeID;
        }
        
        /// <summary>
        /// Inserts an AnnotationType into the database.
        /// </summary>
        /// <param name="annotationType">The AnnotationType object to insert.</param>
        /// <returns>The ID of the AnnotationType inserted on success, NULL_ID on failure.</returns>
        public int InsertAnnotationType(AnnotationType annotationType)
        {
            var annotationTypeID = GetAnnotationTypeID(annotationType.Name);

            if (annotationTypeID != NullID)
                return annotationTypeID;

            using (var context = GetTrackingDataContext())
            {
                try
                {
                    context.AnnotationTypes.InsertOnSubmit(annotationType);
                    context.SubmitChanges();
                    annotationTypeID = annotationType.TypeID;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    annotationTypeID = annotationType.TypeID = NullID;
                }
            }

            return annotationTypeID;
        }

        #endregion =================      AnnotationTypes Table Methods      ====================

        #region ====================        Collections Table Methods        ====================
        /// <summary>
        /// Returns the collection ID of the collection with the specified name.
        /// </summary>
        /// <param name="accession">The unique name identifying the desired collection.</param>
        /// <returns>A valid collection ID on success, NULL_ID on failure.</returns>
        public int GetCollectionID(string accession)
        {
            int collectionID;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    collectionID = (from c in context.Collections
                                    where c.Accession == accession
                                    select c.CollectionID).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    collectionID = NullID;
                }
            }

            return collectionID;
        }

        /// <summary>
        /// Returns the accession number of the collection with the specified id.
        /// </summary>
        /// <param name="collectionID">The unique ID identifying the collection.</param>
        /// <returns>A valid collection name on succes, String.Empty on failure.</returns>
        public string GetCollectionAccession(int collectionID)
        {
            string accession;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    accession = (from c in context.Collections
                                 where c.CollectionID == collectionID
                                 select c.Accession).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    accession = string.Empty;
                }
            }

            return accession;
        }

        /// <summary>
        /// Inserts a Collection into the database.
        /// </summary>
        /// <param name="collection">The Collection object to insert.</param>
        /// <returns>The ID of the Collection inserted on success, NULL_ID on failure.</returns>
        public int InsertCollection(Collection collection)
        {
            var collectionID = GetCollectionID(collection.Accession);

            if (collectionID != NullID)
                return collectionID;

            using (var context = GetTrackingDataContext())
            {
                // Proceed with inserting the new collection.
                try
                {
                    context.Collections.InsertOnSubmit(collection);
                    context.SubmitChanges();
                    collectionID = collection.CollectionID;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    collectionID = collection.CollectionID = NullID;
                }
            }
            return collectionID;
        }
        #endregion =================        Collections Table Methods        ====================

        #region ====================      NamedEntities  Table  Methods      ====================
        /// <summary>
        /// Deletes all NamedEntities related to the given segment identifier.
        /// </summary>
        /// <param name="segmentID"></param>
        public void DeleteNamedEntities(int segmentID)
        {
            using (var context = GetTrackingDataContext())
            {
                try
                {
                    var entities = from e in context.NamedEntities
                                   where e.SegmentID == segmentID
                                   select e;

                    foreach (var entity in entities)
                    {
                        context.NamedEntities.DeleteOnSubmit(entity);
                    }

                    context.SubmitChanges();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                }
            }
        }

        /// <summary>
        /// Returns a bool indicating if the given NamedEntity exists or not.
        /// </summary>
        /// <param name="namedEntity">A valid NamedEntity instance.</param>
        /// <returns>True if it exists; false otherwise.</returns>
        private bool ExistsNamedEntity(NamedEntity namedEntity)
        {
            using (var context = GetTrackingDataContext())
            {
                try
                {
                    var entity = (from e in context.NamedEntities
                                  where e.SegmentID == namedEntity.SegmentID &&
                                        e.Type      == namedEntity.Type &&
                                        e.Value     == namedEntity.Value
                                  select e).SingleOrDefault();

                    return (entity != null);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Inserts the given NamedEntity into the database.
        /// </summary>
        /// <param name="namedEntity">A valid NamedEntity instance.</param>
        /// <returns>True if inserted successfully; false otherwise.</returns>
        public bool InsertNamedEntity(NamedEntity namedEntity)
        {
            if (ExistsNamedEntity(namedEntity)) return true;

            using (var context = GetTrackingDataContext())
            {
                try
                {
                    context.NamedEntities.InsertOnSubmit(namedEntity);
                    context.SubmitChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    return false;
                }
            }
        }
        #endregion =================      NamedEntities  Table  Methods      ====================

        #region ====================          Movies Table Methods           ====================
        /// <summary>
        /// Inserts a Movie into the database.
        /// </summary>
        /// <param name="movie">The Movie object to insert.</param>
        /// <returns>The ID of the Movie inserted on success, NULL_ID on failure.</returns>
        public int InsertMovie(Movie movie)
        {
            var movieID = GetMovieID(movie.MovieName);

            // If a movie already exists with the given name
            // then return the ID to the caller
            if (movieID != NullID)
                return movieID;

            using (var context = GetTrackingDataContext())
            {
                // Proceed with inserting the new movie.
                try
                {
                    context.Movies.InsertOnSubmit(movie);
                    context.SubmitChanges();
                    movieID = movie.MovieID;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    movieID = movie.MovieID = NullID;
                }
            }

            return movieID;
        }

        /// <summary>
        /// Get the Movie corresponding to the given movie id.
        /// </summary>
        /// <param name="movieID">A valid movie id.</param>
        /// <returns>A Movie object on success, null on failure.</returns>
        public Movie GetMovie(int movieID)
        {
            using (var context = GetNonTrackingDataContext())
            {
                Movie movie;
                try
                {
                    movie = (from m in context.Movies
                             where m.MovieID == movieID
                             select m).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    movie = null;
                }

                return movie;
            }
        }

        /// <summary>
        /// Gets all the movies in the database.
        /// </summary>
        /// <returns>An array of Movie objects on success, null on failure.</returns>
        public Movie[] GetMovies()
        {
            Movie[] movies;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    movies = (from m in context.Movies
                              select m).ToArray();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    movies = null;
                }
            }

            return movies;
        }

        /// <summary>
        /// Gets the movie ID belonging to the movie with the specified name.
        /// </summary>
        /// <param name="movieName">The unique name of the desired movie.</param>
        /// <returns>A valid movie ID on success, NULL_ID on failure.</returns>
        public int GetMovieID(string movieName)
        {
            int movieID;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    movieID = (from m in context.Movies
                               where m.MovieName == movieName
                               select m.MovieID).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    movieID = NullID;
                }
            }

            return movieID;
        }

        /// <summary>
        /// Gets the movie name belonging to the movie with the ID specified.
        /// </summary>
        /// <param name="movieID">The ID of the movie of interest.</param>
        /// <returns>A valid movie name on success, String.Empty on failure.</returns>
        public string GetMovieName(int movieID)
        {
            // Assume failure.
            string movieName;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    movieName = (from m in context.Movies
                                 where m.MovieID == movieID
                                 select m.MovieName).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    movieName = String.Empty;
                }
            }

            return movieName;
        }
        #endregion =================          Movies Table Methods         ====================

        #region ====================        Partitions Table Methods         ====================
        /// <summary>
        /// Get's the ID belonging to the partition with the given name.
        /// </summary>
        /// <param name="partitionName">The unique name of the desired partition.</param>
        /// <returns>A valid partition ID on success, NULL_ID on failure.</returns>
        public int GetPartitionID(string partitionName)
        {
            int partitionID;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    partitionID = (from p in context.Partitions
                                   where p.Name == partitionName
                                   select p.PartitionID).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    partitionID = NullID;
                }
            }

            return partitionID;
        }

        /// <summary>
        /// Inserts the given partition into the database.
        /// </summary>
        /// <param name="partition">The Partition object to insert.</param>
        /// <returns>The ID of the partition inserted on success, NULL_ID on failure.</returns>
        public int InsertPartition(Partition partition)
        {
            var partitionID = GetPartitionID(partition.Name);

            // If a partition already exists with the given name
            // then return the ID to the caller
            if (partitionID != NullID)
                return partitionID;

            using (var context = GetTrackingDataContext())
            {
                // Proceed with inserting the new partition.
                try
                {
                    context.Partitions.InsertOnSubmit(partition);
                    context.SubmitChanges();
                    partitionID = partition.PartitionID;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    partitionID = partition.PartitionID = NullID;
                }
            }

            return partitionID;
        }
        #endregion =================        Partitions Table Methods         ====================

        #region ====================     PartitionMembers Table Methods      ====================
        /// <summary>
        /// Returns a bool indicating whether the given collection is a member of the given partition.
        /// </summary>
        /// <param name="collectionID">The numeric id of an existing collection.</param>
        /// <param name="partitionID">The numeric id of an existing partition.</param>
        /// <returns>True if the collection is a member, false otherwise.</returns>
        private bool ExistsPartitionMember(int collectionID, int partitionID)
        {
            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    var pm = (from m in context.PartitionMembers
                              where m.CollectionID == collectionID && m.PartitionID == partitionID
                              select m).FirstOrDefault();

                    return (pm != null);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Inserts the given partition member record into the database.
        /// </summary>
        /// <param name="member">The PartitionMember object to insert.</param>
        /// <returns>Returns true on success, false on failure.</returns>
        public bool InsertPartitionMember(PartitionMember member)
        {
            // If a partition member already exists with the given 
            // signature then return true to the caller.
            if (ExistsPartitionMember(member.CollectionID, member.PartitionID))
                return true;

            using (var context = GetTrackingDataContext())
            {
                // Proceed with inserting the new partition.
                try
                {
                    context.PartitionMembers.InsertOnSubmit(member);
                    context.SubmitChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    return false;
                }
            }
        }        
        #endregion =================     PartitionMembers Table Methods      ====================

        #region ====================         Segments Table Methods          ====================
        /// <summary>
        /// Get's the ID belonging to the segment with the given name.
        /// </summary>
        /// <param name="segmentName">The unique name of the desired segment.</param>
        /// <returns>A valid segment ID on success, NULL_ID on failure.</returns>
        public int GetSegmentID(string segmentName)
        {
            int segmentID;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    segmentID = (from s in context.Segments
                                 where s.SegmentName == segmentName
                                 select s.SegmentID).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    segmentID = NullID;
                }
            }

            return segmentID;
        }

        /// <summary>
        /// Get the Segment corresponding to the specified segment ID.
        /// </summary>
        /// <param name="segmentID">The unique ID of the desired segment.</param>
        /// <returns>A Segment object on success, null on failure.</returns>
        public Segment GetSegment(int segmentID)
        {
            Segment segment;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    segment = (from s in context.Segments
                               where s.SegmentID == segmentID
                               select s).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    segment = null;
                }
            }

            return segment;
        }

        /// <summary>
        /// Get the Segment corresponding to the specified segment name.
        /// </summary>
        /// <param name="segmentName">The unique name of the desired segment.</param>
        /// <returns>A Segment object on success, null on failure.</returns>
        public Segment GetSegment(string segmentName)
        {
            Segment segment;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    segment = (from s in context.Segments
                               where s.SegmentName == segmentName
                               select s).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    segment = null;
                }
            }

            return segment;
        }

        /// <summary>
        /// Get all segments in the given session.
        /// </summary>
        /// <param name="sessionID">The id of the session containing the desired segments.</param>
        /// <returns>An array of Segment objects on success, null on failure.</returns>
        public Segment[] GetSegments(int sessionID)
        {
            Segment[] segments;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    // Fetch all segments in given session and order them
                    // first by movie then by segment order
                    segments = (from s in context.Segments
                                where s.Movie.SessionID == sessionID
                                select s).OrderBy(s => s.Movie.Tape).ThenBy(s => s.SegmentOrder).ToArray();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    segments = null;
                }
            }

            return segments;
        }
        
        /// <summary>
        /// Inserts the given segment into the database.
        /// </summary>
        /// <param name="segment">The Segment object to insert.</param>
        /// <returns>The ID of the segment inserted on success, NULL_ID on failure.</returns>
        public int InsertSegment(Segment segment)
        {
            var segmentID = GetSegmentID(segment.SegmentName);

            // If a segment already exists with the given name
            // then return the ID to the caller.
            if (segmentID != NullID)
                return segmentID;

            using (var context = GetTrackingDataContext())
            {
                // Proceed with inserting the new segment.
                try
                {
                    context.Segments.InsertOnSubmit(segment);
                    context.SubmitChanges();
                    segmentID = segment.SegmentID;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    segmentID = segment.SegmentID = NullID;
                }
            }

            return segmentID;
        }

        /// <summary>
        /// Commit changes for the given segment to the database.
        /// </summary>
        /// <param name="segment">The segment to update.</param>
        /// <remarks>True on success, false otherwise.</remarks>
        public bool UpdateSegment(Segment segment)
        {
            using (var context = GetTrackingDataContext())
            {
                try
                {
                    // Get a copy of the current state
                    var original = GetSegment(segment.SegmentName);

                    // Submit the changes.
                    context.Segments.Attach(segment, original);
                    context.SubmitChanges();

                    return true;
                }
                catch (SqlException ex)
                {
                    Logger.Exception(ex);
                    return false;                   
                }
            }
        }

        /// <summary>
        /// Commit changes for all segments in the given array to the database.
        /// </summary>
        /// <param name="segments">An array of Segment objects to update.</param>
        public void UpdateSegments(Segment[] segments)
        {
            foreach(var s in segments)
            {
                UpdateSegment(s);
            }
        }
        #endregion =================         Segments Table Methods          ====================

        #region ====================         Sessions Table Methods          ====================
        /// <summary>
        /// Returns the session ID corresponding to the collection and ordinal specified.
        /// </summary>
        /// <param name="collectionID">The ID of the parent collection.</param>
        /// <param name="sessionOrder">The session's 1-based chronological sequence number.</param>
        /// <returns>A valid session ID on success, NULL_ID on failure.</returns>
        public int GetSessionID(int collectionID, int sessionOrder)
        {
            int sessionID;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    sessionID = (from s in context.Sessions
                                 where s.CollectionID == collectionID && s.SessionOrder == sessionOrder
                                 select s.SessionID).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    sessionID = NullID;
                }
            }

            return sessionID;
        }

        /// <summary>
        /// Inserts the given session into the database.
        /// </summary>
        /// <param name="session">The Session object to insert.</param>
        /// <returns>The ID of the session inserted on success, NULL_ID on failure.</returns>
        public int InsertSession(Session session)
        {
            var sessionID = GetSessionID(session.CollectionID, session.SessionOrder);

            if (sessionID != NullID)
                return sessionID;

            using (var context = GetTrackingDataContext())
            {
                // Proceed with inserting the new collection.
                try
                {
                    context.Sessions.InsertOnSubmit(session);
                    context.SubmitChanges();
                    sessionID = session.SessionID;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    sessionID = session.SessionID = NullID;
                }
            }

            return sessionID;
        }
        #endregion =================         Sessions Table Methods          ====================

        #region ====================          Worlds Table Methods           ====================
        /// <summary>
        /// Get's the ID belonging to the world with the given name.
        /// </summary>
        /// <param name="worldName">The unique name of the desired world.</param>
        /// <returns>A valid world ID on success, NULL_ID on failure.</returns>
        public int GetWorldID(string worldName)
        {
            int worldID;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    worldID = (from w in context.Worlds
                               where w.Name == worldName
                               select w.WorldID).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    worldID = NullID;
                }
            }

            return worldID;
        }

        /// <summary>
        /// Inserts the given world into the database.
        /// </summary>
        /// <param name="world">The World object to insert.</param>
        /// <returns>The ID of the world inserted on success; NULL_ID on failure.</returns>
        public int InsertWorld(World world)
        {
            var worldID = GetWorldID(world.Name);

            // If a world already exists with the given name
            // then return the ID to the caller.
            if (worldID != NullID) return worldID;

            using (var context = GetTrackingDataContext())
            {
                // Proceed with inserting the new world.
                try
                {
                    context.Worlds.InsertOnSubmit(world);
                    context.SubmitChanges();
                    worldID = world.WorldID;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    worldID = world.WorldID = NullID;
                }
            }

            return worldID;
        }
        #endregion =================          Worlds Table Methods           ====================

        #region ====================             Private Methods             ====================

        /*
         * Using LINQ data contexts in an n-tier architecture requires certain considerations,
         * especially for update operations.  Typically, the LINQ data context tracks entity
         * state changes and allowing to know what changes should be commited to the database
         * on a SubmitChanges() operation.  Unfortunately, when a data context is disposed,
         * entities retreived from that context are no longer have their states tracked.  To
         * further compound the problem, each entity has an internal identifier referencing
         * the disposed data context.  As such, attempts to Attach() the entity to a new
         * instance of the data context will result in the following exception:
         * 
         * "An attempt has been made to Attach or Add an entity that is not new..."
         *
         * There is much discussion of this problem in the related forums, and a number
         * of solutions exist, depending upon the specific circumstances.  The least 
         * "hacky" solution is to turn off object tracking for all database operations
         * except for updates. This essentially detaches the entity results allowing 
         * them to be subsequently Attached as needed for updates.  The following forum
         * posts led to this solution:
         * 
         * http://geekswithblogs.net/michelotti/archive/2007/12/25/117984.aspx
         * http://geekswithblogs.net/michelotti/archive/2007/12/27/118022.aspx      
         */

        /// <summary>
        /// Gets an IDVLDataContext with object tracking disabled. Use for read, insert, and delete operations.
        /// </summary>
        /// <returns>An instance of IDVLDataContext.</returns>
        protected IDVLDataContext GetNonTrackingDataContext()
        {
            // Create a new IDVLDataContext
            var context = new IDVLDataContext(_connectionString) {ObjectTrackingEnabled = false};

            // Turn object tracking off

            return context;
        }

        /// <summary>
        /// Gets an IDVLDataContext with object tracking enabled.  Use for update operations.
        /// </summary>
        /// <returns>An instance of IDVLDataContext.</returns>
        protected IDVLDataContext GetTrackingDataContext()
        {
            // Create a new IDVLDataContext
            var context = new IDVLDataContext(_connectionString) {ObjectTrackingEnabled = true};

            // Turn object tracking on.

            return context;
        }
        #endregion =================             Private Methods             ====================

        #region ====================             Static  Methods             ====================
        /// <summary>
        /// Determines if the given accession is valid for the configured database.
        /// </summary>
        /// <param name="accession">A collection accession number</param>
        /// <returns>True if accession exists; false otherwise.</returns>
        public static bool IsValidAccession(string accession)
        {
            using (var context = GetDataContext())
            {
                return context.Collections.Any(c => c.Accession == accession);
            }
        }

        /// <summary>
        /// Generates a detailed report of all sessions, movies (tapes), and segments (stories)
        /// for the given accession.
        /// </summary>
        /// <param name="accession">A collection accession number</param>
        /// <returns>A list of strings representing each line of the report.</returns>
        public static List<string> GetCollectionSummary(string accession)
        {
            var summary = new List<string>();

            using (var context = GetDataContext(true))
            {
                try
                {
                    var collection = (from c in context.Collections
                                      where c.Accession == accession
                                      select c).Single();

                    summary.Add($"Summary Report for biographical collection #{collection.CollectionID}: {collection.Accession} {collection.PreferredName}");

                    foreach (var session in collection.Sessions)
                    {
                        summary.Add($"  Session #{session.SessionOrder}: Conducted {session.InterviewDate:MM-dd-yyyy} by {session.Interviewer}");

                        foreach (var movie in session.Movies)
                        {
                            summary.Add($"    Tape {movie.Tape}:");

                            foreach (var segment in movie.Segments)
                            {
                                summary.Add($"      Story {segment.SegmentOrder,2}: {segment.Title.Truncate(65)}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    summary.Add("ERROR generating report:");
                    summary.Add(ex.Message);
                }
            }
            return summary;
        }

        /// <summary>
        /// Returns the value of the specified Collection's publishing Phase.
        /// </summary>
        /// <param name="accession">A valid collection accession number.</param>
        /// <returns>The Collection's publishing phase on success; '?' otherwise.</returns>
        public static char GetCollectionPublishingPhase(string accession)
        {
            using (var context = GetDataContext())
            {
                var phase = 
                    (from c in context.Collections
                     where c.Accession == accession
                     select c.Phase).Single();

                return (phase == ' ') ? 'D' : phase;
            }
        }

        /// <summary>
        /// Sets the specified Collection's publishing phase to the given value.
        /// </summary>
        /// <param name="accession">A collection accession identifier.</param>
        /// <param name="phase">An enum indicating the new publishing phase.</param>
        public static void SetCollectionPublishingPhase(string accession, PublishingPhase phase)
        {
            using (var context = GetDataContext(true))
            {
                context.ObjectTrackingEnabled = true;

                var collection = 
                    (from c in context.Collections
                     where c.Accession == accession
                     select c).Single();

                if (phase == PublishingPhase.Published)
                {
                    collection.Published = DateTime.Now;
                }
                else
                {
                    collection.Published = null;
                }

                collection.Phase = (char)phase;

                context.SubmitChanges();
            }
        }

        /// <summary>
        /// Determines if the given session is valid for the configured database.
        /// </summary>
        /// <param name="accession">A collection accession number.</param>
        /// <param name="sessionOrder">The 1-based Session order number.</param>
        /// <returns>True if the session exists; false otherwise.</returns>
        public static bool IsValidSession(string accession, int sessionOrder)
        {
            using (var context = GetDataContext())
            {
                var collectionID = (from c in context.Collections
                                    where c.Accession == accession
                                    select c.CollectionID).SingleOrDefault();

                if (collectionID == NullID)
                {
                    return false;
                }
                else
                {
                    return context.Sessions.Any(s => s.CollectionID == collectionID && s.SessionOrder == sessionOrder);
                }                
            }
        }

        /// <summary>
        /// Returns the value of the specified Session's publishing Phase.
        /// </summary>
        /// <param name="accession">A collection accession number.</param>
        /// <param name="sessionOrder">The 1-based Session order number.</param>
        /// <returns>The Session's publishing phase on success; '?' otherwise.</returns>
        public static char GetSessionPublishingPhase(string accession, int sessionOrder)
        {
            using (var context = GetDataContext())
            {
                var collectionID = 
                    (from c in context.Collections
                     where c.Accession == accession
                     select c.CollectionID).Single();

                var phase = 
                    (from s in context.Sessions
                     where s.CollectionID == collectionID
                     && s.SessionOrder == sessionOrder
                     select s.Phase).Single();

                return (phase == ' ') ? 'D' : phase;
            }
        }

        /// <summary>
        /// Sets the specified Session's publishing phase to the given value.
        /// </summary>
        /// <param name="accession">A collection accession identifier.</param>
        /// <param name="sessionOrder">The session's ordinal</param>
        /// <param name="phase">An enum indicating the new publishing phase.</param>
        public static void SetSessionPublishingPhase(string accession, int sessionOrder, PublishingPhase phase)
        {
            using (var context = GetDataContext(true))
            {
                var collection = 
                    (from c in context.Collections
                        where c.Accession == accession
                        select c).Single();

                var session =
                    (from s in context.Sessions
                        where s.CollectionID == collection.CollectionID
                        && s.SessionOrder == sessionOrder
                        select s).Single();

                if (phase == PublishingPhase.Published)
                {
                    session.Published = DateTime.Now;
                }
                else
                {
                    session.Published = null;
                }

                session.Phase = (char)phase;

                context.SubmitChanges();
            }
        }

        /// <summary>
        /// Gets the counts for biographies and stories for the given publishing phase.
        /// </summary>
        /// <param name="phase">Publishing phase such as Review or Published.</param>
        /// <returns>A CopusMetrics instance populated with the results.</returns>
        public static CorpusMetrics GetCorpusMetrics(PublishingPhase phase)
        {
            List<char> phases = new List<char>() { 'P' };
            if (phase == PublishingPhase.Review)
            {
                phases.Add('R');
            }

            using (var context = GetDataContext(false))
            {
                //////////////////////////////////////////////////////////////////////////////////////////
                ///// HISTORYMAKERS CORPUS

                // All Collections
                var fullCorpusBiographies = (
                    from c in context.Collections
                    select new { c.CollectionID, c.Phase }).ToList().Where(c => phases.Contains(c.Phase)).Select(c => c.CollectionID).ToList();

                var fullCorpusStories = (
                    from s in context.Segments
                    select new { s.SegmentID, s.CollectionID }).ToList().Where(s => fullCorpusBiographies.Contains(s.CollectionID)).ToList();

                int tagTypeID = (
                    from t in context.AnnotationTypes
                    where t.Name == "THM*NEW"
                    select t.TypeID).Single();

                var fullCorpusStoriesTagged = (
                    from s in context.Segments
                    where (
                        from a in context.Annotations
                        where a.TypeID == tagTypeID
                        select a.SegmentID).Distinct().Contains(s.SegmentID)
                    select new { s.SegmentID, s.CollectionID }).ToList().Where(s => fullCorpusBiographies.Contains(s.CollectionID)).ToList();

                var fullCorpusBiographiesTagged = 
                    fullCorpusStoriesTagged.Where(s => fullCorpusBiographies.Contains(s.CollectionID)).Select(s => s.CollectionID).Distinct().ToList();

                //////////////////////////////////////////////////////////////////////////////////////////
                ///// SCIENCEMAKERS CORPUS

                var smPartitionID = (
                    from p in context.Partitions
                    where p.Name == "ScienceMaker"
                    select p.PartitionID).Single();

                var scienceMakersBiographies = (
                    from pm in context.PartitionMembers
                    where pm.PartitionID == smPartitionID
                    select pm.CollectionID).Distinct().ToList().Where(cid => fullCorpusBiographies.Contains(cid)).ToList();

                var scienceMakersBiographiesTagged =
                    scienceMakersBiographies.Where(cid => fullCorpusBiographiesTagged.Contains(cid)).ToList();

                var scienceMakersStories =
                    fullCorpusStories.Where(s => scienceMakersBiographies.Contains(s.CollectionID)).ToList();

                var scienceMakersStoriesTagged =
                    fullCorpusStoriesTagged.Where(s => scienceMakersBiographiesTagged.Contains(s.CollectionID)).ToList();

                //////////////////////////////////////////////////////////////////////////////////////////
                ///// RETURN
                return new CorpusMetrics
                {
                    LastUpdated = DateTime.Now,
                    HistoryMakers = new CorpusCounts
                    {
                        Biographies = new ScopeCounts
                        {
                            All = fullCorpusBiographies.Count,
                            Tagged = fullCorpusBiographiesTagged.Count
                        },
                        Stories = new ScopeCounts
                        {
                            All = fullCorpusStories.Count,
                            Tagged = fullCorpusStoriesTagged.Count
                        }
                    },
                    ScienceMakers = new CorpusCounts
                    {
                        Biographies = new ScopeCounts
                        {
                            All = scienceMakersBiographies.Count,
                            Tagged = scienceMakersBiographiesTagged.Count
                        },
                        Stories = new ScopeCounts
                        {
                            All = scienceMakersStories.Count,
                            Tagged = scienceMakersStoriesTagged.Count
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Gets an IDVLDataContext instantiated with the configured connection string.
        /// </summary>
        /// <param name="useObjectTracking">Object change tracking will be enabled if true.</param>
        /// <returns>An instance of the IDVLDataContext class.</returns>
        public static IDVLDataContext GetDataContext(bool useObjectTracking = false)
        {
            return GetDataContext(Settings.Current.ConnectionString, useObjectTracking);
        }

        /// <summary>
        /// Gets an IDVLDataContext instantiated with the given connection string.
        /// </summary>
        /// <param name="connectionString">A valid connection string </param>
        /// <param name="useObjectTracking">Object change tracking will be enabled if true.</param>
        /// <returns>An instance of the IDVLDataContext class.</returns>
        public static IDVLDataContext GetDataContext(string connectionString, bool useObjectTracking = false)
        {
            // Create a new IDVLDataContext
            return new IDVLDataContext(connectionString) {ObjectTrackingEnabled = useObjectTracking};
        }
        #endregion =================             Static  Methods             ====================
    }
}
