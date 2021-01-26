using CommandLine;
using InformediaCORE.Common;
using InformediaCORE.Processing;

namespace InformediaCORE.ImportXml
{
    // Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [DefaultArgument(
            ArgumentType.Required,
            HelpText = "Fully qualified path to the XML segmentation file."
        )]
        public string XmlFile;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = "",
            HelpText = "Overrides the path to the video file specified in a .movie.xml file."
        )]
        public string VideoPath;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = false,
            HelpText = "Specifies existing record should be updated with values from XML file."
        )]
        public bool Update;
    }
#pragma warning restore 0169, 0649

    internal class ImportXml
    {
        private static void Main(string[] args)
        {
            // Parse command line arguments
            var arguments = new Arguments();

            if (!Parser.ParseArgumentsWithUsage(args, arguments)) return;

            // Log header
            Logger.Start();

            // Run application
            XmlImporter.ImportXml(arguments.XmlFile, arguments.Update);

            // Log footer
            Logger.End();
        }
    }
}
