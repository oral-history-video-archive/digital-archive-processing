using InformediaCORE.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/**
 * NOTES: (CHRISTEL)
 * The program NER-Polisher extracts people names, locations, and organizations in a "Pass 2" across 
 * the output of named entity identification from Stanford NER and spaCy. This program works only with 
 * spaCy output to find dates. It limits its check to 3 spaCy types: DATE, CARDINAL, and EVENT. 
 * Furthermore, the program is also searching the transcripts directly to extract well-qualified year 
 * mentions of the form 'xx, yyyy, or variants 'xxs or yyyys with yyyy and xx having to be numbers and 
 * years in range [1500, 2199] treated as ok confidence and range [1900, today] high confidence. 
 *
 * NOTES: (BM3N)
 * Unlike Organizations and Locations which work on combined spaCy and Stanford results, dates only 
 * care about spaCy. Because of this, date processing occurs earlier in the pipeline and maintains it's 
 * own spaCy parsing logic instead of sharing the "pass 3" combined results used by the remainder of 
 * the resolvers. 
 * 
 * UPDATES:
 * CHRISTEL, 2021/09/23: Via IsNumberOrAnyQuoteAtOffset, do not consider 'xx" with trailing double quote 
 * as a year (e.g., "I am 5'11" tall..." no longer becomes a possible date '11 interpreted as 1911).
 */

namespace InformediaCORE.Processing.NamedEntities
{
    /// <summary>
    /// A spaCy date entity
    /// </summary>
    class DateEntity : NamedEntity
    {
        public DateEntity(string dateReference, string contextualizedText,
            int startOffset, int length, string spacyType, int confidence)
        {
            this.Text = dateReference.Replace('\t', ' ').Trim(); // it is IMPERATIVE that string has no tabs as \t is field separator in data files
            this.ContextualizedText = contextualizedText.Replace('\t', ' ').Trim();
            this.StartOffset = startOffset;
            this.Length = length;
            this.SpacyType = spacyType;
            this.Confidence = confidence;
        }
    }

    /// <summary>
    /// A confirmed date reference
    /// </summary>
    public class DateReference
    {
        public string Value { get; set; }
        public bool FromTranscriptDirectProcessingOnly { get; set; }
        public int Count { get; set; } // number of times this reference made
        public int Confidence { get; set; }

        public DateReference(string dateValue, int givenConfidence, bool givenFromTranscriptDirectProcessing)
        {
            Value = dateValue;
            Confidence = givenConfidence;
            Count = 1;
            FromTranscriptDirectProcessingOnly = givenFromTranscriptDirectProcessing;
        }
    }

    /// <summary>
    /// Extract a "dates" subset of information from the Named Entity Recognition results of spaCy (https://spacy.io/).
    /// Always take advantage of square bracket markup within the original text to qualify results (e.g., "'29 [1929]").
    /// Always also take advantage of any sort of returns '\n' in original text to indicate a separation of any
    /// sequence of words collected for an entity (so 1929\n1930 is two entities 1929 and 1930).
    /// </summary>
    /// <remarks>
    /// NOTE ON GENERAL HEURISTICS:  When looking to conclude xxs or yyyys as in 1990s, restrict the last numeral to be
    /// a 0 preceding a "s": x0s and yyy0s only, not [1,9] when "s" ends it.
    /// 
    /// NOTE ON STREET ADDRESS HEURISTICS: There are street addresses like "1900 South Michigan" that would be tagged as a year of 1900 if not for
    /// the street heuristic.  If the transcript yyyy has in the following +2 word to +3 word (i.e., not immediately following word) one of 
    /// Street, Avenue, Boulevard, Lane, Road, St., Ave., Blvd., or immediately following word is one of South, North, East, or West, then do not consider
    /// this as a year but instead as a number in an address.  See ParsesToAddress() for more details.
    /// 
    /// Future processing could be more clever against the transcript by looking for time-marker words such as "in yyyy" or "from yyyy" but that 
    /// starts to remake a named entity parser such as spaCy and this program is using spaCy plus some low-hanging fruit "easy" heuristics ('xx, yyyy) 
    /// that break when yyyy is a common address part unless a bit of work is done to protect against addresses.
    /// 
    /// Confidences have also been normalized a bit to land in integer range [1,6].
    /// </remarks>
    public static class DateResolver
    {
        private static readonly int CONFIDENCE_CEILING = 6; // keep confidences at or under 6
        private static readonly string TRANSCRIPT_DIRECT_PROCESSING_FLAG = "transcript"; // NOTE: must be different from kept spaCy types of DATE, etc.

        /// <summary>
        /// Generate a list of named entity candidates from the given spaCy input and transcript.
        /// 
        /// Brings in additional context from the transcript like square-bracketed text via heuristics
        /// spaCyType is one of DATE, CARDINAL, EVENT
        /// </summary>
        /// <param name="spacyFile">Fully qualified path to spaCy output to be parsed.</param>
        /// <param name="transcript">Full transcript text as originally analyzed by spaCy.</param>
        /// <returns>A list of date references upon success; an empty list upon failure.</returns>
        public static List<DateReference> Resolve(string spacyFile, string transcript)
        {
            List<DateEntity> dateEntities = new List<DateEntity>();

            int foundOffset;
            string oneSpaCyLine = null;
            string[] linePieces;
            char[] spaCyLineSeparatorChars = { ',' };

            string targetString;

            int givenOffset;
            int workOffset;

            string currentType;
            int currentConfidence;
            int FORGIVENESS_EPSILON = 2;        // allow spaCy offsets to slide a bit, but report when they do in status output
            int MORE_CONTEXT_EXTENSION = 40;    // look forward another 420 characters for possible [1920] or similar helping context or a 1900 ... Avenue address.
            string contextString;
            string valueString;
            Dictionary<string, DateReference> cachedStoryEntries = new Dictionary<string, DateReference>();

            DateEntity currentEntry;
            using (var spacy_sr = new StreamReader(spacyFile))
            {
                // Discard header row
                _ = spacy_sr.ReadLine();

                while ((oneSpaCyLine = spacy_sr.ReadLine()) != null)
                {
                    linePieces = oneSpaCyLine.Split(spaCyLineSeparatorChars);
                    targetString = "";  // in case we have no data, blank this out...

                    // Validate input                       
                    if (!(linePieces.Count() == 4 && linePieces[0].Length > 0))
                    {
                        Logger.Warning("Skipping malformed input line: {0}", targetString.Replace('\t', '*'));
                        continue; // Skip current line, proceed to next
                    }

                    targetString = linePieces[0];

                    if (targetString.Contains('"'))
                    {
                        // Use of punctuation such as " throws off spaCy, e.g., 'minister--"I ' gets seen as this entity:
                        Logger.Warning("Skipping entry with double quote issue: {0}", targetString.Replace('\t', '*'));
                        continue; // Skip current line, proceed to next
                    }

                    if (targetString.Contains("\t"))
                    {
                        Logger.Warning("Skipping entry with tab (changed to *): {0}", targetString.Replace('\t', '*'));
                        continue;   // Skip current line, proceed to next
                    }

                    currentType = TypeForSpaCyType(linePieces[3]);

                    if (currentType == "")
                    {
                        continue;   // Skip non-date entity, proceed to next line
                    }

                    if (!(int.TryParse(linePieces[1], out givenOffset) && int.TryParse(linePieces[2], out int givenEndOffset) && givenEndOffset > givenOffset))
                    {
                        Logger.Warning("Skipping malformed input line: {0}", targetString.Replace('\t', '*'));
                        continue; // Skip current line, proceed to next
                    }

                    // Roll the transcriptOffset forward because we may do something with this entry
                    foundOffset = transcript.IndexOf(targetString, givenOffset);
                    if (foundOffset != givenOffset)
                    {
                        if (givenOffset > FORGIVENESS_EPSILON)
                            foundOffset = transcript.IndexOf(targetString, givenOffset - FORGIVENESS_EPSILON);
                        else
                            foundOffset = transcript.IndexOf(targetString);
                        if ((foundOffset < givenOffset && foundOffset + FORGIVENESS_EPSILON >= givenOffset) ||
                            (foundOffset > givenOffset && foundOffset - FORGIVENESS_EPSILON <= givenOffset))
                        {
                            Logger.Warning($"Resetting entry of '{0}' to offset {1} rather than {2}.", targetString, foundOffset, givenOffset);
                            // Adjust to the actual transcript location.
                            givenEndOffset += (foundOffset - givenOffset);
                            givenOffset = foundOffset;
                        }
                        else
                        {
                            Logger.Warning($"Transcript did not have this text matching SpaCy NER at given SpaCy offset {0}: {1}", givenOffset, targetString);
                            continue; // Give up processing current line and proceed to next.
                        }
                    }

                    if (foundOffset > 0)
                        foundOffset--; // back up one character to check for a preceding ' as in '89
                    workOffset = givenEndOffset + MORE_CONTEXT_EXTENSION; // look forward for [context] 
                    if (workOffset >= transcript.Length)
                        workOffset = transcript.Length - 1;
                    contextString = transcript.Substring(foundOffset, workOffset - foundOffset + 1);
                    // Stop the context at a \n though, if there is one.
                    workOffset = contextString.IndexOf("\n");
                    if (workOffset == 0)
                    {
                        contextString = contextString.Substring(1); // peel off opening \n that we backed into
                        workOffset = contextString.IndexOf("\n");
                    }
                    if (workOffset == 0)
                        contextString = ""; // give up when we are given a target string starting with \n
                    else if (workOffset > 0)
                        contextString = contextString.Substring(0, workOffset); // stop at the \n
                    if (!FoundYearEvidence(contextString, out valueString, out currentConfidence, out bool isAddress))
                    {
                        // More context didn't help.  Back away to just the target string unless this is seen as an address,
                        // as in the 1900 for "1900 South Michigan Ave."
                        if (!isAddress)
                        {
                            valueString = targetString;
                            contextString = targetString;
                        }
                    }
                    if (!isAddress)
                    {
                        // Give spaCy extra confidence credit for tagging this as a date reference:
                        if (currentConfidence < CONFIDENCE_CEILING)
                            currentConfidence++;
                        currentEntry = new DateEntity(valueString, contextString, givenOffset,
                                givenEndOffset - givenOffset, currentType, currentConfidence);

                        dateEntities.Add(currentEntry);
                        if (cachedStoryEntries.ContainsKey(currentEntry.Text))
                        {
                            cachedStoryEntries[currentEntry.Text].Count++;
                            if (cachedStoryEntries[currentEntry.Text].Confidence < currentEntry.Confidence)
                                cachedStoryEntries[currentEntry.Text].Confidence = currentEntry.Confidence;
                        }
                        else
                            cachedStoryEntries.Add(currentEntry.Text, new DateReference(currentEntry.Text, currentEntry.Confidence, false));
                    }
                } // end of processing input file
            }

            // NOTE: Also walk the transcript looking for specific date patterns of 'xx, yyyy, or variants
            // 'xxs or yyyys to pull out more dates (perhaps already given by spaCy, perhaps not).
            // This logic is similar to what is found within FoundYearEvidence.
            givenOffset = 0;
            while (givenOffset < transcript.Length && givenOffset >= 0 &&
                FoundYearEvidenceInTranscript(transcript, givenOffset, out valueString, out currentConfidence, out workOffset))
            {
                givenOffset = workOffset; // update where in the transcript we are next to process (-1 to signal "all done")
                currentEntry = new DateEntity(valueString, valueString, workOffset,
                            valueString.Length, TRANSCRIPT_DIRECT_PROCESSING_FLAG, currentConfidence);
                dateEntities.Add(currentEntry);
                if (cachedStoryEntries.ContainsKey(currentEntry.Text))
                {
                    cachedStoryEntries[currentEntry.Text].Count++;
                    if (cachedStoryEntries[currentEntry.Text].Confidence < currentEntry.Confidence)
                        cachedStoryEntries[currentEntry.Text].Confidence = currentEntry.Confidence;
                }
                else
                    cachedStoryEntries.Add(currentEntry.Text, new DateReference(currentEntry.Text, currentEntry.Confidence, true));
            }
            // End of transcript processing to push more entries perhaps to dateEntities and into cachedStoryEntries.

            foreach (string uniqueYearRef in cachedStoryEntries.Keys)
            {
                if (cachedStoryEntries[uniqueYearRef].Count > 1)
                {
                    if (cachedStoryEntries[uniqueYearRef].Confidence < CONFIDENCE_CEILING)
                        cachedStoryEntries[uniqueYearRef].Confidence++; // up confidence for repeat entries
                }
                if (cachedStoryEntries[uniqueYearRef].FromTranscriptDirectProcessingOnly)
                {
                    // Mark the entries coming just from transcript processing (not spaCy)
                    valueString = "0";
                }
                else
                {
                    // outputting 1 if entry originates with spaCy, 0 if it is just from transcript mining
                    valueString = "1"; 
                }
            }

            return cachedStoryEntries.Values.ToList();
        }

        /// <summary> 
        /// Return true iff given string contains a sequence as one of 15xx, 16xx, 17xx, 18xx, 19xx, 20xx, 21xx with xx a 
        /// meaningful numeral in range 00-99, or in 'xx form, or also decades as 1990s etc., with "s" at end.
        /// </summary>
        /// <param name="givenString">given string</param>
        /// <param name="confirmedDateString">typically a year yyyy but sometimes a decade like 1990s or century 1900s</param>
        /// <param name="confidenceValue">output confidence value of 1 by default, higher up to 3 for discovered year in believable range</param>
        /// <param name="isAddressPiece">output boolean of false if this is not an address piece, true if it is (like "1900 South Street")</param>
        /// <returns>true if a year value found, false otherwise</returns>
        /// <remarks>Check given string for 'xx format with xx in [00, 99] range.  Check also for xx [yyxx] extra context in 
        /// square brackets at the end of the given string as in "57 [1957]". Also check for decades reference as in 1990s.
        /// Lastly, check for sequence of one of 15xx, ..., 21xx. 
        /// Make sure each of the x and y are numeral digits, not space or punctuation.</remarks>
        static bool FoundYearEvidence(string givenString, out string confirmedDateString, out int confidenceValue, out bool isAddressPiece)
        {
            int BELIEVABLE_LOWER_BOUND_ON_YEAR = 1500; // allow "believable" years down to 1500
            int BELIEVABLE_UPPER_BOUND_ON_YEAR = 2199; // allow "believable" years up to 2199
            int STRONGLY_BELIEVABLE_LOWER_BOUND_ON_YEAR = 1900;
            int STRONGLY_BELIEVABLE_UPPER_BOUND_ON_YEAR = DateTime.Now.Year;

            char[] leadingYearNumber = { '1', '2' };
            int startIndex = 0;
            int foundIndex, workingStringLength;
            string candidateYearPiece = "";
            bool foundYear = false;
            confirmedDateString = "0";
            confidenceValue = 1;

            string workString = givenString.Trim();
            workingStringLength = workString.Length;

            // First, decide if this is an address.
            isAddressPiece = ParsesToAddress(givenString);
            if (!isAddressPiece)
            {
                // Next pattern to check: yyyy with year in believable range 
                if (workingStringLength >= 4 && int.TryParse(workString.Substring(0, 4), out int yearValue) &&
                    yearValue >= BELIEVABLE_LOWER_BOUND_ON_YEAR && yearValue <= BELIEVABLE_UPPER_BOUND_ON_YEAR &&
                    IsNumberAtOffset(workString, 0) && IsNumberAtOffset(workString, 1) &&
                    IsNumberAtOffset(workString, 2) && IsNumberAtOffset(workString, 3) &&
                    !IsNumberAtOffset(workString, 4))
                {
                    foundYear = true;
                    if (yearValue >= STRONGLY_BELIEVABLE_LOWER_BOUND_ON_YEAR && yearValue <= STRONGLY_BELIEVABLE_UPPER_BOUND_ON_YEAR)
                        confidenceValue = 4;
                    else
                        confidenceValue = 3;

                    if (PassesDecadeSpec(workString, 3))
                        confirmedDateString = workString.Substring(0, 5); // string is of form yyyys as in 1990s, 1950s, etc.
                    else
                        confirmedDateString = workString.Substring(0, 4); // date is just the yyyy
                }

                if (!foundYear)
                {
                    // Second pattern to check: 'xx or xx followed by a qualifying yyxx with xx in range [00, 99].
                    // NOTE: do not consider 'xx' with trailing single quote as a year (e.g., "I was told '73' ..." no longer refers to 1973).
                    // NOTE: do not consider 'xx" with trailing double quote as a year (e.g., "I am 5'11" tall..." no longer refers to 1911).
                    if (workString.Substring(0, 1) == "'" && int.TryParse(workString.Substring(1, 2), out yearValue) &&
                        yearValue >= 0 && yearValue <= 99 &&
                        IsNumberAtOffset(workString, 1) && IsNumberAtOffset(workString, 2) && !IsNumberOrAnyQuoteAtOffset(workString, 3))
                    {
                        candidateYearPiece = workString.Substring(1, 2);
                        startIndex = 3;
                    }
                    else if (int.TryParse(workString.Substring(0, 2), out yearValue) &&
                        yearValue >= 0 && yearValue <= 99 &&
                        IsNumberAtOffset(workString, 0) && IsNumberAtOffset(workString, 1) && !IsNumberAtOffset(workString, 2))
                    {
                        candidateYearPiece = workString.Substring(0, 2);
                        startIndex = 2;
                    }

                    if (startIndex > 0)
                    {   // Found the pattern of xx or 'xx.  Now see if it gets qualified later in the same string by having it 
                        // repeated with a given century, as in '57 [1957] or '57, you know, 1957.
                        foundIndex = workString.IndexOf(candidateYearPiece, startIndex);
                        if (foundIndex >= startIndex + 2 && !IsNumberAtOffset(workString, foundIndex + 2))
                        {
                            // Take two characters immediately before the found xx, see if they are a century.
                            if (int.TryParse(workString.Substring(foundIndex - 2, 2), out int centuryValue) &&
                                IsNumberAtOffset(workString, foundIndex - 2) && IsNumberAtOffset(workString, foundIndex - 1) &&
                                !IsNumberAtOffset(workString, foundIndex - 3))
                            {
                                yearValue = (centuryValue * 100) + yearValue;
                                foundYear = true;
                                if (yearValue >= BELIEVABLE_LOWER_BOUND_ON_YEAR && yearValue <= BELIEVABLE_UPPER_BOUND_ON_YEAR)
                                {
                                    confidenceValue = 5; // with double qualification of xx ..yyxx.. give high confidence
                                }
                                else
                                {
                                    // Still use, but at lower confidence.
                                    confidenceValue = 3;
                                }
                                // If confirming year(s) mention ends in 0 and immediate s, keep it, as in '50s [1950s]
                                if (PassesDecadeSpec(workString, foundIndex + 1))
                                    confirmedDateString = workString.Substring(foundIndex - 2, 5); // string is of form yyyys as in 1990s, 1950s, etc.
                                else
                                    confirmedDateString = workString.Substring(foundIndex - 2, 4); // date is just the yyyy
                            }
                        }
                        else if (startIndex == 3) // qualifier for this being the 'xx pattern
                        {
                            // Due to the nature of the corpus being mainly 1900s content, default 'xx to 19xx but with lower confidence of "2".
                            yearValue = 1900 + yearValue; // map unqualified 'xx year to 19xx
                            confidenceValue = 2;
                            foundYear = true;
                            // If confirming year(s) mention ends in immediate s, keep it, as in '50s -- make this 1950s, etc.
                            if (startIndex < workingStringLength && workString.Substring(startIndex, 1) == "s")
                                confirmedDateString = yearValue.ToString() + "s"; // string is of form 19xxs as in 1990s, 1950s, etc.
                            else
                                confirmedDateString = yearValue.ToString(); // date is just the 19xx
                        }
                    }

                    if (!foundYear)
                    { // Last pattern to check: yyyy (or yyyys) in the given String starting from the front with leading y a 1 or 2 and
                        // yyyy in believable range....
                        while (startIndex < workString.Length - 4)
                        { 
                            if ((foundIndex = workString.IndexOfAny(leadingYearNumber, startIndex)) >= 0 && foundIndex <= workString.Length - 4)
                            {
                                if (int.TryParse(workString.Substring(foundIndex, 4), out yearValue) &&
                                    yearValue >= BELIEVABLE_LOWER_BOUND_ON_YEAR && yearValue <= BELIEVABLE_UPPER_BOUND_ON_YEAR &&
                                    IsNumberAtOffset(workString, foundIndex) && IsNumberAtOffset(workString, foundIndex + 1) &&
                                    IsNumberAtOffset(workString, foundIndex + 2) && IsNumberAtOffset(workString, foundIndex + 3) &&
                                    !IsNumberAtOffset(workString, foundIndex + 4))
                                {
                                    isAddressPiece = ParsesToAddress(workString);
                                    if (isAddressPiece)
                                        break; // exit loop with foundYear remaining as false since this is an address
                                    else
                                    {
                                        foundYear = true;
                                        if (yearValue >= STRONGLY_BELIEVABLE_LOWER_BOUND_ON_YEAR && yearValue <= STRONGLY_BELIEVABLE_UPPER_BOUND_ON_YEAR)
                                            confidenceValue = 4;
                                        else
                                            confidenceValue = 3;
                                        if (foundIndex + 4 < workingStringLength && workString.Substring(foundIndex + 4, 1) == "s")
                                            confirmedDateString = workString.Substring(foundIndex, 5); // string is of form yyyys as in 1990s, 1950s, etc.
                                        else
                                            confirmedDateString = workString.Substring(foundIndex, 4); // date is just the yyyy
                                        break;
                                    }
                                }
                                startIndex = foundIndex + 1;
                            }
                            else
                                break;
                        }
                    }
                }
            }

            return foundYear;
        }

        /// <summary>
        /// Returns true iff given string is considered an address with a number lead as in "1900 South Michigan" or "1500 Pennsylvania Ave."
        /// </summary>
        /// <param name="givenString">string to check</param>
        /// <returns>true iff string should be considered as a street address (number followed by street)</returns>
        static bool ParsesToAddress(string givenString)
        {
            // First, a check: if ] (to close off # from following text) or , or . or ) or ? or : or ; (same, a divider) or " in " or " at " (preposition divider)
            // ...so that "1959], East St. Louis" and "1899 in St. Augustine" parse to years and not streets
            bool isAddress = false;
            bool giveUpOnAddress = false;
            string workStr = givenString;
            int maxOffsetToCheck = workStr.Length - 1;
            int firstNonDateSlot;
            char valToCheck;
            int workVal = 0;
            if (maxOffsetToCheck <= 0)
                giveUpOnAddress = true;
            else
            {
                while (workVal <= maxOffsetToCheck && !IsNumberOrQuoteAtOffset(workStr, workVal))
                    workVal++;                  // move forward to first number or quote ' for '60s or 1957 for example
                firstNonDateSlot = workVal;
                while (firstNonDateSlot <= maxOffsetToCheck && IsNumberOrQuoteAtOffset(workStr, firstNonDateSlot))
                    firstNonDateSlot++;         // get to end of number

                if (firstNonDateSlot > maxOffsetToCheck)
                    giveUpOnAddress = true;     // just have #, not # with following text that could be a street address
                else
                {
                    // NOTE: logic above gives us firstNonDateSlot as always being 1 or greater
                    if (workStr[firstNonDateSlot - 1] == '0' && (firstNonDateSlot > maxOffsetToCheck || workStr[firstNonDateSlot] == 's'))
                        giveUpOnAddress = true; // found #0s -- not an address but could be a decade reference like '60s or 1960s
                }
                if (!giveUpOnAddress)
                {
                    workVal = workStr.IndexOf(' ', firstNonDateSlot);
                    if (workVal == -1)
                        giveUpOnAddress = true; // no whitespace break after the number --> no address by this simple parsing for "# street-address"
                    else if (firstNonDateSlot < workVal)
                    {
                        /// Check for intervening punctuation that breaks apart the words (potential address) from number;
                        /// if found, immediately return false as this then is not an address; note that "[" will not break an 
                        /// address but "]" will as [] used to fill in context like "1900 [West Michigan St.]"
                        while (!giveUpOnAddress && firstNonDateSlot < workVal)
                        {
                            valToCheck = workStr[firstNonDateSlot];
                            if (valToCheck == ',' || valToCheck == ']' || valToCheck == '.' || valToCheck == ';' || valToCheck == ':' ||
                                valToCheck == ')' || valToCheck == '?')
                                giveUpOnAddress = true;
                            else
                                firstNonDateSlot++;
                        }
                    }
                    else
                    {
                        workStr = workStr.Substring(workVal).Trim();
                        if (workStr.StartsWith("in ") || workStr.StartsWith("at ") || workStr.StartsWith("from "))
                            giveUpOnAddress = true;
                        else if (workStr.StartsWith("[") &&
                            ((workStr.Length >= 6 && IsNumberAtOffset(workStr, 1) && IsNumberAtOffset(workStr, 2) &&
                            IsNumberAtOffset(workStr, 3) && IsNumberAtOffset(workStr, 4) && workStr[5] == ']') ||
                            (workStr.Length >= 7 && IsNumberAtOffset(workStr, 1) && IsNumberAtOffset(workStr, 2) &&
                            IsNumberAtOffset(workStr, 3) && workStr[4] == '0' && workStr[5] == 's' && workStr[6] == ']')))
                            giveUpOnAddress = true; // if we have # [dddd] or # [ddd0s] with d a numerical digit, then give up on parsing this as address,
                                                    // e.g., "'87 [1987] well Lane Tech" should parse as a 1987 date reference
                    }
                }
            }

            if (!giveUpOnAddress)
            { // Keep checking the ***rest*** part of "# ***rest***"
                string textToCheck = givenString.Replace('[', ' ').Replace(']', ' ').Replace("  ", " ");
                workVal = textToCheck.IndexOf(' ');
                maxOffsetToCheck = textToCheck.Length - 1;

                if (workVal > 0 && workVal < maxOffsetToCheck)
                {
                    textToCheck = textToCheck.Substring(workVal + 1).Trim(); // "Next" words after possible number, e.g., the wwww for "1900 wwww"
                    if (textToCheck.StartsWith("South ") || textToCheck.StartsWith("North ") || textToCheck.StartsWith("East ") || textToCheck.StartsWith("West "))
                        isAddress = true;
                    else
                    { // test second heuristic: look at next two words and if either are street-related, return this as an address
                        workVal = textToCheck.IndexOf(' ');
                        if (workVal > 0 && workVal < textToCheck.Length - 1)
                        {
                            textToCheck = textToCheck.Substring(workVal + 1).Trim();
                            if (textToCheck.StartsWith("St.") || textToCheck.StartsWith("Street") || textToCheck.StartsWith("Ave.") || textToCheck.StartsWith("Avenue") ||
                                textToCheck.StartsWith("Boulevard") || textToCheck.StartsWith("Blvd") || textToCheck.StartsWith("Road") || textToCheck.StartsWith("Lane"))
                                isAddress = true;
                            else
                            {
                                workVal = textToCheck.IndexOf(' ');
                                if (workVal > 0 && workVal < textToCheck.Length - 1)
                                {
                                    textToCheck = textToCheck.Substring(workVal + 1).Trim();
                                    if (textToCheck.StartsWith("St.") || textToCheck.StartsWith("Street") || textToCheck.StartsWith("Ave.") || textToCheck.StartsWith("Avenue") ||
                                        textToCheck.StartsWith("Boulevard") || textToCheck.StartsWith("Blvd") || textToCheck.StartsWith("Road") || textToCheck.StartsWith("Lane"))
                                        isAddress = true;
                                }
                            }
                        }
                    }
                }
            }

            return isAddress;
        }

        /// <summary>
        /// Returns true iff there is a "0" at the given offset and then an immediately following "s".
        /// </summary>
        /// <param name="givenString">given string</param>
        /// <param name="givenOffsetToExpectedEndZero">offset to expected "0"</param>
        /// <returns>true iff "0s" is found at given offset</returns>
        /// <remarks>Additional numeric processing assumed to be done by the caller to place this as a #0s or ###0s pattern.
        /// Proper American English formatting is assumed: no 's and no upper case S, just immediately trailing s.</remarks>
        static bool PassesDecadeSpec(string givenString, int givenOffsetToExpectedEndZero)
        {
            return (givenString.IndexOf("0s", givenOffsetToExpectedEndZero) == givenOffsetToExpectedEndZero);
        }

        /// <summary>
        /// Very similar to FoundYearEvidence except that an offset into the string is manipulated as well (moved forward).
        /// </summary>
        /// <param name="givenTranscript">transcript text</param>
        /// <param name="givenStartOffset">offset into transcript text from which to start consideration</param>
        /// <param name="confirmedDateString">typically a year yyyy but sometimes a decade like 1990s or century 1900s</param>
        /// <param name="confidenceValue">output confidence value of 1 by default, higher up for discovered year in believable range</param>
        /// <param name="updatedWorkOffset">updated offset into transcript which has not yet been considered (-1 if all considered)</param>
        /// <returns>true if a year value found, false otherwise</returns>
        /// <remarks>Check given transcript from the given offset forward for 'xx format with xx in [00, 99] range.  
        /// Check also for yyyy.  Also check for decades references like 'xxs ('50s) and yyyys (1950s).
        /// Make sure each of the x and y digits are numerals as well, not spaces or punctuation.</remarks>
        static bool FoundYearEvidenceInTranscript(string givenTranscript, int givenStartOffset, out string confirmedDateString,
            out int confidenceValue, out int updatedWorkOffset)
        {
            int BELIEVABLE_LOWER_BOUND_ON_YEAR = 1500; // allow "believable" years down to 1500
            int BELIEVABLE_UPPER_BOUND_ON_YEAR = 2199; // allow "believable" years up to 2199
            int STRONGLY_BELIEVABLE_LOWER_BOUND_ON_YEAR = 1900;
            int STRONGLY_BELIEVABLE_UPPER_BOUND_ON_YEAR = DateTime.Now.Year;

            updatedWorkOffset = givenStartOffset + 1;

            int yearValue;
            bool foundYear = false;
            bool isAddress;
            confirmedDateString = "0";
            confidenceValue = 1;

            string workString = givenTranscript.Trim();
            int workingStringLength = workString.Length;
            char[] leadingYearIndicator = { '\'', '1', '2' };

            int startIndex = givenStartOffset;
            int foundIndex, secondaryIndex;
            while (!foundYear && startIndex <= workingStringLength - 3) // need at least 2 characters after ' for 'xx (3 for yyy after 1yyy or 2yyy)
            {
                if ((foundIndex = workString.IndexOfAny(leadingYearIndicator, startIndex)) >= startIndex &&
                    foundIndex <= workingStringLength - 3)
                {
                    if (workString[foundIndex] == '\'')
                    { // 'xx and 'xxs processing with dismissal of 'xx' as well as 'xx" (which occurs in dimensions like height of 5'10" in transcripts) if found
                        if (int.TryParse(workString.Substring(foundIndex + 1, 2), out yearValue) &&
                            yearValue >= 0 && yearValue <= 99 &&
                            IsNumberAtOffset(workString, foundIndex + 1) && IsNumberAtOffset(workString, foundIndex + 2) &&
                            !IsNumberOrAnyQuoteAtOffset(workString, foundIndex + 3))
                        {
                            string candidateYearPiece = workString.Substring(foundIndex + 1, 2);
                            // Found the pattern of 'xx.  Now see if it gets qualified later in the same string by having it 
                            // repeated with a given century, as in "'57 [1957]" or "'57, you know, 1957."
                            secondaryIndex = workString.IndexOf(candidateYearPiece, foundIndex + 3);
                            if (secondaryIndex >= foundIndex + 3 && !IsNumberAtOffset(workString, secondaryIndex + 2))
                            {
                                // Take two characters immediately before the found xx, see if they are a century.
                                if (int.TryParse(workString.Substring(secondaryIndex - 2, 2), out int centuryValue) &&
                                    IsNumberAtOffset(workString, secondaryIndex - 2) && IsNumberAtOffset(workString, secondaryIndex - 1) &&
                                    !IsNumberAtOffset(workString, secondaryIndex - 3))
                                {
                                    yearValue = (centuryValue * 100) + yearValue;
                                    foundYear = true;
                                    updatedWorkOffset = foundIndex + 3; // move past 'xx but no further (in case of "'57 or '58 [1957 or 1958]" compounds)
                                    if (yearValue >= BELIEVABLE_LOWER_BOUND_ON_YEAR && yearValue <= BELIEVABLE_UPPER_BOUND_ON_YEAR)
                                    {
                                        confidenceValue = 4; // with double qualification of xx ..yyxx.. give high confidence
                                    }
                                    else
                                    {
                                        // Still use, but at lower confidence.
                                        confidenceValue = 3;
                                    }
                                    // If confirming year(s) mention ends in immediate s, keep it, as in '50s [1950s]
                                    if (workingStringLength - secondaryIndex >= 3 && workString.Substring(secondaryIndex + 2, 1) == "s")
                                        confirmedDateString = workString.Substring(secondaryIndex - 2, 5); // string is of form yyyys as in 1990s, 1950s, etc.
                                    else
                                        confirmedDateString = workString.Substring(secondaryIndex - 2, 4); // date is just the yyyy
                                }
                            }
                            if (!foundYear)
                            { // the 'xx pattern without century ever given
                                // Due to the nature of the corpus being mainly 1900s content, default 'xx to 19xx but with lower "special" confidence.
                                yearValue = 1900 + yearValue; // map unqualified 'xx year to 19xx
                                confidenceValue = 2;
                                foundYear = true;
                                updatedWorkOffset = foundIndex + 3; // move past 'xx but no further
                                // If confirming year(s) mention ends in immediate s, keep it, as in '50s -- make this 1950s, etc.
                                if (foundIndex + 3 < workingStringLength && workString.Substring(foundIndex + 3, 1) == "s")
                                    confirmedDateString = yearValue.ToString() + "s"; // string is of form 19xxs as in 1990s, 1950s, etc.
                                else
                                    confirmedDateString = yearValue.ToString(); // date is just the 19xx
                            }
                        }
                    }
                    else if (foundIndex <= workingStringLength - 4 && int.TryParse(workString.Substring(foundIndex, 4), out yearValue) &&
                        yearValue >= BELIEVABLE_LOWER_BOUND_ON_YEAR && yearValue <= BELIEVABLE_UPPER_BOUND_ON_YEAR &&
                        IsNumberAtOffset(workString, foundIndex) && IsNumberAtOffset(workString, foundIndex + 1) &&
                        IsNumberAtOffset(workString, foundIndex + 2) && IsNumberAtOffset(workString, foundIndex + 3) &&
                        !IsNumberAtOffset(workString, foundIndex + 4))
                    { // yyyy and yyyys processing
                        isAddress = ParsesToAddress(workString.Substring(foundIndex));
                        if (!isAddress)
                        {
                            foundYear = true;
                            updatedWorkOffset = foundIndex + 4; // move past yyyy
                            if (yearValue >= STRONGLY_BELIEVABLE_LOWER_BOUND_ON_YEAR && yearValue <= STRONGLY_BELIEVABLE_UPPER_BOUND_ON_YEAR)
                                confidenceValue = 4;
                            else
                                confidenceValue = 3;
                            if (foundIndex + 4 < workingStringLength && workString.Substring(foundIndex + 4, 1) == "s")
                            {
                                confirmedDateString = workString.Substring(foundIndex, 5); // string is of form yyyys as in 1990s, 1950s, etc.
                                updatedWorkOffset++; // move past the "s" too
                            }
                            else
                                confirmedDateString = workString.Substring(foundIndex, 4); // date is just the yyyy
                        }
                        else
                            foundIndex += 5; // move past these 4 digits plus at least one character in "address" part as this is taken as a street address with # to start
                    }
                    if (!foundYear)
                        startIndex = foundIndex + 1; // Move forward to process next potential candidate for a 'xx or yyyy.
                }
                else
                    break; // give up, nothing left to process
            }
            if (!foundYear)
                updatedWorkOffset = -1; // marker for "all done", i.e., no more years in transcript from point givenStartOffset forward

            return foundYear;
        }

        /// <summary>
        /// If string ends before given offset or the character there is not a number, return false, else return true
        /// </summary>
        /// <param name="givenString">given string</param>
        /// <param name="givenOffset">given offset</param>
        /// <returns>true if number at given offset, false otherwise</returns>
        static bool IsNumberAtOffset(string givenString, int givenOffset)
        {
            bool isNumber = false;

            if (givenString.Length > givenOffset && Char.IsNumber(givenString[givenOffset]))
                isNumber = true; // the given offset is a number

            return isNumber;
        }

        /// <summary>
        /// If string ends before given offset or the character there is not a number or ', return false, else return true
        /// </summary>
        /// <param name="givenString">given string</param>
        /// <param name="givenOffset">given offset</param>
        /// <returns>true if number or single quote at given offset, false otherwise</returns>
        static bool IsNumberOrQuoteAtOffset(string givenString, int givenOffset)
        {
            bool isNumberOrQuote = false;

            if (givenString.Length > givenOffset && (givenString[givenOffset] == '\'' || Char.IsNumber(givenString[givenOffset])))
                isNumberOrQuote = true; // the given offset is a number or single quote

            return isNumberOrQuote;
        }

        /// <summary>
        /// If string ends before given offset or the character there is not a number or ' or ", return false, else return true
        /// </summary>
        /// <param name="givenString">given string</param>
        /// <param name="givenOffset">given offset</param>
        /// <returns>true if number or single quote or double quote at given offset, false otherwise</returns>
        static bool IsNumberOrAnyQuoteAtOffset(string givenString, int givenOffset)
        {
            bool isNumberOrAnyQuote = false;

            if (givenString.Length > givenOffset && (givenString[givenOffset] == '\'' || givenString[givenOffset] == '"' || Char.IsNumber(givenString[givenOffset])))
                isNumberOrAnyQuote = true; // the given offset is a number or single quote or double quote

            return isNumberOrAnyQuote;
        }

        static string TypeForSpaCyType(string givenText)
        {
            // From https://spacy.io/api/annotation there are these types, with notation here on which we skip over overall,
            // but for this program we keep only: DATE, CARDINAL, EVENT
            // PERSON      As-is           People, including fictional.
            // NORP        treated as ORG  Nationalities or religious or political groups.
            // FAC         treated as LOC  Buildings, airports, highways, bridges, etc.
            // ORG         As-is           Companies, agencies, institutions, etc.
            // GPE         treated as LOC  Countries, cities, states.
            // LOC         As-is           Non-GPE locations, mountain ranges, bodies of water.
            // PRODUCT     ignored*        Objects, vehicles, foods, etc. (Not services.)
            // EVENT       year-check        Named hurricanes, battles, wars, sports events, etc.
            // WORK_OF_ART ignored*        Titles of books, songs, etc.
            // LAW         ignored*        Named documents made into laws.
            // LANGUAGE    ignored*        Any named language.
            // DATE        year-check      Absolute or relative dates or periods.
            // TIME        ignored*        Times smaller than a day.
            // PERCENT     ignored         Percentage, including ”%“.
            // MONEY       ignored         Monetary values, including unit.
            // QUANTITY    ignored         Measurements, as of weight or distance.
            // ORDINAL     ignored         “first”, “second”, etc.
            // CARDINAL    year-check      Numerals that do not fall under another type.
            // 
            // *Those marked "ignored*" will water down any Stanford NER entries because they will not be considered
            // as universally recognized people, places, or organizations.  In this program emphasizing year/date extraction,
            // only consider DATE, CARDINAL, or EVENT.
            string typeOfNamedEntity = "";
            if (givenText == "EVENT" || givenText == "DATE" || givenText == "CARDINAL")
                typeOfNamedEntity = givenText;
            return typeOfNamedEntity;
        }
    }
}
