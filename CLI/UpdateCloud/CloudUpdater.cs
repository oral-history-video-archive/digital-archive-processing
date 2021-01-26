using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace UpdateCloud
{
    /// <summary>
    /// The list of valid digital archive specifiers
    /// </summary>
    internal enum TargetSpecifier
    {
        /// <summary>
        /// Specifies that both the Processing and Production Digital Archives should be updated.
        /// </summary>
        Both,
        /// <summary>
        /// Specifies only the Processing (a.k.a. QA) Digital Archive should be updated.
        /// </summary>
        ProcessingOnly,
        /// <summary>
        /// Specifies only the Prodution (a.k.a. Live) Digtial Archive should be updated.
        /// </summary>
        ProductionOnly
    }

    internal static class CloudUpdater
    {
        /// <summary>
        /// Gets the list of collections which were updated by UpdateDatabase tool.
        /// </summary>
        /// <returns>A list of accession numbers requiring updates.</returns>
        internal static List<string> GetQueuedUpdates()
        {
            using (var context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                var connection = context.Connection;

                Logger.Write($"Opened processing database: {connection.DataSource}/{connection.Database}.");

                Logger.Write("Retrieving list of collections...");

                var accessions = (from q in context.QueuedUpdates
                                  orderby q.Accession
                                  select q.Accession).ToList();

                Logger.Write($"Found {accessions.Count} collections in the update queue.");

                return accessions;
            }
        }

        /// <summary>
        /// Delete the given accession from the QueuedUpdates table.
        /// </summary>
        /// <param name="accession">The collection accession identifier to be deleted.</param>
        internal static void DeleteQueuedUpdate(string accession)
        {
            Logger.Write($"Deleting {accession} from the update queue...");

            using (var context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                try
                {
                    var item = (from q in context.QueuedUpdates
                                where q.Accession == accession
                                select q).Single();

                    context.QueuedUpdates.DeleteOnSubmit(item);
                    context.SubmitChanges();

                    Logger.Write($"...{accession} deleted successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    Logger.Warning($"...failed to delete {accession} from update queue.");                    
                }
            }
        }

        /// <summary>
        /// Update the given accessions in both the Processing and Production Archives
        /// </summary>
        /// <param name="accessions">A list of collection accession identifiers.</param>
        internal static void ApplyUpdates(List<string> accessions)
        {
            Logger.Write("Beginning udpate phase...");

            int processed = 0;
            var successes = new List<string>();
            var failures = new List<string>();
            var stopwatch = Stopwatch.StartNew();

            foreach (var accession in accessions)
            {
                try
                {
                    if (UpdateDigitalArchive(accession, DigitalArchiveSpecifier.Processing) &&
                        UpdateDigitalArchive(accession, DigitalArchiveSpecifier.Production))
                    {
                        successes.Add(accession);
                        DeleteQueuedUpdate(accession);
                    }
                    else
                    {
                        failures.Add(accession);
                    }
                }
                catch (UpdatePackageException ex)
                {
                    Logger.Error(ex.Message);
                    failures.Add(accession);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    failures.Add(accession);
                }


                processed++;

                var timespan = stopwatch.Elapsed;
                double secondsPerCollection = timespan.TotalSeconds / processed;
                double ttc = secondsPerCollection * (accessions.Count - processed);
                DateTime timeOfCompletion = DateTime.Now.AddSeconds(ttc);

                Logger.Write("");
                Logger.Write("================================================================================");
                Logger.Write($"{processed} out of {accessions.Count} ({100 * processed / accessions.Count}%) updated.");
                Logger.Write($"Estimated time of completion is {timeOfCompletion:MM-dd-yyyy hh:mm:ss tt}.");
                Logger.Write("================================================================================");
                Logger.Write("");
            }

            stopwatch.Stop();

            Logger.Write("================================================================================");
            Logger.Write($"UpdateCloud Summary Report:");
            Logger.Write($"{processed,8:d} collections were found in the update queue.");
            Logger.Write($"{successes.Count,8:d} collections were successfully updated.");
            Logger.Write($"{failures.Count,8:d} collections were not updated due to errors.");
            Logger.Write("================================================================================");
            Logger.Write("");

            SendReport(processed, successes, failures, stopwatch.Elapsed);
        }

        /// <summary>
        /// Updates the specified collection on the target Digital Archive.
        /// </summary>
        /// <param name="accession">Accession identifier of collection to be updated.</param>
        /// <param name="target">Enumeration specifying which Digital Archive to update.</param>
        /// <returns>True if the update was successful; False otherwise.</returns>
        private static bool UpdateDigitalArchive(string accession, DigitalArchiveSpecifier target)
        {
            Logger.Write($"--------------------------[ { accession} >>> {target} ]--------------------------");

            var biographyDetails = AzureContentManager.GetBiographyDetails(accession, target);

            if (biographyDetails == null)
            {
                Logger.Write($"Collection {accession} not found on {target} Archive, no update required.");
                return true;
            }

            var updatePackage = new UpdatePackage(biographyDetails, target);
            AzureContentManager.UpdateCollection(updatePackage, target);

            Logger.Write("");
            return true;
        }

        /// <summary>
        /// Sends a detailed report to the processing team via email.
        /// </summary>
        /// <param name="total">Total number of items found in the update queue.</param>
        /// <param name="successes">A list of successfully updated accessions.</param>
        /// <param name="failures">A list of accessions which failed to update.</param>
        /// <param name="elapsed"></param>
        private static void SendReport(int total, List<string> successes, List<string> failures, TimeSpan elapsed)
        {
            const string EOL = "\r\n";
            var now = DateTime.Now;

            var subject = $"Digital Archive Update Report {now:ddd, dd MMMM yyyy}";
            
            var body = $"Digital Archive Update Report{EOL}{EOL}";
            body += $"Summary:{EOL}{EOL}";
            body += $"The Digital Archive Update tool (UpdateCloud) completed operations ";
            body += $"at {now:h:mm tt} on {now:ddd, dd MMMM yyyy}{EOL}{EOL}";

            if (total == 1)
            {
                body += $"One collection was processed in {GetPrintableTime(elapsed)}.{EOL}";
            }
            else
            {
                body += $"{total} collections were processed in {GetPrintableTime(elapsed)}.{EOL}";
            }
            // Success Summary
            if (successes.Count == 1)
            {
                body += $"One collection was updated successfully.{EOL}";
            }
            else if (successes.Count == total)
            {
                body += $"All collections were updated successfully.{EOL}";
            }            
            else if (successes.Count > 1)
            {
                body += $"{successes.Count,8:d} collections were updated successfully.{EOL}";
            }
            // Failure Summary
            if (failures.Count == 1)
            {
                body += $"One collection could not be updated due to errors.{EOL}";
            }
            else if (failures.Count == total)
            {
                body += $"No collections were updated due to errors.{EOL}";
            }
            else if (failures.Count > 1)
            {
                body += $"{failures.Count,8:d} collections could not be updated due to errors.{EOL}";
            }

            body += $"{EOL}{EOL}";

            if (successes.Count > 0)
            {
                body += $"----------------------------------------{EOL}";
                body += $"Collections Updated:{EOL}{EOL}";
                foreach (var accession in successes)
                {
                    body += $"    {accession}{EOL}";
                }
                body += $"{EOL}{EOL}";
            }

            if (failures.Count > 0)
            {
                body += $"----------------------------------------{EOL}";
                body += $"Failures:{EOL}{EOL}";
                foreach (var accession in failures)
                {
                    body += $"    {accession}{EOL}";
                }
                body += $"{EOL}{EOL}";
            }

            body += $"";

            Utilities.SendEmail(subject, body, (failures.Count > 0));
            Logger.Write("Final report emailed to processing team.");
        }

        /// <summary>
        /// Formats the given timespan as a human readable string.
        /// </summary>
        /// <param name="span">A timespan.</param>
        /// <returns>A nicely formatted string.</returns>
        private static string GetPrintableTime(TimeSpan span)
        {
            var timeToProcess = string.Empty;
            if (span.Days == 1) timeToProcess += "1 day, ";
            if (span.Days > 1) timeToProcess += $"{span.Days} days, ";

            if (span.Hours == 1) timeToProcess += $"1 hour, ";
            if (span.Hours > 1) timeToProcess += $"{span.Hours} hours, ";

            if (span.Minutes == 1) timeToProcess += $"1 minute";
            if (span.Minutes > 0) timeToProcess += $"{span.Minutes} minutes";

            if (timeToProcess.Length > 0) timeToProcess += "and ";

            if (span.Seconds == 0) timeToProcess += "no seconds";
            if (span.Seconds == 1) timeToProcess += "1 second";
            if (span.Seconds > 1) timeToProcess += $"{span.Seconds} seconds";

            return timeToProcess;
        }
    }
}
