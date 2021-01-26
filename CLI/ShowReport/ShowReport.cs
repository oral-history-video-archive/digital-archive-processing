using CommandLine;

namespace InformediaCORE.ShowReport
{
// Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = 0,
            HelpText = "Specify id of a collection to detail."
        )]
        public int Collection;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = null,
            HelpText = "Specify accession of a collection to detail."
        )]
        public string Accession;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = ProcessFilter.All,
            HelpText = "(Optional) Set the results filter for the Processing Status report.",
            ShortName="pf"
        )]
        public ProcessFilter ProcessFilter;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = 0,
            HelpText = "Specify id of a movie to detail."
        )]
        public int Movie;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = false,
            HelpText = "Database overview."
        )]
        public bool Overview;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = false,
            HelpText = "Processing status per segment. Optionally used with /filter."
        )]
        public bool Processing;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = 0,
            HelpText = "Specify id of a segment to detail."
        )]
        public int Segment;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = 0,
            HelpText = "Specify id of a session to detail."
        )]
        public int Session;

        [Argument(
            ArgumentType.AtMostOnce, 
            DefaultValue= false,
            HelpText="Report status of generated web videos, must supply root path.  /something optional.",
            ShortName = "cv"
            )]
        public bool CheckVids;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = "",
            HelpText = "(Optional) Overrides the default WebVideo path given by InformediaCORE.config.",
            ShortName = "rp"
            )]
        public string RootPath;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = VideoReportFilter.MissingOnly,
            HelpText = "(Optional) Set results filter for the CheckVid report.",
            ShortName = "vf"
            )]
        public VideoReportFilter VideoFilter;
    }
#pragma warning restore 0169, 0649

    internal class ShowReport
    {
        private static void Main(string[] args)
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

            // Run application
            var reporter = new ReportGenerator();

            if (arguments.Collection != 0)
                reporter.CollectionDetails(arguments.Collection);

            if (arguments.Accession != null)
                reporter.CollectionDetails(arguments.Accession);

            if (arguments.Overview)
                reporter.DatabaseOverview();

            if (arguments.Movie != 0)
                reporter.MovieDetails(arguments.Movie);

            if (arguments.Segment != 0)
                reporter.SegmentDetails(arguments.Segment);

            if (arguments.Session != 0)
                reporter.SessionDetails(arguments.Session);

            if (arguments.Processing)
                reporter.ProcessingDetails(arguments.ProcessFilter);

            if (arguments.CheckVids)
                reporter.WebVideoFileCheck(arguments.RootPath, arguments.VideoFilter);
        }
    }
}
