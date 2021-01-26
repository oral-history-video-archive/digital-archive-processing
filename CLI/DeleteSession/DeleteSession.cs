using System;

using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing.Database;

namespace InformediaCORE.DeleteSession
{
// Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.Required,
            HelpText = "The accession number of the interview containing the session to be deleted."
        )]
        public string Accession;

        [Argument(
            ArgumentType.Required,
            HelpText = "The numeric ordinal of the session to be deleted."
        )]
        public int SessionOrder;
    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Commandline tool for deleting sessions and child data from the database.
    /// </summary>   
    internal class DeleteSession
    {
        private static void Main(string[] args)
        {
            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            // Log header
            Logger.Start();

            // Enforce uppercase naming rule.
            var accession = arguments.Accession.ToUpper();

            // Validate accesion number
            if (!DataAccess.IsValidAccession(accession))
            {
                Logger.Write("Invalid accession number {0}", accession);
                Logger.End();
                return;
            }

            // Validate session ordinal
            if (!DataAccess.IsValidSession(accession, arguments.SessionOrder))
            {
                Logger.Write("Invalid session specified.");
                Logger.End();
                return;
            }

            Logger.Write("Requesting user confirmation for session deletion.");
            // TODO: Add a summary report about which session is going to be affected
            var isPublished = (DataAccess.GetSessionPublishingPhase(accession, arguments.SessionOrder) == (char)PublishingPhase.Published);            


            Utilities.NewLine();
            Utilities.WriteLine("*****     WARNING!! THIS ACTION WILL HAVE THE FOLLOWING EFFECTS:", ConsoleColor.Red);
            Utilities.WriteLine("*****", ConsoleColor.Red);
            Utilities.WriteLine("*****     * THE SESSION WILL BE DELETED FROM THE DATABASE IN ENTIRETY", ConsoleColor.Red);
            Utilities.WriteLine("*****     * THE RELATED SEGEMENT VIDEOS WILL BE DELETED FROM THE FILE SYSTEM", ConsoleColor.Red);
            Utilities.WriteLine("*****     * THE SESSION WILL BE DELETED FROM THE PROCESSING ARCHIVE", ConsoleColor.Red);
            // TODO: Add ability to select which digital archive(s) will be affected.
            // TODO: Will need to re-publish the collection without the session.

            const string prompt = ">>>>> Please confirm that you wish to delete the session and all related content [y/n]: ";

            if (Utilities.GetUserConfirmation(prompt))
            {
                Logger.Write("Operation confirmed by user.");
                var dae = new DataAccessExtended();
                // TODO: UNCOMMENT THIS LINE  dae.DeleteSession(arguments.Accession, arguments.SessionOrder, true);

            }
            else
            {
                Logger.Write("Operation cancelled by user.");
            }

            // Log footer
            Logger.End();
        }
    }
}
