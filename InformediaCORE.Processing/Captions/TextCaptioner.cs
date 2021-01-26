using System;
using System.Collections.Generic;

using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing.Captions.Types;
using InformediaCORE.Processing.Gentle.Models;

namespace InformediaCORE.Processing.Captions
{
    public static class TextCaptioner
    {
        /// <summary>
        /// Shorthand reference to relevant configuration settings.
        /// </summary>
        private static readonly CaptioningTaskSettings settings = Settings.Current.CaptioningTask;

        /// <summary>
        /// Generates captions for the given Segment from the given alignment results.
        /// </summary>
        /// <param name="alignmentResult">Alignment output from Gentle.</param>
        /// <param name="segment"></param>
        /// <returns></returns>
        public static TextCaptions CaptionText(AlignmentResult alignmentResult, Segment segment)
        {
            var data = AlignmentFormatter.FormatAlignment(alignmentResult, segment.Duration ?? 0);

            // PASS 1 - CAPTIONS
            Logger.Write("Captioner: Initial Pass ...");
            var captions = new TextCaptions
            {
                Cues = GenerateCues(data)
            };

            // PASS 2 - MERGE TOO SMALL
            Logger.Write("Captioner: Combining Pass ...");
            CoalescingPass(captions.Cues);

            // NOTE: Consider a borrow/timeshift pass here

            // PASS 3 - BORROW TIME IF STILL TOO SMALL
            Logger.Write("Captioner: Validation Pass ...");
            ValidationPass(captions.Cues);

            return captions;
        }

        /// <summary>
        /// Determines which order the speakers appear within the transcript.
        ///   S1 = the interviewer
        ///   S2 = the subject being interviewed
        /// Default (expected) order is S1 then S2 but sometimes it's not.
        /// </summary>
        /// <param name="alignment">The formatted alignment data.</param>
        /// <returns>A array with two strings identifying the speaker order.</returns>
        private static string[] DetermineSpeakerOrder(FormattedAlignment alignment)
        {
            // Count total characters spoken by first and second speaker.
            int[] charCount = new int[2];
            var p = alignment.Paragraphs;
            for(int i = 0; i < p.Count; i++)
            {
                int index = i % 2;
                charCount[index] += p[i].Text.Length;
            }

            // Calculate the ratio of characters between speakers
            var ratio = (charCount[1] == 0) ? 0.0 : (double)(charCount[0] / charCount[1]);

            string[] results;

            if (ratio < settings.Speaker1ToSpeaker2CharRatio)
            {
                // Default ordering: interviewer speaks first
                results = new string[] { "S1", "S2" };
            }
            else
            {
                // Reverse ordering: subject speaks first
                results = new string[] { "S2", "S1" };
            }

            return results;
        }

        /// <summary>
        /// Generate initial draft list of cues.
        /// </summary>
        /// <param name="alignment">The formatted alignment data.</param>
        /// <returns>A list of cues which may require additional clean-up.</returns>
        private static List<CaptionCue> GenerateCues(FormattedAlignment alignment)
        {
            var speakers = DetermineSpeakerOrder(alignment);

            var lines = new List<CueText>();

            for(var i = 0; i < alignment.Paragraphs.Count; i++)
            {
                var p = alignment.Paragraphs[i];

                // SPECIAL CASE: NO NARRATION (may span entire length of video)
                if (p.Text == FormattedAlignment.NO_NARRATION)
                {
                    lines.Add(new CueText(string.Empty, p.Words));
                }
                // CASE: Too Big
                else if( p.Duration > settings.MaxCueDuration || p.Length > settings.MaxCueLength )
                {
                    string speakerID = speakers[i % 2];
                    var tmpLine = new CueText(speakerID, p.Words);

                    CueText newLine = new CueText(speakerID);
                    while (tmpLine.Words.Count > 0)
                    {
                        // NOTE: Experiment with weighting function as possible solution to better segmentation
                        // Consider breaking on: pauses, commas, periods, and ??
                        newLine.Words.Add(tmpLine.Words[0]);    // Add first word from temp cue to new cue
                        tmpLine.Words.RemoveAt(0);              // Remove that word from temp

                        // Check for a breaking condition
                        if  (  newLine.Length >= settings.TargetLength      // Hit target length
                            || newLine.Duration >= settings.TargetDuration  // Hit target duration
                            || tmpLine.Words.Count == 0)                    // We ran out of words.
                        {
                            // If the remaining amount can be incorporated, then do it.
                            if  (  tmpLine.Words.Count > 0
                                && tmpLine.Duration < settings.MinCueDuration
                                && tmpLine.Duration + newLine.Duration < settings.MaxCueDuration )
                            {
                                newLine.Words.AddRange(tmpLine.Words.GetRange(0, tmpLine.Words.Count));
                                tmpLine.Words.Clear();
                            }

                            lines.Add(newLine);
                            newLine = new CueText(speakerID);
                        }
                    }
                }
                // CASE: Just Right (or possibly too small, we'll deal with that in a later pass)
                else
                {
                    lines.Add(new CueText(speakers[i % 2], p.Words));
                }
            }

            // Encapsulate each line within a cue for later processing
            var cues = new List<CaptionCue>();

            foreach(var line in lines)
            {
                var cue = new CaptionCue();
                cue.Lines.Add(line);
                cues.Add(cue);
            }

            return cues;
        }


        /// <summary>
        /// Enumeration defining which neighboring lines are candidates for combining.
        /// </summary>
        [Flags]
        private enum Candidates : short
        {
            None = 0,
            Prev = 1,
            Next = 2,
            Both = Prev | Next
        }

        /// <summary>
        /// Coalesce short cues with their neighbors if possible.
        /// </summary>
        /// <param name="cues">The list of caption cues.</param>
        private static void CoalescingPass(List<CaptionCue> cues)
        {
            for(var i = 0; i < cues.Count; i++)
            {
                var thisCue = cues[i];
                if (thisCue.Duration < settings.MinCueDuration)
                {
                    var prevCue = (i > 0) ? cues[i - 1] : null;
                    var nextCue = (i < cues.Count - 1) ? cues[i + 1] : null;

                    Candidates candidate = Candidates.None;

                    if ( prevCue != null
                      && prevCue.Duration + thisCue.Duration < settings.MaxCueDuration
                      && prevCue.Lines.Count < settings.MaxCueLineCount)
                    {
                        candidate |= Candidates.Prev;
                    }

                    if ( nextCue != null
                      && nextCue.Duration + thisCue.Duration < settings.MaxCueDuration
                      && nextCue.Lines.Count < settings.MaxCueLineCount)
                    {
                        candidate |= Candidates.Next;
                    }

                    switch (candidate)
                    {
                        case Candidates.Both:
                            // Pick the shorter of the two
                            if (prevCue.Duration <= nextCue.Duration) 
                            {
                                goto case Candidates.Prev;
                            } 
                            else
                            {
                                goto case Candidates.Next;
                            }                            
                        case Candidates.Prev:
                            Logger.Write($"CoalescingPass: cue[{i}] combining with prev cue.");
                            CombineCues(prevCue, thisCue);
                            cues.Remove(thisCue);
                            i -= 1;     // The current cue is gone, move the pointer back to the prior cue.
                            break;
                        case Candidates.Next:
                            Logger.Write($"CoalescingPass: cue[{i}] combining with next cue.");
                            CombineCues(thisCue, nextCue);
                            cues.Remove(nextCue);
                            break;
                        case Candidates.None:
                            Logger.Warning($"CoalescingPass: cue[{i}] too short but no suitable neighbors for combining.");
                            continue;
                        default:
                            Logger.Error($"CoalescingPass: cue[{i}] generated unknown candidate value {candidate}");
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Combine the given cues.
        /// </summary>
        /// <param name="targetCue">The cue to be kept; assumed to be temporally first.</param>
        /// <param name="sourceCue">The cue to be assimilated; assumed to be temporally next.</param>
        private static void CombineCues(CaptionCue targetCue, CaptionCue sourceCue)
        {
            // Copy lines from source to target
            foreach(var line in sourceCue.Lines)
            {
                targetCue.Lines.Add(line);
            }

            // Merge target lines where applicable
            var lines = targetCue.Lines;
            for(var i = 0; i < lines.Count - 1; i++)
            {
                var thisLine = lines[i];
                var nextLine = lines[i + 1];
                if (thisLine.SpeakerID == nextLine.SpeakerID)
                {
                    // Merge the two lines
                    foreach(var word in nextLine.Words)
                    {
                        thisLine.Words.Add(word);
                    }
                    // Delete the latter
                    lines.RemoveAt(i + 1);
                }
            }
        }

        /// <summary>
        /// Check integrity of captions and log possible problems.
        /// </summary>
        /// <param name="cues">The list of caption cues.</param>
        private static void ValidationPass(List<CaptionCue> cues)
        {
            int durationTooShort = 0;
            int durationTooLong = 0;
            int lineCountTooFew = 0;
            int lineCountTooMany = 0;
            int lineTextMissing = 0;
            int lineLengthTooLong = 0;

            Logger.Write($"Validating {cues.Count} cues...");

            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];

                if (cue.Duration < settings.MinCueDuration)
                {
                    durationTooShort++;
                    Logger.Warning($"cue[{i}]: duration {cue.Duration}ms less than allowed minimum {settings.MinCueDuration}ms.");
                }

                if (cue.Duration > settings.MaxCueDuration)
                {
                    durationTooLong++;
                    Logger.Warning($"cue[{i}]: duration {cue.Duration}ms greather than allowed maximum {settings.MaxCueDuration}ms.");
                }

                if (cue.Lines.Count < 1)
                {
                    lineCountTooFew++;
                    Logger.Warning($"cue[{i}]: line count of {cue.Lines.Count} less than allowed minimum of 1.");
                }

                if (cue.Lines.Count > settings.MaxCueLineCount)
                {
                    lineCountTooMany++;
                    Logger.Warning($"cue[{i}]: line count of {cue.Lines.Count} greather than allowed maximum {settings.MaxCueLineCount}.");
                }

                for(int j = 0; j < cue.Lines.Count; j++)
                {
                    var line = cue.Lines[j];
                    if (line.Length == 0)
                    {
                        lineTextMissing++;
                        Logger.Warning($"  line[{j}]: has no text.");
                    }
                    if (line.Length > settings.MaxCueLength)
                    {
                        lineLengthTooLong++;
                        Logger.Warning($"  line[{j}]: length {line.Length} chars greater than allowed maximum {settings.MaxCueLength} chars.");
                    }
                }
            }

            var totalErrors = durationTooShort + durationTooLong + lineCountTooFew + lineCountTooMany + lineTextMissing + lineLengthTooLong;
            Logger.Write($"Validation complete: {totalErrors} problems detected.");

            if (durationTooShort > 0)   Logger.Write($"  {durationTooShort:###} cues too short.");
            if (durationTooLong > 0)    Logger.Write($"  {durationTooLong:###} cues too long.");
            if (lineCountTooFew > 0)    Logger.Write($"  {lineCountTooFew:###} cues with too few lines.");
            if (lineCountTooMany > 0)   Logger.Write($"  {lineCountTooMany:###} cues with too many lines.");
            if (lineTextMissing > 0)    Logger.Write($"  {lineTextMissing:###} lines missing text.");
            if (lineLengthTooLong > 0)  Logger.Write($"  {lineLengthTooLong:###} lines too long.");
        }

    }
}
