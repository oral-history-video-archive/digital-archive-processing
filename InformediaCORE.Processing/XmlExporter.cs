using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Common.Media;
using InformediaCORE.Common.Xml;

namespace InformediaCORE.Processing
{
    /// <summary>
    /// Exports data from the processing database as XML files suitable for (re-)processing. 
    /// </summary>
    public static class XmlExporter
    {
        /// <summary>
        /// The processing database context.
        /// </summary>
        private static IDVLDataContext _context;

        /// <summary>
        /// Cached contents of the AnnotationTypes table.
        /// </summary>
        private static IQueryable<AnnotationType> _annotationTypes;

        /// <summary>
        /// Cached contents of the Worlds table.
        /// </summary>
        private static IQueryable<World> _worlds;

        /// <summary>
        /// Cached contents of the Partitions table.
        /// </summary>
        private static IQueryable<Partition> _partitions;

        /// <summary>
        /// Export process-ready XML from the configured database.
        /// </summary>
        /// <param name="accession">The accession number of the collection to be exported.</param>
        /// <param name="path">The directory where the generated output should be written.</param>
        public static void ExportCollection(string accession, string path)
        {
            using (_context = DataAccess.GetDataContext(Settings.Current.ConnectionString))
            {
                var collection = 
                    (from c in _context.Collections
                     where c.Accession == accession
                     select c).FirstOrDefault();

                if (collection == null)
                {
                    Logger.Error("Could not find accession {0}.", accession);
                    return;
                }

                if (!Directory.Exists(path))
                {
                    Logger.Error("Specified output path does not exist: {0}", path);
                    return;
                }

                // Cache lookup tables to reduce round-trips to database
                _worlds = from w in _context.Worlds select w;
                _partitions = from p in _context.Partitions select p;
                _annotationTypes = from a in _context.AnnotationTypes select a;

                // Export the Collection and sub-types
                var xmlCollection = new XmlCollection
                {
                    Accession = collection.Accession,
                    DescriptionShort = collection.DescriptionShort,
                    BiographyShort = collection.BiographyShort,
                    FirstName = collection.FirstName,
                    LastName = collection.LastName,
                    PreferredName = collection.PreferredName,
                    Gender = collection.Gender == 'M' ? XmlGenderType.Male : XmlGenderType.Female,
                    WebsiteURL = collection.WebsiteURL,
                    Region = collection.Region,
                    BirthCity = collection.BirthCity,
                    BirthState = collection.BirthState,
                    BirthCountry = collection.BirthCountry,
                    BirthDate = collection.BirthDate,
                    DeceasedDate = collection.DeceasedDate,
                    PortraitPath = ExportPortrait(collection, path),
                    Sessions = GetSessions(collection, path),
                    Annotations = GetCollectionAnnotations(collection, path),
                    Partitions = GetPartitions(collection, path)
                };

                var filename = Path.Combine(path, $"{GetSafeFileName(collection.Accession)}.collection.xml");
                Logger.Write("...{0}", filename);
                XmlUtilities.Write(xmlCollection, filename);

                ExportMovies(collection, path);
            }
        }

        /// <summary>
        /// Saves the collection portrait to the given path.
        /// </summary>
        /// <param name="collection">A database Collection instance.</param>
        /// <param name="path">The directory where the image should be written.</param>
        /// <returns>The fully qualified path and name of the saved image.</returns>
        private static string ExportPortrait(Collection collection, string path)
        {
            var filename = Path.Combine(path, $"{GetSafeFileName(collection.Accession)}.{collection.FileType}");

            if (!File.Exists(filename))
            {
                Logger.Write("...{0}", filename);
                MediaTools.SaveImage(collection.Portrait.ToArray(), filename);
            }

            return filename;
        }

        /// <summary>
        /// Retrieve the list of sessions associated with the given collection.
        /// </summary>
        /// <param name="collection">A database Collection instance.</param>
        /// <param name="path">The directory where the generated output should be written.</param>
        /// <returns>A list of XmlSessions.</returns>
        private static List<XmlSession> GetSessions(Collection collection, string path)
        {
            return
                (from s in _context.Sessions
                 where s.CollectionID == collection.CollectionID
                 select new XmlSession
                 {
                    SessionOrder = s.SessionOrder,
                    Interviewer = s.Interviewer,
                    InterviewDate = s.InterviewDate,
                    Location = s.Location,
                    Videographer = s.Videographer,
                    Sponsor = s.Sponsor,
                    SponsorURL = s.SponsorURL,
                    SponsorImagePath = ExportSponsorImage(s, path)
                 }).ToList();
        }

        /// <summary>
        /// Saves the session's sponsor image to the given path.
        /// </summary>
        /// <param name="session">A database Session instance.</param>
        /// <param name="path">The directory where the image should be written.</param>
        /// <returns>The fully qualified path to the saved image if the session has one; null otherwise.</returns>
        private static string ExportSponsorImage(Session session, string path)
        {
            if (session.SponsorImage == null) return null;

            var accession = session.Collection.Accession;
            var filename = Path.Combine(path, $"{GetSafeFileName(accession)}_{session.SessionOrder:000}.{session.ImageType}");

            if (!File.Exists(filename))
            {
                Logger.Write("...{0}", filename);
                MediaTools.SaveImage(session.SponsorImage.ToArray(), filename);
            }

            return filename;
        }

        /// <summary>
        /// Retrieve the list of annotations associated with the given collection.
        /// A .annotationType.xml file will be generated for each of the unique 
        /// annotation types in the result list.
        /// </summary>
        /// <param name="collection">A database Collection instance.</param>
        /// <param name="path">The directory where the generated XML should be written.</param>
        /// <returns>A list of XmlAnnotations.</returns>
        private static List<XmlAnnotation> GetCollectionAnnotations(Collection collection, string path)
        {
            Logger.Write("Exporting collection-level AnnotationTypes...");

            var annotations = (
                from x in _context.Annotations
                where x.CollectionID == collection.CollectionID
                select x).ToList();


            var xmlAnnotations = new List<XmlAnnotation>();

            foreach (var annotation in annotations)
            {
                var annotationType = _annotationTypes.First(x => x.TypeID == annotation.TypeID);

                var xmlAnnotationType = new XmlAnnotationType
                {
                    Name = annotationType.Name,
                    Description = annotationType.Description,
                    Scope = XmlAnnotationScope.Collection
                };

                var filename = Path.Combine(path, $"{GetSafeFileName(xmlAnnotationType.Name)}.annotationType.xml");

                if (!File.Exists(filename))
                {
                    Logger.Write("...{0}", filename);
                    XmlUtilities.Write(xmlAnnotationType, filename);
                }

                xmlAnnotations.Add(new XmlAnnotation
                {
                    Type = annotationType.Name,
                    Value = annotation.Value
                });
            }

            return xmlAnnotations;
        }

        /// <summary>
        /// Retrieve a list of partitions associated with the given collection.
        /// A .world.xml file will be generated for each of the worlds associated
        /// with the given collection.
        /// </summary>
        /// <param name="collection">A database Collection instance.</param>
        /// <param name="path">The directory where the generated XML should be written.</param>
        /// <returns>A list of partition names.</returns>
        private static List<string> GetPartitions(Collection collection, string path)
        {
            var members =
                from m in _context.PartitionMembers
                where m.CollectionID == collection.CollectionID
                select m;

            var partitionList = new List<string>();
            var worldDict = new Dictionary<int, XmlWorld>();

            foreach (var member in members)
            {
                var partition = _partitions.First(p => p.PartitionID == member.PartitionID);

                partitionList.Add(partition.Name);

                var xmlPartition = new XmlPartition
                {
                    Description = partition.Description,
                    Name = partition.Name
                };

                if (!worldDict.ContainsKey(partition.WorldID))
                {
                    var world = _worlds.First(w => w.WorldID == partition.WorldID);

                    worldDict.Add(partition.WorldID, new XmlWorld
                    {
                        Name = world.Name,
                        Description = world.Description,
                        Partitions = new List<XmlPartition>()
                    });
                }

                worldDict[partition.WorldID].Partitions.Add(xmlPartition);
            }

            foreach (var xmlWorld in worldDict.Values)
            {
                var filename = Path.Combine(path, $"{GetSafeFileName(xmlWorld.Name)}.world.xml");
                Logger.Write("...{0}", filename);
                XmlUtilities.Write(xmlWorld, filename);
            }

            return partitionList;
        }

        /// <summary>
        /// Generate the .movie.xml file for each of the movies associated with the given collection.
        /// </summary>
        /// <param name="collection">A database Collection instance.</param>
        /// <param name="path">The directory where the generated XML should be written.</param>
        private static void ExportMovies(Collection collection, string path)
        {
            var sessions =
                from s in _context.Sessions
                where s.CollectionID == collection.CollectionID
                orderby s.SessionOrder
                select s;

            foreach (var session in sessions)
            {
                var movies =
                    from m in _context.Movies
                    where m.SessionID == session.SessionID
                    orderby m.Tape
                    select m;

                foreach (var movie in movies)
                {
                    // Replace old-style movie names with new canonical form
                    var movieName = GetSafeFileName($"{collection.Accession}_{session.SessionOrder:000}_{movie.Tape:000}");

                    var xmlMovie = new XmlMovie
                    {
                        Name = movieName,
                        Path = movie.MediaPath,
                        Collection = collection.Accession,
                        Abstract = movie.Abstract,
                        TapeNumber = movie.Tape,
                        SessionNumber = session.SessionOrder,
                        Annotations = GetMovieAnnotations(movie, path),
                        Segments = GetSegments(movie, path)
                    };

                    var filename = Path.Combine(path, $"{movieName}.movie.xml");
                    Logger.Write("...{0}", filename);
                    XmlUtilities.Write(xmlMovie, filename);
                }

            }
        }

        /// <summary>
        /// Retrieve the list of annotations associated with the given movie.
        /// A .annotationType.xml file will be generated for each of the unique
        /// annotation types in the result list.
        /// </summary>
        /// <param name="movie">A database Movie instance.</param>
        /// <param name="path">The directory where the generated XML should be written.</param>
        /// <returns></returns>
        private static List<XmlAnnotation> GetMovieAnnotations(Movie movie, string path)
        {
            Logger.Write("Exporting movie-level AnnotationTypes...");

            var annotations = (
                from x in _context.Annotations
                where x.MovieID == movie.MovieID
                select x).ToList();


            var xmlAnnotations = new List<XmlAnnotation>();

            foreach (var annotation in annotations)
            {
                var annotationType = _annotationTypes.First(x => x.TypeID == annotation.TypeID);

                var xmlAnnotationType = new XmlAnnotationType
                {
                    Name = annotationType.Name,
                    Description = annotationType.Description,
                    Scope = XmlAnnotationScope.Movie
                };

                var filename = Path.Combine(path, $"{GetSafeFileName(xmlAnnotationType.Name)}.annotationType.xml");

                if (!File.Exists(filename))
                {
                    Logger.Write("...{0}", filename);
                    XmlUtilities.Write(xmlAnnotationType, filename);
                }

                xmlAnnotations.Add(new XmlAnnotation
                {
                    Type = annotationType.Name,
                    Value = annotation.Value
                });
            }

            return xmlAnnotations;
        }

        /// <summary>
        /// Retreive the list of segments associated with the given movie.
        /// </summary>
        /// <param name="movie">A database Movie instance.</param>
        /// <param name="path">The directory where the generated XML should be written.</param>
        /// <returns>A list of XmlSegments.</returns>
        private static List<XmlSegment> GetSegments(Movie movie, string path)
        {
            var segments =
                from s in _context.Segments
                where s.MovieID == movie.MovieID
                orderby s.SegmentOrder
                select s;

            var xmlSegments = new List<XmlSegment>();
            foreach (var segment in segments)
            {
                xmlSegments.Add(new XmlSegment
                {
                    Title = segment.Title,
                    Abstract = segment.Abstract,
                    TimeFormat = XmlTimeFormatSpecifier.HMSHMS,
                    StartTime = DateTime.MinValue.AddMilliseconds(segment.StartTime),
                    EndTime = DateTime.MinValue.AddMilliseconds(segment.EndTime),
                    Transcript = GetParagraphs(segment.TranscriptText),
                    Annotations = GetSegmentAnnotation(segment, path)
                });
            }

            return xmlSegments;
        }

        /// <summary>
        /// Retrieve the list of annotations associated with the given segment.
        /// A .annotationType.xml file will be generated for each of the unique
        /// annotation types in the result list.
        /// </summary>
        /// <param name="segment">A database Segment instance.</param>
        /// <param name="path">The directory where the generated XML should be written.</param>
        /// <returns></returns>
        private static List<XmlAnnotation> GetSegmentAnnotation(Segment segment, string path)
        {
            Logger.Write("Exporting segment-level AnnotationTypes...");

            var annotations = (
                from x in _context.Annotations
                where x.SegmentID == segment.SegmentID
                select x).ToList();


            var xmlAnnotations = new List<XmlAnnotation>();

            foreach (var annotation in annotations)
            {
                var annotationType = _annotationTypes.First(x => x.TypeID == annotation.TypeID);

                var xmlAnnotationType = new XmlAnnotationType
                {
                    Name = annotationType.Name,
                    Description = annotationType.Description,
                    Scope = XmlAnnotationScope.Segment
                };

                var filename = Path.Combine(path, $"{GetSafeFileName(xmlAnnotationType.Name)}.annotationType.xml");

                if (!File.Exists(filename))
                {
                    Logger.Write("...{0}", filename);
                    XmlUtilities.Write(xmlAnnotationType, filename);
                }

                xmlAnnotations.Add(new XmlAnnotation
                {
                    Type = annotationType.Name,
                    Value = annotation.Value
                });
            }

            return xmlAnnotations;
        }

        /// <summary>
        /// Split a transcript into a list of paragraphs.
        /// </summary>
        /// <param name="transcript">Transcript text to split.</param>
        /// <returns>A list of strings representing each paragraph.</returns>
        private static List<string> GetParagraphs(string transcript)
        {
            return transcript.Split(new[] { "\n\n" }, StringSplitOptions.None).ToList();
        }

        /// <summary>
        /// Replaces illegal characters in a filename with an underscore.
        /// </summary>
        /// <param name="filename">The potentially unsafe filename.</param>
        /// <returns>A string containing a valid filename.</returns>
        private static string GetSafeFileName(string filename)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"[^a-zA-Z0-9_]");
            return regex.Replace(filename, "_");
        }
    }
}
