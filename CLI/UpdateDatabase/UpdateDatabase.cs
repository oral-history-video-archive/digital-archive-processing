using System;
using System.IO;
using CommandLine;
using InformediaCORE.Common;

namespace InformediaCORE.UpdateDatabase
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [DefaultArgument(
            ArgumentType.Required,
            HelpText = "Fully qualified path to the Excel spreadsheet containing the updated data."
        )]
        public string ExcelFile;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = DataType.Collection,
            HelpText = "Specifies the type of data in the spreadsheet: Collection|Occupation|Session"
        )]
        public DataType Type;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = "Sheet1",
            HelpText = "Specifies the name of the worksheet to import."
        )]
        public string Worksheet;

    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Updates the database from Excel spreadsheets exported from 
    /// The HistoryMakers' Filemaker database.
    /// </summary>
    internal class UpdateDatabase
    {
        /// <summary>
        /// Program entry point
        /// </summary>
        /// <param name="args"></param>
        private static void Main(string[] args)
        {
            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            // Log header
            Logger.Start();

            try
            {
                if (!File.Exists(arguments.ExcelFile))
                {
                    Logger.Error("Could not find specified spreadsheet: {0}", arguments.ExcelFile);
                    return;
                }


                switch (arguments.Type)
                {
                    case DataType.Collection:
                        CollectionUpdater.UpdateData(arguments.ExcelFile, arguments.Worksheet);
                        break;
                    case DataType.Session:
                        SessionUpdater.UpdateData(arguments.ExcelFile, arguments.Worksheet);
                        break;
                    default:
                        Logger.Error("Invalid Type specified: {0}", arguments.Type.ToString());
                        return;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }

            // Log footer
            Logger.End();
        }
    }
}
