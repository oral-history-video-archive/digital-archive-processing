using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace InformediaCORE.UpdateDatabase
{
    /// <summary>
    /// Updates the Collection table with data exported from FileMaker via spreadsheet.
    /// </summary>
    internal static class CollectionUpdater
    {
        #region ===== DECLARATIONS
        /// <summary>
        /// A dictionary mapping the FileMaker Description of the MakerCategory
        /// to the processing database ID for the same.
        /// </summary>
        private static readonly Dictionary<string, int> MakerCategories = new Dictionary<string, int>();

        /// <summary>
        /// A dictionary mapping the FileMaker Description of the JobType
        /// to the processing database ID for the same.
        /// </summary>
        private static readonly Dictionary<string, int> OccupationTypes = new Dictionary<string, int>();

        /// <summary>
        /// A dictionary mapping annotation type "names" to the processing
        /// database ID for the same.
        /// </summary>
        private static readonly Dictionary<string, int> AnnotationTypes = new Dictionary<string, int>();

        /// <summary>
        /// The list of required column names used to validate the spreadsheet.
        /// </summary>
        private static readonly List<string> ColumnNames = new List<string>
        {
            "Accession#",
            "LastName",
            "FirstName",
            "PreferredName",
            "HISTORYMAKER::Gender",
            "HISTORYMAKER::DateBirth",
            "HISTORYMAKER::DateDeath",
            "HISTORYMAKER::DescriptionShort",
            "HISTORYMAKER::BiographyShort",
            "HISTORYMAKER::Website URL",
            "HISTORYMAKER::BirthCity",
            "HISTORYMAKER::BirthState_Abr",
            "HISTORYMAKER::BirthCountry_Filter",
            "HISTORYMAKER::Category_Report",
            "HISTORYMAKER::OccupationNarrowTerms for Report",
            "HISTORYMAKER::OccupationJobType_WEB",
            "HISTORYMAKER::Region CityState_Abr",
            "HISTORYMAKER::Favorite_Color_WEB",
            "HISTORYMAKER::Favorite_Food_WEB",
            "HISTORYMAKER::Favorite_Quote_WEB",
            "HISTORYMAKER::Favorite_Season_WEB",
            "HISTORYMAKER::Favorite_VacationSpot_WEB"
        };
        #endregion == DECLARATIONS

        /// <summary>
        /// Updates the database with the given spreadsheet data.
        /// </summary>
        /// <param name="spreadsheet">Fully qualified path to an Excel spreadsheet.</param>
        /// <param name="worksheet">The name of the worksheet containing the table.</param>
        internal static void UpdateData(string spreadsheet, string worksheet)
        {
            Logger.Write("Loading spreadsheet...");
            var table = Utilities.ExcelToDataTable(spreadsheet, worksheet);

            Logger.Write("Validating column names...");
            if (!Utilities.ValidateColumnNames(table, ColumnNames))
            {
                Logger.Error("Spreadsheet missing required columns, update cannot be performed.");
                return;
            }

            UpdateCollectionData(table);
        }

        /// <summary>
        /// Updates the Collection table with the given spreadsheet data.
        /// </summary>
        /// <param name="table">The spreadsheet table.</param>
        private static void UpdateCollectionData(DataTable table)
        {
            Logger.Write("Updating Collection Table...");

            int updated = 0;
            int skipped = 0;
            var failures = new List<string>();
            var stopwatch = Stopwatch.StartNew();

            CacheMakerCategoriesAndJobTypes();
            CacheAnnotationTypes();

            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            { 
                foreach (DataRow row in table.Rows)
                {
                    var accession = row["Accession#"].ToString();

                    var collection = 
                            (from c in context.Collections
                             where c.Accession == accession
                             select c).FirstOrDefault();

                    if (collection == null)
                    {
                        Logger.Write("Skipping Collection {0}: not found in processing database.", accession);
                        skipped++;
                        continue;
                    }

                    Logger.Write("Updating Collection {0} {1}...", accession, collection.PreferredName);
                    try
                    {
                        // COLLECTIONS TABLE
                        collection.DescriptionShort = row["HISTORYMAKER::DescriptionShort"].ToString();
                        collection.BiographyShort = row["HISTORYMAKER::BiographyShort"].ToString();
                        collection.FirstName = row["FirstName"].ToString();
                        collection.LastName = row["LastName"].ToString();
                        collection.PreferredName = row["PreferredName"].ToString();
                        collection.Gender = Utilities.GetGenderFromString(row["HISTORYMAKER::Gender"].ToString());
                        collection.WebsiteURL = row["HISTORYMAKER::Website URL"].ToString();
                        collection.Region = row["HISTORYMAKER::Region CityState_Abr"].ToString();
                        collection.BirthCity = row["HISTORYMAKER::BirthCity"].ToString();
                        collection.BirthState = row["HISTORYMAKER::BirthState_Abr"].ToString();
                        collection.BirthCountry = row["HISTORYMAKER::BirthCountry_Filter"].ToString();
                        collection.BirthDate = Utilities.GetDateFromString(row["HISTORYMAKER::DateBirth"].ToString());
                        collection.DeceasedDate = Utilities.GetDateFromString(row["HISTORYMAKER::DateDeath"].ToString());

                        context.SubmitChanges();

                        // RELATED TABLES
                        UpdatePartitions(collection.CollectionID, row);
                        UpdateAnnotations(collection.CollectionID, row);

                        // Put the collection in the queue for cloud updates
                        Utilities.QueueUpdate(collection.Accession);
                        updated++;
                    }
                    catch (ParsingException ex)
                    {
                        Logger.Error(ex.Message);
                        Logger.Warning($"*** Collection {accession} not updated due to the preceeding error.");
                        failures.Add(accession);
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex);
                        Logger.Warning($"*** Collection {accession} not updated due to the preceeding exception.");
                        failures.Add(accession);
                    }
                }
            }

            stopwatch.Stop();
            Logger.Write("Collection table update complete.");
            Logger.Write("================================================================================");
            Logger.Write($"{table.Rows.Count} rows processed in {stopwatch.Elapsed.TotalMinutes:F2} minutes.");
            Logger.Write($"{updated} collections updated successfully.");
            Logger.Write($"{skipped} collections skipped.");
            Logger.Write($"{failures.Count} collections failed due to errors.");
            Logger.Write("================================================================================");
        }

        /// <summary>
        /// Caches the MakerCategories and JobTypes into memory.
        /// </summary>
        private static void CacheMakerCategoriesAndJobTypes()
        {
            Logger.Write("Caching MakerCategory and JobType codes from database...");
            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                var makerCategoryID =
                    (from w in context.Worlds
                     where w.Name == "Maker Group"  // NOTE: Should this be a global constant?
                     select w.WorldID).FirstOrDefault();

                var jobTypeID =
                    (from w in context.Worlds
                     where w.Name == "Job Type"     // NOTE: Should this be a global constant?
                     select w.WorldID).FirstOrDefault();

                var partitions =
                    from p in context.Partitions
                    orderby p.PartitionID
                    select p;

                MakerCategories.Clear();
                OccupationTypes.Clear();

                // Separate the partition values into MakerCategories and JobTypes
                foreach (var partition in partitions)
                {
                    if (partition.WorldID == makerCategoryID)
                    {
                        // NOTE: For partitions we're relying on Description to be cannonical
                        MakerCategories.Add(partition.Description, partition.PartitionID);
                    }
                    else if (partition.WorldID == jobTypeID)
                    {
                        OccupationTypes.Add(partition.Description, partition.PartitionID);
                    }
                }                    
            }
        }

        /// <summary>
        /// Caches the AnnotationTypes into memory.
        /// </summary>
        private static void CacheAnnotationTypes()
        {
            Logger.Write("Caching AnnotationType codes from database... ");
            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                var annotationTypes =
                    from t in context.AnnotationTypes
                    where t.Scope == 'C'    // We're only interested in Colletion level annotations.
                    orderby t.TypeID
                    select t;

                AnnotationTypes.Clear();
                foreach (var annotationType in annotationTypes)
                {
                    // NOTE: For annotations we're relying on the Name to be cannonical
                    AnnotationTypes.Add(annotationType.Name, annotationType.TypeID);
                }
            }
        }

        /// <summary>
        /// Updates the MakerCategories and JobTypes for the specified collection.
        /// </summary>
        /// <param name="collectionID">Database identifier of the collection.</param>
        /// <param name="row">The row to be processed.</param>
        private static void UpdatePartitions(int collectionID, DataRow row)
        {
            Logger.Write("   Updating partition table...");

            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                Logger.Write("      Deleting existing MakerCategories and JobTypes");
                var members =
                    from m in context.PartitionMembers
                    where m.CollectionID == collectionID
                    select m;

                foreach (var member in members)
                {
                    context.PartitionMembers.DeleteOnSubmit(member);
                }

                context.SubmitChanges();
            }

            UpdateMakerCategories(collectionID, row);
            UpdateJobTypes(collectionID, row);
        }

        #region ===== UpdatePartitions Helpers
        /// <summary>
        /// Adds the given list of MakerCategories to the specified collection.
        /// </summary>
        /// <param name="collectionID">Database identifier of the collection.</param>
        /// <param name="row">The row to be processed.</param>
        private static void UpdateMakerCategories(int collectionID, DataRow row)
        {
            var categories = row["HISTORYMAKER::Category_Report"].ToString();
            if (string.IsNullOrEmpty(categories)) return;

            Logger.Write("      Updating MakerCategories...");
            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                var values = categories.Split('|').Distinct();

                foreach (var value in values)
                {
                    // NOTE: Trimming plural 's' from word 'Makers' for historical consistency.
                    var category = value.Trim().TrimEnd('s');

                    if (string.IsNullOrEmpty(category)) continue;

                    if (MakerCategories.ContainsKey(category))
                    {
                        Logger.Write("         assigning to category: {0}", category);
                        context.PartitionMembers.InsertOnSubmit(new PartitionMember
                        {
                            CollectionID = collectionID,
                            PartitionID = MakerCategories[category]
                        });
                    }
                    else
                    {
                        Logger.Warning("         unknown category: {0}", category);
                    }
                }
                
                context.SubmitChanges();
            }
        }

        /// <summary>
        /// Adds the given list of JobTypes to the specified collection.
        /// </summary>
        /// <param name="collectionID">The collection's database identifier.</param>
        /// <param name="row">The row to be processed.</param>
        private static void UpdateJobTypes(int collectionID, DataRow row)
        {
            var jobTypes = row["HISTORYMAKER::OccupationJobType_WEB"].ToString();
            if (string.IsNullOrEmpty(jobTypes)) return;

            Logger.Write("      Updating JobTypes...");
            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                var values = jobTypes.Split('|').Distinct();

                foreach (var value in values)
                {
                    var jobType = value.Trim();

                    if (string.IsNullOrEmpty(jobType)) continue;

                    if (!OccupationTypes.ContainsKey(jobType))
                    {
                        AddJobType(jobType);
                    }

                    Logger.Write("         Assigning job type: {0}", jobType);
                    context.PartitionMembers.InsertOnSubmit(new PartitionMember
                    {
                        CollectionID = collectionID,
                        PartitionID = OccupationTypes[jobType]
                    });
                }

                context.SubmitChanges();
            }
        }

        /// <summary>
        /// Adds the given job type to the Partitions table.
        /// </summary>
        /// <param name="jobType">A string description of the job classification.</param>
        private static void AddJobType(string jobType)
        {
            Logger.Write("      Adding new JobType to database: {0}...", jobType);
            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                if (context.Partitions.Any(p => p.Description == jobType))
                {
                    Logger.Warning("         JobType '{0}' already exists in database.");
                    return;
                }

                var jobTypeID =
                    (from w in context.Worlds
                     where w.Name == "Job Type"     // NOTE: Should this be a global constant?
                     select w.WorldID).FirstOrDefault();

                var partition = new Partition
                {
                    Name = jobType,
                    Description = jobType,
                    WorldID = jobTypeID
                };

                context.Partitions.InsertOnSubmit(partition);
                context.SubmitChanges();

                // Cache the new JobType into memory to avoid repeat insertions
                OccupationTypes.Add(jobType, partition.PartitionID);
            }
        }
        #endregion == UpdatePartitions Helpers

        /// <summary>
        /// Updates the Occupation and Favorites annotations for the specified collection.
        /// </summary>
        /// <param name="collectionID">Database identifier of the collection.</param>
        /// <param name="row">The row to be processed.</param>
        private static void UpdateAnnotations(int collectionID, DataRow row)
        {
            Logger.Write("   Updating annotation table...");

            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                Logger.Write("      Deleting existing Occupation and Favorites annotations");
                var annotations =
                    from a in context.Annotations
                    where a.CollectionID == collectionID
                    select a;

                foreach (var annotation in annotations)
                {
                    context.Annotations.DeleteOnSubmit(annotation);
                }

                context.SubmitChanges();
            }

            UpdateOccupations(collectionID, row);
            UpdateFavorites(collectionID, row);
        }

        #region ===== UpdateAnnotations Helpers
        /// <summary>
        /// Update the Occupation annotations for the specified collection.
        /// </summary>
        /// <param name="collectionID">Database identifier of the collection.</param>
        /// <param name="row">The row to be processed.</param>
        private static void UpdateOccupations(int collectionID, DataRow row)
        {
            var occupations = row["HISTORYMAKER::OccupationNarrowTerms for Report"].ToString();
            if (string.IsNullOrEmpty(occupations)) return;

            Logger.Write("      Updating Occupations...");
            var values = occupations.Split('|').Distinct();

            foreach (var value in values)
            {
                InsertAnnotation(
                    collectionID,
                    "Occupation",           // NOTE: Should this be a global constant?
                    value
                );
            }
        }

        /// <summary>
        /// Update the Favorites annotations for the specified collection.
        /// </summary>
        /// <param name="collectionID">Database identifier of the collection.</param>
        /// <param name="row">The row to be processed.</param>
        private static void UpdateFavorites(int collectionID, DataRow row)
        {
            Logger.Write("      Updating Favorites...");

            InsertAnnotation(
                collectionID,
                "Favorite Color",           // NOTE: Should this be a global constant?
                row["HISTORYMAKER::Favorite_Color_WEB"].ToString()
            );

            InsertAnnotation(
                collectionID,
                "Favorite Food",            // NOTE: Should this be a global constant?
                row["HISTORYMAKER::Favorite_Food_WEB"].ToString()
            );

            InsertAnnotation(
                collectionID,
                "Favorite Quote",           // NOTE: Should this be a global constant?
                row["HISTORYMAKER::Favorite_Quote_WEB"].ToString()
            );

            InsertAnnotation(
                collectionID,
                "Favorite Time of Year",    // NOTE: Should this be a global constant?
                row["HISTORYMAKER::Favorite_Season_WEB"].ToString()
            );

            InsertAnnotation(
                collectionID,
                "Favorite Vacation Spot",   // NOTE: Should this be a global constant?
                row["HISTORYMAKER::Favorite_VacationSpot_WEB"].ToString()
            );
        }

        /// <summary>
        /// Insert the given collection annotation.
        /// </summary>
        /// <param name="collectionID">The collection's database identifier.</param>
        /// <param name="type">Name of AnnotationType to insert</param>
        /// <param name="value">Actual content of the annotation.</param>
        private static void InsertAnnotation(int collectionID, string type, string value)
        {
            value = value.Trim();

            if (string.IsNullOrEmpty(value)) return;

            if (!AnnotationTypes.ContainsKey(type))
            {
                AddAnnotationType(type);
            }

            Logger.Write("         Adding '{0}': {1}", type, value.Truncate(60));
            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                context.Annotations.InsertOnSubmit(new Annotation
                {
                    CollectionID = collectionID,
                    TypeID = AnnotationTypes[type],
                    Value = value
                });

                context.SubmitChanges();
            }
        }

        /// <summary>
        /// Adds the given annotation type to the AnnotationTypes table.
        /// </summary>
        /// <param name="type">The cannonical description of the type.</param>
        private static void AddAnnotationType(string type)
        {
            Logger.Write("         Adding new AnnotationType: {0}", type);
            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                if (context.AnnotationTypes.Any(t => t.Description == type))
                {
                    Logger.Warning("         AnnotationType '{0}' already exists in database.");
                    return;
                }

                var annotationType = new AnnotationType
                {
                    Name = type,
                    Scope = 'C',
                    Description = type
                };

                context.AnnotationTypes.InsertOnSubmit(annotationType);
                context.SubmitChanges();

                // Cache the new AnnotationType into memory to avoid repeat insertions
                AnnotationTypes.Add(type, annotationType.TypeID);
            }
        }
        #endregion == UpdateAnnotations Helpers
    }
}
