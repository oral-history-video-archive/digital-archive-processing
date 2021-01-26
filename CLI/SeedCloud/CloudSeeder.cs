using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace SeedCloud
{
    /// <summary>
    /// List of valid content selection specifications.
    /// </summary>
    public enum ContentSpecifier
    {
        /// <summary>
        /// Selects all content from the processing database which has been processed.
        /// </summary>
        All,
        /// <summary>
        /// Selects all content which has been modified since it's last publishing date.
        /// </summary>
        Modified,
        /// <summary>
        /// Selects all content created prior to a specific date. 
        /// </summary>
        PriorTo,
        /// <summary>
        /// Selects all content which has been previously published.
        /// </summary>
        Published
    }

    /// <summary>
    /// Library for efficiently seeding the entire contents of the local processing server to Azure.
    /// </summary>
    public static class CloudSeeder
    {
        #region ====================             PUBLIC  METHODS             ====================
        public static void MakeItRain(DigitalArchiveSpecifier archiveSpecifier, ContentSpecifier contentSpecifier, bool importSegmentTagsFromFirebase, DateTime? date = null)
        {
            if (contentSpecifier == ContentSpecifier.PriorTo && date == null)
            {
                return;
            }

            using (var context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                var connection = context.Connection;

                Logger.Write("Opened processing database: {0}/{1}.", connection.DataSource, connection.Database);

                Logger.Write("Retrieving list of collections to process...");
                List<String> collections = null;
                switch (contentSpecifier)
                {
                    case ContentSpecifier.All:
                        collections = (from c in context.Collections
                                       orderby c.Accession
                                       select c.Accession).ToList();
                        break;
                    case ContentSpecifier.Modified:
                        collections = (from c in context.Collections
                                       where c.Published <= c.Modified
                                       orderby c.Accession
                                       select c.Accession).ToList();
                        break;
                    case ContentSpecifier.PriorTo:
                        collections = (from c in context.Collections
                                       where c.Created <= date
                                       orderby c.Accession
                                       select c.Accession).ToList();
                        break;
                    case ContentSpecifier.Published:
                        collections = (from c in context.Collections
                                       where c.Phase == (char)PublishingPhase.Published
                                       orderby c.Accession
                                       select c.Accession).ToList();
                        break;
                }

                Logger.Write("Selected {0} collections for publishing", collections.Count);

                MakeItRain(collections, archiveSpecifier, importSegmentTagsFromFirebase);
            }
        }
        /// <summary>
        /// Uploads all content from the processing database to the configured cloud services.
        /// </summary>
        private static void MakeItRain(List<String> accessions, DigitalArchiveSpecifier target, bool importSegmentTagsFromFirebase)
        {            
            using (var context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                Logger.Write("Beginning publishing phase...");

                int processed = 0;
                var failures = new List<string>();
                var stopwatch = Stopwatch.StartNew();

                foreach (var accession in accessions)
                {
                    Logger.Write("----------------------------------[ {0} ]---------------------------------", accession);

                    try
                    {
                        if (!AzureContentManager.PublishCollection(accession, target, importSegmentTagsFromFirebase))
                            failures.Add(accession);
                    }
                    catch (PublishingPackageException ex)
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
                    Logger.Write("{0} out of {1} ({2}%) complete.", processed, accessions.Count, 100 * processed / accessions.Count);
                    Logger.Write("Estimated time of completion is {0}.", timeOfCompletion.ToString("MM-dd-yyyy hh:mm:ss tt"));
                    Logger.Write("================================================================================");
                    Logger.Write("");
                }

                // Report collections which failed to publish (if any)
                if (failures.Count > 0)
                {
                    var s = (failures.Count > 1) ? "s" : string.Empty;
                    Logger.Write("The following {0} collection{1} failed to upload to the digital archive:", failures.Count, s);
                    foreach(var failure in failures)
                    {
                        Logger.Write("     {0}", failure);
                    }
                }
                else
                {
                    var s = (accessions.Count() > 1) ? "s" : string.Empty;
                    Logger.Write("{0} collection{1} published successfully, no errors reported.", accessions.Count(), s);
                }

                stopwatch.Stop();
            }
        }
        #endregion =================             PUBLIC  METHODS             ====================
    }
}
