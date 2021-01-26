using System;
using System.IO;
using CommandLine;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Processing;

namespace InformediaCORE.ExportXml
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.Required,
            HelpText = "Accession number of the collection to export. Ex: /Accession:A2001.001"
        )]
        public string Accession;

        [Argument(
            ArgumentType.AtMostOnce,
            HelpText = @"Path where XML output should be generated. Ex: /Path:C:\XmlFiles\Here",
            DefaultValue = null
        )]
        public string Path;
    }
#pragma warning restore 0169, 0649

    internal class ExportXml
    {
        private static void Main(string[] args)
        {
            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            // Log header
            Logger.Start();

            // Enforce uppercase naming rule.
            arguments.Accession = arguments.Accession.ToUpper();

            if (arguments.Path == null)
            {
                arguments.Path = Path.Combine(Settings.Current.BuildPath, "Interviews", arguments.Accession);
            }

            if (!Directory.Exists(arguments.Path))
            {
                Utilities.NewLine();
                Utilities.WriteLine($"*****     The specified path {arguments.Path} does not exist", ConsoleColor.Yellow);
                if (Utilities.GetUserConfirmation(">>>>> Would you like to create it? [y/n]: "))
                {
                    Directory.CreateDirectory(arguments.Path);
                }
            }

            // Run application
            XmlExporter.ExportCollection(arguments.Accession, arguments.Path);

            // Log footer
            Logger.End();
        }
    }
}
