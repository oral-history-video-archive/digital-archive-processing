using System;
using System.Collections.Generic;

using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Database;

namespace InformediaCORE.PublishCollection
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
            ArgumentType.MultipleUnique,
            HelpText = "Specifies which session(s) to publish.  You can specify more than one by repeating the argument.  Ex: /S:1 /S:2"
        )]
        public int[] Sessions;

        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "Determines which digital archive will be the target of this operation.",
            DefaultValue = DigitalArchiveSpecifier.Production
        )]
        public DigitalArchiveSpecifier Target;
        
        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "If true, segment tags will be imported from Firebase tag database; if false, local copy of tags will be used.",
            DefaultValue = true
        )]
        public bool ImportTags;

        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = "Skips user confirmation. Use with caution.",
            DefaultValue = false
        )]
        public bool BatchMode;
    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Publishes the database records and media associated with a given collection to Azure 
    /// </summary>
    internal class PublishCollection
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

            if (arguments.Sessions == null || arguments.Sessions.Length < 1)
            {
                Utilities.WriteLine("You must specify at least one session to publish.", ConsoleColor.Red);
                ShowUsage();                
                return;
            }

            // Format strings for report
            var accession = arguments.Accession.ToUpper();
            var sessions = string.Join(", ", arguments.Sessions);
            var target = arguments.Target.ToString();

            // Log header
            Logger.Start();

            // Defined here so it is visible in the Finally block
            PublishingPackage package = null;

            try
            {
                package = new PublishingPackage(accession, arguments.Target, arguments.Sessions, arguments.ImportTags);

                bool confirmed = false;
                if (arguments.BatchMode)
                {
                    Logger.Write("Publishing operation confirmed by /BatchMode.");
                    confirmed = true;
                }
                else
                {
                    Logger.Write($"Requesting user confirmation to publish {accession} sessions {sessions} to {target}");

                    Utilities.NewLine();

                    switch (arguments.Target)
                    {
                        case DigitalArchiveSpecifier.Processing:
                            Utilities.WriteLine("The following content will be published to the processing (review) digital archive:", ConsoleColor.Cyan);
                            break;
                        case DigitalArchiveSpecifier.Production:
                            Utilities.WriteLine("The following content will be published to the production (public) digital archive:", ConsoleColor.Cyan);
                            break;
                    }

                    Utilities.WriteLine(package.GetSummaryReport());

                    // Get user confirmation prior to proceeding.
                    const string prompt = ">>>>> Please confirm that you wish to publish the content summarized above: [y/n]: ";
                    if (Utilities.GetUserConfirmation(prompt))
                    {
                        Logger.Write("Publishing operation confirmed by user.");
                        confirmed = true;
                    }
                    else
                    {
                        Logger.Write("Publishing operation cancelled by user.");
                        confirmed = false;
                    }
                }

                if (confirmed)
                {
                    AzureContentManager.PublishCollection(package, arguments.Target);
                }
            }
            catch (PublishingPackageException ex)
            {
                Logger.Error(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
            finally
            {
                if (package != null) package.Dispose();
            }

            // Log footer
            Logger.End();
        }

        private static void ShowUsage()
        {
            Utilities.NewLine();
            Utilities.WriteLine("PublishCollection <Accession> /S:n [/S:n] [/T:Processing|Production]");
            Utilities.NewLine();
            Utilities.WriteLine(Parser.ArgumentsUsage(typeof(Arguments)));
        }
    }
}
