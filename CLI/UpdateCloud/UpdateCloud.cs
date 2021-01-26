using System;
using System.IO;
using CommandLine;
using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;


namespace UpdateCloud
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = TargetSpecifier.Both,
            HelpText = "Determines which digital archive will be the target of this operation."
        )]
        public TargetSpecifier Target;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = false,
            HelpText = "Bypasses the user confirmation step when running as a batch process."
        )]
        public bool SkipConfirmation;

    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Command line utility which pushes queued updates from the processing database
    /// to the appropriate Azure Blob Storage and Azure Search Service accounts.
    /// </summary>
    class UpdateCloud
    {
        /// <summary>
        /// Program entry point
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        static void Main(string[] args)
        {
            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            // Log header
            Logger.Start();

            var accessions = CloudUpdater.GetQueuedUpdates();

            if (accessions == null || accessions.Count < 1)
            {
                Logger.Write("No records found requiring updates.");
            }
            else if (IsConfirmed(arguments, accessions.Count))
            {
                CloudUpdater.ApplyUpdates(accessions);
            }

            // Log footer
            Logger.End();
        }

        /// <summary>
        /// Determines if the operation is confirmed to proceed.
        /// </summary>
        /// <param name="arguments">Parsed command line arguments.</param>
        /// <param name="count">Number of modified records to be updated.</param>
        /// <returns>True if confirmed; false otherwise.</returns>
        internal static bool IsConfirmed(Arguments arguments, int count)
        {
            if (arguments.SkipConfirmation)
            {
                Logger.Write("Skipping user confirmation.");
                return true;
            }

            Logger.Write("Requesting user confirmation...");

            Utilities.NewLine();
            Utilities.WriteLine("****** ATTENTION ******", ConsoleColor.Red);
            Utilities.NewLine();

            Utilities.WriteLine($"This operation will upload {count} modified records from the", ConsoleColor.Red);
            Utilities.WriteLine("processing database to the following:", ConsoleColor.Red);            
            if (arguments.Target == TargetSpecifier.Both || arguments.Target == TargetSpecifier.ProcessingOnly)
            {
                Utilities.NewLine();
                Utilities.WriteLine("Processing Digital Archive (a.k.a. test site) accounts:", ConsoleColor.Red);
                Utilities.NewLine();
                Utilities.WriteLine($"     Azure Search: {Settings.Current.Processing.AzureSearchServiceName}", ConsoleColor.Red);
                Utilities.WriteLine($"    Azure Storage: {Settings.Current.Processing.AzureStorageAccountName}", ConsoleColor.Red);
                Utilities.NewLine();
            }

            if (arguments.Target == TargetSpecifier.Both || arguments.Target == TargetSpecifier.ProductionOnly)
            {
                Utilities.NewLine();
                Utilities.WriteLine("Production Digital Archive (a.k.a. live site) accounts:", ConsoleColor.Red);
                Utilities.NewLine();
                Utilities.WriteLine($"     Azure Search: {Settings.Current.Production.AzureSearchServiceName}", ConsoleColor.Red);
                Utilities.WriteLine($"    Azure Storage: {Settings.Current.Production.AzureStorageAccountName}", ConsoleColor.Red);
                Utilities.NewLine();
            }

            const string prompt = ">>>>> Do you wish to continue? [y/n]: ";

            if (Utilities.GetUserConfirmation(prompt))
            {
                Logger.Write("Operation confirmed by user.");
                return true;
            }
            else
            {
                Logger.Write("Operation cancelled by user.");
                return false;
            }
        }
    }
}
