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
    /// Generates all data necessary to publish a database collection to the digital archive.
    /// </summary>
    public class UpdatePackage : AbstractPackage
    {
        #region ====================               CONSTRUCTOR               ====================
        /// <summary>
        /// Instantiates an UpdatePackage with content matching the existing collection.
        /// </summary>
        public UpdatePackage(BiographyDetails biographyDetails, DigitalArchiveSpecifier target)
        {
            using (_context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                Logger.Write($"Generating update package for collection {biographyDetails.Accession}...");

                _collection = (from c in _context.Collections
                               where c.Accession == biographyDetails.Accession
                               select c).FirstOrDefault();

                if (_collection == null)
                {
                    throw new UpdatePackageException($"...unable to initialize UpdatePackage, accession {biographyDetails.Accession} invalid.");
                }

                // Select sessions matching target publishing phase
                _sessions = (from s in _collection.Sessions
                             where AllowedPhases(target).Contains(s.Phase)
                             select s)?.ToList();

                if (_sessions == null)
                {
                    throw new UpdatePackageException("...unable to initialize UpdatePackage: no sessions found.");
                }

                if (!ValidateData(biographyDetails))
                {
                    throw new UpdatePackageException("...unable to initialize UpdatePackage due to data validation errors.");
                }

                Logger.Write("Initializing update package...");

                // This could be a conditional but since this explicitly and update package,
                // it seems prudent to force a tag update as well.
                TagImporter.ImportTagsForCollection(_collection.Accession);

                LoadCollectionAnnotations();
                LoadMakerCategoriesAndJobTypes();
                
                Stories = new List<Story>();
                StoryDetails = new List<StoryDetails>();

                var biographySessions = new List<BiographySession>();
                foreach (var session in _sessions)
                {
                    var biographyTapes = new List<BiographyTape>();
                    foreach (var movie in session.Movies)
                    {
                        var biographyStories = new List<BiographyStory>();
                        foreach (var segment in movie.Segments)
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

                Logger.Write($"...update package initialized successfully.");
            }
        }
        #endregion =================               CONSTRUCTOR               ====================

        #region ====================            DATABASE  METHODS            ====================
        /// <summary>
        /// Make sure the processing data matches the previously published data.
        /// </summary>
        /// <returns>Returns true if the collection passes validation checks; false otherwise.</returns>
        private bool ValidateData(BiographyDetails biographyDetails)
        {
            Logger.Write($"Validating collection {_collection.Accession}...");

            // Validate session counts
            if (biographyDetails.Sessions.Count != _sessions.Count)
            {
                Logger.Warning($"...session counts don't match: {biographyDetails.Sessions.Count} on digital archive <> {_sessions.Count} selected from database.");
                return false;
            }

            // Validate the Session Data
            foreach (var bioSession in biographyDetails.Sessions)
            {
                Logger.Write($"...validating session #{bioSession.SessionOrder}");

                var session = _sessions.SingleOrDefault(s => s.SessionOrder == bioSession.SessionOrder);
                if (session == null)
                {
                    Logger.Write($"...did not find matching database session.");
                    return false;
                }

                // Validate the Tape / Movie Counts
                var movies = session.Movies;
                if (bioSession.Tapes.Count != movies.Count)
                {
                    Logger.Write($"...movie counts don't match: {bioSession.Tapes.Count} tapes on digital archive <> {movies.Count} movies selected from database.");
                    return false;
                }

                // Validate the Tape / Movie Data
                foreach (var tape in bioSession.Tapes)
                {
                    var movie = movies.SingleOrDefault(m => m.Tape == tape.TapeOrder);
                    if (movie == null)
                    {
                        Logger.Write($"...did not find a database movie matching tape #{tape.TapeOrder}.");
                        return false;
                    }

                    // Validate the Story / Segment Counts
                    var segments = movie.Segments;
                    if (tape.Stories.Count != segments.Count)
                    {
                        Logger.Write($"...story count mismatch: {tape.Stories.Count} stories found for tape #{tape.TapeOrder} <> {segments.Count} segments for movie #{movie.Tape}.");
                        return false;
                    }

                    // Validate the Story / Segment Data
                    foreach (var story in tape.Stories)
                    {
                        var segment = segments.SingleOrDefault(s => s.SegmentOrder == story.StoryOrder);
                        if (segment == null)
                        {
                            Logger.Write($"...did not find database segment matching story #{story.StoryID}");
                            return false;
                        }

                        if (segment.Ready != 'Y')
                        {
                            Logger.Warning($"...Segment {segment.SegmentID} is not fully processed.");
                            return false;
                        }
                    }
                }
            }

            Logger.Write("...validation successful.");

            return true;
        }
        #endregion =================            DATABASE  METHODS            ====================
    }

    /// <summary>
    /// Represents errors specific to the UpdatePackage class.
    /// </summary>
    public class UpdatePackageException : Exception
    {
        public UpdatePackageException() { }

        public UpdatePackageException(string message) : base(message) { }
    }
}
