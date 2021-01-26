using System;
using System.Collections.Generic;
using System.Linq;
using InformediaCORE.Azure.Models;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace InformediaCORE.Azure
{
    public class AzureSearchHandler
    {
        #region ====================              DECLARATIONS               ====================
        /// <summary>
        /// The name of the Azure Search index which will contain the collection (table of contents) data.
        /// </summary>
        private const string BiographyIndex = "biographies";

        /// <summary>
        /// The name of the Azure Search index which will contain the segment (story) data.
        /// </summary>
        private const string StoryIndex = "stories";

        /// <summary>
        /// The Azure Search Service API
        /// </summary>
        private readonly SearchServiceClient _serviceClient;
        #endregion =================              DECLARATIONS               ====================

        #region ====================              CONSTRUCTORS               ====================
        /// <summary>
        /// Instantiate an instance of the AzureSearchHandler class using the configured
        /// API key and Azure Search service name.
        /// </summary>
        public AzureSearchHandler() : this(Settings.Current.Processing.AzureSearchServiceName, Settings.Current.Processing.AzureSearchApiKey) { }

        /// <summary>
        /// Instantiate an instance of the AzureSearchHandler class with the given API key
        /// and Azure Search service name.
        /// </summary>
        /// <param name="searchServiceName">A valid Azure Search service name.</param>
        /// <param name="apiKey">The API key to the Azure Search service.</param>
        public AzureSearchHandler(string searchServiceName, string apiKey)
        {
            _serviceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));
        }
        #endregion =================              CONSTRUCTORS               ====================

        #region ====================             PUBLIC  METHODS             ====================
        /// <summary>
        /// Initializes the search service by creating the Biography and Story indices with their
        /// respective schemas.
        /// 
        /// WARNING: CALLING THIS METHOD WILL RESULT IN THE LOSS OF ALL PRE-EXISTING SEARCH DATA!!
        /// </summary>
        public void InitializeSearchService()
        {
            Logger.Write("Initializing Azure Search Service...");
            CreateBiographyIndex();
            CreateStoryIndex();
        }

        /// <summary>
        /// Adds a biography and it's associated stories to the search index.
        /// </summary>
        /// <param name="biography">The biography document to upload to the search index.</param>
        /// <param name="stories">The list of stories (a.k.a segments) related to the given biography.</param>
        public bool AddBiography(Biography biography, List<Story> stories)
        {
            try
            {
                Logger.Write("Uploading biography to search index...");
                var collectionIndex = _serviceClient.Indexes.GetClient(BiographyIndex);
                collectionIndex.Documents.Index(IndexBatch.Upload(new List<Biography> { biography }));

                Logger.Write("Uploading stories to search index...");
                var storyIndex = _serviceClient.Indexes.GetClient(StoryIndex);
                storyIndex.Documents.Index(IndexBatch.Upload(stories));

                return true;
            }
            catch (IndexBatchException ex)
            {
                Logger.Error(
                    "Failed to index some of the documents: {0}",
                    String.Join(", ", ex.IndexingResults.Where(r => !r.Succeeded).Select(r => r.Key))
                );
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }

            return false;
        }

        /// <summary>
        /// Deletes the given biography from the search index.
        /// </summary>
        /// <param name="biography">The biography document to be deleted.</param>
        /// <returns>True on success; false otherwise.</returns>
        public bool DeleteBiography(Biography biography)
        {
            try
            {
                Logger.Write($"Deleting biography {biography.Accession} from search index...");
                var biographyIndex = _serviceClient.Indexes.GetClient(BiographyIndex);
                biographyIndex.Documents.Index(IndexBatch.Delete(new List<Biography> { biography }));

                return true;
            }
            catch (IndexBatchException)
            {
                Logger.Error($"Failed to delete biography {biography.Accession}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }

            return false;
        }

        /// <summary>
        /// Deletes the given biography and it's associated stories from the search index.
        /// </summary>
        /// <param name="biography">The biography document to be deleted.</param>
        /// <param name="stories">The list of stories (a.k.a segments) related to the given biography.</param>
        /// <returns>True on success; false otherwise.</returns>
        public bool DeleteBiography(Biography biography, List<Story> stories)
        {
            try
            {
                Logger.Write("Deleting stories from search index...");
                var storyIndex = _serviceClient.Indexes.GetClient(StoryIndex);
                storyIndex.Documents.Index(IndexBatch.Delete(stories));

                Logger.Write("Deleting biography from search index...");
                var biographyIndex = _serviceClient.Indexes.GetClient(BiographyIndex);
                biographyIndex.Documents.Index(IndexBatch.Delete(new List<Biography> { biography }));

                return true;
            }
            catch (IndexBatchException ex)
            {
                Logger.Error(
                    "Failed to delete some of the documents: {0}",
                    string.Join(", ", ex.IndexingResults.Where(r => !r.Succeeded).Select(r => r.Key))
                );
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }

            return false;
        }

        public bool DeleteStory(Story story)
        {
            try
            {
                Logger.Write($"Deleting story {story.StoryID} from search index...");
                var storyIndex = _serviceClient.Indexes.GetClient(StoryIndex);
                storyIndex.Documents.Index(IndexBatch.Delete( new List<Story> { story } ));

                return true;
            }
            catch (IndexBatchException)
            {
                Logger.Error($"Failed to delete biography {story.StoryID}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }

            return false;
        }

        /// <summary>
        /// Lists the documents in the Biography index.
        /// </summary>
        /// <returns>The next Biography in the Biography index.</returns>
        public IEnumerable<Biography> ListBiographies()
        {
            var biographyIndex = _serviceClient.Indexes.GetClient(BiographyIndex);

            var pageSize = 200;
            var currentPage = 1;

            SearchParameters sp = new SearchParameters()
            {
                SearchMode = SearchMode.All,
                SearchFields = new List<string>() { "lastName" },
                Top = 200,                
                IncludeTotalResultCount = true,
                Select = new List<string> { "biographyID", "accession" }
            };

            while (true)
            {
                sp.Skip = (currentPage - 1) * pageSize;

                var searchResults = biographyIndex.Documents.Search<Biography>("*", sp);

                // If no results returned then break of loop.
                if (searchResults.Results.Count == 0) break;

                foreach (var result in searchResults.Results)
                {
                    yield return result.Document;
                }

                // Advance to next page
                currentPage++;
            }
        }

        /// <summary>
        /// Lists the documents in the Story index.
        /// </summary>
        /// <returns>The next Story in the Story index.</returns>
        public IEnumerable<Story> ListStories()
        {
            var storyIndex = _serviceClient.Indexes.GetClient(StoryIndex);

            var pageSize = 200;

            SearchParameters sp = new SearchParameters()
            {
                SearchMode = SearchMode.All,
                SearchFields = new List<string> { "storyID" },
                Top = pageSize,
                OrderBy = new List<string> { "interviewDate" },
                IncludeTotalResultCount = true,
                Select = new List<string> { "storyID", "biographyID", "sessionOrder", "tapeOrder", "storyOrder", "interviewDate" }
            };

            // The Skip directive has a maximum allowable value of 100,000,
            // therefore the only way to page through datasets larger than
            // 100,000 (i.e. the current Digital Archive) is to partition
            // the data into smaller chunks using a filter such as on
            // InterviewDate.
            for (int year = 1993; year <= DateTime.Now.Year; year++)
            {
                sp.Filter = $"interviewDate ge {year}-01-01T00:00:00Z and interviewDate lt {year+1}-01-01T00:00:00Z";
                var currentPage = 1;

                while (true)
                {
                    sp.Skip = (currentPage - 1) * pageSize;

                    var searchResults = storyIndex.Documents.Search<Story>("*", sp);

                    // If no results returned then break of loop.
                    if (searchResults.Results.Count == 0) break;

                    foreach (var result in searchResults.Results)
                    {
                        yield return result.Document;
                    }

                    // Advance to next page
                    currentPage++;
                }
            }
        }


        #endregion =================             PUBLIC  METHODS             ====================

        #region ====================             PRIVATE METHODS             ====================
        /// <summary>
        /// Delete the given index.
        /// </summary>
        /// <param name="index">Name of index to delete.</param>
        private void DeleteIndex(string index)
        {
            if (_serviceClient.Indexes.Exists(index))
            {
                Logger.Write("Deleting Azure Search index: {0}", index);
                _serviceClient.Indexes.Delete(index);
            }
        }

        /// <summary>
        /// Create the Azure Search index that will hold the collection-level data.
        /// </summary>
        private void CreateBiographyIndex()
        {
            DeleteIndex(BiographyIndex);

            Logger.Write("Creating Azure Search index: {0}", BiographyIndex);

            var definition = new Index
            {
                Name = BiographyIndex,
                Fields = FieldBuilder.BuildForType<Biography>(),
                Suggesters = new[]
                {
                    new Suggester
                    {
                        Name = "biography_suggester",
                        // NOTE: As of April 2020, Azure Search Suggesters only support infix
                        // matching, and the current API eliminated the SuggesterSearchMode
                        // enumeration, therefore, the following line is commmented out.
                        // SearchMode = SuggesterSearchMode.AnalyzingInfixMatching,
                        SourceFields = new[] {"lastName", "preferredName"}
                    }
                }
            };

            _serviceClient.Indexes.Create(definition);
        }

        /// <summary>
        /// Create the Azure Search index that will hold the segment-level data.
        /// </summary>
        private void CreateStoryIndex()
        {
            DeleteIndex(StoryIndex);

            Logger.Write("Creating Azure Search index: {0}", StoryIndex);

            var definition = new Index
            {
                Name = StoryIndex,
                Fields = FieldBuilder.BuildForType<Story>()
            };

            _serviceClient.Indexes.Create(definition);
        }
        #endregion =================             PRIVATE METHODS             ====================
    }
}
