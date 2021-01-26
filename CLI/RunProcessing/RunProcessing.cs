using System;

using CommandLine;
using InformediaCORE.Common;
using InformediaCORE.Processing;

namespace InformediaCORE.RunProcessing
{
// Suppress warnings about unused and uninitialized variables
#pragma warning disable 0169, 0649
    internal class Arguments
    {
        [Argument(
            ArgumentType.AtMostOnce,
            ShortName = "name",
            HelpText = "Name of segment to process."
        )]
        public string Name = String.Empty;

        [Argument(
            ArgumentType.AtMostOnce,
            ShortName = "id",
            HelpText = "ID of segment to process."
        )]        
        public int ID = 0;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue=false,
            ShortName = "all",
            HelpText = "Specifies all unprocessed segments should be processed."
        )]
        public bool All;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = false,
            ShortName = "frun",
            HelpText = "Forces all specified processes to generate new output."
        )]
        public bool ForceRerun;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = true,
            ShortName = "vid",
            HelpText = "Runs the web video transcoding task."
        )]
        public bool Video;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = true,
            ShortName = "key",
            HelpText = "Runs the keyframe extraction task."
        )]
        public bool Keyframe;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = true,
            ShortName = "aln",
            HelpText = "Runs the forced alignment task."
        )]
        public bool Alignment;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = true,
            ShortName = "cap",
            HelpText = "Runs the text captioning task."
        )]
        public bool Captions;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = true,
            ShortName = "nlp",
            HelpText = "Runs the spaCy NLP task."
        )]
        public bool SpacyNLP;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = true,
            ShortName = "ner",
            HelpText = "Runs the Stanford NER task."
        )]
        public bool StanfordNER;

        [Argument(
            ArgumentType.AtMostOnce,
            DefaultValue = true,
            ShortName = "ent",
            HelpText = "Runs the named entity recognition task"
        )]
        public bool Entity;
    }
#pragma warning restore 0169, 0649

    /// <summary>
    /// Command line tool for processing segments.
    /// </summary>
    internal class RunProcessing
    {
        private static void Main(string[] args)
        {
            // Restrict to a single running instance. Pattern gleaned from:
            // https://www.c-sharpcorner.com/UploadFile/f9f215/how-to-restrict-the-application-to-just-one-instance/
            var mutex = new System.Threading.Mutex(true, "RunProcessing", out bool createdNew);

            if (!createdNew)
            {
                Utilities.WriteLine("Only one instance of RunProcessing is allowed per machine. Exiting application.", ConsoleColor.Red);
                return;
            }

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

            // Initialize processor with arguments
            var processor = new SegmentProcessor
            {
                RunVideoTranscoder = arguments.Video,
                RunKeyFrameExtractor = arguments.Keyframe,
                RunForcedAligner = arguments.Alignment,
                RunCaptionGenerator = arguments.Captions,
                RunSpacyNLP = arguments.SpacyNLP,
                RunStanfordNER = arguments.StanfordNER,
                RunNamedEntityRecognizer = arguments.Entity,
                ForceRerun = arguments.ForceRerun
            };

            // Attempt to handle CTRL+C in a graceful manner
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                if (e.SpecialKey == ConsoleSpecialKey.ControlC)
                {
                    e.Cancel = true;
                    processor.Halt = true;
                    Logger.Write("CTRL+C received, gracefull shutdown in progress.");
                }
            };

            // Determine which type of segment processing to do here
            if (arguments.All && arguments.ForceRerun)
            {
                Logger.Error("Combination of /All + /RunAlways is not allowed.");
            }                
            else if (arguments.All)
            {
                processor.ProcessSegments();
            }
            else if (arguments.Name != String.Empty)
            {
                processor.ProcessSegment(arguments.Name);
            }
            else if (arguments.ID != 0)
            {
                processor.ProcessSegment(arguments.ID);
            }
            else
            {
                Logger.Error("You must specify something to process.");
            }

            // Log footer
            Logger.End();
        }
    }
}
