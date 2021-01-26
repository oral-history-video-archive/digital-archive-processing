using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing;
using InformediaCORE.Processing.Database;

namespace InformediaCORE.DatabaseEditor
{
    /// <summary>
    /// Extends the InformediaCORE.Common.Database.DataAccess class for use with the Database Editor application.
    /// </summary>
    public class DataAccessCollectionEditor : InformediaCORE.Processing.Database.DataAccessExtended
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public DataAccessCollectionEditor() : base() { }

        /// <summary>
        /// Get the Collection corresponding to the given accession number.
        /// </summary>
        /// <param name="accession">The accession number of the desired collection.</param>
        /// <returns>A Collection object on success, null on failure.</returns>
        public Collection GetCollection(string accession)
        {
            Collection collection;

            using (IDVLDataContext context = GetNonTrackingDataContext())
            {
                try
                {
                    collection = (from c in context.Collections
                                  where c.Accession == accession
                                  select c).FirstOrDefault();
                }
                catch
                {
                    collection = null;
                }
            }

            return collection;
        }

        /// <summary>
        /// Retrieves a list of all Collection accession numbers from the database.
        /// </summary>
        /// <returns>An array of accession numbers.</returns>
        public string[] GetCollectionAccessionNumbers()
        {
            string[] accessionNumbers;

            using (IDVLDataContext context = GetNonTrackingDataContext())
            {
                try
                {
                    accessionNumbers = (from c in context.Collections
                                        orderby c.Accession
                                        select c.Accession).ToArray<string>();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    accessionNumbers = null;
                }
            }

            return accessionNumbers;
        }
    }
}
