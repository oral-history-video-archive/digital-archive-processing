using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;

using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Processing.Captions.Types;
using InformediaCORE.Processing.Gentle.Models;


namespace InformediaCORE.Processing.Captions
{
    public static class AlignmentFormatter
    {
        #region == CONSTANTS
        /// <summary>
        /// If the number of consecutive unaligned words at the end of transcript
        /// exceeds this threshold, the entire block will be deleted from the
        /// alignment data.
        /// </summary>
        private static readonly int MaxUnalignedTrailingWordsAllowed = Settings.Current.AlignmentFormatter.MaxUnalignedTrailingWordsAllowed;
        #endregion CONSTANTS

        /// <summary>
        /// Converts the output from the forced aligner into a caption-ready format.
        /// This is a class factory for FormattedAligment.
        /// </summary>
        /// <param name="alignmentResult"></param>
        /// <param name="duration"></param>
        /// <returns>Caption-ready alignment data.</returns>
        internal static FormattedAlignment FormatAlignment(AlignmentResult alignmentResult, int duration)
        {
            var paragraphs = new List<AlignedParagraph>();

            // SPECIAL CASE - NO NARRATION
            // If the word array is null or empty, then we assume there is no spoken audio
            if (0 == (alignmentResult.Words?.Length ?? 0))
            {
                var p = new AlignedParagraph(0, FormattedAlignment.NO_NARRATION.Length, FormattedAlignment.NO_NARRATION);
                p.Words.Add(new TimedText
                {
                    Case = "interpolated",
                    Text = FormattedAlignment.NO_NARRATION,
                    Type = "no-narration",
                    OffsetStart = 0,
                    OffsetEnd = 10,
                    TimeStart = 0,
                    TimeEnd = duration
                });
                paragraphs.Add(p);
            }
            // NORMAL CASE
            else
            {
                // Initialize words list with raw data
                var words = new List<TimedText>();
                foreach (var w in alignmentResult.Words)
                {
                    words.Add(new TimedText
                    {
                        Case = (w.Case == "success") ? "aligned" : "unaligned",
                        Text = w.Word,
                        Type = "word",
                        OffsetStart = w.StartOffset,
                        OffsetEnd = w.EndOffset,
                        TimeStart = (int)(Math.Round(w.Start, 3) * 1000),
                        TimeEnd = (int)(Math.Round(w.End, 3) * 1000)
                    });
                }

                // Fix data bugs
                FixKnownDataBugs(words, duration);

                // Estimate times for words which Gentle couldn't align.
                // InterpolateUnalignedWordTimes(words, duration);
                var transcript = InterpolateUnalignedWordTimes(words, duration, alignmentResult.Transcript);

                // Break data into clean caption-ready paragraphs.
                paragraphs = GenerateParagraphs(words, transcript);
            }

            return new FormattedAlignment(paragraphs);
        }

        #region == FIX DATA BUGS
        private static void FixKnownDataBugs(List<TimedText> words, int duration)
        {
            // Filter out unaligned words for now, they'll just get in the way
            var alignedWords = words.Where(w => w.Case == "aligned").ToList();

            if (alignedWords.Count > 1)
            {
                FixNonMonotonicStartTimes(alignedWords);
                FixOverlappingWordTimes(alignedWords);
            }

            if (alignedWords.Count > 0)
            {
                FixIllegalEndTimes(alignedWords, duration);
            }
        }

        /// <summary>
        /// Fix words which have nonmonotonically increasing timing data.
        /// </summary>
        /// <param name="alignedWords">A list of just the words with timing data.</param>
        /// <param name="duration">The duration of the video in milliseconds.</param>
        private static void FixNonMonotonicStartTimes(List<TimedText> alignedWords)
        {
            Logger.Write("Checking for non-monotonic word times...");

            // Create a list of words sorted by start time...
            var timeSorted = alignedWords.OrderBy(w => w.TimeStart).ToList();

            // Compare that to the original list...
            for(int i = 0; i < alignedWords.Count - 1; i++)
            {
                if (alignedWords[i] != timeSorted[i])
                {
                    // Adjust times when out of order
                    alignedWords[i].TimeStart = timeSorted[i].TimeStart;
                    alignedWords[i].TimeEnd = timeSorted[i].TimeEnd;
                }
            }
        }

        /// <summary>
        /// Fix words with overlapping start and end times by swapping the two
        /// </summary>
        /// <param name="alignedWords">A list of just the words with timing data.</param>
        /// <param name="duration">The duration of the video in milliseconds.</param>
        private static void FixOverlappingWordTimes(List<TimedText> alignedWords)
        {
            Logger.Write("Checking for overlapping word times...");

            var lastIndex = alignedWords.Count - 2;     // Stop 1 short of last index
            for(int i = 0; i < lastIndex; i++)
            {
                if (alignedWords[i].TimeEnd > alignedWords[i+1].TimeStart)
                {
                    int cachedEnd = alignedWords[i].TimeEnd;
                    alignedWords[i].TimeEnd = alignedWords[i + 1].TimeStart;
                    alignedWords[i + 1].TimeStart = cachedEnd;
                }
            }
        }

        /// <summary>
        /// Fix words whose times exceed the end of the video.
        /// </summary>
        /// <param name="alignedWords">A list of just the words with timing data.</param>
        /// <param name="duration">The duration of the video in milliseconds.</param>
        private static void FixIllegalEndTimes(List<TimedText> alignedWords, int duration)
        {
            Logger.Write("Checking for illegal end times...");

            int lastGoodIndex = 0;
            int lastIndex = alignedWords.Count - 1;

            for (int i = lastIndex; i >= 0; i--)
            {
                if (alignedWords[i].TimeEnd <= duration)
                {
                    lastGoodIndex = i;
                    break;
                }
            }

            if (lastGoodIndex != lastIndex)
            {
                InterpolateRange(
                    alignedWords.GetRange(lastGoodIndex, lastIndex - lastGoodIndex + 1),
                    alignedWords[lastGoodIndex].TimeStart,
                    duration,
                    "aligned"
                );
            }
        }
        #endregion FIX DATA BUGS

        #region == INTERPOLATION
        /// <summary>
        /// Generate interpolated timing data for the unaligned words in the given word list.
        /// </summary>
        /// <param name="words">The complete list of words from the alignment file.</param>
        /// <param name="duration">The known duration of the segment video.</param>
        /// <param name="transcript">The full transcript text.</param>
        /// <returns>An updated copy of the transcript.</returns>
        private static string InterpolateUnalignedWordTimes(List<TimedText> words, int duration, string transcript)
        {
            var priorCase = "aligned";
            int startTime = 0;
            int startIndex = 0;

            // Find blocks of one or more consecutive unaligned words and
            // pass that block onto helper function to be interpolated.
            int maxIndex = words.Count - 1;
            for (int i = 0; i <= maxIndex; i++)
            {
                var currentCase = words[i].Case;

                if (priorCase == "aligned")
                {
                    if (currentCase == "aligned")
                    {
                        // Possible candidate for start time
                        startTime = words[i].TimeEnd;
                    }
                    else
                    {
                        // Start of unaligned block
                        startIndex = i;

                        if (i == maxIndex)
                        {
                            // Special Case: hit end-of-array before closing unaligned block
                            InterpolateRange(words.GetRange(startIndex, 1), startTime, duration);
                        }
                    }
                }
                else // priorCase == "unaligned"
                {
                    if (currentCase == "aligned")
                    {
                        var count = i - startIndex;
                        var endTime = words[i].TimeStart;
                        InterpolateRange(words.GetRange(startIndex, count), startTime, endTime);
                    }
                    else if (i == maxIndex)
                    {
                        // Special Case: hit end-of-array before closing unaligned block
                        var count = maxIndex - startIndex + 1;
                        
                        // Are there too many unaligned words at the end?
                        if (maxIndex - startIndex + 1 > MaxUnalignedTrailingWordsAllowed)
                        {
                            // Yes, so remove them and we're done.
                            Logger.Warning("Truncating {0} unaligned words from end of transcript.", count);
                            transcript = transcript.Substring(0, words[startIndex].OffsetStart);
                            words.RemoveRange(startIndex, count);                            
                        }
                        else
                        {
                            // No, so we need to interpolate using the end of the video as the end time.
                            InterpolateRange(words.GetRange(startIndex, count), startTime, duration);
                        }                        
                    }
                }

                priorCase = currentCase;
            }

            return transcript;
        }

        /// <summary>
        /// Interpolate timing data for the given range of words.
        /// </summary>
        /// <param name="range">A range of words to be interpolated.</param>
        /// <param name="startTime">The start time to use for the range.</param>
        /// <param name="endTime">The end time to use for the range.</param>
        /// <param name="wordCase">The resulting Case value for each interpolated word.  Defaults to "interpoloted".</param>
        private static void InterpolateRange(List<TimedText> range, int startTime, int endTime, string wordCase = null)
        {
            // Calculate interpolated word duration
            int wordDuration = (endTime - startTime) / range.Count;

            // Update words with new interpolated values
            foreach (var word in range)
            {
                word.Case = String.IsNullOrEmpty(wordCase) ? "interpolated" : wordCase;
                word.TimeStart = startTime;
                word.TimeEnd = startTime + wordDuration;
                startTime = word.TimeEnd;
            }
        }
        #endregion INTERPOLATION

        #region == PARAGRAPH PROCESSING
        /// <summary>
        /// Break transcript into paragraphs with associated list of aligned words.
        /// </summary>
        /// <param name="words">Full list of aligned words.</param>
        /// <param name="transcript">Complete original transcript.</param>
        private static List<AlignedParagraph> GenerateParagraphs(List<TimedText> words, string transcript)
        {
            var paragraphs = new List<AlignedParagraph>();

            string[] doubleNewline = { "\n\n" };
            var list = transcript.Split(doubleNewline, StringSplitOptions.None);

            int endOffset;
            int startOffset = 0;

            // Create an AlignedParagraph for each text paragraph...
            foreach (var item in list)
            {
                endOffset = startOffset + item.Length;

                paragraphs.Add(new AlignedParagraph(startOffset, endOffset, item));

                startOffset = endOffset + 2; // Account for double newline at end of each paragraph
            }

            // Find words belonging to each AlignedParagraph
            int priorIndex = 0;
            foreach (var paragraph in paragraphs)
            {
                // Find words between start and end offsets inclusive
                for (var i = priorIndex; i < words.Count; i++)
                {
                    // Is the word past the given start?
                    if (words[i].OffsetStart >= paragraph.OriginalStartOffset)
                    {
                        // Is the word prior to the given end?
                        if (words[i].OffsetEnd <= paragraph.OriginalEndOffset)
                        {
                            paragraph.Words.Add(words[i]);
                        }
                        else
                        {
                            priorIndex = i;
                            break;
                        }
                    }
                }
            }

            // Perform clean-up pass
            foreach (var paragraph in paragraphs)
            {
                paragraph.Text = CleanText(paragraph.Text);
                RepairWordOffsets(paragraph);
                ExpandWordBoundaries(paragraph);
            }

            return paragraphs;
        }

        /// <summary>
        /// Fixes the start and end offset of each word to re-align with the
        /// text which may have been altered via the clean-up process.
        /// </summary>
        /// <param name="paragraph"></param>
        private static void RepairWordOffsets(AlignedParagraph paragraph)
        {
            int priorWordEnd = 0;

            foreach (var word in paragraph.Words)
            {
                var len = word.Text.Length;
                var max = paragraph.Text.Length - len;
                for (var j = priorWordEnd; j <= max; j++)
                {
                    var sub = paragraph.Text.Substring(j, len);
                    if (word.Text == sub)
                    {
                        word.OffsetStart = j;
                        word.OffsetEnd = j + len;
                        priorWordEnd = word.OffsetEnd;
                        break;
                    }
                    if (j == max)
                    {
                        throw new Exception($"RepairWordOffsets: No match found for '{word.Text}' in paragraph text: '{paragraph.Text}'");
                    }
                }
            }
        }

        /// <summary>
        /// Expand the boundaries of each word to include the surrounding whitespace and punctuation.
        /// </summary>
        /// <param name="paragraph">The paragraph to be processed.</param>
        private static void ExpandWordBoundaries(AlignedParagraph paragraph)
        {
            var words = paragraph.Words;

            // Pass 1 - Incorporate leading quotes into word (expand left)
            foreach (var w in words)
            {
                var pos = w.OffsetStart - 1;
                if (pos >= 0 && paragraph.Text[pos] == '"')
                {
                    w.OffsetStart = pos;
                }
            }

            // Pass 2 - Combine hyphenated words            
            for (int i = 0; i < words.Count - 1; i++)
            {
                var next = i + 1;   // Look ahead

                // If words are 1 char apart...
                if (words[next].OffsetStart - words[i].OffsetEnd == 1)
                {
                    // ...and that char is a hyphen...
                    if (paragraph.Text[words[i].OffsetEnd] == '-')
                    {
                        // Comine words
                        words[i].OffsetEnd = words[next].OffsetEnd;
                        var pos = words[i].OffsetStart;
                        var len = words[i].OffsetEnd - pos;
                        words[i].Text = paragraph.Text.Substring(pos, len);

                        words.RemoveAt(next);
                    }
                }
            }

            // Pass 3 - Incorporate all chars up to the next word
            for (int i = 0; i < words.Count; i++)
            {
                var next = i + 1;   // Look ahead

                if (next < words.Count)
                {
                    words[i].OffsetEnd = words[next].OffsetStart;
                }
                else
                {
                    // Last word, expand until end of transcript.
                    words[i].OffsetEnd = paragraph.Text.Length;
                }

                var pos = words[i].OffsetStart;
                var len = words[i].OffsetEnd - pos;
                words[i].Text = paragraph.Text.Substring(pos, len);
            }
        }
        #endregion PARAGRAPH PROCESSING

        #region == TEXT PROCESSING
        /// <summary>
        /// Remove bracketed passages and tighten up whitespace.
        /// </summary>
        /// <param name="text">The text to be processed.</param>
        /// <returns>The resulting clean text.</returns>
        private static string CleanText(string text)
        {
            // Remove bracketed text...
            var cleanText = RemoveBrackets(text);

            Regex regex;

            // Condense multiple spaces into one
            regex = new Regex(@"\s+");
            cleanText = regex.Replace(cleanText, " ");

            // Remove spaces prior to terminal punction
            regex = new Regex(@"\s(?=[\.\?,;!])");
            cleanText = regex.Replace(cleanText, "");

            // Remove any leading or trailing whitespace
            cleanText = cleanText.Trim();

            return cleanText;
        }

        /// <summary>
        /// Removes bracketed passages from the given text.
        /// </summary>
        /// <param name="text">The text to be processed.</param>
        /// <returns>The text remaining unbracketed text.</returns>
        private static string RemoveBrackets(string text)
        {
            var chars = new List<char>();
            bool bracketsOpen = false;
            char closingBracket = '\0';

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (bracketsOpen)
                {
                    // Ignore everything - including nested brackets - until closing bracket encountered
                    if (c == closingBracket)
                    {
                        closingBracket = '\0';
                        bracketsOpen = false;
                    }
                }
                else
                {
                    switch (c)
                    {
                        case '[':
                            // Beginning of square bracket passage
                            closingBracket = ']';
                            bracketsOpen = true;
                            break;
                        case '(':
                            // Beginning of round bracket passage
                            closingBracket = ')';
                            bracketsOpen = true;
                            break;
                        default:
                            // Normal text passage, keep it.
                            chars.Add(c);
                            break;
                    }
                }
            }

            return new string(chars.ToArray());
        }
        #endregion TEXT PROCESSING
    }
}
