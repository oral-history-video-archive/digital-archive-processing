using System;
using InformediaCORE.Azure.Models;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace InformediaCORE.Azure
{
    public static class AzureContentManager
    {
        #region == AZURE SEARCH INITIALIZATION

        /// <summary>
        /// Initializes the specified Azure Search service(s).
        /// </summary>
        /// <param name="target">Determines which digital archive will be the target of this operation.</param>
        public static void InitializeSearchService(DigitalArchiveSpecifier target)
        {
            switch(target)
            {
                case DigitalArchiveSpecifier.Processing:
                    Logger.Write("Initializing the Azure Search service for the processing (review) digital archive.");
                    InitializeSearchService(Settings.Current.Processing);
                    break;
                case DigitalArchiveSpecifier.Production:
                    Logger.Write("Initializing the Azure Search service for the production (public) digital archive.");
                    InitializeSearchService(Settings.Current.Production);
                    break;
            }
        }

        /// <summary>
        /// Initializes the Azure Search service specified by the given Azure account settings.
        /// </summary>
        /// <param name="settings">The section of the configuration file specifying which digital archive to act upon.</param>
        private static void InitializeSearchService(AzureSettings settings)
        {
            var searchHandler = new AzureSearchHandler(settings.AzureSearchServiceName, settings.AzureSearchApiKey);
            searchHandler.InitializeSearchService();
        }

        /// <summary>
        /// Test the ability to connect to the specified Azure Search Service.
        /// </summary>
        /// <param name="target">Determines which digital archive will be the target of this operation.</param>
        /// <returns>True if the Search Service configuration is correct; false otherwise.</returns>
        public static bool TestSearchService(DigitalArchiveSpecifier target)
        {
            var result = false;

            switch (target)
            {
                case DigitalArchiveSpecifier.Processing:
                    result = TestSearchService(Settings.Current.Processing);
                    break;
                case DigitalArchiveSpecifier.Production:
                    result = TestSearchService(Settings.Current.Production);
                    break;
            }

            return result;
        }

        /// <summary>
        /// Test the ability to connect to the Azure Search Service.
        /// </summary>
        /// <param name="settings">The section of the configuration file specifying which digital archive to act upon.</param>
        /// <returns>True if the Search Service configuration is correct; false otherwise.</returns>
        private static bool TestSearchService(AzureSettings settings)
        {
            try
            {
                var searchHandler = new AzureSearchHandler(settings.AzureSearchServiceName, settings.AzureSearchApiKey);                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion AZURE SEARCH INITIALIZATION

        #region == AZURE STORAGE INITIALIZATION

        /// <summary>
        /// Initializes the specified Azure Storage service(s).
        /// </summary>
        /// <param name="target">Determines which digital archive will be the target of this operation.</param>
        public static void InitializeStorageService(DigitalArchiveSpecifier target)
        {
            switch (target)
            {
                case DigitalArchiveSpecifier.Processing:
                    Logger.Write("Initializing the Azure Storage service for the processing (review) digital archive.");
                    InitializeStorageService(Settings.Current.Processing);
                    break;
                case DigitalArchiveSpecifier.Production:
                    Logger.Write("Initializing the Azure Storage service for the production (public) digital archive.");
                    InitializeStorageService(Settings.Current.Production);
                    break;
            }
        }

        /// <summary>
        /// Initializes the Azure Storage service specified by the given Azure account settings.
        /// </summary>
        /// <param name="settings">The section of the configuration file specifying which digital archive to act upon.</param>
        private static void InitializeStorageService(AzureSettings settings)
        {
            var storageHandler = new AzureStorageHandler(settings.AzureStorageAccountName, settings.AzureStorageAccountKey);
            storageHandler.InitializeStorageService();
        }

        /// <summary>
        /// Test the ability to connect to the specified Azure Storage Service.
        /// </summary>
        /// <param name="target">Determines which digital archive will be the target of this operation.</param>
        /// <returns>True if the Stoage Service configuration is correct; false otherwise.</returns>
        public static bool TestStorageService(DigitalArchiveSpecifier target)
        {
            var result = false;

            switch (target)
            {
                case DigitalArchiveSpecifier.Processing:
                    result = TestStorageService(Settings.Current.Processing);
                    break;
                case DigitalArchiveSpecifier.Production:
                    result = TestStorageService(Settings.Current.Production);
                    break;
            }

            return result;
        }

        /// <summary>
        /// Test the ability to connect to the Azure Storage Service.
        /// </summary>
        /// <param name="settings">The section of the configuration file specifying which digital archive to act upon.</param>
        /// <returns>True if the Stoage Service configuration is correct; false otherwise.</returns>
        private static bool TestStorageService(AzureSettings settings)
        {
            try
            {
                var storageHandler = new AzureStorageHandler(settings.AzureStorageAccountName, settings.AzureStorageAccountKey);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion AZURE STORAGE INITIALIZATION

        #region == PUBLISH COLLECTION
        /// <summary>
        /// Publishes the specified biographical collection to the specified Digital Archive.
        /// </summary>
        /// <param name="accession">A collection accession identifier.</param>
        /// <param name="target">Determines which digital archive will be the target of this operation.</param>
        /// <returns>True if the publishing operation completed successfully; false otherwise.</returns>
        public static bool PublishCollection(string accession, DigitalArchiveSpecifier target, bool importSegmentTagsFromFirebase)
        {
            var publishingPackage = new PublishingPackage(accession, importSegmentTagsFromFirebase);
            var currentPhase = DataAccess.GetCollectionPublishingPhase(accession);
            var result = true;

            switch (target)
            {
                case DigitalArchiveSpecifier.Processing:
                    Logger.Write("Publishing biography {0} to the processing (review) digital archive.", accession);
                    result = PublishCollection(publishingPackage, Settings.Current.Processing);
                    break;
                case DigitalArchiveSpecifier.Production:
                    if (currentPhase == (char)PublishingPhase.Draft)
                    {
                        Logger.Error("Cannot publish Draft content to production (public) digital archive.");
                        return false;
                    }
                    Logger.Write("Publishing biography {0} to the production (public) digital archive.", accession);
                    result = PublishCollection(publishingPackage, Settings.Current.Production);
                    break;
            }

            return result;
        }

        /// <summary>
        /// Publishes the specified biographical collection's sessions to the specified Digital Archive.
        /// </summary>
        /// <param name="accession">A collection accession identifier.</param>
        /// <param name="target">Determines which digital archive will be the target of this operation.</param>
        /// <param name="sessionOrdinals">An array of session ordinals specifying which sessions to publish.</param>
        /// <returns>True if the publishing operation completed successfully; false otherwise.</returns>
        public static bool PublishCollection(string accession, DigitalArchiveSpecifier target, int[] sessionOrdinals, bool importSegmentTagsFromFirebase)
        {
            PublishingPackage package = null;
            var result = true;

            try
            {
                package = new PublishingPackage(accession, target, sessionOrdinals, importSegmentTagsFromFirebase);
                result = PublishCollection(package, target);
            }
            catch (PublishingPackageException ex)
            {
                Logger.Error(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
            finally
            {
                if (package != null) package.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Publishes the contents of the given PublishingPackage to the target Digital Archive
        /// </summary>
        /// <param name="package">The PublishingPackage to be published.</param>
        /// <param name="target">The Digital Archive where the contents should be uploaded.</param>
        /// <returns>True if the publishing operation completed successfully; false otherwise.</returns>
        public static bool PublishCollection(PublishingPackage package, DigitalArchiveSpecifier target)
        {
            var currentPhase = package.Phase;
            var result = true;

            switch (target)
            {
                case DigitalArchiveSpecifier.Processing:
                    Logger.Write("Publishing biography {0} to the processing (review) digital archive.", package.Accession);
                    result = PublishCollection(package, Settings.Current.Processing);
                    if (result)
                    {
                        package.PromotePublishingPhase(PublishingPhase.Review);
                    }
                    break;
                case DigitalArchiveSpecifier.Production:
                    if (currentPhase == PublishingPhase.Draft)
                    {
                        Logger.Error("Cannot publish Draft content to production (public) digital archive.");
                        return false;
                    }
                    Logger.Write("Publishing biography {0} to the production (public) digital archive.", package.Accession);
                    result = PublishCollection(package, Settings.Current.Production);
                    if (result)
                    {
                        package.PromotePublishingPhase(PublishingPhase.Published);
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// Uses the given Azure account settings to exectute the given publishing package.
        /// </summary>
        /// <param name="publishingPackage">The data to be published.</param>
        /// <param name="settings">The section of the configuration file specifying which digital archive to act upon.</param>
        private static bool PublishCollection(PublishingPackage publishingPackage, AzureSettings settings)
        {
            var p = publishingPackage;
            var failed = AzureStorageHandler.StatusCode.Failed;
            var errors = 0;

            var storageHandler = new AzureStorageHandler(settings.AzureStorageAccountName, settings.AzureStorageAccountKey);
            if (storageHandler.UploadBiographyDetails(p.BiographyDetails) == failed) errors++;

            var bioImage = p.GetBiographyImage();
            if (storageHandler.UploadBiographyImage(bioImage.ID, bioImage.FileType, bioImage.Data) == failed) errors++;

            foreach (var storyDetails in p.StoryDetails)
            {
                if (storageHandler.UploadStoryDetails(storyDetails) == failed) errors++;

                var storyImage = p.GetStoryImage(storyDetails.StoryID);
                if (storageHandler.UploadStoryImage(storyImage.ID, storyImage.FileType, storyImage.Data) == failed) errors++;

                var storyVideo = p.GetStoryVideo(storyDetails.StoryID);
                if (storageHandler.UploadStoryVideo(storyVideo.ID, storyVideo.MediaPath) == failed) errors++;

                var storyCaptions = p.GetStoryCaptions(storyDetails.StoryID);
                if (storageHandler.UploadStoryCaptions(storyCaptions.ID, storyCaptions.FilePath) == failed) errors++;
            }

            if (errors > 0)
            {
                Logger.Error("Biographical collection {0} will not be indexed due to preceeding errors.  See log for details.", p.Biography.Accession);
                return false;
            }

            var searchHandler = new AzureSearchHandler(settings.AzureSearchServiceName, settings.AzureSearchApiKey);
            if (!searchHandler.AddBiography(p.Biography, p.Stories))
            {
                Logger.Warning("Failed to index biographical collection {0}. See log for details.", p.Biography.Accession);
                return false;
            }

            Logger.Write("Biographical collection {0} successfully indexed.", p.Biography.Accession);
            return true;
        }
        #endregion PUBLISH COLLECTION

        #region == UPDATE COLLECTION
        /// <summary>
        /// Get the specified BiographyDetails document from the specified digital archive.
        /// </summary>
        /// <param name="accession">A valid collection accession identifier.</param>
        /// <param name="source">An enum specifying the source archive.</param>
        /// <returns></returns>
        public static BiographyDetails GetBiographyDetails(string accession, DigitalArchiveSpecifier source)
        {
            // Get configuration for the source archive
            var settings =
                (source == DigitalArchiveSpecifier.Production)
                ? Settings.Current.Production
                : Settings.Current.Processing;

            var storageHandler =
                new AzureStorageHandler(settings.AzureStorageAccountName, settings.AzureStorageAccountKey);

            return storageHandler.GetBiographyDetails(accession);
        }

        /// <summary>
        /// Updates a biographical collection on the target Digital Archive
        /// </summary>
        /// <param name="package">The updated biographical data to be uploaded to the Digital Archive.</param>
        /// <param name="target">The Digital Archive where the contents should be uploaded.</param>
        /// <returns>True if update was successful; False otherwise.</returns>
        public static bool UpdateCollection(UpdatePackage package, DigitalArchiveSpecifier target)
        {
            Logger.Write($"Beginning update of collection {package.Accession} on the {target} archive...");

            var settings = (target == DigitalArchiveSpecifier.Production)
                ? Settings.Current.Production
                : Settings.Current.Processing;

            var storageHandler = new AzureStorageHandler(settings.AzureStorageAccountName, settings.AzureStorageAccountKey);

            if (storageHandler.UploadBiographyDetails(package.BiographyDetails) == AzureStorageHandler.StatusCode.Failed)
            {
                Logger.Warning($"...failed to upload BiographyDetails for collection {package.Accession}, update aborted.");
                return false;
            }

            foreach (var storyDetails in package.StoryDetails)
            {
                if (storageHandler.UploadStoryDetails(storyDetails) == AzureStorageHandler.StatusCode.Failed)
                {
                    Logger.Warning($"...failed to upload StoryDetails for StoryID #{storyDetails.StoryID}, update aborted.");
                    return false;
                }
            }

            var searchHandler = new AzureSearchHandler(settings.AzureSearchServiceName, settings.AzureSearchApiKey);
            if (!searchHandler.AddBiography(package.Biography, package.Stories))
            {
                Logger.Warning($"...failed to index biographical collection {package.Biography.Accession}, update aborted.");
                return false;
            }

            Logger.Write($"...update of collection {package.Accession} on the {target} archive completed successfully.");                

            return true;
        }
        #endregion UPDATE COLLECTION

        #region == RECALL COLLECTION
        /// <summary>
        /// Recalls the specified biographical collection from the specified digital archive.
        /// </summary>
        /// <param name="accession">A collection accession number.</param>
        /// <param name="target">Determines which digital archive will be the target of this operation.</param>
        public static void RecallCollection(string accession, DigitalArchiveSpecifier target)
        {
            string accountKey, accountName;

            switch (target)
            {
                case DigitalArchiveSpecifier.Processing:
                    accountKey = Settings.Current.Processing.AzureStorageAccountKey;
                    accountName = Settings.Current.Processing.AzureStorageAccountName;
                    break;
                case DigitalArchiveSpecifier.Production:
                    accountKey = Settings.Current.Production.AzureStorageAccountKey;
                    accountName = Settings.Current.Production.AzureStorageAccountName;
                    break;
                default:
                    return;
            }

            var storageHandler = new AzureStorageHandler(accountName, accountKey);
            var biographyDetails = storageHandler.GetBiographyDetails(accession);

            if (biographyDetails == null)
            {
                Logger.Warning("Could not find biographical collection {0} on the {1} Archive, recall not possible.", accession, target.ToString());
                return;
            }

            RecallCollection(biographyDetails, target);
        }

        /// <summary>
        /// Recalls the biographical collection corresponding to the given BiographyDetails from the specified digital archive.
        /// </summary>
        /// <param name="biographyDetails">The BiographicalDetails document corresponding to the collection to be deleted.</param>
        /// <param name="target">Determines which digital archive will be the target of this operation.</param>
        public static void RecallCollection(Models.BiographyDetails biographyDetails, DigitalArchiveSpecifier target)
        {
            var recallPackage = new RecallPackage(biographyDetails);
            
            switch (target)
            {
                case DigitalArchiveSpecifier.Processing:
                    Logger.Write("Recalling biography {0} from the processing (review) digital archive.", biographyDetails.Accession);
                    RecallCollection(recallPackage, Settings.Current.Processing);
                    recallPackage.DemotePublishingPhase(PublishingPhase.Draft);
                    break;
                case DigitalArchiveSpecifier.Production:
                    Logger.Write("Recalling biography {0} from the production (public) digital archive.", biographyDetails.Accession);
                    RecallCollection(recallPackage, Settings.Current.Production);
                    recallPackage.DemotePublishingPhase(PublishingPhase.Review);
                    break;
            }
        }

        /// <summary>
        /// Uses the given Azure account settings to execute the given recall package.
        /// </summary>
        /// <param name="recallPackage">The data necessary to recall a collection.</param>
        /// <param name="settings">The section of the configuration file specifying which digital archive to act upon.</param>
        private static void RecallCollection(RecallPackage recallPackage, AzureSettings settings)
        {
            var searchHandler = new AzureSearchHandler(settings.AzureSearchServiceName, settings.AzureSearchApiKey);
            searchHandler.DeleteBiography(recallPackage.Biography, recallPackage.Stories);

            var storageHandler = new AzureStorageHandler(settings.AzureStorageAccountName, settings.AzureStorageAccountKey);

            foreach (var story in recallPackage.Stories)
            {
                storageHandler.DeleteStoryDetails(story.StoryID);
                storageHandler.DeleteStoryImage(story.StoryID);
                storageHandler.DeleteStoryVideo(story.StoryID);
                storageHandler.DeleteStoryCaptions(story.StoryID);
            }

            storageHandler.DeleteBiographyImage(recallPackage.Biography.BiographyID);
            storageHandler.DeleteBiographyDetails(recallPackage.Biography.Accession);            
        }
        #endregion RECALL COLLECTION

        #region == UPDATE METRICS
        public static void UpdateCorpusMetrics(DigitalArchiveSpecifier target)
        {

        }
        #endregion UPDATE METRICS
    }
}
