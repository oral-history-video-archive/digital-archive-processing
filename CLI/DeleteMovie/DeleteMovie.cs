using System;

using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Processing.Database;

namespace InformediaCORE.DeleteMovie
{
// Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.Required,
            HelpText = "The canonical name of the movie to be deleted."
        )]
        public string MovieName;
    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Commandline tool for deleting movies and child data from the database.
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            // Log header
            Logger.Start();

            // Enforce uppercase naming rule.
            arguments.MovieName = arguments.MovieName.ToUpper();

            Logger.Write("Requesting user confirmation for collection deletion.");
            // TODO: Add a summary report about which movie is going to be affected
            Utilities.NewLine();
            Utilities.WriteLine("*****     WARNING!! THIS ACTION WILL HAVE THE FOLLOWING EFFECTS:", ConsoleColor.Red);
            Utilities.WriteLine("*****", ConsoleColor.Red);
            Utilities.WriteLine("*****     * THE MOVIE WILL BE DELETED FROM THE DATABASE IN ENTIRETY", ConsoleColor.Red);
            Utilities.WriteLine("*****     * THE RELATED SEGEMENT VIDEOS WILL BE DELETED FROM THE FILE SYSTEM", ConsoleColor.Red);
            Utilities.WriteLine("*****     * THE MOVIE WILL BE DELETED FROM THE PROCESSING ARCHIVE", ConsoleColor.Red);
            // TODO: Add ability to select which digital archive(s) will be affected.
            // TODO: Will need to re-publish the collection without the movie

            const string prompt = ">>>>> Please confirm that you wish to delete the movie and all related content [y/n]: ";

            if (Utilities.GetUserConfirmation(prompt))
            {
                Logger.Write("Operation confirmed by user.");
                //TODO: AzureContentManager.RecallMovie(arguments.MovieName);
                
                var dae = new DataAccessExtended();
                dae.DeleteMovie(arguments.MovieName, true);
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
