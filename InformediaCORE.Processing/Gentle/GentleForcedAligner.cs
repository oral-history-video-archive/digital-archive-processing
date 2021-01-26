using System;
using System.Diagnostics;
using System.IO;

using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Processing.Gentle.Models;
using Newtonsoft.Json;

namespace InformediaCORE.Processing.Gentle
{
    /// <summary>
    /// A class for aligning text using Gentle Forced Aligner.
    /// </summary>
    /// <remarks>
    /// SEE: https://lowerquality.com/gentle/
    /// </remarks>
    public static class GentleForcedAligner
    {
        /// <summary>
        /// Directory where PowerShell Core 6+ is installed.
        /// </summary>
        private static readonly string PowerShellPath = Settings.Current.ExternalTools.PowerShellPath;

        /// <summary>
        /// Aligns the given transcript file to the spoken audio in given MPEG-4 file.
        /// </summary>
        /// <param name="mp4File">Fully qualified path to an MPEG-4 video.</param>
        /// <param name="txtFile">Fully qualified path to the transcript file to align.</param>
        /// <returns></returns>
        /// <remarks>
        /// Input files must be located in the same directory and be on a locally accessible file system.
        /// </remarks>
        public static AlignmentResult AlignTranscript(string mp4File, string txtFile)
        {
            AlignmentResult result;

            // Prepare pathing information to be passed to RunAligner
            var workingDirectory = Path.GetDirectoryName(mp4File);
            mp4File = Path.GetFileName(mp4File);
            txtFile = Path.GetFileName(txtFile);

            // Run once with default settings
            var pass1Result = RunAligner(workingDirectory, mp4File, txtFile, false);
            var (unaligned1, maxConsec1) = AnalyzeResults(pass1Result);
            Logger.Write($"Results: {unaligned1} unaligned words with a maximum consecutive run of {maxConsec1} words.");

            if (unaligned1 == 0)
            {
                // Perfect results, skip second pass
                result = pass1Result;
            }
            else
            {
                // Run once with conservative settings
                var pass2Result = RunAligner(workingDirectory, mp4File, txtFile, true);
                var (unaligned2, maxConsec2) = AnalyzeResults(pass2Result);
                Logger.Write($"Results: {unaligned2} unaligned words with a maximum consecutive run of {maxConsec2} words.");

                if (unaligned1 == unaligned2)
                {
                    // It's a tie - use MaxConsec as tie breaker
                    Logger.Write("Unaligned words equivalent; selecting result set with least maximum consecutive words.");
                    result = (maxConsec1 <= maxConsec2) ? pass1Result : pass2Result;
                }
                else
                {
                    // Choose one with fewest unaligned words
                    Logger.Write("Selecting result set with fewest unaligned words.");
                    result = (unaligned1 < unaligned2) ? pass1Result : pass2Result;
                }
            }

            if (result == pass1Result)
            {
                Logger.Write("Results from default pass selected.");
            }
            else
            {
                Logger.Write("Results from conservative pass selected.");
            }

            return result;
        }

        #region == PRIVATE STATIC
        /// <summary>
        /// Runs Gentle Forced Aligner on the given input.
        /// </summary>
        /// <param name="workingDirectory">Directory containing the media files.</param>
        /// <param name="mp4File">MPEG4 video file.</param>
        /// <param name="txtFile">Transcript to align.</param>
        /// <param name="useConservativeSettings">If true, runs alignment with conservative settings; uses default settings if false or unspecified.</param>
        /// <returns>An object containing the alignment results.</returns>
        private static AlignmentResult RunAligner(string workingDirectory, string mp4File, string txtFile, bool useConservativeSettings = false)
        {
            var options = string.Empty;
            if (useConservativeSettings)
            {
                Logger.Write("Running Gentle with conservative settings.");
                options = "--conservative";
            }
            else
            {
                Logger.Write("Running Gentle with default settings.");
            }

            AlignmentResult result = null;

            using (var process = new Process())
            {
                process.StartInfo.FileName = Path.Combine(PowerShellPath, "pwsh.exe");
                process.StartInfo.Arguments = $"-Command wsl python3 ~/gentle/align.py {options} {mp4File} {txtFile}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.WorkingDirectory = workingDirectory;

                if (process.Start())
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    process.Close();

                    result = JsonConvert.DeserializeObject<AlignmentResult>(output);
                }
                else
                {
                    Logger.Error("FAILED to start process.");
                }
            }

            return result;
        }

        /// <summary>
        /// Calculates performance metrics for the given alignment document.
        /// </summary>
        /// <param name="doc">The results be analyzed.</param>
        /// <returns>
        /// A tuple of integers containing the total count of unaligned words and the 
        /// maximum number of consecutive unaligned words.
        /// </returns>
        private static (int Unaligned, int MaxConsecutive) AnalyzeResults(AlignmentResult doc)
        {
            // SPECIAL CASE: Likely no narration in audio
            if (doc.Words == null) return (0, 0);

            int maxConsecutive = 0;
            int consecutive = 0;
            int unaligned = 0;

            foreach (var word in doc.Words)
            {
                switch (word.Case)
                {
                    case "success":
                        maxConsecutive = Math.Max(consecutive, maxConsecutive);
                        consecutive = 0;
                        break;
                    case "not-found-in-audio":
                        unaligned++;
                        consecutive++;
                        break;
                    case "not-found-in-transcript":
                        Logger.Warning($"Ignoring disfluency in alignment data: {word.Word}");
                        break;
                    default:
                        Logger.Warning($"Found unknown case in alignment data: {word.Case}");
                        goto case "success";
                }
            }

            return (unaligned, maxConsecutive);
        }
        #endregion PRIVATE STATIC
    }
}
