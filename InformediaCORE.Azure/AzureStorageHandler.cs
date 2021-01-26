using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using InformediaCORE.Azure.Models;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace InformediaCORE.Azure
{
    public class AzureStorageHandler
    {
        #region ====================               DECLARATIONS              ====================
        /// <summary>
        /// Storage container for JSON data
        /// </summary>
        private const string DataContainer = "data";

        /// <summary>
        /// Storage container for image and video media.
        /// </summary>
        private const string MediaContainer = "media";        

        /// <summary>
        /// Cached reference to the Azure storage account.
        /// </summary>
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly CloudStorageAccount _storageAccount;

        /// <summary>
        /// Cached reference to the Azure blob storage client.
        /// </summary>
        private readonly CloudBlobClient _blobClient;

        /// <summary>
        /// Possible values returned by upload methods.
        /// </summary>
        public enum StatusCode
        {
            Failed = -1,
            Skipped = 0,
            Success = 1
        }

        /// <summary>
        /// Determines if logging output will be verbose or limited to just error output.
        /// </summary>
        public bool Verbose { get; set; } = true;
        #endregion =================               DECLARATIONS              ====================

        #region ====================               CONSTRUCTOR               ====================
        /// <summary>
        /// Instantiates and instance of the Azure storage handler using the configured storage account.
        /// </summary>
        public AzureStorageHandler() : this(Settings.Current.Processing.AzureStorageAccountName, Settings.Current.Processing.AzureStorageAccountKey) { }

        /// <summary>
        /// Instantiates an instance of the Azure storage handler.
        /// </summary>
        /// <param name="azureStorageAccountName">Name of the Azure Storage account.</param>
        /// <param name="azureStorageAccountKey">The Account Key associated with the account.</param>
        public AzureStorageHandler(string azureStorageAccountName, string azureStorageAccountKey)
        {
            var azureStorageConnectionString =
                $"DefaultEndpointsProtocol=https;AccountName={azureStorageAccountName};AccountKey={azureStorageAccountKey}";

            // Initialize the connection to the Azure storage account
            _storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);

            // Initialize the blob storage client.
            _blobClient = _storageAccount.CreateCloudBlobClient();
        }
        #endregion =================               CONSTRUCTOR               ====================

        #region ====================             PUBLIC  METHODS             ====================

        #region ===== BIOGRAPHY DETAILS
        /// <summary>
        /// Return the BiographDetails document for the given accession identifier.
        /// </summary>
        /// <param name="accession">Biography accession identifier.</param>
        /// <returns>A fully </returns>
        public BiographyDetails GetBiographyDetails(string accession)
        {
            var blobName = $"biography/details/{accession}";
            var json = GetBlobAsText(DataContainer, blobName);

            return JsonConvert.DeserializeObject<BiographyDetails>(json);
        }

        /// <summary>
        /// Return the BlockBlob contentMD5 property value fro the given BiographyDetails document.
        /// </summary>
        /// <param name="accession">Biography accession is equivalent to the database Collection.Accession</param>
        /// <returns>Base64 MD5 checksum.</returns>
        public string GetBiographyDetailsMD5(string accession)
        {
            return GetBlobMD5(DataContainer, $"biography/details/{accession}");
        }

        /// <summary>
        /// Uploads a BiographyDetails JSON object to Azure Storage
        /// </summary>
        /// <param name="biographyDetails">The BiographyDetails instance to upload.</param>
        /// <returns>A status code indication the outcome of the action.</returns>
        public StatusCode UploadBiographyDetails(BiographyDetails biographyDetails)
        {
            var jsonSerializerSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var json = JsonConvert.SerializeObject(biographyDetails, Formatting.None, jsonSerializerSettings);

            var bytes = Encoding.UTF8.GetBytes(json);

            var blobName = $"biography/details/{biographyDetails.Accession}";
            return UploadCloudBlob(DataContainer, blobName, "application/json", bytes);
        }

        /// <summary>
        /// Deletes the specified BiographyDetails document from Azure Storage
        /// </summary>
        /// <param name="accession">Biography accession is equivalent to database Collection.Accession</param>
        public void DeleteBiographyDetails(string accession)
        {
            var blobName = $"biography/details/{accession}";
            DeleteCloudBlob(DataContainer, blobName);
        }
        #endregion == BIOGRAPHY DETAILS

        #region ===== BIOGRAPHY IMAGE
        /// <summary>
        /// Return the BlockBlob contentMD5 property value for the given Biography's portrait image.
        /// </summary>
        /// <param name="biographyID">Biography ID is equivalent to database CollectionID</param>
        /// <returns>Base64 MD5 checksum.</returns>
        public string GetBiographyImageMD5(int biographyID)
        {
            return GetBlobMD5(MediaContainer, $"biography/image/{biographyID}");
        }

        /// <summary>
        /// Uploads the given image to the configured Azure storage account.
        /// </summary>
        /// <param name="biographyID">The biography identifier for the image.</param>
        /// <param name="fileType">Three character file extension indicating type of image data. ex: jpg</param>
        /// <param name="image">A byte array containing the image data.</param>
        /// <remarks>Supports JPEG (.jpg) and PNG (.png) formats only.</remarks>
        public StatusCode UploadBiographyImage(string biographyID, string fileType, byte[] image)
        {
            var blobName = $"biography/image/{biographyID}";
            return UploadCloudBlob(MediaContainer, blobName, GetContentType(fileType), image);
        }

        /// <summary>
        /// Uploads the given image to the configured Azure storage account.
        /// </summary>
        /// <param name="biographyID">Numeric ID of the biography owning the image.</param>
        /// <param name="fileType">Three character file extension indicating type of image data. ex: jpg</param>
        /// <param name="stream">A stream containing the image data.</param>
        /// <remarks>Supports JPEG (.jpg) and PNG (.png) formats only.</remarks>
        public StatusCode UploadBiographyImage(int biographyID, string fileType, Stream stream)
        {
            var blobName = $"biography/{biographyID}";
            return UploadCloudBlob(MediaContainer, blobName, GetContentType(fileType), stream);
        }

        /// <summary>
        /// Deletes the specified biography image from the configured Azure Storage account.
        /// </summary>
        /// <param name="biographyID"></param>
        public void DeleteBiographyImage(string biographyID)
        {
            var blobName = $"biography/image/{biographyID}";
            DeleteCloudBlob(MediaContainer, blobName);
        }
        #endregion == BIOGRAPHY IMAGE

        #region ===== STORY DETAILS
        /// <summary>
        /// Return the BlockBlob contentMD5 property value fro the given StoryDetails document.
        /// </summary>
        /// <param name="storyID">Story ID is equivalent to the database SegmentID</param>
        /// <returns>Base64 MD5 checksum.</returns>
        public string GetStoryDetailsMD5(int storyID)
        {
            return GetBlobMD5(DataContainer, $"story/details/{storyID}");
        }

        /// <summary>
        /// Uploads a StoryDetails JSON object to Azure Storage
        /// </summary>
        /// <param name="storyDetails">The StoryDetails instance to upload.</param>
        /// <returns>A status code indication the outcome of the action.</returns>
        public StatusCode UploadStoryDetails(StoryDetails storyDetails)
        {
            var jsonSerializerSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var json = JsonConvert.SerializeObject(storyDetails, Formatting.None, jsonSerializerSettings);

            var bytes = Encoding.UTF8.GetBytes(json);

            var blobName = $"story/details/{storyDetails.StoryID}";
            return UploadCloudBlob(DataContainer, blobName, "application/json", bytes);
        }

        /// <summary>
        /// Deletes the specified story details document from the configured Azure Storage account.
        /// </summary>
        /// <param name="storyID">Story ID is equivalent to the database SegmentID</param>
        public void DeleteStoryDetails(string storyID)
        {
            var blobName = $"story/details/{storyID}";
            DeleteCloudBlob(DataContainer, blobName);
        }
        #endregion == STORY DETAILS

        #region ===== STORY IMAGE
        /// <summary>
        /// Return the BlockBlob contentMD5 property value for the given Story's keyframe image.
        /// </summary>
        /// <param name="storyID">Story ID is equivalent to database SegmentID</param>
        /// <returns>Base64 MD5 checksum.</returns>
        public string GetStoryImageMD5(int storyID)
        {
            return GetBlobMD5(MediaContainer, $"story/image/{storyID}");
        }

        /// <summary>
        /// Uploads the given image to the configured Azure storage account.
        /// </summary>
        /// <param name="storyID">The story identifier for the image.</param>
        /// <param name="fileType">Three character file extension indicating type of image data. ex: jpg</param>
        /// <param name="image">A byte array containing the image data.</param>
        /// <remarks>Supports JPEG (.jpg) and PNG (.png) formats only.</remarks>
        public StatusCode UploadStoryImage(string storyID, string fileType, byte[] image)
        {
            var blobName = $"story/image/{storyID}";
            return UploadCloudBlob(MediaContainer, blobName, GetContentType("jpg"), image);
        }

        /// <summary>
        /// Uploads the given image to the configured Azure storage account.
        /// </summary>
        /// <param name="storyID">Numeric ID of the story owning the image.</param>
        /// <param name="stream">A stream containing the image data.</param>
        /// <remarks>It is assumed all keyframes are JPEG (.jpg) images as they are generated by processing scripts.</remarks>
        public StatusCode UploadStoryImage(int storyID, Stream stream)
        {
            var blobName = $"story/image/{storyID}";
            return UploadCloudBlob(MediaContainer, blobName, GetContentType("jpg"), stream);
        }

        public void DeleteStoryImage(string storyID)
        {
            var blobName = $"story/image/{storyID}";
            DeleteCloudBlob(MediaContainer, blobName);
        }
        #endregion ===== STORY IMAGE

        #region ===== STORY VIDEO
        /// <summary>
        /// Return the BlockBlob contentMD5 property value for the given Story's video.
        /// </summary>
        /// <param name="storyID">Equivalent to database SegmentID.</param>
        /// <returns>Base64 MD5 checksum.</returns>
        public string GetStoryVideoMD5(int storyID)
        {
            return GetBlobMD5(MediaContainer, $"story/video/{storyID}");
        }

        /// <summary>
        /// Uploads the given video file to the configured Azure storage account.
        /// </summary>
        /// <param name="storyID">Equivalent to database SegmentID.</param>
        /// <param name="mediaPath">The fully qualified path to the video.</param>
        /// <remarks>File assumed to be in MPEG4 (.mp4) format as they are generate by processing scripts.</remarks>
        public StatusCode UploadStoryVideo(string storyID, string mediaPath)
        {
            var blobName = $"story/video/{storyID}";
            return UploadCloudBlob(MediaContainer, blobName, GetContentType("mp4"), mediaPath);
        }

        /// <summary>
        /// Deletes the specified video from the configured Azure Storage account.
        /// </summary>
        /// <param name="storyID">Equivalent to database SegmentID.</param>
        public void DeleteStoryVideo(string storyID)
        {
            var blobName = $"story/video/{storyID}";
            DeleteCloudBlob(MediaContainer, blobName);
        }
        #endregion == STORY VIDEO

        #region ===== STORY CAPTIONS
        /// <summary>
        /// Return the BlockBlob contentMD5 property value for the given Story's captions.
        /// </summary>
        /// <param name="storyID">Equivalent to database SegmentID</param>
        /// <returns>Base64 MD5 checksum.</returns>
        public string GetStoryCaptionsMD5(int storyID)
        {
            return GetBlobMD5(MediaContainer, $"story/captions/{storyID}");
        }

        /// <summary>
        /// Uploads the given captions file to the configured Azure storage account.
        /// </summary>
        /// <param name="storyID">Equivalent to database SegmentID.</param>
        /// <param name="vttPath"></param>
        /// <returns>The fully qualified path to the vtt file.</returns>
        /// <remarks>File assumed to be in WebVTT format as they are generate by processing scripts.</remarks>
        public StatusCode UploadStoryCaptions(string storyID, string vttPath)
        {
            var blobName = $"story/captions/{storyID}";
            return UploadCloudBlob(MediaContainer, blobName, GetContentType("vtt"), vttPath);
        }

        /// <summary>
        /// Deletes the specified captions from the configured Azure Storage account.
        /// </summary>
        /// <param name="storyID">Equivalent to database SegmentID.</param>
        public void DeleteStoryCaptions(string storyID)
        {
            var blobName = $"story/captions/{storyID}";
            DeleteCloudBlob(MediaContainer, blobName);
        }
        #endregion == STORY CAPTIONS

        #region ===== BLOB
        /// <summary>
        /// Deletes the blob specified by the given relative URI
        /// </summary>
        /// <param name="blobUri">In form: container/data|media/biography|story/id</param>
        public void DeleteBlob(string blobUri)
        {
            var parts = blobUri.Split('/');

            if (parts.Length != 4)
            {
                Logger.Warning($"DeleteBlob: '{blobUri}' is an invalid URI. Nothing was deleted.");
                return;
            }

            DeleteCloudBlob(parts[0], $"{parts[1]}/{parts[2]}/{parts[3]}");
        }
        #endregion === BLOB

        #region ===== ENUMERATORS
        /// <summary>
        /// Lists the blobs in Data container
        /// </summary>
        /// <returns>The relative URI to the next blob in the container.</returns>
        public IEnumerable<string> ListDataBlobs()
        {
            var container = _blobClient.GetContainerReference(DataContainer);

            foreach (var blobItem in container.ListBlobs(useFlatBlobListing: true))
            {
                yield return blobItem.Container.Uri.MakeRelativeUri(blobItem.Uri).ToString();
            }
        }

        /// <summary>
        /// Lists the blobs in the Media container
        /// </summary>
        /// <returns>The relative URI to the next blob in the container.</returns>
        public IEnumerable<string> ListMediaBlobs()
        {
            var container = _blobClient.GetContainerReference(MediaContainer);

            foreach (var blobItem in container.ListBlobs(useFlatBlobListing: true))
            {
                yield return blobItem.Container.Uri.MakeRelativeUri(blobItem.Uri).ToString();
                    
            }
        }
        #endregion == ENUMERATORS

        /// <summary>
        /// Initializes the storage service by creating the DATA and MEDIA containers with
        /// their respective access levels.
        /// 
        /// WARNING: CALLING THIS METHOD WILL RESULT IN THE LOSS OF ALL PRE-EXISTING BLOB DATA!!
        /// </summary>
        public void InitializeStorageService()
        {
            Logger.Write("Initializing Azure Storage Service...");

            // Fix: Seekbar not working in Chrome
            // https://stackoverflow.com/questions/21032796/seekbar-not-working-in-chrome
            // We need to set the default service version to 2013-08-15 or later
            var serviceProperties = _blobClient.GetServiceProperties();
            serviceProperties.DefaultServiceVersion = "2019-02-02";
            _blobClient.SetServiceProperties(serviceProperties);
            Logger.Write("Set default service version to ''");

            var waitRequired = false;
            var dataContainer = _blobClient.GetContainerReference(DataContainer);
            if (dataContainer.Exists())
            {
                Logger.Write("Deleting 'DATA' container.");
                dataContainer.Delete();
                waitRequired = true;
            }

            var mediaContainer = _blobClient.GetContainerReference(MediaContainer);
            if (mediaContainer.Exists())
            {
                Logger.Write("Deleting 'MEDIA' container.");
                mediaContainer.Delete();
                waitRequired = true;
            }

            /*
             * Waiting for Godot...
             * Once you delete the container you have to WAIT AT LEAST 30 SECONDS before
             * you can create a new container with the same name otherwise the API will
             * throw an exception.
             * 
             * SEE: https://docs.microsoft.com/en-us/rest/api/storageservices/fileservices/delete-container
             */
            var waitSeconds = 30;
            var keepTrying = true;

            while (keepTrying)
            {
                if (waitRequired)
                {
                    Logger.Write("Waiting {0} seconds for container deletion to complete...", waitSeconds);
                    Thread.Sleep(waitSeconds * 1000);
                }
                try
                {
                    if (dataContainer.CreateIfNotExists(BlobContainerPublicAccessType.Off))
                    {
                        Logger.Write("'DATA' container created.");
                    }
                    if (mediaContainer.CreateIfNotExists(BlobContainerPublicAccessType.Blob))
                    {
                        Logger.Write("'MEDIA' container created.");
                    }
                    keepTrying = false;
                }
                catch (StorageException ex)
                {
                    if (ex.RequestInformation.HttpStatusCode == 409)
                    {
                        Logger.Write("Delete operations still pending, additional wait time required.");
                        waitRequired = true;
                        waitSeconds = 10;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
        #endregion =================             PUBLIC  METHODS             ====================

        #region ====================           PRIVATE FUNCTIONS             ====================
        /// <summary>
        /// Fetch the specified BlockBlob as a text string.
        /// </summary>
        /// <param name="containerName">Name of blob storage container.</param>
        /// <param name="blobName">Name of blob.</param>
        /// <returns>The blob contents as a string.</returns>
        private string GetBlobAsText(string containerName, string blobName)
        {
            var container = _blobClient.GetContainerReference(containerName);
            var blockBlob = container.GetBlockBlobReference(blobName);

            if (blockBlob.Exists())
            {
                return blockBlob.DownloadText();
            }
            return string.Empty;
        }

        /// <summary>
        /// Return the contentMD5 property of the specified BlockBlob
        /// </summary>
        /// <param name="containerName">Name of blob storage container.</param>
        /// <param name="blobName">Name of blob.</param>
        /// <returns>The blob's MD5 checksum if it exists; string.empty otherwise.</returns>
        private string GetBlobMD5(string containerName, string blobName)
        {
            var container = _blobClient.GetContainerReference(containerName);
            var blockBlob = container.GetBlockBlobReference(blobName);

            if (blockBlob.Exists())
            {
                return blockBlob.Properties.ContentMD5;
            }
            return string.Empty;
        }  
             
        /// <summary>
        /// Returns the IETF Media Type and Subtype for for the given file extension.
        /// </summary>
        /// <param name="fileType">The three character file extension of the original content.</param>
        /// <returns>A media type (aka MIME type, aka content type) string.</returns>
        /// <remarks>
        /// SEE: http://www.iana.org/assignments/media-types/media-types.xhtml
        /// </remarks>
        private string GetContentType(string fileType)
        {
            switch (fileType.ToLower())
            {
                case "gif":
                    return "image/gif";
                case "jpg":
                    return "image/jpeg";
                case "png":
                    return "image/png";
                case "mp4":
                    return "video/mp4";
                case "vtt":
                    return "text/vtt";
                default:
                    return "application/octet-stream";
            }
        }

        /// <summary>
        /// Iterates over the list of blobs in the specified container.
        /// </summary>
        /// <param name="containerName">Name of blob storage container.</param>
        /// <returns>The next IListBlobItem in the container's blob collection.</returns>
        private IEnumerable<IListBlobItem> ListContainerBlobs(string containerName)
        {
            var container = _blobClient.GetContainerReference(containerName);

            foreach (var blobItem in container.ListBlobs(useFlatBlobListing: true))
            {
                yield return blobItem;
            }
        }

        /// <summary>
        /// Uploads the stream data to the given Azure blob storage container
        /// </summary>
        /// <param name="containerName">Name of an existing Azure blob storage container.</param>
        /// <param name="blobName">Name of the blob to be created or overwritten.</param>
        /// <param name="mimeType">Mime type of the data being uploaded.</param>
        /// <param name="stream">The the stream data to be uploaded.</param>
        /// <returns>A value indicating the result of the operation.</returns>
        private StatusCode UploadCloudBlob(string containerName, string blobName, string mimeType, Stream stream)
        {
            var container = _blobClient.GetContainerReference(containerName);

            if (Verbose) Logger.Write("Uploading {0}/{1} ...", containerName, blobName);

            var blockBlob = container.GetBlockBlobReference(blobName);

            var expectedMD5 = GetMD5(stream);

            if (blockBlob.Exists() && blockBlob.Properties.ContentMD5 == expectedMD5 )
            {
                if (Verbose) Logger.Write("  ...skipping upload, data exists and checksums match.");
                return StatusCode.Skipped;
            }
            stream.Position = 0;
            blockBlob.UploadFromStream(stream);
            blockBlob.Properties.ContentType = mimeType;
            blockBlob.SetProperties();

            if (blockBlob.Properties.ContentMD5 != expectedMD5)
            {
                Logger.Error("  Checksum mismatch for {0}/{1}", containerName, blobName);
                return StatusCode.Failed;
            }

            if (Verbose) Logger.Write("  Primary URI: {0}", blockBlob.StorageUri.PrimaryUri);
            if (Verbose) Logger.Write("  ContentType: {0}", blockBlob.Properties.ContentType);

            return StatusCode.Success;
        }

        /// <summary>
        /// Uploads a byte array to the given Azure blob storage container.
        /// </summary>
        /// <param name="containerName">Name of an existing Azure blob storage container.</param>
        /// <param name="blobName">Name of the blob to be created or overwritten.</param>
        /// <param name="mimeType">Mime type of the data being uploaded.</param>
        /// <param name="data">The byte array to be uploaded.</param>
        /// <returns>A value indicating the result of the operation.</returns>
        private StatusCode UploadCloudBlob(string containerName, string blobName, string mimeType, byte[] data)
        {
            using (var stream = new MemoryStream(data))
            { 
                return UploadCloudBlob(containerName, blobName, mimeType, stream);
            }
        }

        /// <summary>
        /// Uploads a file to to the given Azure blob storage container.
        /// </summary>
        /// <param name="containerName">Name of an existing Azure blob storage container.</param>
        /// <param name="blobName">Name of the blob to be created or overwritten.</param>
        /// <param name="mimeType">Mime type of the data being uploaded.</param>
        /// <param name="filename">The fully qualified path to the file.</param>
        /// <returns>A value indicating the result of the operation.</returns>
        private StatusCode UploadCloudBlob(string containerName, string blobName, string mimeType, string filename)
        {
            if (File.Exists(filename))
            {
                using (var stream = File.OpenRead(filename))
                {
                    return UploadCloudBlob(containerName, blobName, mimeType, stream);
                }
            }
            Logger.Error("UploadCloudBlob failed. Specified file does not exist: {0}", filename);
            return StatusCode.Failed;
        }

        /// <summary>
        /// Deletes a blob from the specified container.
        /// </summary>
        /// <param name="containerName">Name of an existing Azure blob storage container.</param>
        /// <param name="blobName">Name of the blob to be deleted.</param>
        private void DeleteCloudBlob(string containerName, string blobName)
        {
            var container = _blobClient.GetContainerReference(containerName);

            if (Verbose) Logger.Write("Deleting {0}/{1} ...", containerName, blobName);

            var blockBlob = container.GetBlockBlobReference(blobName);

            blockBlob.DeleteIfExists();
        }

        /// <summary>
        /// Get the MD5 checksum for the contents of a stream.
        /// </summary>
        /// <param name="data">A binary data stream.</param>
        /// <returns>Base64 encoded MD5 hash.</returns>
        private string GetMD5(Stream data)
        {
            using (var md5 = MD5.Create())
            {
                data.Position = 0;

                // Azure stores Blob MD5 as Base64 encoded string
                return Convert.ToBase64String(md5.ComputeHash(data));
            }
        }
        #endregion =================           PRIVATE FUNCTIONS             ====================
    }
}
