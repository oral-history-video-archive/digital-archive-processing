using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using InformediaCORE.Common.Database;

namespace InformediaCORE.Firebase.Database
{
    class DataAccessExtended : DataAccess
    {
        /// <summary>
        /// Retrieve an order list of segment id's belonging to the given collection.
        /// </summary>
        /// <param name="collectionID">Database id for the collection.</param>
        /// <returns>An array of segmentIDs</returns>
        public int[] GetSegmentIDsForAccession(string accession)
        {
            using (var context = GetNonTrackingDataContext())
            {
                return (from s in context.Segments
                        where s.Collection.Accession == accession
                        orderby s.Session.SessionOrder, s.Movie.Tape, s.SegmentOrder
                        select s.SegmentID).ToArray();
            }
        }

        /// <summary>
        /// Deletes all THM*NEW (semantic tag) annotations related to the given segment.
        /// </summary>
        /// <param name="segmentID">The numeric id of the owning segment.</param>
        public int DeleteSegmentTagAnnotations(int segmentID)
        {
            using (var context = GetTrackingDataContext())
            {
                var typeID = GetAnnotationTypeID("THM*NEW");

                // Find the Annotation specified by the given ID
                var tags = (from a in context.Annotations
                            where a.SegmentID == segmentID
                               && a.TypeID == typeID
                            select a).ToList();

                // Add each Annotation to the changeset for deletion
                foreach (var tag in tags)
                {
                    context.Annotations.DeleteOnSubmit(tag);
                }

                // Bulk delete
                context.SubmitChanges();

                return tags.Count();
            }
        }

        /// <summary>
        /// Get the tag import status for the given segment.
        /// </summary>
        /// <param name="segmentID">Numeric id of the owning segment.</param>
        /// <returns>The existing TagImportStatus if it exists; a new TagImportStatus otherwise.</returns>
        public TagImportStatus GetTagImportStatus(int segmentID)
        {
            using (var context = GetNonTrackingDataContext())
            {
                var tagImportStatus = (from tus in context.TagImportStatus
                                       where tus.SegmentID == segmentID
                                       select tus).SingleOrDefault();

                if (tagImportStatus == null)
                {
                    tagImportStatus = new TagImportStatus
                    {
                        SegmentID = segmentID,
                        FirebaseTimestamp = 0,
                        LastStatus = null,
                        LastUpdated = null
                    };
                }

                tagImportStatus.LastChecked = DateTime.Now;

                return tagImportStatus;
            }
        }

        /// <summary>
        /// Update the tag import status for the related segment.
        /// </summary>
        /// <param name="updatedStatus">A valid TagImportStatus instance.</param>
        public void UpdateTagImportStatus(TagImportStatus updatedStatus)
        {
            using (var context = GetTrackingDataContext())
            {
                var currentStatus = (from tus in context.TagImportStatus
                                     where tus.SegmentID == updatedStatus.SegmentID
                                     select tus).SingleOrDefault();

                if (currentStatus == null)
                {
                    context.TagImportStatus.InsertOnSubmit(updatedStatus);
                }
                else
                {
                    currentStatus.FirebaseTimestamp = updatedStatus.FirebaseTimestamp;
                    currentStatus.LastChecked = updatedStatus.LastChecked;
                    currentStatus.LastStatus = updatedStatus.LastStatus;
                    currentStatus.LastUpdated = updatedStatus.LastUpdated;
                }

                context.SubmitChanges();
            }
        }
    }
}
