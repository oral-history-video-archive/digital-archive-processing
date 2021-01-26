
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace InformediaCORE.Processing.Database
{
    #region ===== Public Structs
    /// <summary>
    /// Return type of the GetDatabaseMetrics method.
    /// </summary>
    /// <see cref="DataAccessExtended.GetTableMetrics()"/>
    public struct TableMetrics
    {
        public int Collections;
        public int Movies;
        public int Segments;
    }

    /// <summary>
    /// Return type of the GetMediaStats method.
    /// </summary>
    /// <see cref="DataAccessExtended.GetMediaMetrics()"/>
    public struct MediaMetrics
    {
        public long ProcessedContent;
        public long ShortestSegment;
        public long LongestSegment;
        public long AverageSegment;
    }

    /// <summary>
    /// Return type of the GetSegmentProcessStats method.
    /// </summary>
    /// <see cref="DataAccessExtended.GetProcessingMetrics()"/>
    public struct ProcessingMetrics
    {
        public int Completed;
        public int Pending;
        public int Running;
        public int Failed;
    }

    /// <summary>
    /// Return type of the GetSegmentProcessingStateAll method.
    /// </summary>
    /// <see cref="DataAccessExtended.GetSegmentProcessingStateAll()"/>
    public struct SegmentProcessingState
    {
        /// <summary>
        /// The numeric id of the segment
        /// </summary>
        public int SegmentID { get; }

        /// <summary>
        /// The name of the segment
        /// </summary>
        public string SegmentName { get; }

        /// <summary>
        /// A dictionary containing the state of each task keyed on name.
        /// </summary>
        public Dictionary<string, char> TaskState { get; }

        /// <summary>
        /// Initialize and instance of the SegmentProcessingState class with the given values.
        /// </summary>
        /// <param name="segmentID">SegmentID</param>
        /// <param name="segmentName">SegmentName</param>
        /// <param name="taskStates">A dictionary containing the state of each task keyed on name.</param>
        public SegmentProcessingState(int segmentID, string segmentName, Dictionary<string, char> taskStates)
        {
            SegmentID = segmentID;
            SegmentName = segmentName;
            TaskState = taskStates;
        }
    }

    /// <summary>
    /// Return type of the GetSegmentMediaPaths method.
    /// </summary>
    public struct SegmentMediaPath
    {
        /// <summary>
        /// The numeric id of the segment.
        /// </summary>
        public int SegmentID;

        /// <summary>
        /// The path to the segment'segmentID media file.
        /// </summary>
        public string MediaPath;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="segmentID">Sets the value of SegmentID</param>
        /// <param name="mediaPath">Sets the value of MediaPath</param>
        public SegmentMediaPath(int segmentID, string mediaPath)
        {
            SegmentID = segmentID;
            MediaPath = mediaPath;
        }
    }
    #endregion == Public Structs

    /// <summary>
    /// Extends the InformediaCORE.Common.Database.DataAccess class with processing specific functionality.
    /// </summary>
    /// <remarks>
    /// History:
    /// 2013-Mar-12: bm3n
    ///     Fixed bug in DeleteSegmentVideo which caused fatal runtime exception if 
    ///     storage device or directory was invalid.
    /// 2013-Mar-08: bm3n
    ///     Added ability to delete segment video and empty directories to DeleteSegment.
    /// </remarks>
    public class DataAccessExtended : DataAccess
    {
        /// <summary>
        /// A list of known processing task names in alphabetical order.
        /// </summary>
        /// <remarks>
        /// Based on StackOverflow post: Get all inherited classes of an abstract class
        /// https://stackoverflow.com/questions/5411694/get-all-inherited-classes-of-an-abstract-class
        /// </remarks>
        public static readonly List<string> KnownTaskNames =
            typeof(Tasks.AbstractTask).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Tasks.AbstractTask)) && !t.IsAbstract)
            .Select(t => t.Name).OrderBy(t => t).ToList();

        #region ===== Collection Methods

        /// <summary>
        /// Get the Collection corresponding to the specified collection ID.
        /// </summary>
        /// <param name="collectionID">The unique ID of the desired collection.</param>
        /// <returns>A Collection object on success, null on failure.</returns>
        public Collection GetCollection(int collectionID)
        {
            Collection collection;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    collection = (from c in context.Collections
                                  where c.CollectionID == collectionID
                                  select c).FirstOrDefault();
                }
                catch
                {
                    collection = null;
                }
            }

            return collection;
        }

        /// <summary>
        /// Commit changes for the given collection to the database.
        /// </summary>
        /// <param name="collection">The collection to update.</param>
        /// <remarks>True on success, false otherwise.</remarks>
        public bool UpdateCollection(Collection collection)
        {
            using (var context = GetTrackingDataContext())
            {
                try
                {
                    // Get a copy of the current state
                    var original = GetCollection(collection.CollectionID);

                    // Submit the changes.
                    context.Collections.Attach(collection, original);
                    context.SubmitChanges();

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion == Collections Table Extensions

        #region ===== Segment Methods
        /// <summary>
        /// Updates the PrevID and NextID values for each segment within the specified collection.
        /// </summary>
        /// <param name="collectionID">The database identifier of a valid session.</param>
        public void LinkSegments(int collectionID)
        {
            using (var context = GetTrackingDataContext())
            {
                // Get all the segments in the 
                var segments = (from s in context.Segments
                                where s.CollectionID == collectionID
                                orderby s.Session.SessionOrder, s.Movie.Tape, s.SegmentOrder
                                select s).ToArray();

                // Now chain them together
                const int first = 0;
                var last = segments.Length - 1;

                for (var i = first; i <= last; i++)
                {
                    // Only one segment, no previous or next
                    if (first == last)
                    {
                        segments[i].PrevSegmentID = 0;
                        segments[i].NextSegmentID = 0;
                    }
                    // First segment, no previous.
                    else if (i == first)
                    {
                        segments[i].PrevSegmentID = 0;
                        segments[i].NextSegmentID = segments[i + 1].SegmentID;
                    }
                    // Last segment, no next.
                    else if (i == last)
                    {
                        segments[i].PrevSegmentID = segments[i - 1].SegmentID;
                        segments[i].NextSegmentID = 0;
                    }
                    // The middle segments
                    else
                    {
                        segments[i].PrevSegmentID = segments[i - 1].SegmentID;
                        segments[i].NextSegmentID = segments[i + 1].SegmentID;
                    }
                }

                // Update the database
                context.SubmitChanges();
            }
        }
        #endregion == Segment Table Extensions

        #region ===== Table Reporting Methods     

        /// <summary>
        /// Get the Session corresponding to the specified session ID.
        /// </summary>
        /// <param name="sessionID">The unique ID of the desired session.</param>
        /// <returns>A Session object on success, null on failure.</returns>
        public Session GetSession(int sessionID)
        {
            Session session;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    session = (from s in context.Sessions
                               where s.SessionID == sessionID
                               select s).FirstOrDefault();
                }
                catch
                {
                    session = null;
                }
            }

            return session;
        }

        /// <summary>
        /// Returns the number of collections, movies, and segments
        /// in the current database.
        /// </summary>
        /// <returns>A DatabaseMetrics structure containing the individual row counts.</returns>
        /// <see cref="TableMetrics"/>
        public TableMetrics GetTableMetrics()
        {
            var counts = new TableMetrics();

            using (var context = GetNonTrackingDataContext())
            {
                counts.Collections = (from i in context.Collections select i).Count();
                counts.Movies = (from i in context.Movies select i).Count();
                counts.Segments = (from i in context.Segments select i).Count();
            }

            return counts;
        }

        /// <summary>
        /// Returns summary information regarding total content duration, as well
        /// as the shortest, longest, and average segment durations.
        /// </summary>
        /// <returns>A MediaMetrics structure with derived duration information.</returns>
        /// <see cref="MediaMetrics"/>
        public MediaMetrics GetMediaMetrics()
        {
            var stats = new MediaMetrics();

            using (var context = GetNonTrackingDataContext())
            {
                var durations = (from s in context.Segments where s.Ready == 'Y' select (long)(s.Duration ?? 0));

                if (!durations.Any()) return stats;

                stats.ProcessedContent = durations.Sum();
                stats.ShortestSegment = durations.Min();
                stats.LongestSegment = durations.Max();
                stats.AverageSegment = (long)durations.Average();
            }

            return stats;
        }

        /// <summary>
        /// Returns summary information regarding number of segments in each of the four
        /// possible processing states.
        /// </summary>
        /// <returns>A ProcessingMetrics object populated with the calculated information.</returns>
        public ProcessingMetrics GetProcessingMetrics()
        {
            var pStats = GetSegmentProcessingStateAll();

            var metrics = new ProcessingMetrics
            {
                Completed =
                    pStats.Where(p => p.TaskState.All(t => t.Value == 'C')).Count(),
                Failed =
                    pStats.Where(p => p.TaskState.Any(t => t.Value == 'F')).Count(),
                Pending =
                    pStats.Where(p => p.TaskState.Any(t => t.Value == 'P')).Count(),
                Running =
                    pStats.Where(p => p.TaskState.Any(t => t.Value == 'R')).Count()
            };

            return metrics;
        }

        /// <summary>
        /// Returns all the segment media paths for the current database.
        /// </summary>
        /// <returns>A list populated with valid SegmentMediaPath items.</returns>
        public List<SegmentMediaPath> GetSegmentMediaPaths()
        {
            using (var context = GetNonTrackingDataContext())
            {
                return (from s in context.Segments
                        select new SegmentMediaPath(s.SegmentID, s.MediaPath)).ToList();
            }
        }

        #endregion == Table Reporting Methods

        #region ===== Process Control Methods

        /// <summary>
        /// Returns a complete list of all segments and their associated processing states.
        /// </summary>
        /// <returns>A list of SegmentProcessingState instances for each segment in the database.</returns>
        public List<SegmentProcessingState> GetSegmentProcessingStateAll()
        {
            var results = new List<SegmentProcessingState>();

            using (var context = GetNonTrackingDataContext())
            {
                var segments = from s in context.Segments
                               join t in context.TaskStates on s.SegmentID equals t.SegmentID into tasks
                               orderby s.CollectionID, s.SegmentID
                               select new { s.SegmentID, s.SegmentName, Tasks = tasks.DefaultIfEmpty() };

                foreach (var segment in segments)
                {
                    var taskStates = new Dictionary<string, char>();
                    foreach (var name in KnownTaskNames)
                    {
                        taskStates.Add(name, GetTaskState(segment.Tasks, name));
                    }

                    results.Add(
                        new SegmentProcessingState(
                            segment.SegmentID,
                            segment.SegmentName,
                            taskStates
                        ));
                }
            }

            return results;
        }

        /// <summary>
        /// Get the next available segment for processing.
        /// </summary>
        /// <returns>A segment instance upon success, null otherwise.</returns>
        /// <param name="includeFailures">
        /// If true, all unprocessed segments are considered viable candidates for next segment;
        /// If false, segments which have failure flags in state table will be ignored.
        /// </param>
        /// <remarks>
        /// By default only segments with no prior failures and no semaphore locks are considered candidates
        /// for next segment.  This is designed to reduce the possibility of getting into an infinite
        /// loop state as was previously the case.  
        /// </remarks>
        public Segment GetNextSegmentToProcess(bool includeFailures)
        {
            // Assume failure
            Segment nextSegment = null;

            using (var context = GetNonTrackingDataContext())
            {
                var nextID = NullID;
                if (includeFailures)
                {
                    // This is the old way, where any incomplete segment is a candidate
                    var candidates = (from s in context.Segments
                                      where s.Ready != 'Y'
                                      select s.SegmentID).ToList();

                    // Get the first segment id which does not have a semaphore lock
                    nextID = candidates.Except(from s in context.Semaphores select s.SegmentID).FirstOrDefault();
                }
                else
                {
                    // New selection method which includes the state of prior processing on the segment.
                    var candidates = (from s in context.Segments
                                      join t in context.TaskStates on s.SegmentID equals t.SegmentID into tasks
                                      where s.Ready != 'Y'
                                      select new {s.SegmentID, tasks});


                    // Now filter the canditates on two criteria:
                    // 1. None of their processes have previously failed.
                    // 2. The segment does not have a processing semaphore lock on it.
                    nextID = (from c in candidates
                              where c.tasks.All(t => t.State != (char)TaskStateValue.Failed)
                              select c.SegmentID).Except(from s in context.Semaphores select s.SegmentID).FirstOrDefault();
                }

                // If the id is non-zero then fetch the specified segment.
                if (nextID != NullID)
                    nextSegment = GetSegment(nextID);
            }

            return nextSegment;
        }

        #endregion == Process Control Methods

        #region ===== Object Deletion Methods

        /// <summary>
        /// Performs a cascade deletion of the specified collection from the database.
        /// </summary>
        /// <param name="accession">The canonical name of the collection to be deleted.</param>
        public void DeleteCollection(string accession)
        {
            using (var context = GetTrackingDataContext())
            {
                // Find the collection specified by the given name
                var collection = (from c in context.Collections
                                  where c.Accession == accession
                                  select c).FirstOrDefault();

                if (collection == null)
                {
                    Logger.Warning("Collection {0} could not be found. No action taken.", accession);
                }
                else
                {
                    DeleteSessions(collection.CollectionID);

                    // Delete the Collection
                    context.Collections.DeleteOnSubmit(collection);
                    context.SubmitChanges();

                    Logger.Write("Collection {0} deleted.", accession);
                }
            }
        }

        /// <summary>
        /// Performs a cascade deletion of all sessions related to the given collection.
        /// </summary>
        /// <param name="collectionID">The database id of the parent collection.</param>
        private void DeleteSessions(int collectionID)
        {
            using (var context = GetTrackingDataContext())
            {
                var sessions = (from s in context.Sessions
                                where s.CollectionID == collectionID
                                orderby s.SessionOrder
                                select s);

                if (sessions == null)
                {
                    Logger.Warning("No sessions found related to Collection({0})", collectionID);
                    return;
                }

                foreach (var session in sessions)
                {
                    Logger.Write("Deleting Collection({0}), Session #{1}...", collectionID, session.SessionOrder);
                    DeleteMovies(session.SessionID);
                    context.Sessions.DeleteOnSubmit(session);
                }

                context.SubmitChanges();
            }
        }

        /// <summary>
        /// Performs a cascade deletion of all movies related to the given session.
        /// </summary>
        /// <param name="sessionID">Database id of the parent session.</param>
        private void DeleteMovies(int sessionID)
        {
            using (var context = GetTrackingDataContext())
            {
                var movies = (from m in context.Movies
                              where m.SessionID == sessionID
                              orderby m.Tape
                              select m);

                if (movies == null)
                {
                    Logger.Warning("No movies found related to Session({0})", sessionID);
                    return;
                }

                foreach (var movie in movies)
                {
                    Logger.Write("Deleting Movie {0}...", movie.MovieName);
                    DeleteSegments(movie.MovieID);
                    context.Movies.DeleteOnSubmit(movie);
                }

                context.SubmitChanges();
            }
        }

        /// <summary>
        /// Performs a cascade deletion of all segments related to the given movie.
        /// </summary>
        /// <param name="movieID">Database id of the parent movie.</param>
        private void DeleteSegments(int movieID)
        {
            using (var context = GetTrackingDataContext())
            {
                // Find the segment specified by the given SegmentName
                var segments = (from s in context.Segments
                                where s.MovieID == movieID
                                orderby s.SegmentOrder
                                select s);

                if (segments == null)
                {
                    Logger.Warning("No segments found related to Movie({0})", movieID);
                    return;
                }

                foreach (var segment in segments)
                {
                    Logger.Write("Deleting Segment {0}...", segment.SegmentName);
                    DeleteSegmentMedia(segment);
                    DeleteSegmentData(segment);

                    // Delete the segment
                    context.Segments.DeleteOnSubmit(segment);
                }

                context.SubmitChanges();
            }
        }

        /// <summary>
        /// Delete all generated media files related to the given segment.
        /// </summary>
        /// <param name="segment">The segment whose media is to be deleted.</param>
        private void DeleteSegmentMedia(Segment segment)
        {
            var basename = Path.GetFileNameWithoutExtension(segment.MediaPath);
            var buildPath = Path.GetDirectoryName(segment.MediaPath);

            if (Directory.Exists(buildPath))
            {
                // Delete all files related to this segment
                foreach (var file in Directory.EnumerateFiles(buildPath, $"{basename}.*"))
                {
                    File.Delete(file);
                    Logger.Write("Deleted Segment media file: {0}", file);
                }

                // Determine if the root directory has any entries, if not, delete it
                if (!Directory.EnumerateFileSystemEntries(buildPath).Any())
                {
                    Directory.Delete(buildPath);
                    Logger.Write("Deleted empty build directory: {0}", buildPath);
                }
            }
        }

        /// <summary>
        /// Delete all ephemeral data files related to the given segment.
        /// </summary>
        /// <param name="segment">The segment whose data is to be deleted.</param>
        private void DeleteSegmentData(Segment segment)
        {
            var rootDataPath = Path.Combine(Settings.Current.BuildPath, "Data", segment.CollectionID.ToString());

            if (Directory.Exists(rootDataPath))
            {
                var segDataPath = Path.Combine(rootDataPath, segment.SegmentID.ToString());

                if (Directory.Exists(segDataPath))
                {
                    Directory.Delete(segDataPath, true);
                    Logger.Write("Deleted data directory: {0}", segDataPath);
                }

                if (!Directory.EnumerateFileSystemEntries(rootDataPath).Any())
                {
                    Directory.Delete(rootDataPath);
                    Logger.Write("Deleted empty data directory: {0}", rootDataPath);
                }
            }
        }

        #endregion == Object Deletion Methods

        #region ===== Semaphore Methods

        /// <summary>
        /// Gets the semaphore object specified by the given criteria.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="pid">The process id of the requesting process.</param>
        /// <param name="hostname">The hostname where the requesting process is running.</param>
        /// <returns>The Semaphore object retrieved from the database.</returns>
        public Semaphore GetSemaphore(int segmentID, int pid, string hostname)
        {
            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    var semaphore = (from s in context.Semaphores
                                     where s.SegmentID == segmentID && s.PID == pid && s.Hostname == hostname
                                     select s).FirstOrDefault();

                    return semaphore;
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Attempts to create a semaphore for the segment specified by the given criteria.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="pid">The process id of the requesting process.</param>
        /// <param name="hostname">The hostname where the requesting process is running.</param>
        /// <returns>The Semaphore object inserted into the database.</returns>
        public Semaphore InsertSemaphore(int segmentID, int pid, string hostname)
        {
            var semaphore = GetSemaphore(segmentID, pid, hostname);

            // If the semaphore already exists, return that one.
            if (semaphore != null)
                return semaphore;

            // It doesn't exist so try to create it.
            using (var context = GetTrackingDataContext())
            {
                try
                {
                    semaphore = new Semaphore
                    {
                        SegmentID = segmentID,
                        PID = pid,
                        Hostname = hostname
                    };

                    // Attempt to insert it.
                    context.Semaphores.InsertOnSubmit(semaphore);
                    context.SubmitChanges();

                    // Return the new semaphore.
                    return semaphore;
                }
                catch (SqlException)
                {
                    // We can assume that a SqlException was generated
                    // due to a failure to insert the semaphore, no
                    // log report necessary.
                    return null;
                }
                catch (Exception ex)
                {
                    // This is an unexpected error which we should report.
                    Logger.Exception(ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Attempts to delete the semaphore for the given segment id.
        /// </summary>
        /// <param name="segmentID">A valid Segment ID.</param>
        /// <param name="pid">The process ID</param>
        /// <param name="hostname">The hostname with the lock.</param>
        public void DeleteSemaphore(int segmentID, int pid, string hostname)
        {
            using (var context = GetTrackingDataContext())
            {
                try
                {
                    var semaphore = (from s in context.Semaphores
                                     where s.SegmentID == segmentID && s.PID == pid && s.Hostname == hostname
                                     select s).FirstOrDefault();

                    if (semaphore == null) return;

                    context.Semaphores.DeleteOnSubmit(semaphore);
                    context.SubmitChanges();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                }
            }
        }

        #endregion == Semaphores Table Methods

        #region ===== TaskState Methods

        /// <summary>
        /// Gets the state of the task defined by the given segment id and task name.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="taskName">A valid task name.</param>
        /// <returns>The TaskStateValue.</returns>       
        public TaskStateValue GetTaskState(int segmentID, string taskName)
        {
            TaskStateValue state;

            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    // Attempt to retreive the process state
                    var temp = (from t in context.TaskStates
                                where t.SegmentID == segmentID && t.Name == taskName
                                select t.State).FirstOrDefault();

                    // If null then create an initial record for the task
                    if (temp == null)
                    {
                        state = TaskStateValue.Pending;
                        UpdateTaskState(segmentID, taskName, state);
                    }
                    else
                    {
                        state = (TaskStateValue)temp;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    state = TaskStateValue.Unknown;
                }
            }

            return state;
        }

        /// <summary>
        /// Get's the status flag corresponding to the given task name within the given list.
        /// </summary>
        /// <param name="tasks">A list of tasks associated with a given segment.</param>
        /// <param name="taskName">The name of a processing task to be queried.</param>
        /// <returns>A character representing the processing state if taskName exists; 'P' otherwise.</returns>
        private static char GetTaskState(IEnumerable<TaskState> tasks, string taskName)
        {
            if (tasks == null) return 'P';
            var task = tasks.FirstOrDefault(t => t != null && t.Name == taskName);
            return task == null ? 'P' : (task.State ?? 'P');
        }

        /// <summary>
        /// Retrieve the state of all valid tasks associated with given segment.
        /// </summary>
        /// <param name="segmentID">Numeric segment identifier.</param>
        /// <returns>A dictionary of task states keyed on task name upon success; null otherwise.</returns>
        public Dictionary<string, char> GetTaskStates(int segmentID)
        {
            using (var context = GetNonTrackingDataContext())
            {
                try
                {
                    var taskStates = from t in context.TaskStates
                                     where t.SegmentID == segmentID
                                     select t;

                    var results = new Dictionary<string, char>();
                    foreach(var name in KnownTaskNames)
                    {
                        var taskState = taskStates.Where(t => t.Name == name).SingleOrDefault();
                        results.Add(name, taskState?.State ?? (char)TaskStateValue.Pending);
                    }

                    return results;
                }
                catch (SqlException ex)
                {
                    Logger.Exception(ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Updates the state of the task defined by the given segment id and task name.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="taskName">A valide task name.</param>
        /// <param name="state">The TaskStateValue to be written to the database.</param>
        public void UpdateTaskState(int segmentID, string taskName, TaskStateValue state)
        {
            using (var context = GetTrackingDataContext())
            {
                try
                {
                    var ts = (from t in context.TaskStates
                              where t.SegmentID == segmentID && t.Name == taskName
                              select t).FirstOrDefault();

                    if (ts == null)
                    {
                        // Record does not exist, create it.
                        ts = new TaskState
                        {
                            SegmentID = segmentID,
                            Name = taskName,
                            State = (char)state
                        };

                        context.TaskStates.InsertOnSubmit(ts);
                        context.SubmitChanges();
                    }
                    else
                    {
                        // Record exists, update it.
                        ts.State = (char)state;
                        context.SubmitChanges();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                }
            }
        }

        #endregion == TaskStates Table Methods
    }
}
