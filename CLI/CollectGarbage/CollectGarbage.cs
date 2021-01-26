using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;


namespace InformediaCORE.CollectGarbage
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.Required,
            HelpText = "Determines which archive will be the target of this operation."
        )]
        public DigitalArchiveSpecifier Target;

        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "List only - don't delete bad data.",
            DefaultValue = false
        )]
        public bool ListOnly;
    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Class for parsing a blob's path into individual fields.
    /// Blobs paths are in the form:
    ///     container/scope/type/id
    ///     
    /// Example:
    ///     data/story/details/71
    /// </summary>
    internal class BlobPath
    {
        /// <summary>
        /// The leading part of the path identifies the Azure storage container
        /// </summary>
        public string Container { get; private set; }

        /// <summary>
        /// The blob belongs to either the 'biography' or 'story' scope
        /// </summary>
        public string Scope { get; private set; }

        /// <summary>
        /// The type of blob such as 'details', 'image', or 'video'
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// The database identifier for the blob; either accession or id number.
        /// </summary>
        public string ID { get; private set; }

        public BlobPath(string blobUri)
        {
            var parts = blobUri.Split('/');
            Container = parts[0];
            Scope = parts[1];
            Type = parts[2];
            ID = parts[3];
        }
    }

    /// <summary>
    /// Return type used to aggregate the results of the clean up operation.
    /// </summary>
    internal class ScanResults
    {
        /// <summary>
        /// Total number of elements reviewed.
        /// </summary>
        public int Total { get; set; }
        /// <summary>
        /// Number of good elements found.
        /// </summary>
        public int Good { get; set; }
        /// <summary>
        /// Number of bad elements found and cleaned.
        /// </summary>
        public int Bad { get; set; }
    }

    /// <summary>
    /// Command line utility to remove orphaned files and duplicate search entries (a.k.a. garbage)
    /// from the Azure Blob Storage and Azure Search Service accounts backing the Digital Archive.
    /// </summary>
    internal class CollectGarbage
    {
        /// <summary>
        /// Program entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        static void Main(string[] args)
        {
            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            // Log header
            Logger.Start();
            Logger.Write("Requesting user confirmation to proceed...");

            ConsoleColor color = (arguments.ListOnly) ? ConsoleColor.Green : ConsoleColor.Red;

            Utilities.NewLine();

            if (arguments.ListOnly)
            {
                Utilities.WriteLine("This operation will find and report all orphaned data files and duplicate search entries", color);
            }
            else
            {
                Utilities.WriteLine("****** ATTENTION ******", color);
                Utilities.NewLine();
                Utilities.WriteLine("This operation will find and REMOVE all orphaned data files and duplicate search entries", color);
            }

            switch (arguments.Target)
            {
                case DigitalArchiveSpecifier.Processing:
                    Utilities.WriteLine($"from the following Processing Digital Archive (a.k.a test site) accounts:", color);
                    Utilities.NewLine();
                    Utilities.WriteLine($"     Azure Search: {Settings.Current.Processing.AzureSearchServiceName}", color);
                    Utilities.WriteLine($"    Azure Storage: {Settings.Current.Processing.AzureStorageAccountName}", color);
                    break;
                case DigitalArchiveSpecifier.Production:
                    Utilities.WriteLine($"from the following Production Digital Archive (a.k.a live site) accounts:", color);
                    Utilities.NewLine();
                    Utilities.WriteLine($"     Azure Search: {Settings.Current.Production.AzureSearchServiceName}", color);
                    Utilities.WriteLine($"    Azure Storage: {Settings.Current.Production.AzureStorageAccountName}", color);
                    break;
            }

            const string prompt = ">>>>> Do you wish to continue? [y/n]: ";

            if (Utilities.GetUserConfirmation(prompt))
            {
                Logger.Write("Operation confirmed by user.");
                TakeOutTheTrash(arguments.Target, arguments.ListOnly);
            }
            else
            {
                Logger.Write("Operation cancelled by user.");
            }

            // Log footer
            Logger.End();
        }

        /// <summary>
        /// Performs the clean up operation on all the cloud data services.
        /// </summary>
        /// <param name="target"></param>
        static void TakeOutTheTrash(DigitalArchiveSpecifier target, bool listOnly)
        {
            // Fetch the configuration settings for the target archive
            var settings =
                (target == DigitalArchiveSpecifier.Production)
                ? Settings.Current.Production
                : Settings.Current.Processing;

            var storageHandler =
                new AzureStorageHandler(settings.AzureStorageAccountName, settings.AzureStorageAccountKey);

            var blob1Results = CleanDataContainer(storageHandler, listOnly);
            var blob2Results = CleanMediaContainer(storageHandler, listOnly);


            var searchHandler =
                new AzureSearchHandler(settings.AzureSearchServiceName, settings.AzureSearchApiKey);

            var index1Results = CleanBiographySearchIndex(searchHandler, listOnly);
            var index2Results = CleanStorySearchIndex(searchHandler, listOnly);

            Logger.Write("-------------------------------- SUMMARY REPORT --------------------------------");
            Logger.Write("'data' container results:");
            Logger.Write($"    Total: {blob1Results.Total}");
            Logger.Write($"     Good: {blob1Results.Good}");
            Logger.Write($"      Bad: {blob1Results.Bad}");

            Logger.Write($"'media' container results:");
            Logger.Write($"    Total: {blob2Results.Total}");
            Logger.Write($"     Good: {blob2Results.Good}");
            Logger.Write($"      Bad: {blob2Results.Bad}");

            Logger.Write($"'biography' index results:");
            Logger.Write($"    Total: {index1Results.Total}");
            Logger.Write($"     Good: {index1Results.Good}");
            Logger.Write($"      Bad: {index1Results.Bad}");

            Logger.Write($"'story' index results:");
            Logger.Write($"    Total: {index2Results.Total}");
            Logger.Write($"     Good: {index2Results.Good}");
            Logger.Write($"      Bad: {index2Results.Bad}");
        }

        /// <summary>
        /// Perform garbage collection on the Azure Storage "data" container.
        /// </summary>
        /// <param name="storageHandler">An AzureStorageHandler instance connected to the storage account to be cleaned.</param>
        /// <returns>A ScanResults instance containing with operation counts.</returns>
        static ScanResults CleanDataContainer(AzureStorageHandler storageHandler, bool listOnly)
        {
            var results = new ScanResults();

            Logger.Write("Scanning contents of 'DATA' container...");

            using (var context = DataAccess.GetDataContext())
            {
                foreach (var blobUri in storageHandler.ListDataBlobs())
                {
                    bool isValid = false;
                    var item = new BlobPath(blobUri);

                    switch (item.Scope)
                    {
                        case "biography":
                            // Biography Details are by Accession number
                            isValid =
                                (from collection in context.Collections
                                 where collection.Accession == item.ID
                                 select collection).Any();
                            break;
                        case "story":
                            isValid = 
                                int.TryParse(item.ID, out var storyID) &&
                                (from segment in context.Segments
                                 where segment.SegmentID == storyID
                                 select segment).Any();                            
                            break;
                    }

                    results.Total++;

                    if (isValid)
                    {
                        results.Good++;
                        Logger.Write($"    OK: {blobUri}");
                    }
                    else
                    {
                        results.Bad++;
                        Logger.Error($"   BAD: {blobUri}");
                        if (!listOnly) storageHandler.DeleteBlob(blobUri);                        
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Perform garbage collection on the Azure Storage 'media' container.
        /// </summary>
        /// <param name="storageHandler">An AzureStorageHandler instance connected to the storage account to be cleaned.</param>
        /// <returns>A ScanResults instance containing with operation counts.</returns>
        static ScanResults CleanMediaContainer(AzureStorageHandler storageHandler, bool listOnly)
        {
            var results = new ScanResults();

            Logger.Write("Scanning contents of 'MEDIA' container...");

            using (var context = DataAccess.GetDataContext())
            {
                foreach (var blobUri in storageHandler.ListMediaBlobs())
                {
                    bool isValid = false;
                    var item = new BlobPath(blobUri);

                    switch (item.Scope)
                    {
                        case "biography":
                            isValid =
                                int.TryParse(item.ID, out var biographyID) &&
                                (from collection in context.Collections
                                 where collection.CollectionID == biographyID
                                 select collection).Any();
                            break;
                        case "story":
                            isValid =
                                int.TryParse(item.ID, out var storyID) &&
                                (from segment in context.Segments
                                 where segment.SegmentID == storyID
                                 select segment).Any();
                            break;
                    }

                    results.Total++;

                    if (isValid)
                    {
                        results.Good++;
                        Logger.Write($"    OK: {blobUri}");
                    }
                    else
                    {
                        results.Bad++;
                        Logger.Error($"   BAD: {blobUri}");
                        if (!listOnly) storageHandler.DeleteBlob(blobUri);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Perform garbage collection on the Azure Search 'biography' index.
        /// </summary>
        /// <param name="searchHandler">An AzureSearchHandler instance connected to the search service to be cleaned.</param>
        /// <returns>A ScanResults instance containing with operation counts.</returns>
        static ScanResults CleanBiographySearchIndex(AzureSearchHandler searchHandler, bool listOnly)
        {
            var results = new ScanResults();

            Logger.Write("Scanning contents of 'BIOGRAPHY' index...");

            using (var context = DataAccess.GetDataContext())
            {
                foreach (var biography in searchHandler.ListBiographies())
                {
                    var isValid =
                        int.TryParse(biography.BiographyID, out var biographyID) &&
                        (from collection in context.Collections
                         where collection.CollectionID == biographyID
                         select collection).Any();

                    results.Total++;

                    if (isValid)
                    {
                        results.Good++;
                        Logger.Write($"    OK: Accession {biography.Accession}");
                    }
                    else
                    {
                        results.Bad++;
                        Logger.Error($"   BAD: Accession {biography.Accession}");
                        if (!listOnly) searchHandler.DeleteBiography(biography);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Perform garbage collection on the Azure Search 'story' index.
        /// </summary>
        /// <param name="searchHandler">An AzureSearchHandler instance connected to the search service to be cleaned.</param>
        /// <returns>A ScanResults instance containing with operation counts.</returns>
        static ScanResults CleanStorySearchIndex(AzureSearchHandler searchHandler, bool listOnly)
        {
            var results = new ScanResults();

            Logger.Write("Scanning contents of 'STORY' index...");

            using (var context = DataAccess.GetDataContext())
            {
                foreach (var story in searchHandler.ListStories())
                {
                    var isValid =
                        int.TryParse(story.StoryID, out var storyID) &&
                        (from segment in context.Segments
                         where segment.SegmentID == storyID
                         select segment).Any();

                    results.Total++;

                    if (isValid)
                    {
                        results.Good++;
                        Logger.Write($"    OK: Story #{story.StoryID}");
                    }
                    else
                    {
                        results.Bad++;
                        Logger.Error($"   BAD: Story #{story.StoryID}");
                        if (!listOnly) searchHandler.DeleteStory(story);
                    }
                }
            }

            return results;
        }
    }
}
