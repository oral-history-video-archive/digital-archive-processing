﻿using System;
using System.Diagnostics;
using System.IO;

using InformediaCORE.Common;
using InformediaCORE.Common.Database;

namespace InformediaCORE.Processing.Tasks
{
    class SpacyTask : AbstractTask
    {
        /// <summary>
        /// The fully qualified path to the Python and Spacy installation folder.
        /// </summary>
        private readonly string SpacyPath = Settings.ExternalTools.SpacyPath;

        /// <summary>
        /// Backing storage for the OutputFile property.
        /// </summary>
        private string _outputFile;

        /// <summary>
        /// The name of the output file generated by this process.
        /// </summary>
        private string OutputFile
        {
            get
            {
                if (String.IsNullOrEmpty(_outputFile))
                {
                    _outputFile = Path.Combine(DataPath, $"{SegmentID}.spacy.txt");
                }
                return _outputFile;
            }
        }

        #region == Constructors
        /// <summary>
        /// Instantiates an instance from the given segment id.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="condition">Sets the conditions for when the task should be run.</param>
        public SpacyTask(int segmentID, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segmentID, condition) { }

        /// <summary>
        /// Instantiates an instance from the given segment name.
        /// </summary>
        /// <param name="segmentName">A valid segment name.</param>
        /// <param name="condition">Sets the conditions for when the task should be run.</param>
        public SpacyTask(string segmentName, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segmentName, condition) { }

        /// <summary>
        /// Instantiates an instance from the given segment.
        /// </summary>
        /// <param name="segment">A database Segment.</param>
        /// <param name="condition">Sets the conditions for when the task should be run.</param>
        public SpacyTask(Segment segment, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segment, condition) { }
        #endregion Constructors

        #region == Overrides
        /// <summary>
        /// Checks that the necessary input requirements are met prior to running the task.
        /// </summary>
        internal override void CheckRequirements()
        {
            if (!Directory.Exists(SpacyPath))
                throw new TaskRunException($"Configured SpacyPath does not exist: {SpacyPath}");

            if (!File.Exists(Path.Combine(SpacyPath, "python.exe")))
                throw new TaskRunException($"Could not find python.exe in configured SpacyPath: {SpacyPath}");

            if (Segment.TranscriptText == null)
                throw new TaskRequirementsException("Transcript is null.");

            if (Segment.TranscriptText == string.Empty)
                throw new TaskRequirementsException("Transcript is empty.");
        }

        /// <summary>
        /// Purges prior task results for the associated segment from the database.
        /// </summary>
        internal override void Purge()
        {
            if (File.Exists(OutputFile) && RunCondition == RunConditionValue.Always)
            {
                Logger.Write("Deleting existing spaCy NLP data {0}", OutputFile);
                File.Delete(OutputFile);
            }
        }

        /// <summary>
        /// Runs the task.
        /// </summary>
        internal override void Run()
        {
            var tmpPath = Path.GetTempPath();
            var txtFile = Path.Combine(tmpPath, Segment.SegmentName + ".txt");

            try
            {
                // ============================================================
                // CREATE OUTPUT DIRECTORY
                if (!Directory.Exists(DataPath))
                    Directory.CreateDirectory(DataPath);

                if (!Directory.Exists(DataPath))
                    throw new TaskRunException($"Failed to create output directory {DataPath}.");

                if (File.Exists(OutputFile) && RunCondition != RunConditionValue.Always)
                {
                    Logger.Write("Keeping existing spaCy NLP data: {0}", OutputFile);
                }
                else
                {
                    // ============================================================
                    // STAGING
                    Logger.Write("Staging transcript to: {0}", txtFile);
                    Utilities.WriteToFile(Segment.TranscriptText, txtFile);

                    // ============================================================
                    // RUN SPACY NLP
                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Combine(SpacyPath, "python.exe"),
                            Arguments = $"runSpacy.py -i \"{txtFile}\" -o \"{OutputFile}\"",
                            UseShellExecute = false,
                            WorkingDirectory = SpacyPath
                        };

                        if (process.Start())
                        {
                            process.WaitForExit();
                            process.Close();
                        }
                        else
                        {
                            throw new TaskRunException("FAILED to start process: spaCy NLP.");
                        }
                    }
                }
            }
            catch
            {
                // Rethrow the exception while preserving the stack trace
                // See: https://docs.microsoft.com/en-us/visualstudio/code-quality/ca2200?view=vs-2019
                throw;
            }
            finally
            {
                // Clean up temp files.
                Logger.Write("Cleaning up temporary files.");
                File.Delete(txtFile);
            }
        }
        #endregion Overrides
    }
}
