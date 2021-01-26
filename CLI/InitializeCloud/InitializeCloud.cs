using System;

using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace InformediaCORE.InitializeCloud
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "Initialize the Azure Search Service for first time use.",
            DefaultValue = false
        )]
        public bool SearchServiceInitialize;

        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "Initialize the Azure Blob Storage account for first time use.",
            DefaultValue = false
        )]
        public bool BlobStorageInitialize;

        [Argument(
            ArgumentType.Required,
            HelpText = "Determines which digital archive will be the target of this operation."
        )]
        public DigitalArchiveSpecifier Target;
    }
#pragma warning restore 0169, 0649
    class InitializeCloud
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

            Utilities.NewLine();
            Utilities.WriteLine("", ConsoleColor.Red);
            Utilities.WriteLine("THE FOLLOWING DIGITAL ARCHIVES AND SERVICES WILL BE AFFECTED:", ConsoleColor.Red);
            Utilities.NewLine();

            switch (arguments.Target)
            {
                case DigitalArchiveSpecifier.Processing:
                    if (arguments.SearchServiceInitialize)
                        Utilities.WriteLine($"  * PROCESSING (REVIEW) SEARCH SERVICE '{Settings.Current.Processing.AzureSearchServiceName}'", ConsoleColor.Red);

                    if (arguments.BlobStorageInitialize)
                        Utilities.WriteLine($"  * PROCESSING (REVIEW) STORAGE SERVICE '{Settings.Current.Processing.AzureStorageAccountName}'", ConsoleColor.Red);
                    break;
                case DigitalArchiveSpecifier.Production:
                    if (arguments.SearchServiceInitialize)
                        Utilities.WriteLine($"  * PRODUCTION (PUBLIC) SEARCH SERVICE '{Settings.Current.Production.AzureSearchServiceName}'", ConsoleColor.Red);

                    if (arguments.BlobStorageInitialize)
                        Utilities.WriteLine($"  * PRODUCTION (PUBLIC) STORAGE SERVICE '{Settings.Current.Production.AzureStorageAccountName}'", ConsoleColor.Red);
                    break;
            }

            const string prompt = ">>>>> Please confirm that you wish to initialize the services above [y/n]: ";

            if (Utilities.GetUserConfirmation(prompt))
            {
                Logger.Write("Operation confirmed by user.");
                try
                {
                    if (arguments.SearchServiceInitialize)
                    {
                        AzureContentManager.InitializeSearchService(arguments.Target);
                    }

                    if (arguments.BlobStorageInitialize)
                    {
                        AzureContentManager.InitializeStorageService(arguments.Target);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                }
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

