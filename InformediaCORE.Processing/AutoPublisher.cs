using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace InformediaCORE.Processing
{
    /// <summary>
    /// Auto-publishes sessions to the review site as the complete processing
    /// </summary>
    public static class AutoPublisher
    {
        private const string EOL = "\r\n";

        /// <summary>
        /// The publishing process will be skipped if false - useful during development.
        /// </summary>
        private static readonly bool AutoPublishEnabled = Settings.Current.Processing.AutoPublish ?? true;

        /// <summary>
        /// If true, segment tags will be imported from Firebase during the publishing process.
        /// </summary>
        private static readonly bool ImportSegmentTags = Settings.Current.Processing.AutoPublishTagImport ?? true;

        /// <summary>
        /// A list of known processing task names in alphabetical order.
        /// </summary>
        /// <remarks>
        /// Based on StackOverflow post: Get all inherited classes of an abstract class
        /// https://stackoverflow.com/questions/5411694/get-all-inherited-classes-of-an-abstract-class
        /// </remarks>
        private static readonly List<string> KnownTaskNames =
            typeof(Tasks.AbstractTask).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Tasks.AbstractTask)) && !t.IsAbstract)
            .Select(t => t.Name).OrderBy(t => t).ToList();

        private static IDVLDataContext _db;

        /// <summary>
        /// Check the processing status of the specified session.  If complete without error
        /// the collection will be uploaded to the review site.  Email notification is sent
        /// to the processing team regarding the success or failure status of the collection.
        /// </summary>
        /// <param name="sessionID">The database identifier if the session to check.</param>
        public static void CheckSession(int sessionID)
        {
            if (AutoPublishEnabled != true)
            {
                Logger.Warning("AutoPublisher not enabled.");
                return;
            }

            Logger.Write("AutoPublisher started.");
            
            using (_db = DataAccess.GetDataContext(true))
            {
                var session =
                    (from s in _db.Sessions
                     where s.SessionID == sessionID
                     select s).SingleOrDefault();

                if (session == null)
                {
                    Logger.Error($"Error retrieving session #{sessionID} from the database.");
                    Logger.Warning("Publishing status could be not be updated.");
                    return;
                }

                var collection = session.Collection;
                var sessionName = $"Collection {collection.Accession} Session #{session.SessionOrder}";

                var segments =
                    from s in _db.Segments
                    where s.SessionID == sessionID
                    select s;

                var failedSegments = new List<Segment>();

                foreach (var segment in segments)
                {
                    // If the segment has been processed then we can ignore it.
                    if (segment.Ready == (char) ReadyStateValue.Ready) continue;

                    // Pending tasks indicate that processing is not complete, exit early.
                    if (IsPending(segment)) return;

                    // Track segments with failures.
                    failedSegments.Add(segment);
                }

                // Processing is complete, check if there were any failures
                if (failedSegments.Any())
                {
                    // Something went wrong.  Send a failure report.
                    Logger.Warning("{0} completed processing with errors.", sessionName);
                    SendProcessingErrorReport(collection, session, failedSegments);
                    return;
                }

                // Publish to Internal QA Site
                Logger.Write("Publishing {0} to QA Digital Archive for review...", sessionName);
                
                var publishingStatus = AzureContentManager.PublishCollection(collection.Accession, DigitalArchiveSpecifier.Processing, new int[] {session.SessionOrder}, ImportSegmentTags);

                if (publishingStatus)
                {
                    SendReadyNotification(collection, session); 
                }
                else
                {
                    SendPublishingErrorReport(collection, session);
                }
            }
        }

        /// <summary>
        /// Determines if there are any pending processing tasks for the given segment.
        /// </summary>
        /// <param name="segment">An instance of a database segment.</param>
        /// <returns>True if there are any pending tasks; false otherwise.</returns>
        private static bool IsPending(Segment segment)
        {
            var taskStates = _db.TaskStates.Where(t => t.SegmentID == segment.SegmentID);

            var knownTasks = new Dictionary<string, char>();
            foreach(var name in KnownTaskNames)
            {
                var taskState = taskStates.Where(t => t.Name == name).SingleOrDefault();
                knownTasks.Add(name, taskState?.State ?? (char)TaskStateValue.Pending);
            }

            return knownTasks.Any(v => v.Value == (char)TaskStateValue.Pending);
        }

        /// <summary>
        /// Emails an error report to the processing team.
        /// </summary>
        /// <param name="collection">The processed collection.</param>
        /// <param name="session">The processed session.</param>
        /// <param name="segments">A list of segments which failed processing.</param>
        private static void SendProcessingErrorReport(Collection collection, Session session,  List<Segment> segments)
        {
            var subject = $"{collection.PreferredName} ({collection.Accession}) completed processing with errors";

            var body = $"{collection.PreferredName}, collection {collection.Accession} session {session.SessionOrder}, ";
            body += $"completed processing with errors: {EOL}{EOL}";

            foreach (var segment in segments)
            {
                var tasks = 
                    from t in _db.TaskStates
                    where t.SegmentID == segment.SegmentID && t.State == (char)TaskStateValue.Failed
                    select t;

                foreach (var task in tasks)
                {
                    body += $"  * Segment {segment.SessionID}: Failed {task.Name}{EOL}";
                }
            }

            body += $"{EOL}";
            body += $"See {GetLogFilePath()} on {Dns.GetHostName()} for details.{EOL}{EOL}";
            body += $"You can attempt to reprocess the segments above by using RunProcessing with the /frun option.{EOL}{EOL}";
            body += $"Example:{EOL}{EOL}";
            body += $"  >RunProcessing /frun /id:{segments[0]?.SegmentID}{EOL}{EOL}";

            Utilities.SendEmail(subject, body, true);
            Logger.Write("Processing error report emailed to processing team.");
        }

        /// <summary>
        /// Emails a publishing error notification to the processing team.
        /// </summary>
        /// <param name="collection">The processed collection.</param>
        /// <param name="session">The processed session.</param>
        private static void SendPublishingErrorReport(Collection collection, Session session)
        {
            var subject = $"{collection.PreferredName} ({collection.Accession}) completed processing with publishing errors";

            var body = $"{collection.PreferredName}, collection {collection.Accession} session {session.SessionOrder}, ";
            body += $"completed processing successfully but errors were logged while uploading to the review site.{EOL}{EOL}";
            body += $"See {GetLogFilePath()} on {Dns.GetHostName()} for details.{EOL}{EOL}";

            Utilities.SendEmail(subject, body, true);
            Logger.Write("Publishing error report emailed to processing team.");
        }

        /// <summary>
        /// Emails a notification to the processing team that a collection is ready for review.
        /// </summary>
        /// <param name="collection">The processed collection.</param>
        /// <param name="session">The processed session.</param>
        private static void SendReadyNotification(Collection collection, Session session)
        {
            var subject = $"{collection.PreferredName} ({collection.Accession}) ready for review.";

            var body = $"{collection.PreferredName}, collection {collection.Accession} session {session.SessionOrder}, ";
            body += $"completed processing successfully and is ready for review.{EOL}{EOL}";
            body += $"You can review the contents of this collection here:{EOL}{EOL}";
            body += $"    {Settings.Current.Processing.BiographyDetailsUrl}{collection.Accession}{EOL}{EOL}";

            Utilities.SendEmail(subject, body);
            Logger.Write("Processing success notification emailed to processing team.");
        }

        /// <summary>
        /// Returns the fully qualified path to the most likely log file containing errors related to the current
        /// processing context.
        /// </summary>
        /// <returns>A fully qualified path and file name.</returns>
        private static string GetLogFilePath()
        {
            var binPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            return $"{binPath}\\logs\\{DateTime.Now:yyyy-MM-dd}.log";
        }
    }
}
