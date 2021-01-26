using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Firebase.Database;
using InformediaCORE.Firebase.Models;

using Newtonsoft.Json;

namespace InformediaCORE.Firebase
{
    public static class TagImporter
    {
        /// <summary>
        /// Base URL to the Firebase service containing the tag database.
        /// </summary>
        private static readonly Uri FIREBASE_URI = new Uri(Settings.Current.ExternalTools.FirebaseURL);

        /// <summary>
        /// Imports the tags for the given given segment.
        /// </summary>
        /// <param name="segmentID">Database ID of a segment.</param>
        public static void ImportTagsForSegment(int segmentID)
        {
            var da = new DataAccessExtended();
            TagImportStatus tagImportStatus = da.GetTagImportStatus(segmentID);

            Logger.Write("Retrieving tags for segment {0} from Firebase...", segmentID);
            Uri requestURI = new Uri(FIREBASE_URI, $"gs/{segmentID}.json");
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync(requestURI).Result;

            if (response.IsSuccessStatusCode)
            {
                Logger.Write("...response received.");
                var responseContent = response.Content;

                // Calling .Result forces the call to be synchronous
                var resultString = responseContent.ReadAsStringAsync().Result;
                var firebaseRecord = JsonConvert.DeserializeObject<FirebaseRecord>(resultString);

                if (firebaseRecord == null)
                {
                    Logger.Warning("Firebase returned null record, import cannot proceed.");
                    tagImportStatus.LastStatus = "Firebase returned null.";
                }
                else
                {
                    if (tagImportStatus.FirebaseTimestamp == firebaseRecord.LastModified)
                    {
                        Logger.Write("Tags are current, no update required.");
                        tagImportStatus.LastStatus = "Success - no update required.";
                    }
                    else
                    {
                        Logger.Write("Tags are out of date, proceeding with update...");
                        UpdateSegmentTags(segmentID, firebaseRecord.Tags);

                        tagImportStatus.LastStatus = "Success - tags updated.";
                        tagImportStatus.LastUpdated = tagImportStatus.LastChecked;
                        tagImportStatus.FirebaseTimestamp = firebaseRecord.LastModified;
                    }
                }
            }
            else
            {
                string status = $"{(int)response.StatusCode} - {response.ReasonPhrase}";
                Logger.Warning("Error contacting Firebase: {0}", status);
                tagImportStatus.LastStatus = Utilities.Truncate(status, 32);
            }

            da.UpdateTagImportStatus(tagImportStatus);
            Logger.Write("TagImportStatus: {0}", tagImportStatus.LastStatus);
        }

        /// <summary>
        /// Imports the tags for all segments related to the given accession.
        /// </summary>
        /// <param name="accession"></param>
        public static void ImportTagsForCollection(string accession)
        {
            Logger.Write("Retrieving tags for accession {0} from Firebase...", accession);

            DateTime timeNow = DateTime.Now;
            string callStatus = "OK";

            Uri requestURI = new Uri(FIREBASE_URI, $"gs.json?orderBy=%22accession%22&startAt=%22{accession}%22&endAt=%22{accession}%22");            
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync(requestURI).Result;

            Dictionary<string, FirebaseRecord> firebaseRecords;
            if (response.IsSuccessStatusCode)
            {
                Logger.Write("...response recevied.");
                var responseContent = response.Content;

                // Calling .Result forces the call to be synchronous
                var resultString = responseContent.ReadAsStringAsync().Result;
                firebaseRecords = JsonConvert.DeserializeObject<Dictionary<string, FirebaseRecord>>(resultString);
            }
            else
            {
                callStatus = $"{(int)response.StatusCode} - {response.ReasonPhrase}";
                Logger.Warning("Error contacting Firebase: {0}", callStatus);
                firebaseRecords = null;
            }

            // Update Segment Status regardless of whether 

            var da = new DataAccessExtended();
            var segmentIDs = da.GetSegmentIDsForAccession(accession);

            foreach(var segmentID in segmentIDs)
            {
                Logger.Write("Updating Segment {0} ...", segmentID);
                TagImportStatus tagImportStatus = da.GetTagImportStatus(segmentID);
                tagImportStatus.LastChecked = timeNow;

                if (!response.IsSuccessStatusCode)
                {
                    tagImportStatus.LastStatus = callStatus;
                }
                else if(firebaseRecords == null)
                {
                    tagImportStatus.LastStatus = "Firebase returned null for accession.";
                }
                else if (firebaseRecords.ContainsKey(segmentID.ToString()))
                {
                    var firebaseRecord = firebaseRecords[segmentID.ToString()];
                    if (tagImportStatus.FirebaseTimestamp == firebaseRecord.LastModified)
                    {
                        tagImportStatus.LastStatus = "Success - no update required.";
                    }
                    else
                    {
                        UpdateSegmentTags(segmentID, firebaseRecord.Tags);

                        tagImportStatus.LastStatus = "Success - tags updated.";
                        tagImportStatus.LastUpdated = tagImportStatus.LastChecked;
                        tagImportStatus.FirebaseTimestamp = firebaseRecord.LastModified;
                    }
                }
                else
                {
                    tagImportStatus.LastStatus = "Firebase returned null for segment.";
                }

                Logger.Write("... {0}", tagImportStatus.LastStatus);

                tagImportStatus.LastStatus = Utilities.Truncate(tagImportStatus.LastStatus, 128);
                da.UpdateTagImportStatus(tagImportStatus);
            }
  
        }

        /// <summary>
        /// Replaces all existing tags with the given list of tags for the specified segment.
        /// </summary>
        /// <param name="segmentID">Database id of the segment to be updated.</param>
        /// <param name="tags">A list of semantic tags.</param>
        /// <returns>Number of tags inserted.</returns>
        private static void UpdateSegmentTags(int segmentID, string[] tags)
        {
            var da = new DataAccessExtended();

            // Old tags are deleted whether new tags exist or not.
            var deleted = da.DeleteSegmentTagAnnotations(segmentID);

            if (deleted > 0)
            {
                Logger.Write("... {0} prior tag annotations deleted for segment {1}.", deleted, segmentID);
            }
            else
            {
                Logger.Write("... no prior tag annotations exist for segment {0}.", segmentID);
            }

            if (tags == null || tags.Count() == 0)
            {
                Logger.Write("... no tags to import, operation complete.");
            }
            else
            {
                var typeID = da.GetAnnotationTypeID("THM*NEW");

                foreach (var tag in tags)
                {
                    Logger.Write("... inserting new tag annotation '{0}'", tag);
                    da.InsertAnnotation(new Annotation
                    {
                        SegmentID = segmentID,
                        TypeID = typeID,
                        Value = tag
                    });
                }

                Logger.Write("... {0} tags imported successfully.", tags.Count());
            }
        }
    }
}
