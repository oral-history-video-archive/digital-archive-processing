using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InformediaCORE.Azure.Models;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Firebase;

namespace InformediaCORE.Azure
{
    /// <summary>
    /// Generates all data necessary to publish a database collection to the digital archive.
    /// </summary>
    public class PublishingPackage : AbstractPackage
    {
        #region ====================             NESTED  CLASSES             ====================
        /// <summary>
        /// Data required to publish an image
        /// </summary>
        public class Image
        {
            /// <summary>
            /// Biography or Story identifier
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// Three letter code indicating type of image file: jpg | png
            /// </summary>
            public string FileType { get; set; }

            /// <summary>
            /// The raw image data as a byte array.
            /// </summary>
            public byte[] Data { get; set; }
        }

        /// <summary>
        /// Data required to publish a video
        /// </summary>
        public class Video
        {
            /// <summary>
            /// StoryID
            /// </summary>
            public string ID { get; set; }
            
            /// <summary>
            /// Fully qualified path to .mp4 video file.
            /// </summary>
            public string MediaPath { get; set; }
        }

        /// <summary>
        /// Data required to publish the VTT captions
        /// </summary>
        public class WebVTT
        {
            /// <summary>
            /// StoryID
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// Fully qualified path to the .vtt caption file.
            /// </summary>
            public string FilePath { get; set; }
        }
        #endregion =================             NESTED  CLASSES             ====================

        #region ====================               CONSTRUCTOR               ====================
        /// <summary>
        /// Instantiates a PublishingPackage with all content for the specified collection.
        /// </summary>
        /// <param name="accession">A collection accession number.</param>
        public PublishingPackage(string accession, bool importSegmentTagsFromFirebase)
        {
            using (_context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                Logger.Write($"Generating publishing package for collection {accession}...");

                _collection = (from c in _context.Collections
                               where c.Accession == accession
                               select c).FirstOrDefault();

                if (_collection == null)
                {
                    throw new PublishingPackageException("Unable to initialize PublishingPackage: Invalid accession number given.");
                }

                // Select all available sessions in sorted order
                _sessions = _collection.Sessions?.OrderBy(s => s.SessionOrder).ToList();

                if (_sessions == null)
                {
                    throw new PublishingPackageException("Unable to initialize PublishingPackage: No sessions found.");
                }

                Initialize(importSegmentTagsFromFirebase);
            }
        }

        /// <summary>
        /// Instantiates a PublishingPackage for the specified collection choosing only sessions
        /// which have been previously published to the given target archive.
        /// </summary>
        /// <param name="accession">The accession number of the database collection to be loaded.</param>
        /// <param name="target">The digital archive which this content will be published to.</param>
        public PublishingPackage(string accession, DigitalArchiveSpecifier target)
        {
            using (_context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                Logger.Write($"Generating publishing package for collection {accession}...");

                _collection = (from c in _context.Collections
                               where c.Accession == accession
                               select c).FirstOrDefault();

                if (_collection == null)
                {
                    throw new PublishingPackageException("Unable to initialize PublishingPackage: Invalid accession number given.");
                }

                // Select sessions matching target publishing phase in sorted order
                _sessions = (from s in _collection.Sessions
                             where AllowedPhases(target).Contains(s.Phase)
                             select s)?.OrderBy(s => s.SessionOrder).ToList();

                if (_sessions == null)
                {
                    throw new PublishingPackageException("Unable to initialize PublishingPackage: No sessions found suitable for the specified target.");
                }

                Initialize();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name = "accession" >A collection accession number</param>
        /// <param name="target">The digital archive which this content will be published to.</param>
        /// <param name="sessionOrdinals">List of SessionOrder numbers specifying which sessions to include.</param>
        public PublishingPackage(string accession, DigitalArchiveSpecifier target, int[] sessionOrdinals, bool importSegmentTagsFromFirebase)
        {
            using (_context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                Logger.Write($"Generating publishing package for collection {accession}...");

                if (sessionOrdinals == null || sessionOrdinals.Length == 0)
                {
                    throw new PublishingPackageException("Unable to initialize PublishingPackage: No sessions specified.");
                }

                _collection = (from c in _context.Collections
                               where c.Accession == accession
                               select c).FirstOrDefault();

                if (_collection == null)
                {
                    throw new PublishingPackageException("Unable to initialize PublishingPackage: Invalid accession number given.");
                }

                // Select sessions explicitly specified by caller
                var explictSessions = (from s in _collection.Sessions
                                       where sessionOrdinals.Contains(s.SessionOrder)
                                       select s).ToList();

                if (explictSessions == null)
                {
                    var plural = (sessionOrdinals.Length > 1) ? "s" : string.Empty;
                    throw new PublishingPackageException($"Unable to initialize PublishingPackage: No sessions found matching the given sessionOrder{plural}.");
                }

                // Select sessions suitable for the publishing target
                var implicitSessions = (from s in _collection.Sessions
                                        where AllowedPhases(target).Contains(s.Phase)
                                        select s).ToList();

                // Union the two lists and sort by session order
                _sessions = explictSessions.Union(implicitSessions).OrderBy(s => s.SessionOrder).ToList();

                Initialize(importSegmentTagsFromFirebase);
            }
        }

        /// <summary>
        /// Initialize the publishing package from the loaded collection and session(s).
        /// </summary>
        private void Initialize(bool importSegmentTagsFromFirebase = false)
        {
            if (!IsReadyToPublish())
            {
                throw new PublishingPackageException("Collection not ready to be published, see log for details.");
            }

            if (importSegmentTagsFromFirebase)
            {
                TagImporter.ImportTagsForCollection(_collection.Accession);
            }

            LoadCollectionAnnotations();
            LoadMakerCategoriesAndJobTypes();

            Stories = new List<Story>();
            StoryDetails = new List<StoryDetails>();

            var biographySessions = new List<BiographySession>();
            foreach (var session in _sessions)
            {
                var biographyTapes = new List<BiographyTape>();
                foreach (var movie in session.Movies.OrderBy(m => m.Tape))
                {
                    var biographyStories = new List<BiographyStory>();
                    foreach (var segment in movie.Segments.OrderBy(s => s.SegmentOrder))
                    {
                        biographyStories.Add(new BiographyStory(segment));
                        Stories.Add(CreateStory(segment));
                        StoryDetails.Add(CreateStoryDetails(segment));
                        _segments.Add(segment);
                    }
                    biographyTapes.Add(new BiographyTape(movie, biographyStories));
                }
                biographySessions.Add(new BiographySession(session, biographyTapes));
            }

            Biography = CreateBiography(_collection);
            BiographyDetails = new BiographyDetails(_collection)
            {
                IsScienceMaker = _isScienceMaker,
                MakerCategories = _categories.ToArray(),
                OccupationTypes = _jobTypes.ToArray(),
                Occupations = _occupations.ToArray(),
                Favorites = _favorites,
                Sessions = biographySessions
            };

            Logger.Write($"Publishing package initialized successfully.");
        }
        #endregion =================               CONSTRUCTOR               ====================

        #region ====================             PUBLIC  METHODS             ====================
        /// <summary>
        /// Get the portrait image for the known biographical collection.
        /// </summary>
        /// <returns>The portrait Image on success; null otherwise.</returns>
        public Image GetBiographyImage()
        {
            if (_collection == null)
            {
                return null;
            }

            return new Image
            {
                ID = _collection.CollectionID.ToString(),
                FileType = _collection.FileType,
                Data = _collection.Portrait.ToArray()
            };
        }

        /// <summary>
        /// Get the keyframe image for the specified story.
        /// </summary>
        /// <param name="storyID">The story identifier.</param>
        /// <returns>The keyframe image on success; null otherwise.</returns>
        public Image GetStoryImage(string storyID)
        {
            var segment = GetSegment(storyID);

            if (segment == null)
            {
                return null;
            }
            return new Image
            {
                ID = storyID,
                FileType = string.Empty,
                Data = segment.Keyframe.ToArray()
            };
        }

        /// <summary>
        /// Get the path to the video for the specified story.
        /// </summary>
        /// <param name="storyID">The story identifier.</param>
        /// <returns>The video media path on success; null otherwise.</returns>
        public Video GetStoryVideo(string storyID)
        {
            var segment = GetSegment(storyID);

            if (segment == null)
            {
                return null;
            }
            return new Video
            {
                ID = storyID,
                MediaPath = segment.MediaPath
            };
        }

        /// <summary>
        /// Get the path to the VTT caption file for the specified story.
        /// </summary>
        /// <param name="storyID">The story identifier.</param>
        /// <returns>The fully qualified path to the WebVTT file.; nu</returns>
        public WebVTT GetStoryCaptions(string storyID)
        {
            var segment = GetSegment(storyID);

            if (segment == null)
            {
                return null;
            }
            return new WebVTT
            {
                ID = storyID,
                FilePath = segment.MediaPath.Replace("mp4", "vtt")
            };
        }

        /// <summary>
        /// Returns a report of all sessions, tapes (movies), and stories (segments)
        /// contained in the PublishingPackage.
        /// </summary>
        /// <returns>A string containing the formatted report.</returns>
        public string GetSummaryReport()
        {
            var summary = $"Summary Report for biographical collection {Biography.Accession} {Biography.PreferredName}\n";

            foreach (var session in BiographyDetails.Sessions)
            {
                summary += $"  Session #{session.SessionOrder}: Conducted {session.InterviewDate:MM-dd-yyyy} by {session.Interviewer}\n";

                foreach (var tape in session.Tapes)
                {
                    summary += $"    Tape {tape.TapeOrder}:\n";

                    foreach (var story in tape.Stories)
                    {
                        summary += $"      Story {story.StoryOrder,2}: {story.Title.Truncate(65)}\n";
                    }
                }
            }

            return summary;
        }

        /// <summary>
        /// Promotes the publishing Phase of the package's collection and session(s) to
        /// the given phase as needed.
        /// </summary>
        /// <param name="newPhase">An enum specifying the new publishing phase.</param>
        public void PromotePublishingPhase(PublishingPhase newPhase)
        {
            var collectionPhase = DataAccess.GetCollectionPublishingPhase(Accession);

            // Promote Collection publishing phase as needed
            if (collectionPhase.Compare(newPhase) < 0)
            {
                Logger.Write($"Promoting collection {Accession} publishing phase from {collectionPhase} to {newPhase}...");
                DataAccess.SetCollectionPublishingPhase(Accession, newPhase);
                Logger.Write("  ...collection publishing phase promoted successfully.");
            }

            // Promote Session publishing phase as needed
            foreach (var session in _sessions)
            {
                var sessionPhase = DataAccess.GetSessionPublishingPhase(Accession, session.SessionOrder);

                if (sessionPhase.Compare(newPhase) < 0)
                {
                    Logger.Write($"Promoting {Accession} session #{session.SessionOrder} publishing phase from {sessionPhase} to {newPhase}...");
                    DataAccess.SetSessionPublishingPhase(Accession, session.SessionOrder, newPhase);
                    Logger.Write("  ...session publishing phase promoted successfully.");
                }
            }
        }
        #endregion =================             PUBLIC  METHODS             ====================

        #region ====================            DATABASE  METHODS            ====================
        /// <summary>
        /// Validate the processing state and media paths for all collection content prior to publishing.  
        /// </summary>
        /// <returns>Returns true if the collection is in a publishable state; false otherwise.</returns>
        private bool IsReadyToPublish()
        {
            var isReady = true;

            Logger.Write("Validating collection {0}...", _collection.Accession);

            if (_collection.Portrait == null)
            {
                Logger.Warning("...collection portrait is missing.");
                isReady = false;
            }

            // Validate only the sessions selected for publishing
            foreach(var session in _sessions)
            {
                foreach (var movie in session.Movies.OrderBy(m => m.Tape))
                {
                    foreach (var segment in movie.Segments.OrderBy(s => s.SegmentOrder))
                    {
                        if (segment.Ready == 'Y')
                        {
                            if (segment.Keyframe == null)
                            {
                                // NOTE: Consider having the keyframe come from the file system like the video.
                                Logger.Warning("...Segment {0} keyframe is missing.", segment.SegmentID);
                                isReady = false;
                            }

                            if (!File.Exists(segment.MediaPath))
                            {
                                Logger.Warning("...Segment {0} video is missing: {1}", segment.SegmentID, segment.MediaPath);
                                isReady = false;
                            }
                        }
                        else
                        {
                            Logger.Warning("...Segment {0} is not fully processed.", segment.SegmentID);
                            isReady = false;
                        }
                    }
                }
            }

            return isReady;
        }
        #endregion =================            DATABASE  METHODS            ====================
    }

    /// <summary>
    /// Represents errors specific to the PublishingPackage class.
    /// </summary>
    public class PublishingPackageException : Exception
    {
        public PublishingPackageException() { }

        public PublishingPackageException(string message) : base(message) { }
    }
}
