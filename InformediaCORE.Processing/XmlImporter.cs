using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.IO;

using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Common.Media;
using InformediaCORE.Common.Xml;
using InformediaCORE.Processing.Database;

namespace InformediaCORE.Processing
{
    /// <summary>
    /// Reads Collection, and Movie data from XML files and inserts it into
    /// the appropriate tables in the database.
    /// </summary>
    public static class XmlImporter
    {
        /// <summary>
        /// Inserts the contents of specified XML file into the database.
        /// </summary>
        /// <param name="xmlFilename">The XML file to be ingested.</param>
        /// <param name="update">If true, existing database records will be updated;
        /// if false, existing records will be skipped.
        /// </param>
        public static void ImportXml(string xmlFilename, bool update)
        {
            // Does file exist?
            if (!File.Exists(xmlFilename))
            {
                Logger.Write("Specified file does not exist: {0}.", xmlFilename);
                return;
            }

            // Determine the type of XML file given.
            var className = XmlUtilities.GetClassName(xmlFilename);
            Logger.Write("XML Document root element is type: {0}.", className); 

            // Process XML based on type
            switch (className)
            {
                case XmlUtilities.XmlClassNames.AnnotationType:
                    InsertAnnotationType(xmlFilename);
                    break;
                case XmlUtilities.XmlClassNames.Collection:
                    if (update)
                    {
                        UpdateCollection(xmlFilename);
                    }
                    else
                    {
                        InsertCollection(xmlFilename);                        
                    }
                    break;
                case XmlUtilities.XmlClassNames.Movie:
                    if (update)
                    {
                        UpdateMovie(xmlFilename);
                    }
                    else
                    {
                        InsertMovie(xmlFilename);
                    }
                    break;
                case XmlUtilities.XmlClassNames.World:
                    InsertWorld(xmlFilename);
                    break;
                default:
                    Logger.Write("Unknown XML document type. Operation failed.");
                    break;
            }
        }

        #region XML ROOT INSERTION METHODS

        /// <summary>
        /// Inserts an AnnotationType into the database.
        /// </summary>
        /// <param name="xmlFile">The XML file containing the AnnotationType to import.</param>
        private static void InsertAnnotationType(string xmlFile)
        {
            Logger.Write("AnnotationType import started.");

            // Use deserializer to read XML file into structured class representation.
            var xmlAnnotationType = XmlUtilities.Read<XmlAnnotationType>(xmlFile);

            // Open Database
            var db = new DataAccessExtended();

            // Check if AnnotationType already exists.
            if (db.GetAnnotationTypeID(xmlAnnotationType.Name) == DataAccess.NullID)
            {
                // Create the LINQ object to hold the data to be inserted.
                var annotationType = new AnnotationType
                {
                    Description = xmlAnnotationType.Description,
                    Name = xmlAnnotationType.Name,
                    Scope = GetCharFromEnum(xmlAnnotationType.Scope)
                };

                // Insert AnnotationType
                db.InsertAnnotationType(annotationType);

                // Check results
                if (annotationType.TypeID == DataAccess.NullID)
                {
                    Logger.Error("Failed to insert AnnotationType \"{0}\".", annotationType.Name);
                }
                else
                {
                    Logger.Write("AnnotationType \"{0}\" inserted successfully as id {1}.", annotationType.Name, annotationType.TypeID);
                }
            }
            else
            {
                Logger.Write("SKIPPING: AnnotationType \"{0}\" already exists.", xmlAnnotationType.Name);
                return;
            }

            Logger.Write("AnnotationType import done.");
        }

        /// <summary>
        /// Inserts a Collection into the database.
        /// </summary>
        /// <param name="xmlFile">The XML file containing the Collection to import.</param>
        private static void InsertCollection(string xmlFile)
        {
            Logger.Write("Collection import started.");

            // Use deserializer to read XML file into structured class representation.
            var xmlCollection = XmlUtilities.Read<XmlCollection>(xmlFile);

            // Open a database connection
            var db = new DataAccessExtended();

            // Bail out if portrait image does not exist.
            if (!File.Exists(xmlCollection.PortraitPath))
            {
                Logger.Error("Could not find specified portrait image: \"{0}\". Import failed.", xmlCollection.PortraitPath);
                return;
            }

            var collectionID = db.GetCollectionID(xmlCollection.Accession);

            // Check if collection specified already exists.
            if (collectionID == DataAccess.NullID)
            {
                // Create the LINQ object to hold the data to be inserted into the database.
                var collection = new Collection
                {
                    Accession = Utilities.UpperCaseWithWarnings(xmlCollection.Accession),
                    DescriptionShort = xmlCollection.DescriptionShort,
                    BiographyShort = xmlCollection.BiographyShort,
                    FirstName = xmlCollection.FirstName,
                    LastName = xmlCollection.LastName,
                    PreferredName = xmlCollection.PreferredName,
                    Gender = GetCharFromEnum(xmlCollection.Gender),
                    WebsiteURL = xmlCollection.WebsiteURL,
                    Region = xmlCollection.Region,
                    BirthCity = xmlCollection.BirthCity,
                    BirthState = xmlCollection.BirthState,
                    BirthCountry = xmlCollection.BirthCountry,
                    BirthDate = xmlCollection.BirthDate,
                    DeceasedDate = xmlCollection.DeceasedDate,
                    FileType = Utilities.GetFileExtension(xmlCollection.PortraitPath),
                    Portrait = MediaTools.LoadImage(xmlCollection.PortraitPath),
                    Phase = (char)PublishingPhase.Draft
                };

                // Insert collection
                collectionID = db.InsertCollection(collection);

                // Check results
                if (collectionID == DataAccess.NullID)
                {
                    Logger.Error("Failed to insert collection \"{0}\".", collection.Accession);
                }
                else
                {
                    Logger.Write("Collection \"{0}\" inserted successfully as id {1}.", collection.Accession, collection.CollectionID);
                }
            }
            else
            {
                Logger.Write("SKIPPING: Collection \"{0}\" already exists.", xmlCollection.Accession);
            }


            // Insert child collections.
            if (collectionID != DataAccess.NullID)
            {
                InsertCollectionAnnotations(xmlCollection.Annotations, collectionID: collectionID);
                InsertPartitionMembers(xmlCollection.Partitions, collectionID);
                InsertSessions(xmlCollection.Sessions, collectionID);
            }


            Logger.Write("Collection import done.");
        }

        /// <summary>
        /// Updates an existing Collection.
        /// </summary>
        /// <param name="xmlFile">The XML file containing the updated Collection data.</param>
        private static void UpdateCollection(string xmlFile)
        {
            Logger.Write("Collection update started.");

            // Use deserializer to read XML file into structured class representation.
            var xmlCollection = XmlUtilities.Read<XmlCollection>(xmlFile);

            // Open a database connection
            var db = new DataAccessExtended();

            // Retrieve the xisting collection with the given name from XML.
            var collectionID = db.GetCollectionID(xmlCollection.Accession);
            var collection = db.GetCollection(collectionID);

            // Check if collection specified exists and update accordingly.
            if (collection != null)
            {
                // Update the collection
                collection.DescriptionShort = xmlCollection.DescriptionShort;
                collection.BiographyShort = xmlCollection.BiographyShort;
                collection.FirstName = xmlCollection.FirstName;
                collection.LastName = xmlCollection.LastName;
                collection.PreferredName = xmlCollection.PreferredName;
                collection.Gender = GetCharFromEnum(xmlCollection.Gender);
                collection.WebsiteURL = xmlCollection.WebsiteURL;
                collection.Region = xmlCollection.Region;
                collection.BirthCity = xmlCollection.BirthCity;
                collection.BirthState = xmlCollection.BirthState;
                collection.BirthCountry = xmlCollection.BirthCountry;
                collection.BirthDate = xmlCollection.BirthDate;
                collection.DeceasedDate = xmlCollection.DeceasedDate;
                collection.FileType = Utilities.GetFileExtension(xmlCollection.PortraitPath);
                collection.Portrait = MediaTools.LoadImage(xmlCollection.PortraitPath);

                // Update the database
                if (db.UpdateCollection(collection))
                {
                    Logger.Write("Collection \"{0}\" with ID {1} updated successfully.", collection.Accession, collection.CollectionID);

                    // Update child collections.
                    // TODO: UpdateAnnotations(xmlCollection.Annotations, collectionID: collection.CollectionID);
                    // TODO: UpdatePartitionMembers(xmlCollection.Partitions, collection.CollectionID);
                    // TODO: UpdateSessions(xmlCollection.Sessions, collection.CollectionID);
                }
                else
                {
                    Logger.Error("Failed to update collection \"{0}\" with ID {1}.", collection.Accession, collection.CollectionID);
                    return;
                }
            }
            else
            {
                Logger.Write("SKIPPING: Collection \"{0}\" does not exist.", xmlCollection.Accession);
            }

            Logger.Write("Collection update done.");
        }

        /// <summary>
        /// Inserts a Movie and child Segments into the database.
        /// </summary>
        /// <param name="xmlFile">The XML file containing the Movie to import.</param>
        private static void InsertMovie(string xmlFile)
        {
            // Parse XML
            Logger.Write("Movie import started.");
            var xmlMovie = XmlUtilities.Read<XmlMovie>(xmlFile);

            #region Check Prerequisites
            // Verify that video exists
            if (!File.Exists(xmlMovie.Path))
            {
                Logger.Error("Missing video file: {0}.  Import failed.", xmlMovie.Path);
                return;
            }

            // Analyze the video
            Logger.Write("Analyzing {0}.", xmlMovie.Path);
            var movieInfo = MediaTools.MediaInfo(xmlMovie.Path);
            if (movieInfo == null)
            {
                Logger.Error("Could not analyze video file: {0}. Import failed.", xmlMovie.Path);
                return;
            }

            Logger.Write("Duration={0}ms; Frames={1}; FPS={2}; Width={3}; Heigth={4}",
                movieInfo.Duration, movieInfo.Frames, movieInfo.FPS, movieInfo.Width, movieInfo.Height);

            // Open Database
            var db = new DataAccessExtended();

            // Get the CollectionID from the accession. 
            var collectionID = db.GetCollectionID(xmlMovie.Collection);

            // Bail out if the collection specified does not exist.
            if (collectionID == DataAccess.NullID)
            {
                Logger.Error("Collecion \"{0}\" does not exist. Import failed.", xmlMovie.Collection);
                return;
            }

            // Get the SessionID of the known CollectionID and SessionNumber.
            var sessionID = db.GetSessionID(collectionID, xmlMovie.SessionNumber);

            // Bail out if the session does not exist.
            if (sessionID == DataAccess.NullID)
            {
                Logger.Error("Session \"{0}\" does not exist. Import failed.", xmlMovie.SessionNumber);
                return;
            }
            #endregion Check Prerequisites

            // Check if movie specified already exists.
            if (db.GetMovieID(xmlMovie.Name) == DataAccess.NullID)
            {
                // Create the LINQ object to hold the data to be inserted into the database.
                var movie = new Movie
                {
                    MovieName = xmlMovie.Name,
                    CollectionID = collectionID,
                    SessionID = sessionID,
                    Abstract = xmlMovie.Abstract,
                    Tape = xmlMovie.TapeNumber,
                    MediaPath = xmlMovie.Path,
                    FileType = xmlMovie.Path == null ? string.Empty : Path.GetExtension(xmlMovie.Path).ToLower(),
                    Duration = movieInfo.Duration,
                    Width = movieInfo.Width,
                    Height = movieInfo.Height,
                    FPS = movieInfo.FPS
                };

                // Insert movie   
                db.InsertMovie(movie);

                // Check results
                if (movie.MovieID == DataAccess.NullID)
                {
                    Logger.Error("Failed to insert movie \"{0}\".", xmlMovie.Name);
                }
                else
                {
                    Logger.Write("Movie \"{0}\" inserted successfully as id {1}.", movie.MovieName, movie.MovieID);

                    // Insert child collections
                    InsertAnnotations(xmlMovie.Annotations, movieID: movie.MovieID);
                    InsertSegments(xmlMovie.Segments, movie);
                }
            }
            else
            {
                Logger.Write("SKIPPING: Movie \"{0}\" already exists.", xmlMovie.Name);
            }
        }

        /// <summary>
        /// Insert the XmlSegments contained in the given xmlMovie
        /// </summary>
        /// <param name="xmlSegments">The list of XmlSegments to be inserted.</param>
        /// <param name="movie">The segments' parent Movie entity.</param>
        private static void InsertSegments(IEnumerable<XmlSegment> xmlSegments, Movie movie)
        {
            Logger.Write("Segment import started.");

            // Open Database
            var db = new DataAccessExtended();

            // Counter used for sequencing segments.
            var segmentOrder = 0;

            // Iterate over the XmlSegments list and insert each one in turn.
            foreach (var xmlSegment in xmlSegments)
            {
                // SegmentOrder is Ones-Based, so increment before using.
                segmentOrder++;

                // Derive the canonical segment name given the movie name and segment order.
                var segmentName = $"{movie.MovieName}_{segmentOrder:000}";

                // Check if the segment already exists in the database.
                if (db.GetSegmentID(segmentName) == DataAccess.NullID)
                {
                    // Validate segment's time boundaries based on the specified time-format,
                    // given values, and the known duration of the parent video.  If a boundary
                    // violation occurs, then abort the XML import.
                    var startTime = Utilities.DateTimeToMS(xmlSegment.StartTime);
                    if (startTime > movie.Duration)
                    {
                        Logger.Error("Segment start time of {0}ms exceeds parent movie's duration of {1}ms.", startTime, movie.Duration);                        
                        break;
                    }

                    var endTime = (xmlSegment.TimeFormat == XmlTimeFormatSpecifier.HMSEND) ? movie.Duration :
                        Math.Min(Utilities.DateTimeToMS(xmlSegment.EndTime), movie.Duration);

                    if (endTime <= startTime)
                    {
                        Logger.Error("Segment end time of {0}ms is less than given start time of {1}ms.", endTime, startTime);
                        break;
                    }

                    // Create the LINQ object to hold the data to be inserted into the database.
                    var segment = new Segment
                    {
                        Abstract = xmlSegment.Title,
                        EndTime = endTime,
                        Ready = 'N',
                        CollectionID = movie.CollectionID,
                        SessionID = movie.SessionID,
                        MovieID = movie.MovieID,
                        SegmentName = segmentName,
                        SegmentOrder = segmentOrder,
                        StartTime = startTime,
                        Title = xmlSegment.Title,
                        TranscriptText = FormatTranscript(xmlSegment.Transcript)
                    };

                    segment.TranscriptLength = segment.TranscriptText.Length;

                    // Insert segment
                    db.InsertSegment(segment);

                    // Check results
                    if (segment.SegmentID == DataAccess.NullID)
                    {
                        // TODO: Rollback or delete movie upon failed insert.
                        Logger.Error("Failed to insert segment \"{0}\".", segment.SegmentName);
                    }
                    else
                    {
                        Logger.Write("Segment \"{0}\" inserted successfully as id {1}.", segment.SegmentName, segment.SegmentID);

                        // Insert child collections
                        InsertAnnotations(xmlSegment.Annotations, segmentID: segment.SegmentID);
                    }
                }
                else
                {
                    Logger.Write("SKIPPING: Segment \"{0}\" already exists.", segmentName);
                }
            }

            Logger.Write("Linking segments for CollectionID {0}.", movie.CollectionID);
            db.LinkSegments(movie.CollectionID);

            Logger.Write("Segment import done.");
        }

        /// <summary>
        /// Inserts a World into the database.
        /// </summary>
        /// <param name="xmlFile">The XML file containing the World to import.</param>
        private static void InsertWorld(string xmlFile)
        {
            Logger.Write("World import started.");

            // Use deserializer to read XML file into structured class representation.
            var xmlWorld = XmlUtilities.Read<XmlWorld>(xmlFile);

            // Open Database
            var db = new DataAccessExtended();

            var worldID = db.GetWorldID(xmlWorld.Name);

            // Check if the world specified exists prior to insertion
            if (worldID == DataAccess.NullID)
            {
                // Create the LINQ object to hold the data to be inserted into the database.
                var world = new World
                {
                    Name = xmlWorld.Name,
                    Description = xmlWorld.Description
                };

                // Perform database insert
                worldID = db.InsertWorld(world);

                // Check results of the insertion
                if (worldID == DataAccess.NullID)
                {
                    Logger.Error("Failed to insert world \"{0}\".", world.Name);
                }
                else
                {
                    Logger.Write("World \"{0}\" inserted successfully as id {1}.", world.Name, world.WorldID);                    
                }
            }
            else
            {
                Logger.Write("SKIPPING: World \"{0}\" already exists.", xmlWorld.Name);
            }

            if (worldID != DataAccess.NullID)
            {
                InsertPartitions(xmlWorld.Partitions, worldID);
            }

            Logger.Write("World import done.");
        }
        #endregion XML ROOT INSERTION METHODS

        #region XML UPDATE METHODS
        /// <summary>
        /// Updates an existing Movie and all of it's Segments with data from the specified XML file.
        /// </summary>
        /// <param name="xmlFile">The fully qualified path to a .movie.xml file.</param>
        private static void UpdateMovie(string xmlFile)
        {
            try
            {
                // Parse XML
                Logger.Write("Updating Movie...");

                var xmlMovie = XmlUtilities.Read<XmlMovie>(xmlFile);
                var media = GetMediaInfo(xmlMovie.Path);

                using (var context = DataAccess.GetDataContext(true))
                {
                    var movie =
                        (from m in context.Movies
                         where m.MovieName == xmlMovie.Name
                         select m).SingleOrDefault();

                    if (movie == null)
                    {
                        Logger.Error($"Specified movie {xmlMovie.Name} does not exist. Movie update failed.");
                        return;
                    }

                    if (movie.Session.Phase == (char)PublishingPhase.Published)
                    {
                        Logger.Error("Cannot update a movie which has been published to the production site.");
                        return;
                    }

                    // These are the only fields that may be updated, others are immutable.
                    movie.Abstract = xmlMovie.Abstract;
                    movie.MediaPath = xmlMovie.Path;
                    movie.FileType = xmlMovie.Path == null ? string.Empty : Path.GetExtension(xmlMovie.Path).ToLower();
                    movie.Duration = media.Duration;
                    movie.Width = media.Width;
                    movie.Height = media.Height;
                    movie.FPS = media.FPS;

                    UpdateMovieAnnotations(movie, xmlMovie, context);
                    UpdateSegments(movie, xmlMovie, context);

                    // Commit all changes at once as a single single transaction.
                    context.SubmitChanges();
                    Logger.Write($"Movie {movie.MovieName} (ID:{movie.MovieID}) updated successfully.");

                    Logger.Write("Demoting session to Draft phase...");
                    context.Refresh(RefreshMode.OverwriteCurrentValues, movie.Session);
                    movie.Session.Phase = (char)PublishingPhase.Draft;
                    context.SubmitChanges(ConflictMode.ContinueOnConflict);

                    Logger.Write("Movie update finished successfully.");
                }
            }
            catch(XmlImportException ex)
            {
                Logger.Error(ex.Message);
                Logger.Error("*** Movie update failed due to XML data errors.");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
                Logger.Error("*** Movie update failed due to unexpected error.");
            }
        }

        /// <summary>
        /// Replaces the old movie annotations with the new ones given by the XmlMovie object.
        /// </summary>
        /// <param name="movie">A Movie entity attached to the active data context.</param>
        /// <param name="xmlMovie">The XmlMovie object containing the update data.</param>
        /// <param name="context">The active database context.</param>
        private static void UpdateMovieAnnotations(Movie movie, XmlMovie xmlMovie, IDVLDataContext context)
        {
            // Load AnnotationType Identifiers
            var annotationTypes = context.AnnotationTypes.OrderBy(a => a.TypeID).ToList();

            if (movie.Annotations.Count > 0)
            {
                Logger.Write("Deleting old movie annotations...");
                var oldAnnotations = movie.Annotations.ToList();
                foreach (var annotation in oldAnnotations)
                {
                    context.Annotations.DeleteOnSubmit(annotation);
                }
            }

            if (xmlMovie.Annotations.Count > 0)
            {
                Logger.Write("Adding new movie annotations...");
                foreach (var xmlAnnotation in xmlMovie.Annotations)
                {
                    Logger.Write($"...adding {xmlAnnotation.Type}");
                    movie.Annotations.Add(
                        new Annotation
                        {
                            MovieID = movie.MovieID,
                            TypeID = annotationTypes.First(t => t.Name == xmlAnnotation.Type).TypeID,
                            Value = xmlAnnotation.Value
                        });
                }
            }
        }

        /// <summary>
        /// Updates all the segments for the given Movie with the provided XML data.
        /// </summary>
        /// <param name="movie">A Movie entity attached to the active data context.</param>
        /// <param name="xmlMovie">The XmlMovie object containing the update data.</param>
        /// <param name="context">The active database context.</param>
        private static void UpdateSegments(Movie movie, XmlMovie xmlMovie, IDVLDataContext context)
        {
            Logger.Write("Updating Segments...");

            var segments = movie.Segments.OrderBy(s => s.SegmentOrder).ToList();
            var xmlSegments = xmlMovie.Segments.ToList();

            if (segments.Count != xmlSegments.Count)
            {
                throw new XmlImportException($"Number of segments in XML file ({xmlSegments.Count}) does not match existing number of segments in database ({segments.Count}).");
            }

            // Update each segment with the assumption that the lists are aligned by SegmentOrder
            for(var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var xmlSegment = xmlSegments[i];

                // Validate segment's time boundaries based on the specified time-format,
                // given values, and the known duration of the parent video.  If a boundary
                // violation occurs, then abort the entire update.
                var startTime = Utilities.DateTimeToMS(xmlSegment.StartTime);
                if (startTime > movie.Duration)
                {
                    throw new XmlImportException($"Segment's given start time of {startTime}ms exceeds the movie's duration of {movie.Duration}ms.");
                }

                var endTime = 
                    (xmlSegment.TimeFormat == XmlTimeFormatSpecifier.HMSEND) 
                    ? movie.Duration 
                    : Math.Min(Utilities.DateTimeToMS(xmlSegment.EndTime), movie.Duration);

                if (endTime <= startTime)
                {
                    throw new XmlImportException($"Segment's given end time of {endTime}ms is less than it's given start time of {startTime}ms.");
                }
                
                Logger.Write($"Updating segment #{segment.SegmentOrder} (ID:{segment.SegmentID})");

                // Update the existing LINQ entity
                segment.Abstract = xmlSegment.Title;
                segment.EndTime = endTime;
                segment.Keyframe = null;
                segment.Ready = 'N';
                segment.StartTime = startTime;
                segment.Title = xmlSegment.Title;
                segment.TranscriptSync = null;
                segment.TranscriptText = string.Join("\n\n", xmlSegment.Transcript);
                segment.TranscriptLength = segment.TranscriptText.Length;

                UpdateSegmentAnnotations(segment, xmlSegment, context);
                ClearSegmentProcessingState(segment, context);
            }

            Logger.Write("Segments updated successfully.");
        }

        /// <summary>
        /// Replaces the old segment annotations with the new ones given by the XmlSegment object.
        /// </summary>
        /// <param name="segment">A Segment entity attached to the active data context.</param>
        /// <param name="xmlSegment">The XmlSegment object containing the update data.</param>
        /// <param name="context">The active database context.</param>
        private static void UpdateSegmentAnnotations(Segment segment, XmlSegment xmlSegment, IDVLDataContext context)
        {
            // Load AnnotationType Identifiers
            var annotationTypes = context.AnnotationTypes.OrderBy(a => a.TypeID).ToList();

            if (segment.Annotations.Count > 0)
            {
                Logger.Write("  Deleting old segment annotations...");
                var oldAnnotations = segment.Annotations.ToList();
                foreach (var annotation in oldAnnotations)
                {
                    context.Annotations.DeleteOnSubmit(annotation);
                }
            }

            if (xmlSegment.Annotations.Count > 0)
            {
                Logger.Write("  Adding new segment annotations...");
                foreach (var xmlAnnotation in xmlSegment.Annotations)
                {
                    Logger.Write($"    Adding {xmlAnnotation.Type}");
                    segment.Annotations.Add(
                        new Annotation
                        {
                            SegmentID = segment.SegmentID,
                            TypeID = annotationTypes.First(t => t.Name == xmlAnnotation.Type).TypeID,
                            Value = xmlAnnotation.Value
                        });
                }
            }
        }

        /// <summary>
        /// Clears all prior processing state related to the segment.
        /// </summary>
        /// <param name="segment">A Segment entity attached to the active data context.</param>
        /// <param name="context">The active databse context.</param>
        private static void ClearSegmentProcessingState(Segment segment, IDVLDataContext context)
        {
            Logger.Write($"  Clearing segment {segment.SegmentOrder}'s processing state...");

            if (segment.NamedEntities.Count > 0)
            {
                Logger.Write("    Deleting NamedEntities...");
                var entities = segment.NamedEntities.ToList();
                foreach (var entity in entities)
                {
                    context.NamedEntities.DeleteOnSubmit(entity);
                }
            }

            if (segment.Semaphore != null)
            {
                Logger.Write("    Deleting processing Semaphore...");
                context.Semaphores.DeleteOnSubmit(segment.Semaphore);
            }

            if (segment.TaskStates.Count > 0)
            {
                Logger.Write("    Reseting TaskStates to 'Pending'...");
                foreach(var taskState in segment.TaskStates)
                {
                    taskState.State = (char)TaskStateValue.Pending;
                }
            }

            ////////////////////////////////////////////////////////////////////////////////
            ///// DELETE FILES FROM THE FILESYSTEM
            Logger.Write("    Deleting segment data files...");
            var dataPath = Utilities.GetSegmentDataPath(segment);
            foreach (string f in Directory.EnumerateFiles(dataPath))
            {
                Logger.Write("        {0}", f);
                File.Delete(f);
            }

            Logger.Write("    Deleting segment media (build) files...");
            var buildPath = Utilities.GetSegmentBuildPath(segment);
            foreach (string f in Directory.EnumerateFiles(buildPath, $"{segment.SegmentName}.*"))
            {
                Logger.Write("        {0}", f);
                File.Delete(f);
            }
        }
        #endregion XML UPDATE METHODS

        #region XML SUB-TYPE INSERTION METHODS
        /// <summary>
        /// Inserts the list of XmlAnnotations for the given collection
        /// </summary>
        /// <param name="xmlAnnotations">A list of XmlAnnotations</param>
        /// <param name="collectionID">A valid collection id</param>
        private static void InsertCollectionAnnotations(List<XmlAnnotation> xmlAnnotations, int collectionID)
        {
            Logger.Write("Importing Collection Annotations...");
            var db = new DataAccessExtended();

            foreach (var xmlAnnotation in xmlAnnotations)
            {
                var typeID = db.GetAnnotationTypeID(xmlAnnotation.Type);

                if (typeID == DataAccess.NullID)
                {
                    Logger.Error("Cannot insert annotation of type \"{0}\", type does not exist.", xmlAnnotation.Type);
                }
                else
                {
                    // Perform database upsert
                    var annotationID = db.UpsertCollectionAnnotation(collectionID, typeID, xmlAnnotation.Value);
                    var abbreviated = xmlAnnotation.Value.Substring(0, Math.Min(25, xmlAnnotation.Value.Length));

                    // Check results
                    if (annotationID == DataAccess.NullID)
                    {
                        Logger.Error("Failed to insert annotation \"{0}...\".", abbreviated);
                    }
                    else
                    {
                        Logger.Write("Annotation \"{0}...\" inserted as id {1}.", abbreviated, annotationID);
                    }
                }
            }

            Logger.Write("Annotation import done.");
        }

        /// <summary>
        /// Inserts a list of XmlAnnotation objects into the database.
        /// </summary>
        /// <param name="xmlAnnotations">A list of XmlAnnotations.</param>
        /// <param name="collectionID">ID of the parent collection. Must be supplied for collection-level annotations.</param>
        /// <param name="movieID">ID of the parent movie.  Must be supplied for movie-level annotations.</param>
        /// <param name="segmentID">ID of the parent segment. Must be supplied for segment-level annotations.</param>
        private static void InsertAnnotations(List<XmlAnnotation> xmlAnnotations, int? collectionID = null, int? movieID = null, int? segmentID = null)
        {
            Logger.Write("Annotation import started.");
            var db = new DataAccessExtended();

            foreach (var xmlAnnotation in xmlAnnotations)
            {
                var typeID = db.GetAnnotationTypeID(xmlAnnotation.Type);

                if (typeID == DataAccess.NullID)
                {
                    Logger.Error("Cannot insert annotation of type \"{0}\", type does not exist.", xmlAnnotation.Type);
                }
                else
                {
                    var annotation = new Annotation
                    {
                        CollectionID = collectionID,
                        MovieID = movieID,
                        SegmentID = segmentID,
                        TypeID = typeID,
                        Value = xmlAnnotation.Value
                    };

                    // Perform database insert
                    var annotationID = db.InsertAnnotation(annotation);
                    var abbreviated = annotation.Value.Substring(0, Math.Min(25, annotation.Value.Length));

                    // Check results
                    if (annotationID == DataAccess.NullID)
                    {
                        Logger.Error("Failed to insert annotation \"{0}...\".", abbreviated);
                    }
                    else
                    {
                        Logger.Write("Annotation \"{0}...\" inserted as id {1}.", abbreviated, annotationID);
                    }
                }
            }

            Logger.Write("Annotation import done.");
        }

        /// <summary>
        /// Inserts a list of XmlPartition objects into the database.
        /// </summary>
        /// <param name="partitions">A list of XmlPartitions.</param>
        /// <param name="worldID">The numeric id of the parent world.</param>
        private static void InsertPartitions(List<XmlPartition> partitions, int worldID)
        {
            Logger.Write("Partition import started.");
            var db = new DataAccessExtended();

            foreach (var xmlPartition in partitions)
            {
                var partitionID = db.GetPartitionID(xmlPartition.Name);

                if (partitionID == DataAccess.NullID)
                {
                    var partition = new Partition
                    {
                        Description = xmlPartition.Description,
                        Name = xmlPartition.Name,
                        WorldID = worldID
                    };

                    // Perform database insert
                    db.InsertPartition(partition);

                    // Check results of the insertion
                    if (partition.PartitionID == DataAccess.NullID)
                    {
                        Logger.Error("Failed to insert partition \"{0}\".", partition.Name);
                    }
                    else
                    {
                        Logger.Write("Partition \"{0}\" inserted successfully as id {1}.", partition.Name, partition.PartitionID);
                    }
                }
                else
                {
                    Logger.Write("SKIPPING: Partition \"{0}\" already exists.", xmlPartition.Name);
                }
            }

            Logger.Write("Partition import done.");
        }

        /// <summary>
        /// Assigns the collection specified by the given id to the given list of partitions.
        /// </summary>
        /// <param name="partitions">A list of partitions specified by canonical name.</param>
        /// <param name="collectionID">The numeric id of the collection to be assigned.</param>
        private static void InsertPartitionMembers(List<string> partitions, int collectionID)
        {
            Logger.Write("Partition assignment started.");

            // Open a database connection
            var db = new DataAccessExtended();

            foreach (var partition in partitions)
            {
                var partitionID = db.GetPartitionID(partition);

                if (partitionID == DataAccess.NullID)
                {
                    Logger.Warning("WARNING: Partition \"{0}\" does not exist. Assignment failed.", partition);
                }
                else
                {
                    // Create the LINQ object to insert
                    var member = new PartitionMember
                    {
                        CollectionID = collectionID,
                        PartitionID = partitionID
                    };

                    // Insert the PartitionMember
                    if (db.InsertPartitionMember(member))
                    {
                        Logger.Write("Collection added to partition \"{0}\"", partition);
                    }
                    else
                    {
                        Logger.Error("Failed to added collection to partition \"{0}\".", partition);
                    }
                }
            }

            Logger.Write("Partition assignment done.");
        }

        /// <summary>
        /// Inserts the Session information for the given collection.
        /// </summary>
        /// <param name="xmlSessions">A list of XmlSessions to be inserted into the database.</param>
        /// <param name="collectionID">The numeric id of the parent collection.</param>
        private static void InsertSessions(List<XmlSession> xmlSessions, int collectionID)
        {
            Logger.Write("Session import started.");

            var db = new DataAccessExtended();

            foreach (var xmlSession in xmlSessions)
            {
                var sessionID = db.GetSessionID(collectionID, xmlSession.SessionOrder);

                if (sessionID == DataAccess.NullID)
                {
                    // Create LINQ object to hold data to be inserted into the database.
                    var session = new Session
                    {
                        CollectionID = collectionID,
                        SessionOrder = xmlSession.SessionOrder,
                        Interviewer = xmlSession.Interviewer,
                        InterviewDate = xmlSession.InterviewDate,                        
                        Location = xmlSession.Location,                        
                        Videographer = xmlSession.Videographer,
                        Sponsor = xmlSession.Sponsor,
                        SponsorURL = xmlSession.SponsorURL,
                        SponsorImage = MediaTools.LoadImage(xmlSession.SponsorImagePath),
                        ImageType = Utilities.GetFileExtension(xmlSession.SponsorImagePath),
                        Phase = (char)PublishingPhase.Draft
                    };

                    // Insert Session
                    db.InsertSession(session);

                    // Check results
                    if (session.SessionID == DataAccess.NullID)
                    {
                        Logger.Error("Failed to insert collection {0} session {1}.", collectionID, xmlSession.SessionOrder);
                    }
                    else
                    {
                        Logger.Write("Collection {0} session {1} inserted successfully.", collectionID, xmlSession.SessionOrder);
                    }
                }
                else
                {
                    Logger.Write("SKIPPING: Collection {0} already contains a session {1}.", collectionID, xmlSession.SessionOrder);
                }
            }

            Logger.Write("Session import done.");
        }

        #endregion XML SUB-TYPE INSERTION METHODS

        #region HELPER METHODS
        /// <summary>
        /// Formats the list into a contiguous string and converts all "curly quotes" to the straight equivalents.
        /// </summary>
        /// <param name="paragraphs"></param>
        /// <returns></returns>
        private static string FormatTranscript(List<string> paragraphs)
        {
            return string.Join("\n\n", paragraphs)
                .Replace('\u2018', '\u0027')    // Unicode left  single quotation mark --> ASCII apostrophe
                .Replace('\u2019', '\u0027')    // Unicode right single quotation mark --> ASCII apostrophe
                .Replace('\u201C', '\u0022')    // Unicode left  double quotation mark --> ASCII quotation mark
                .Replace('\u201D', '\u0022');   // Unicode right double quotation mark --> ASCII quotation mark
        }

        /// <summary>
        /// Rereives encoding information about the specified media file.
        /// </summary>
        /// <param name="mediaFile">The fully qualified path to a video file.</param>
        /// <returns>A MediaInfo instance containing the analysis.</returns>
        private static MediaInfo GetMediaInfo(string mediaFile)
        {
            // Verify that video exists
            if (!File.Exists(mediaFile))
            {
                throw new XmlImportException($"Video file does not exist: {mediaFile}.");
            }

            // Analyze the video
            Logger.Write($"Analyzing: {mediaFile}");
            var mediaInfo = MediaTools.MediaInfo(mediaFile);
            if (mediaInfo == null)
            {
                throw new XmlImportException($"Unable to analyze video file: {mediaFile}");
            }

            Logger.Write($"Duration={mediaInfo.Frames} frames ({mediaInfo.Duration}ms); {mediaInfo.Width}x{mediaInfo.Height} @ {mediaInfo.FPS} fps");

            return mediaInfo;
        }

        /// <summary>
        /// Returns the first character of an enumeration's value-name.
        /// </summary>
        /// <param name="enumeration">The enumeration value to parse.</param>
        /// <returns>The first character of the enumeration's value-name.</returns>
        private static char GetCharFromEnum(Enum enumeration)
        {
            // The ToString method on enumerations returns the friendly name.
            // ToString is then treated as a char array whose first element is
            // the value we are interested in.
            return enumeration.ToString()[0];
        }
        #endregion HELPER METHODS
    }

    /// <summary>
    /// The exception that is thrown when there is an error during the XmlImport process.
    /// </summary>
    internal class XmlImportException : Exception
    {
        public XmlImportException() { }

        public XmlImportException(string message) : base(message) { }
    }
}
