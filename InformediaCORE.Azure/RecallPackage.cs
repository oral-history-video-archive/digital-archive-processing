using System;
using System.Collections.Generic;
using System.Linq;
using InformediaCORE.Azure.Models;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace InformediaCORE.Azure
{
    /// <summary>
    /// Generates all data necessary to recall a collection from the digital archive.
    /// </summary>
    public class RecallPackage
    {
        #region ====================          PRIVATE  DECLARATIONS          ====================
        /// <summary>
        /// A list of the session ordinals affected by the recall.
        /// </summary>
        private List<int> _sessionOrdinals;
        #endregion =================          PRIVATE  DECLARATIONS          ====================

        #region ====================            PUBLIC PROPERTIES            ====================
        /// <summary>
        /// Gets teh Biography document to be recalled.
        /// </summary>
        public readonly Biography Biography;

        /// <summary>
        /// Gets the list of Story documents to be recalled.
        /// </summary>
        public readonly List<Story> Stories;

        /// <summary>
        /// Gets the accession identifier for the loaded Collection.
        /// </summary>
        public string Accession
        {
            get { return Biography.Accession; }
        }
        #endregion =================            PUBLIC PROPERTIES            ====================

        /// <summary>
        /// Instantiates an instance of the RecallPackage class.
        /// </summary>
        /// <param name="accession">A collection accession number.</param>
        public RecallPackage(string accession)
        {
            BiographyDetails details = new BiographyDetails();

            Logger.Write("Initializing recall package for collection {0}...", accession);

            using (var context = new IDVLDataContext(Settings.Current.ConnectionString))
            {
                var collection = 
                    (from c in context.Collections
                     where c.Accession == accession
                     select c).FirstOrDefault();

                if (collection == null)
                {
                    throw new RecallPackageException("Invalid collection name, unable to initialize RecallPackage.");
                }

                _sessionOrdinals = collection.Sessions.Select(s => s.SessionOrder).ToList();

                Biography = new Biography
                {
                    Accession = collection.Accession,
                    BiographyID = collection.CollectionID.ToString()                    
                };

                Stories =
                    (from s in context.Segments
                     where s.CollectionID == collection.CollectionID
                     select new Story { StoryID = s.SegmentID.ToString() }).ToList();
            }
        }

        /// <summary>
        /// Instantiates a RecallPackage from the given BiographyDetails.
        /// </summary>
        /// <param name="biographyDetails">The BiographyDetails of the collection to be deleted.</param>
        public RecallPackage(BiographyDetails biographyDetails)
        {
            Biography = new Biography
            {
                Accession = biographyDetails.Accession,
                BiographyID = biographyDetails.BiographyID
            };

            _sessionOrdinals = new List<int>();
            Stories = new List<Story>();
            foreach (var session in biographyDetails.Sessions)
            {
                _sessionOrdinals.Add(session.SessionOrder ?? 0);
                foreach (var tape in session.Tapes)
                {
                    foreach (var story in tape.Stories)
                    {
                        Stories.Add(new Story { StoryID = story.StoryID });
                    }
                }
            }
        }

        /// <summary>
        /// Demotes the publishing Phase of the package's collection and session(s) to
        /// the given phase as needed.
        /// </summary>
        /// <param name="newPhase">An enum specifying the new publishing phase.</param>
        public void DemotePublishingPhase(PublishingPhase newPhase)
        {
            var collectionPhase = DataAccess.GetCollectionPublishingPhase(Accession);

            // Demote Collection publishing phase as needed
            if (collectionPhase.Compare(newPhase) > 0)
            {
                Logger.Write($"Demoting collection {Accession} publishing phase from {collectionPhase} to {newPhase}...");
                DataAccess.SetCollectionPublishingPhase(Accession, newPhase);
                Logger.Write("  ...collection publishing phase demoted successfully.");
            }

            // Promote Session publishing phase as needed
            foreach (var sessionOrder in _sessionOrdinals)
            {
                var sessionPhase = DataAccess.GetSessionPublishingPhase(Accession, sessionOrder);

                if (sessionPhase.Compare(newPhase) > 0)
                {
                    Logger.Write($"Demoting {Accession} session #{sessionOrder} publishing phase from {sessionPhase} to {newPhase}...");
                    DataAccess.SetSessionPublishingPhase(Accession, sessionOrder, newPhase);
                    Logger.Write("  ...session publishing phase demoted successfully.");
                }
            }
        }
    }

    /// <summary>
    /// Represents errors specific to the RecallPackage class.
    /// </summary>
    public class RecallPackageException : Exception
    {
        public RecallPackageException() { }

        public RecallPackageException(string message) : base(message) { }
    }
}
