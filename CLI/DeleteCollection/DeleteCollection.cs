using System;

using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing.Database;

namespace InformediaCORE.DeleteCollection
{
// Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [DefaultArgument(
            ArgumentType.Required,
            HelpText = "The accession number of the collection to be deleted."
        )]
        public string Accession;
    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Commandline tool for deleting collections and child data from the database and digital archive(s).
    /// </summary>
    internal class DeleteCollection
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

            if (!DataAccess.IsValidAccession(accession))
            {
                Logger.Write("Invalid accession number {0}", accession);
                Logger.End();
                return;
            }

            Logger.Write("Requesting user confirmation for collection deletion.");
            var isPublished = (DataAccess.GetCollectionPublishingPhase(accession) == (char)PublishingPhase.Published);
            var summary = DataAccess.GetCollectionSummary(accession);
            Utilities.NewLine();
            foreach (var line in summary)
            {
                Utilities.WriteLine(line, ConsoleColor.Cyan);
            }

            Utilities.NewLine();
            Utilities.WriteLine("THIS ACTION WILL HAVE THE FOLLOWING EFFECTS ON THE CONTENT SUMMARIZED ABOVE:", ConsoleColor.Red);
            Utilities.NewLine();
            Utilities.WriteLine("  * THE COLLECTION WILL BE DELETED FROM THE DATABASE IN ENTIRETY", ConsoleColor.Red);
            Utilities.WriteLine("  * THE RELATED SEGEMENT VIDEOS WILL BE DELETED FROM THE FILE SYSTEM", ConsoleColor.Red);
            Utilities.WriteLine("  * THE COLLECTION WILL BE DELETED FROM THE PROCESSING (REVIEW) ARCHIVE", ConsoleColor.Red);
            if (isPublished)
            {
                Utilities.WriteLine("  * THE COLLECTION WILL BE DELETED FROM THE PRODUCTION (PUBLIC) ARCHIVE", ConsoleColor.Red);
            }

            const string prompt = ">>>>> Please confirm that you wish to delete the collection and all related content [y/n]: ";

            if (Utilities.GetUserConfirmation(prompt))
            {
                Logger.Write("Operation confirmed by user.");

                if (isPublished)
                {
                    AzureContentManager.RecallCollection(accession, DigitalArchiveSpecifier.Production);
                }

                AzureContentManager.RecallCollection(accession, DigitalArchiveSpecifier.Processing);
                
                var dae = new DataAccessExtended();
                dae.DeleteCollection(accession);
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
