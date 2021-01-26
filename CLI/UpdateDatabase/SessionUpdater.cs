using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Common.Media;

namespace InformediaCORE.UpdateDatabase
{
    /// <summary>
    /// Updates the Session table with data exported from FileMaker via spreadsheet.
    /// </summary>
    internal static class SessionUpdater
    {
        #region ===== DECLARATIONS
        /// <summary>
        /// The list of required column names used to validate the spreadsheet.
        /// </summary>
        private static readonly List<string> ColumnNames = new List<string>
        {
            "InterviewFromSession_Acc#::Accession#",    
            "InterviewFromSession_Acc#::PreferredName", // Purely for log feedback
            "SessionNumber",                            // SessionOrder
            "Date_of_Session",                          // InterviewDate
            "Videographer 01",                          // \_Combine into Videographer
            "Videographer 02",                          // /
            "Interviewer",                              // Interviewer
            "Location_Interview_City",                  // \ 
            "Location_Interview_State_Abr",             //  } Combine into Location
            "Location_Interview_Country_Filter",        // /
            "Sponsored_YN",                             // Determines whether to load or not...
            "Sponsor",                                  // Sponsor
            "Sponsor URL",                              // SponsorURL
            "Sponsor Logo FilePath Web"                 // Load into SponsorImage
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

            UpdateSessionData(table);
        }

        /// <summary>
        /// Updates the Session table with the given spreadsheet data.
        /// </summary>
        /// <param name="table">The spreadsheet table.</param>
        private static void UpdateSessionData(DataTable table)
        {
            Logger.Write("Updating Session Table...");

            int updated = 0;
            int skipped = 0;
            var failures = new List<string>();
            var stopwatch = Stopwatch.StartNew();

            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                foreach (DataRow row in table.Rows)
                {
                    var accession = row["InterviewFromSession_Acc#::Accession#"].ToString();
                    var preferredName = row["InterviewFromSession_Acc#::PreferredName"].ToString();

                    if (!int.TryParse(row["SessionNumber"].ToString(), out int sessionOrder))
                    {
                        Logger.Warning("*** Could not parse SessionOrder.");
                        continue;
                    }

                    if (!context.Collections.Any(c => c.Accession == accession))
                    {
                        Logger.Write($"Skipping Session {accession} #{sessionOrder} {preferredName}: owning collection not found in database.");
                        continue;
                    }

                    var collectionID =
                        (from c in context.Collections
                         where c.Accession == accession
                         select c.CollectionID).First();

                    var session =
                        (from s in context.Sessions
                         where s.CollectionID == collectionID && s.SessionOrder == sessionOrder
                         select s).FirstOrDefault();

                    if (session == null)
                    {
                        Logger.Write($"Skipping Session {accession} #{sessionOrder} {preferredName}: not found in database.");
                        skipped++;
                        continue;
                    }

                    Logger.Write($"Updating Session {accession} #{sessionOrder} {preferredName}...");
                    try
                    {
                        // SESSIONS TABLE
                        PopulateSession(session, row);
                        context.SubmitChanges();

                        // Put the parent collection in the queue for cloud updates
                        Utilities.QueueUpdate(accession);
                        updated++;
                    }
                    catch (ParsingException ex)
                    {
                        Logger.Error(ex.Message);
                        Logger.Warning($"*** Session {accession} #{sessionOrder} not updated due to the preceeding error.");
                        failures.Add($"{accession} #{sessionOrder}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex);
                        Logger.Warning($"*** Session {accession} #{sessionOrder} not updated due to the preceeding exception.");
                        failures.Add($"{accession} #{sessionOrder}");
                    }
                }
            }

            stopwatch.Stop();
            Logger.Write("Collection table update complete.");
            Logger.Write("================================================================================");
            Logger.Write($"{table.Rows.Count} rows processed in {stopwatch.Elapsed.TotalMinutes:F2} minutes.");
            Logger.Write($"{updated} sessions updated successfully.");
            Logger.Write($"{skipped} sessions skipped.");
            Logger.Write($"{failures.Count} sessions failed due to errors.");
            Logger.Write("================================================================================");
        }

        /// <summary>
        /// Populate the given session with the data from the given row.
        /// </summary>
        /// <param name="session">The session to be updated.</param>
        /// <param name="row">The row to be processed.</param>
        private static void PopulateSession(Session session, DataRow row)
        {
            var interviewDate = Utilities.GetDateFromString(row["Date_of_Session"].ToString()) ?? DateTime.MinValue;

            var videographer = FormatName(row["Videographer 01"].ToString());
            var videographer2 = FormatName(row["Videographer 02"].ToString());
            if (!string.IsNullOrEmpty(videographer2))
                videographer += string.IsNullOrEmpty(videographer) ? $"{videographer2}" : $" and {videographer2}";

            var country = row["Location_Interview_Country_Filter"].ToString();
            var state = row["Location_Interview_State_Abr"].ToString();
            var location = row["Location_Interview_City"].ToString();
            if (!string.IsNullOrEmpty(state))
                location += string.IsNullOrEmpty(location) ? $"{state}" : $", {state}";
            if (!string.IsNullOrEmpty(country))
                location += string.IsNullOrEmpty(location) ? $"{country}" : $", {country}";

            session.InterviewDate = interviewDate;
            session.Videographer = videographer;
            session.Interviewer = FormatName(row["Interviewer"].ToString());
            session.Location = location;
            session.Sponsor = null;
            session.SponsorURL = null;
            session.SponsorImage = null;
            session.ImageType = null;

            var sponsored = row["Sponsored_YN"].ToString().ToLower();
            if (sponsored == "no") return;

            session.Sponsor = row["Sponsor"].ToString();
            session.SponsorURL = row["Sponsor URL"].ToString();

            var imageFile = row["Sponsor Logo FilePath Web"].ToString();
            if (string.IsNullOrEmpty(imageFile)) return;

            session.SponsorImage = MediaTools.LoadImage(imageFile);
            session.ImageType = Common.Utilities.GetFileExtension(imageFile);
        }

        /// <summary>
        /// Put the given string into Firstname Lastname order.
        /// </summary>
        /// <param name="name">A string containing a name.</param>
        /// <returns>A string in Firstname Lastname order.</returns>
        private static string FormatName(string name)
        {
            if (!name.Contains(',')) return name;

            var names = name.Split(',');
            return $"{names[1].Trim()} {names[0].Trim()}";
        }
    }
}
