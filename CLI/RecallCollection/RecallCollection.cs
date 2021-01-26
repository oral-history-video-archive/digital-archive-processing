using System;

using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Azure.Models;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;


namespace InformediaCORE.RecallCollection
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [DefaultArgument(
            ArgumentType.Required,
            HelpText = "The accession number of the collection to be published."
        )]
        public string Accession;

        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "Determines which digital archive will be the target of this operation.",
            DefaultValue = DigitalArchiveSpecifier.Production
        )]
        public DigitalArchiveSpecifier Target;
    }
#pragma warning restore 0169, 0649

    class RecallCollection
    {
        static void Main(string[] args)
        {
            // If no arguments specified, show usage.
            if (args == null || args.Length == 0)
            {
                // Force usage to display by sending the /? flag to the argument parser.
                args = new[] { "/?" };
            }

            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            // Log header
            Logger.Start();

            // Enforce uppercase naming rule.
            var accession = arguments.Accession.ToUpper();

            Recall(accession, arguments.Target);

            // Log footer
            Logger.End();
        }

        private static void Recall(string accession, DigitalArchiveSpecifier target)
        {
            string accountKey, accountName, message;

            switch (target)
            {
                case DigitalArchiveSpecifier.Processing:
                    accountKey = Settings.Current.Processing.AzureStorageAccountKey;
                    accountName = Settings.Current.Processing.AzureStorageAccountName;
                    message = "The following content will be recalled from the processing (review) digital archive:";
                    break;
                case DigitalArchiveSpecifier.Production:
                    accountKey = Settings.Current.Production.AzureStorageAccountKey;
                    accountName = Settings.Current.Production.AzureStorageAccountName;
                    message = "The following content will be recalled from the production (public) digital archive:";
                    break;
                default:
                    Logger.Error("Invalid target specified.");
                    return;
            }

            Logger.Write("Retrieving biography details...");
            var storageHandler = new AzureStorageHandler(accountName, accountKey);
            var details = storageHandler.GetBiographyDetails(accession);

            if (details == null)
            {
                Logger.Error("Could not find biography {0} on the {1} Archive.", accession, target.ToString());
                return;
            }

            Logger.Write("Requesting user confirmation to recall {0} from {1}", accession, target.ToString());

            Utilities.NewLine();
            Utilities.WriteLine(message, ConsoleColor.Cyan);

            // Summarize report for user
            Utilities.NewLine();
            Utilities.WriteLine($"Summary Report for biographical collection {details.Accession} {details.PreferredName}", ConsoleColor.Cyan);
            foreach (var session in details.Sessions)
            {
                Utilities.WriteLine($"  Session #{session.SessionOrder}: Conducted {session.InterviewDate:MM-dd-yyyy} by {session.Interviewer}", ConsoleColor.Cyan);
                foreach (var tape in session.Tapes)
                {
                    Utilities.WriteLine($"    Tape {tape.TapeOrder}:", ConsoleColor.Cyan);
                    foreach (var story in tape.Stories)
                    {
                        Utilities.WriteLine($"      Story {story.StoryOrder,2}: {story.Title.Truncate(65)}", ConsoleColor.Cyan);
                    }
                }
            }

            Utilities.NewLine();
            const string prompt = ">>>>> Please confirm that you wish to recall the content summarized above: [y/n]: ";

            if (Utilities.GetUserConfirmation(prompt))
            {
                Logger.Write("Recall operation confirmed by user.");
                try
                {
                    AzureContentManager.RecallCollection(details, target);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                }
            }
            else
            {
                Logger.Write("Recall operation cancelled by user.");
            }
        }
    }
}
