using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing.Database;

namespace InformediaCORE.ShowReport
{
    /// <summary>
    /// Used to filter results of the ProcessingDetails report.
    /// </summary>
    public enum ProcessFilter
    {
        /// <summary>
        /// Display all segments
        /// </summary>
        All,

        /// <summary>
        /// Display only segments with all Complete flags.
        /// </summary>
        Completed,

        /// <summary>
        /// Display only segments with at least one Failure flag.
        /// </summary>
        Failed,
        
        /// <summary>
        /// Display only segments with all Pending flags.
        /// </summary>
        Pending,

        /// <summary>
        /// Display only segments with at least one Running flag.
        /// </summary>
        Running
    }

    /// <summary>
    /// Used to filter the results of the WebVideoCheck report.
    /// </summary>
    public enum VideoReportFilter
    {
        None = 0,
        MissingOnly = 1,
        MissingAndFound = 2,
        ShowAll = 3
    }

    /// <summary>
    /// Generates a number of useful reports about the database, processing status, and
    /// completeness of the web video output tree.
    /// </summary>
    /// <remarks>
    /// History:
    /// 2013-03-11 bm3n:
    ///     Changed ProcessingDetails report output from VKGT to VKTG to match order of processing.
    ///     
    /// </remarks>
    class ReportGenerator 
    {
        private const string DOUBLELINE = "================================================================================";
        private const string SINGLELINE = "--------------------------------------------------------------------------------";

        // HACK: Ideally this list would be dynamically generated.
        // The dynamic version is unaware of the task sequence so this list
        // was created manually to match SegementProcessor task sequence.
        private readonly List<string> TaskNames = new List<string>()
        {
            "TranscodingTask",
            "KeyFrameTask",
            "AlignmentTask",
            "CaptioningTask",
            "SpacyTask",
            "StanfordTask",
            "EntityResolutionTask"            
        };

        private readonly DataAccessExtended DAE = new DataAccessExtended();


        /// <summary>
        /// Database Stats Report.
        /// Gives some general information about the quantity of data in the database.
        /// </summary>
        internal void DatabaseOverview()
        {
            if (DAE == null)
            {
                Utilities.WriteLine("Failed to open database, operation aborted.", ConsoleColor.Red);
                return;
            }

            Utilities.NewLine();
            Utilities.WriteLine("Database Overview", ConsoleColor.Yellow);
            Utilities.WriteLine(DOUBLELINE, ConsoleColor.Gray);

            //
            // TABLE METRICS
            //
            TableMetrics tableMetrics = DAE.GetTableMetrics();

            Utilities.WriteLine("Table Metrics", ConsoleColor.DarkYellow);
            Utilities.WriteLine("----------------------------------", ConsoleColor.Gray);
            Utilities.Write("         Collections: ", ConsoleColor.Green); Utilities.WriteLine($"{tableMetrics.Collections,8:N0}");
            Utilities.Write("              Movies: ", ConsoleColor.Green); Utilities.WriteLine($"{tableMetrics.Movies,8:N0}");
            Utilities.Write("            Segments: ", ConsoleColor.Green); Utilities.WriteLine($"{tableMetrics.Segments,8:N0}");
            Utilities.WriteLine("----------------------------------", ConsoleColor.Gray);
            Utilities.NewLine();
            Utilities.NewLine();

            //
            // Processing Metrics
            //
            ProcessingMetrics processingMetrics = DAE.GetProcessingMetrics();

            Utilities.WriteLine("Segment Processing Metrics", ConsoleColor.DarkYellow);
            Utilities.WriteLine("----------------------------------", ConsoleColor.Gray);
            Utilities.Write("           Completed: ", ConsoleColor.Green); Utilities.WriteLine($"{processingMetrics.Completed,8:N0}");
            Utilities.Write("             Running: ", ConsoleColor.Green); Utilities.WriteLine($"{processingMetrics.Running,8:N0}");
            Utilities.Write("              Failed: ", ConsoleColor.Green); Utilities.WriteLine($"{processingMetrics.Failed,8:N0}");
            Utilities.Write("             Pending: ", ConsoleColor.Green); Utilities.WriteLine($"{processingMetrics.Pending,8:N0}");
            Utilities.WriteLine("           -----------------------", ConsoleColor.Gray);
            Utilities.Write("               Total: ", ConsoleColor.Green); 
            Utilities.WriteLine($"{processingMetrics.Completed + processingMetrics.Running + processingMetrics.Failed + processingMetrics.Pending,8:N0}");
            Utilities.NewLine();
            Utilities.NewLine();

            //
            // Segment Metrics
            //
            MediaMetrics MStats = DAE.GetMediaMetrics();

            Utilities.WriteLine("Content Metrics (HH:MM:SS)", ConsoleColor.DarkYellow);
            Utilities.WriteLine("----------------------------------", ConsoleColor.Gray);
            Utilities.Write("   Processed Content: ", ConsoleColor.Green); Utilities.WriteLine($"{MStoHMS(MStats.ProcessedContent),12}");
            Utilities.Write("    Shortest Segment: ", ConsoleColor.Green); Utilities.WriteLine($"{MStoHMS(MStats.ShortestSegment),12}");
            Utilities.Write("     Longest Segment: ", ConsoleColor.Green); Utilities.WriteLine($"{MStoHMS(MStats.LongestSegment),12}");
            Utilities.Write("     Average Segment: ", ConsoleColor.Green); Utilities.WriteLine($"{MStoHMS(MStats.AverageSegment),12}");
            Utilities.NewLine();
            Utilities.NewLine();

            //
            // Corpus Metrics
            //
            CorpusMetrics corpusMetrics;
            
            corpusMetrics = DataAccess.GetCorpusMetrics(PublishingPhase.Review);

            Utilities.WriteLine("Corpus Metrics: Processing (test) Site", ConsoleColor.DarkYellow);
            Utilities.WriteLine("--------------------------------------", ConsoleColor.Gray);
            Utilities.WriteLine("HistoryMakers", ConsoleColor.DarkYellow);
            Utilities.WriteLine("-------------", ConsoleColor.Gray);
            Utilities.Write("         Biographies: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.HistoryMakers.Biographies.All,8:N0}");
            Utilities.Write("             Stories: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.HistoryMakers.Stories.All,8:N0}");
            Utilities.Write("  Tagged Biographies: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.HistoryMakers.Biographies.Tagged,8:N0}");            
            Utilities.Write("      Tagged Stories: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.HistoryMakers.Stories.Tagged,8:N0}");
            Utilities.NewLine();
            Utilities.WriteLine("ScienceMakers", ConsoleColor.DarkYellow);
            Utilities.WriteLine("-------------", ConsoleColor.Gray);
            Utilities.Write("         Biographies: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.ScienceMakers.Biographies.All,8:N0}");
            Utilities.Write("             Stories: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.ScienceMakers.Stories.All,8:N0}");
            Utilities.Write("  Tagged Biographies: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.ScienceMakers.Biographies.Tagged,8:N0}");
            Utilities.Write("      Tagged Stories: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.ScienceMakers.Stories.Tagged,8:N0}");
            Utilities.NewLine();
            Utilities.NewLine();

            corpusMetrics = DataAccess.GetCorpusMetrics(PublishingPhase.Published);

            Utilities.WriteLine("Corpus Metrics: Production (live) Site", ConsoleColor.DarkYellow);
            Utilities.WriteLine("--------------------------------------", ConsoleColor.Gray);
            Utilities.WriteLine("HistoryMakers", ConsoleColor.DarkYellow);
            Utilities.WriteLine("-------------", ConsoleColor.Gray);
            Utilities.Write("         Biographies: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.HistoryMakers.Biographies.All,8:N0}");
            Utilities.Write("             Stories: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.HistoryMakers.Stories.All,8:N0}");
            Utilities.Write("  Tagged Biographies: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.HistoryMakers.Biographies.Tagged,8:N0}");
            Utilities.Write("      Tagged Stories: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.HistoryMakers.Stories.Tagged,8:N0}");
            Utilities.NewLine();
            Utilities.WriteLine("ScienceMakers", ConsoleColor.DarkYellow);
            Utilities.WriteLine("-------------", ConsoleColor.Gray);
            Utilities.Write("         Biographies: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.ScienceMakers.Biographies.All,8:N0}");
            Utilities.Write("             Stories: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.ScienceMakers.Stories.All,8:N0}");
            Utilities.Write("  Tagged Biographies: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.ScienceMakers.Biographies.Tagged,8:N0}");
            Utilities.Write("      Tagged Stories: ", ConsoleColor.Green); Utilities.WriteLine($"{corpusMetrics.ScienceMakers.Stories.Tagged,8:N0}");
            Utilities.NewLine();
            Utilities.NewLine();
        }

        /// <summary>
        /// Displays detailed information about the collection specified.
        /// </summary>
        internal void CollectionDetails(string accession)
        {
            if (DAE == null)
            {
                Utilities.WriteLine("Failed to open database, operation aborted.", ConsoleColor.Red);
                return;
            }

            var collectionID = DAE.GetCollectionID(accession);

            if (collectionID == DataAccess.NullID)
            {
                Utilities.WriteLine("Could not retrieve the collection specified.", ConsoleColor.Red);
                return;
            }

            CollectionDetails(collectionID);
        }

        /// <summary>
        /// Displays detailed information about the collection specified.
        /// </summary>
        internal void CollectionDetails(int collectionID)
        {
            if (DAE == null)
            {
                Utilities.WriteLine("Failed to open database, operation aborted.", ConsoleColor.Red);
                return;
            }

            Utilities.NewLine();
            Utilities.WriteLine("Collection Details", ConsoleColor.Yellow);
            Utilities.WriteLine(DOUBLELINE, ConsoleColor.Gray);

            Collection collection = DAE.GetCollection(collectionID);

            if (collection == null)
            {
                Utilities.WriteLine("Could not retrieve the collection specified.", ConsoleColor.Red);
            }
            else
            {
                Utilities.Write("       Accession: ", ConsoleColor.Green); Utilities.WriteLine($"{collection.Accession}");
                Utilities.Write("    CollectionID: ", ConsoleColor.Green); Utilities.WriteLine($"{collection.CollectionID}");
                Utilities.NewLine();
                Utilities.Write("        LastName: ", ConsoleColor.Green); Utilities.WriteLine($"{collection.LastName}");
                Utilities.Write("   PreferredName: ", ConsoleColor.Green); Utilities.WriteLine($"{collection.PreferredName}");
                Utilities.Write("       BirthDate: ", ConsoleColor.Green); Utilities.WriteLine($"{collection.BirthDate:MM/dd/yyy}");
                Utilities.Write("    DeceasedDate: ", ConsoleColor.Green); Utilities.WriteLine($"{collection.DeceasedDate:MM/dd/yyy}");
                Utilities.Write("          Gender: ", ConsoleColor.Green); Utilities.WriteLine($"{collection.Gender}");
                Utilities.WriteLine(SINGLELINE, ConsoleColor.Gray);
                Utilities.Write("  BiographyShort: ", ConsoleColor.Green); Wrap(collection.BiographyShort, 18, 80);
                Utilities.WriteLine(SINGLELINE, ConsoleColor.Gray);
                Utilities.Write("DescriptionShort: ", ConsoleColor.Green); Wrap(collection.DescriptionShort, 18, 80);
                Utilities.WriteLine(SINGLELINE, ConsoleColor.Gray);
            }
            
            Utilities.NewLine();
        }

        /// <summary>
        /// Displays detailed information about the collection specified.
        /// </summary>
        internal void SessionDetails(int sessionID)
        {
            if (DAE == null)
            {
                Utilities.WriteLine("Failed to open database, operation aborted.", ConsoleColor.Red);
                return;
            }

            Utilities.NewLine();
            Utilities.WriteLine("Session Details", ConsoleColor.Yellow);
            Utilities.WriteLine(DOUBLELINE, ConsoleColor.Gray);

            Session session = DAE.GetSession(sessionID);

            if (session == null)
            {
                Utilities.WriteLine("Could not retrieve the session specified.", ConsoleColor.Red);
            }
            else
            {
                Utilities.Write("  CollectionID: ", ConsoleColor.Green); Utilities.WriteLine($"{session.CollectionID}");
                Utilities.Write("  SessionOrder: ", ConsoleColor.Green); Utilities.WriteLine($"{session.SessionOrder}");
                Utilities.Write(" InterviewDate: ", ConsoleColor.Green); Utilities.WriteLine($"{session.InterviewDate:yyyy-MM-dd}");
                Utilities.Write("   Interviewer: ", ConsoleColor.Green); Utilities.WriteLine($"{session.Interviewer}");
                Utilities.Write("      Location: ", ConsoleColor.Green); Utilities.WriteLine($"{session.Location}");
            }

            Utilities.NewLine();
        }

        /// <summary>
        /// Displays detailed information about the movie specified.
        /// </summary>
        internal void MovieDetails(int movieID)
        {
            if (DAE == null)
            {
                Utilities.WriteLine("Failed to open database, operation aborted.", ConsoleColor.Red);
                return;
            }

            Utilities.NewLine();
            Utilities.WriteLine("Movie Details", ConsoleColor.Yellow);
            Utilities.WriteLine(DOUBLELINE, ConsoleColor.Gray);

            Movie movie = DAE.GetMovie(movieID);

            if (movie == null)
            {
                Utilities.WriteLine("Could not retrieve the movie specified.");
            }
            else
            {
                Utilities.Write("    MovieName: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.MovieName}");
                Utilities.Write("      MovieID: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.MovieID}");
                Utilities.Write("    SessionID: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.SessionID}");                
                Utilities.Write(" CollectionID: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.CollectionID}");
                Utilities.Write("     Duration: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.Duration,6}ms ({MStoHMS(movie.Duration),5}");                
                Utilities.Write("          FPS: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.FPS}");
                Utilities.Write("        Width: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.Width}");
                Utilities.Write("       Height: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.Height}");
                Utilities.Write("     FileType: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.FileType}");
                Utilities.Write("     FileType: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.MediaPath}");
                Utilities.Write("         Tape: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.Tape}");
                Utilities.Write("      Created: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.Created:yyyy-MM-dd hh:mm:ss}");
                Utilities.Write("     Modified: ", ConsoleColor.Green); Utilities.WriteLine($"{movie.Modified:yyyy-MM-dd hh:mm:ss}");
                Utilities.WriteLine(SINGLELINE, ConsoleColor.Gray);
                Utilities.Write("     Abstract: ", ConsoleColor.Green); Wrap(movie.Abstract, 15, 80);
                Utilities.WriteLine(SINGLELINE, ConsoleColor.Gray);
            }

            Utilities.NewLine();
        }

        /// <summary>
        /// Displays detailed information about the segment specified.
        /// </summary>
        internal void SegmentDetails(int segmentID)
        {
            if (DAE == null)
            {
                Utilities.WriteLine("Failed to open database, operation aborted.", ConsoleColor.Red);
                return;
            }

            Utilities.NewLine();
            Utilities.WriteLine("Segment Details", ConsoleColor.Yellow);
            Utilities.WriteLine(DOUBLELINE, ConsoleColor.Gray);

            Segment segment = DAE.GetSegment(segmentID);            

            if (segment == null)
            {
                Utilities.WriteLine("Could not retrieve the segment specified.", ConsoleColor.Red);
            }
            else
            {
                Utilities.Write("  SegmentName: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.SegmentName}");
                Utilities.Write("    SegmentID: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.SegmentID}");
                Utilities.Write("      MovieID: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.MovieID}");
                Utilities.Write("        Title: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.Title}");
                Utilities.Write("    StartTime: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.StartTime,6}ms ({MStoHMS(segment.StartTime),5})");
                Utilities.Write("      EndTime: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.EndTime,6}ms ({MStoHMS(segment.EndTime),5})");
                Utilities.Write("     Duration: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.Duration,6}ms ({MStoHMS((int)(segment.Duration ?? 0)),5})");
                Utilities.Write("          FPS: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.FPS}");
                Utilities.Write("        Width: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.Width}");
                Utilities.Write("       Height: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.Height}");
                Utilities.Write("    MediaPath: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.MediaPath}");
                Utilities.Write(" SegmentOrder: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.SegmentOrder}");
                Utilities.Write("  PrevSegment: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.PrevSegmentID}");
                Utilities.Write("  NextSegment: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.NextSegmentID}");
                Utilities.Write("        Ready: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.Ready}");
                Utilities.Write("      Created: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.Created:yyyy-MM-dd hh:mm:ss}");
                Utilities.Write("     Modified: ", ConsoleColor.Green); Utilities.WriteLine($"{segment.Modified:yyyy-MM-dd hh:mm:ss}");
                Utilities.Write("     Abstract: ", ConsoleColor.Green); Wrap(segment.Abstract, 15, 80);
                Utilities.WriteLine(SINGLELINE, ConsoleColor.Gray);
                Utilities.Write("   Transcript: ", ConsoleColor.Green); Wrap(segment.TranscriptText, 15, 80);
                Utilities.WriteLine(SINGLELINE, ConsoleColor.Gray);
            }

            Utilities.NewLine();
        }

        /// <summary>
        /// Processing status per segment
        /// </summary>
        internal void ProcessingDetails(ProcessFilter resultsFilter)
        {
            if (DAE == null)
            {
                Utilities.WriteLine("Failed to open database, operation aborted.", ConsoleColor.Red);
                return;
            }

            // Get all the segments and their states
            List<SegmentProcessingState> results = DAE.GetSegmentProcessingStateAll();

            // Filter the result set
            List<SegmentProcessingState> filteredResults;

            string filter = "";
            switch (resultsFilter)
            {
                case ProcessFilter.All:
                    filter = "Results filtered by: All";
                    filteredResults = results;
                    break;
                case ProcessFilter.Completed:
                    filter = "Results filtered by: Completed";
                    filteredResults = results.Where(r => r.TaskState.All(t => t.Value == 'C')).ToList();
                    break;
                case ProcessFilter.Failed:
                    filter = "Results filtered by: Failed";
                    filteredResults = results.Where(r => r.TaskState.Any(t => t.Value == 'F')).ToList();
                    break;
                case ProcessFilter.Pending:
                    filter = "Results filtered by: Pending";
                    filteredResults = results.Where(r => r.TaskState.Any(t => t.Value == 'P')).ToList();
                    break;
                case ProcessFilter.Running:
                    filter = "Results filtered by: Running";
                    filteredResults = results.Where(r => r.TaskState.Any(t => t.Value == 'R')).ToList();
                    break;
                default:
                    filter = "Unknown filter specified.";
                    return;
            }


            Utilities.NewLine();
            Utilities.WriteLine($"Processing Status Per Segment - {filter}", ConsoleColor.Yellow);

            // Display the filtered set
            string accession = "";
            foreach (var result in filteredResults)
            {
                if (accession != result.SegmentName.Substring(0, 9))
                {
                    accession = result.SegmentName.Substring(0, 9);
                    Utilities.WriteLine(DOUBLELINE, ConsoleColor.Gray);
                    Utilities.WriteLine($"Accession: {accession.Replace('_', '.')}", ConsoleColor.DarkYellow);
                    Utilities.WriteLine("SegmentID  SegmentName               vid key aln cap nlp ner ent", ConsoleColor.DarkYellow);
                    Utilities.WriteLine("---------  ------------------------  --- --- --- --- --- --- ---", ConsoleColor.Gray);
                }

                Utilities.Write($"{result.SegmentID,9:G0}  {Shorten(result.SegmentName, 24),-24}  ");
                foreach (var name in TaskNames)
                {
                   switch (result.TaskState[name])
                    {
                        case 'P' :
                            Utilities.Write(" P  ", ConsoleColor.Blue);
                            break;
                        case 'R':
                            Utilities.Write(" R  ", ConsoleColor.Yellow);
                            break;
                        case 'C':
                            Utilities.Write(" C  ", ConsoleColor.Green);
                            break;
                        case 'F':
                            Utilities.Write(" F  ", ConsoleColor.Red);
                            break;
                    }
                }
                Utilities.NewLine();
            }

            // Summary
            Utilities.WriteLine(DOUBLELINE, ConsoleColor.Gray);
            Utilities.WriteLine($"{filteredResults.Count:0,0} out of {results.Count:0,0} segments matched \"{resultsFilter}\" criteria.");


            // Legend
            Utilities.NewLine();
            Utilities.WriteLine("Explanation of Status Codes", ConsoleColor.DarkYellow);
            Utilities.WriteLine("----------------------------------------------------------------------", ConsoleColor.Gray);
            Utilities.WriteLine("  vid - TranscodingTask      : generates web-ready video");
            Utilities.WriteLine("  key - KeyFrameTask         : extracts key frame from video");
            Utilities.WriteLine("  aln - AlignmentTask        : aligns transcript to video");
            Utilities.WriteLine("  cap - CaptioningTask       : generates closed captions");
            Utilities.WriteLine("  nlp - SpacyTask            : runs spaCy natural language processor");
            Utilities.WriteLine("  ner - StanfordTask         : runs Stanford named entity recognizer");
            Utilities.WriteLine("  ent - EntityResolutionTask : turn named entities into useful facets");
            Utilities.NewLine();
            Utilities.WriteLine("    P - Pending processing", ConsoleColor.Blue);
            Utilities.WriteLine("    R - Running processing", ConsoleColor.Yellow);
            Utilities.WriteLine("    C - Completed processing ", ConsoleColor.Green);            
            Utilities.WriteLine("    F - Failed processing", ConsoleColor.Red);
            
            Utilities.NewLine();
            Utilities.NewLine();
        }

        /// <summary>
        /// Shortens (truncates) a string if greater than length.
        /// </summary>
        /// <param name="source">String to be shortened.</param>
        /// <param name="length">Length of shortened string.</param>
        /// <returns></returns>
        internal string Shorten(string source, int length)
        {
            if (source.Length > length)
            {
                source = source.Substring(0, length);
            }
            return source;
        }

        /// <summary>
        /// Outputs the given string as a series of lines of specfied
        /// maximum length. First line is output unindented, subsequent
        /// lines have indent added.
        /// </summary>
        /// <param name="text">The paragraph text to be output to console.</param>
        /// <param name="indent">Number of spaces to indent lines.</param>
        /// <param name="maxLength">Maximum length of the line including padding.</param>
        internal void Wrap(string text, int indent, int maxLength)
        {
            var pad = new string(' ', indent);
            var words = text.Split(' ');

            bool addPad = false;
            string line = "";

            foreach(var word in words)
            {
                if (indent + line.Length + word.Length < maxLength)
                {
                    line += word + " ";
                }
                else
                {
                    if (addPad)
                    {
                        Utilities.WriteLine(pad + line);
                    }
                    else
                    {
                        Utilities.WriteLine(line);
                        addPad = true;
                    }
                    line = word + " ";
                }
            }

            if (addPad)
            {
                Utilities.WriteLine(pad + line);
            }
            else
            {
                Utilities.WriteLine(line);
            }
        }

        /// <summary>
        /// Reports 
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="resultsFilter"></param>
        /// <remarks>
        /// TODO: Add video duration check
        /// </remarks>
        internal void WebVideoFileCheck(string rootPath, VideoReportFilter resultsFilter)
        {
            if (rootPath == String.Empty)
            {
                // Path was not specified so derive it from the configuration file
                rootPath = Path.Combine(Settings.Current.BuildPath, "WebVideo");
            }

            if (!Directory.Exists(rootPath))
            {
                Logger.Write("Could not find root path specified: {0}", rootPath);
                return;
            }

            // Traverse the folder and add each video to a dictionary keyed on filename.
            //  Dictionary(filename) = full_path_to_video
            Logger.Write("Searching for videos under {0}...", rootPath);

            string[] videos = Directory.GetFiles(rootPath, "*.mp4", SearchOption.AllDirectories);

            Logger.Write("...{0} videos found.", videos.Length);

            // Build a dictionary of paths keyed on the filename.
            Logger.Write("Building dictionary...");
            
            Dictionary<string, string> index = new Dictionary<string, string>();
            
            foreach(string video in videos)
            {
                string key = Path.GetFileName(video).ToLower();
                string val = video;
                index.Add(key, val);
            }
            
            Logger.Write("...dictionary ready.");

            // Get all the segments in the database
            Logger.Write("Retreiving segment media paths from database...");

            List<SegmentMediaPath> segmentMediaPaths = DAE.GetSegmentMediaPaths();

            Logger.Write("...{0} media paths retrieved.", segmentMediaPaths.Count);

            // Traverse the segments looking for matches
            Logger.Write("Starting comparison operation...");

            // Initialize lists to contain results
            var matched = new Dictionary<string, string>();
            var found = new Dictionary<string, string>();
            var missing = new Dictionary<string, string>();

            foreach (SegmentMediaPath segmentMediaPath in segmentMediaPaths)
            {
                // Extract just the filename from the path.
                string filename = (segmentMediaPath.MediaPath != null) ? Path.GetFileName(segmentMediaPath.MediaPath).ToLower() : string.Empty;

                if (index.ContainsKey(filename))
                {
                    if (File.Exists(segmentMediaPath.MediaPath))
                    {
                        // Exact match to path specified in database
                        matched.Add(segmentMediaPath.SegmentID.ToString(), index[filename]);
                        if (resultsFilter >= VideoReportFilter.ShowAll)
                            Logger.Write("Matched: SegmentID({0}) => Filename={1}", segmentMediaPath.SegmentID, filename);
                    }
                    else
                    {
                        // File was found in a path different than expected
                        found.Add(segmentMediaPath.SegmentID.ToString(), index[filename]);
                        if (resultsFilter >= VideoReportFilter.MissingAndFound)
                            Logger.Warning("  Found: SegmentID({0}) => Filename={1}", segmentMediaPath.SegmentID, filename);
                    }

                    // Remove matched file from dictionary, items remaining
                    // at the end of the search are unreferenced videos.
                    index.Remove(filename);
                }
                else
                {
                    // The file could not be found
                    missing.Add(segmentMediaPath.SegmentID.ToString(), segmentMediaPath.MediaPath);
                    if (resultsFilter >= VideoReportFilter.MissingOnly)
                        Logger.Warning("Missing: SegmentID({0}) => Filename={1}", segmentMediaPath.SegmentID, filename);
                }
            }

            // The remaining items in index were found on disk but not referenced by the database.
            var reportDir = Path.Combine(Directory.GetCurrentDirectory(), "reports");
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            WriteToFile(index, Path.Combine(reportDir, "files_unreferenced.txt"));
            WriteToFile(matched, Path.Combine(reportDir, "files_matched.txt"));
            WriteToFile(found, Path.Combine(reportDir, "files_found.txt")); 
            WriteToFile(missing, Path.Combine(reportDir, "files_missing.txt")); 

            // Display the final report
            Logger.Write("==============================================================================");
            Logger.Write("{0} out of {1} files found in the exact location specifed by the database.", matched.Count, segmentMediaPaths.Count);
            Logger.Write("{0} out of {1} files found in a location other than specified by the database.", found.Count, segmentMediaPaths.Count);
            Logger.Write("{0} out of {1} files are missing!", missing.Count, segmentMediaPaths.Count);
            Logger.Write("----------");
            Logger.Write("{0} out of {1} videos found in {2} are not referenced by the current database.", index.Count, videos.Length, rootPath);
            Logger.Write("==============================================================================");
        }

        /// <summary>
        /// Converts the given time in milliseconds to HH:MM:SS format.
        /// </summary>
        /// <param name="duration">Time in milliseconds.</param>
        /// <returns>An HH:MM:SS formatted string.</returns>
        internal string MStoHMS(double duration)
        {
            // Convert to seconds for simplicity
            double seconds = duration / 1000;

            // Calculate hours
            int hours = (int)(seconds / 3600);
            seconds -= hours * 3600;

            // Calculate minutes
            int minutes = (int)(seconds / 60);
            seconds -= minutes * 60;

            return String.Format("{0:000}:{1:00}:{2:00}", hours, minutes, seconds);
        }

        /// <summary>
        /// Writes the contents of a dictionary to a plain text file suitable for batch processing.
        /// </summary>
        /// <param name="dictionary">The dictionary of filenames and fully qualified paths.</param>
        /// <param name="filename">The fully qualified filename to write the report to.</param>
        internal void WriteToFile(Dictionary<string,string> dictionary, string filename)
        {
            Logger.Write("Writing data to {0}.", filename);

            using (var file = new StreamWriter(filename))
            {
                foreach(var item in dictionary)
                {
                    file.WriteLine(String.Format("{0},{1}", item.Key, item.Value));
                }
            }
        }
    }
}
