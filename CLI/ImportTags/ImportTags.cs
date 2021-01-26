using CommandLine;
using InformediaCORE.Common;
using InformediaCORE.Firebase;
using System;

namespace ImportTags
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.AtMostOnce,
            ShortName = "s",
            HelpText = "ID of the segment to retrieve from Firebase."
        )]
        public int SegmentID = 0;

        [Argument(
            ArgumentType.AtMostOnce,
            ShortName = "a",
            HelpText = "Accession of the collection to retrieve from Firebase."
        )]
        public string Accession = null;
    }
#pragma warning restore 0169, 0649

    class ImportTags
    {
        static void Main(string[] args)
        {
            // If no arguments specified, show usage.
            if (args == null || args.Length == 0)
            {
                // Force usage to display by sending the /? flag to the argument parser.
                args = new[] { "/?" };
            }

            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            Logger.Start();

            if (arguments.SegmentID > 0)
            {
                TagImporter.ImportTagsForSegment(arguments.SegmentID);
            }
            else if (arguments.Accession != null)
            {
                TagImporter.ImportTagsForCollection(arguments.Accession);
            }

            Logger.End();
        }
    }
}
