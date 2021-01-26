using System;

using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;

namespace SeedCloud
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.Required,
            HelpText = "Determines which archive will be the target of this operation."
        )]
        public DigitalArchiveSpecifier Target;

        [Argument(
            ArgumentType.Required,
            HelpText = "Selects which content should be seeded."
        )]
        public ContentSpecifier Content;

        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "Specifies the maximum creation date for the PriorTo option. Use form \"mm/dd/yyyy\"",
            DefaultValue = null
        )]
        public string Date;

        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "If true, segment tags will be imported from Firebase tag database; if false, local copy of tags will be used.",
            DefaultValue = false
        )]
        public bool ImportTags;
    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Command line utility for reliably and efficiently uploading all or most of the
    /// processing database contents to the cloud.
    /// </summary>
    internal class SeedCloud
    {
        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        private static void Main(string[] args)
        {
            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            if (arguments.Content == ContentSpecifier.PriorTo && arguments.Date == null)
            {
                Logger.Warning("Must supply a valid Date when using the PiorTo option.");
                return;
            }

            DateTime? date = null;
            if (arguments.Content == ContentSpecifier.PriorTo)
            {
                try
                {
                    date = DateTime.Parse(arguments.Date);   
                }
                catch
                {
                    Logger.Warning("Given date could not be parsed, must be in the form  \"mm/dd/yyyy\"");
                    return;
                }
            }

            // Log header
            Logger.Start();
            Logger.Write("Requesting user confirmation to proceed...");

            Utilities.NewLine();
            Utilities.WriteLine("****** ATTENTION ******", ConsoleColor.Red);
            Utilities.NewLine();

            switch (arguments.Content)
            {
                case ContentSpecifier.All:
                    Utilities.WriteLine("You are about to upload the entire contents from the processing database", ConsoleColor.Red);
                    break;
                case ContentSpecifier.PriorTo:
                    Utilities.WriteLine($"You are about to upload content created prior to {arguments.Date} from the processing database", ConsoleColor.Red);
                    break;
                case ContentSpecifier.Published:
                    Utilities.WriteLine("You are about to upload previously published content from the processing database", ConsoleColor.Red);

                    break;
            }

            switch (arguments.Target)
            {
                case DigitalArchiveSpecifier.Processing:
                    Utilities.WriteLine($"to the following Processing Digital Archive (a.k.a. test site) accounts:", ConsoleColor.Red);
                    Utilities.NewLine();
                    Utilities.WriteLine($"     Azure Search: {Settings.Current.Processing.AzureSearchServiceName}", ConsoleColor.Red);
                    Utilities.WriteLine($"    Azure Storage: {Settings.Current.Processing.AzureStorageAccountName}", ConsoleColor.Red);
                    break;
                case DigitalArchiveSpecifier.Production:
                    Utilities.WriteLine($"to the following Production Digital Archive (a.k.a. live site) accounts:", ConsoleColor.Red);
                    Utilities.NewLine();
                    Utilities.WriteLine($"     Azure Search: {Settings.Current.Production.AzureSearchServiceName}", ConsoleColor.Red);
                    Utilities.WriteLine($"    Azure Storage: {Settings.Current.Production.AzureStorageAccountName}", ConsoleColor.Red);
                    break;
            }

            const string prompt = ">>>>> Do you wish to continue? [y/n]: ";

            if (Utilities.GetUserConfirmation(prompt))
            {
                Logger.Write("Operation confirmed by user.");
                CloudSeeder.MakeItRain(arguments.Target, arguments.Content, arguments.ImportTags, date);
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
