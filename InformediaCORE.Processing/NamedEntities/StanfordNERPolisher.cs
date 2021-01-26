using System.IO;
using System.Collections.Generic;
using System.Linq;

using InformediaCORE.Common;

/**
 * NOTES: (CHRISTEL)
 * 
 * Some stories, like collection 1 story 8193, have no named entities resulting in a zero-length output file.
 * 
 * Output of Stanford NER does not lend itself to easy extraction of source text (transcript) offsets, because
 * a union of the rows of the Stanford NER output is not consistent with the source text.  Sometimes the 
 * Stanford rows repeat, as in collection 10, story 2867 with these rows:
 * 
 *     Eddie  PERSON
 *     ,      O
 *     Jr.    O
 *     .      O
 *     
 *     Eddie  PERSON
 * 
 * ...for this source text:
 * 
 *     Eddie, Jr. Eddie
 * 
 * The Stanford NER places 2 "." after "Jr" but the source file has only one.
 * 
 * Workaround: all sequences of non-alphanumeric characters in Stanford NER receiving an empty grade, aside from 
 *             the special [ and ], will be ignored.
 * 
 * New issue that such removal of punctuation brings up:  
 * locatons especially will get clumped together as some appear in comma-separated lists, e.g.,
 * 
 *     South   LOCATION
 *     ,       O
 *     New     LOCATION
 *     Orleans LOCATION
 *     ,       O
 *     Atlanta LOCATION
 *     ,       O
 *     Georgia LOCATION
 *     
 * becomes ONE location entry of South New Orleans Atlanta Georgia.  
 * The transcript form has "South, New Orleans, Atlanta, Georgia" so with clever subsequent processing we can 
 * clean up, but not unless we fully trust commas from Stanford NER.
 * 
 * Doing that now: assume that , will NOT be repeated as was the . in Jr. case noted above.
 */

namespace InformediaCORE.Processing.NamedEntities
{
    /// <summary>
    /// Polish, i.e., improve, the Named Entity Recognition results from 2 NER systems run across a corpus:
    /// (A) Stanford NER (https://nlp.stanford.edu/software/CRF-NER.html)
    /// (B) spaCy (https://spacy.io/)
    /// By default, run taking advantage of square bracket markup within the original text to qualify results
    /// (which is done, for example, by The HistoryMakers in their transcription process, e.g., a mention of 
    /// the city of Buffalo as in "I grew up in Buffalo where it snows a lot!" becomes qualified as 
    /// "I grew up in Buffalo [New York] where it snows a lot!"
    /// By default, also take advantage of hard returns '\n\n' in original text to indicate a separation of any
    /// sequence of words collected for an entity (so Paris\n\nTexas is two entities Paris and Texas instead of one Paris Texas).
    /// </summary>
    public static class StanfordNERPolisher
    {
        static readonly bool useSquareBracketHinting = true;
        static readonly bool useSourceTextNewLinesAsHardEntityBreaks = true;

        /// <summary>
        /// Ggenerate a list of named entity candidates for the given Stanford NER output as follows:
        /// name    name-with-context transcript-start-offset length  type
        /// where:
        /// name-with-context brings in context like square-bracketed text via heuristics
        /// type is one of PERSON, LOC, ORG
        /// An output of confidence or qualification of people/locations is expected with additional processing.
        /// </summary>
        /// <param name="stanfordFile">Fully qualified path to Stanford NER results.</param>
        /// <param name="transcript">The story segment's transcript.</param>
        /// <returns>A list of entities on success; null on failure.</returns>
        public static List<NamedEntity> Polish(string stanfordFile, string transcript)
        {
            List<NamedEntity> entries = new List<NamedEntity>();
            NamedEntity currentEntry;
            EntityType currentType;

            int transcriptOffset = 0;
            int foundOffset;
            bool isWithinSquareBrackets;
            string oneStanfordNERLine;
            string[] linePieces;
            char[] lineSeparatorChars = { '\t' };
            bool fillingCurrentEntry;
            string targetString;
            bool consideringSBPrefix; // needed when parsing square bracket prefix [stuff]words which may become part of entity tagged in following word(s)
            int numberOfCloseSquareBracketsToEat = 0;

            using (var stanford_sr = new StreamReader(stanfordFile))
            {
                while ((oneStanfordNERLine = stanford_sr.ReadLine()) != null)
                {
                    linePieces = oneStanfordNERLine.Split(lineSeparatorChars);
                    targetString = ""; // in case we have no data, blank this out...
                    if (linePieces.Count() == 2 && linePieces[0].Length > 0)
                    {
                        targetString = GetTranscriptSourceForm(linePieces[0]); // show transcript form, e.g., [ rather than -LSB-
                    }

                    if (targetString.Length > 0)
                    {
                        currentType = TypeForText(linePieces[1]);

                        // Roll the transcriptOffset forward regardless of if this is a named entity or not,
                        // so that we line up later labeled named entities properly (i.e., keep stanford NER and transcript in sync)
                        foundOffset = transcript.IndexOf(targetString, transcriptOffset);
                        if (foundOffset >= transcriptOffset)
                            // Move up where we are processing in transcript:
                            transcriptOffset = foundOffset + targetString.Length;
                        else
                        {
                            Logger.Error("StanfordNERPolisher: Transcript did not have this text from Stanford NER after position {0}: {1}", transcriptOffset, targetString);
                            return null;
                        }

                        /// Cases to cover within the if clause:
                        /// (A) NER recognized an entity
                        /// (B) we are using [] hinting and are at an opening [] which may or may not prefix a found entity, e.g.,
                        /// 
                        ///   -LSB-       O
                        ///   US          LOCATION
                        ///   Congressman O
                        ///   William     PERSON
                        ///   -RSB -      PERSON
                        ///   Dawson      PERSON
                        /// 
                        /// should become a PERSON entry for "US Congressman William Dawson".  Of course, we might also have
                        /// 
                        ///   -LSB-    0
                        ///   with     0
                        ///   emphasis 0
                        ///   -RSB-    0
                        ///   .        0
                        ///   
                        /// ...in which case the [] prefix is ignored (no entity found). [] are used by transcribers for more than just 
                        /// entity disambiguation.

                        if (currentType != EntityType.Unset || (useSquareBracketHinting && (targetString == "[")) )
                        {
                            // Start of something important.  Make it a NE entry, moving up in transcript to collect its full text as well.

                            // NOTE: [ may or may not be labeled as a named entity, so adjust for that first if we are using [-hinting ...
                            isWithinSquareBrackets = useSquareBracketHinting && (targetString == "[");
                            consideringSBPrefix = isWithinSquareBrackets; // the [sequence] may come before any entity tag (see Dawson example above)

                            // Make a new entry.
                            currentEntry = new NamedEntity
                            {
                                Text = targetString,
                                ContextualizedText = targetString,
                                StartOffset = foundOffset,
                                Length = targetString.Length,
                                Type = currentType,
                                Confidence = EntityConfidence.None
                            };

                            fillingCurrentEntry = true;
                            /// Read next line(s).  If it is [ or ] and we have useSquareBracketHinting, fold that into entry.
                            /// If it is between [ and ], fold that into entry.
                            /// !!!TBD!!! Later, parse whether we describe 2+ entities in "string [string]" or "[string] string" patterns instead of a collapse into just 1.
                            /// If it is tagged the same way as the current entry, fold that into current entry.
                            /// Keep doing that until we hit end of file or have a different entry outside of a bracketed string (if useSquareBracketHinting).
                            while (fillingCurrentEntry)
                            {
                                oneStanfordNERLine = stanford_sr.ReadLine();
                                if (oneStanfordNERLine == null)
                                {
                                    // End of file.  Check for error cases first.
                                    if (isWithinSquareBrackets) // NOTE: this takes care of premature ending of consideringSBPrefix as well...
                                    {
                                        Logger.Error("StanfordNERPolisher: Square brackets mismatched.");
                                        return null;
                                    }
                                    else
                                    {
                                        currentEntry.Length = transcriptOffset - currentEntry.StartOffset;
                                        currentEntry.ContextualizedText = transcript.Substring(currentEntry.StartOffset, currentEntry.Length);
                                        fillingCurrentEntry = false; // end of file marks end of the current entry
                                    }
                                }
                                else
                                {   // have a data line
                                    linePieces = oneStanfordNERLine.Split(lineSeparatorChars);
                                    targetString = ""; // in case we have no data, blank this out...
                                    if (linePieces.Count() == 2 && linePieces[0].Length > 0)
                                        targetString = GetTranscriptSourceForm(linePieces[0]); // show transcript form, e.g., [ rather than -LSB-

                                    if (targetString.Length > 0)
                                    {   // have an expected data line of non-empty text followed by line splitter followed by NE-type indicator

                                        foundOffset = transcript.IndexOf(targetString, transcriptOffset);
                                        if (foundOffset == -1)
                                        {
                                            Logger.Error($"StanfordNERPolisher: Transcript at/after offset {transcriptOffset} does not have this text: {targetString}");
                                            return null;
                                        }

                                        if (isWithinSquareBrackets)
                                        {   // Keep processing until closing square bracket found.
                                            if (targetString == "[")
                                            {
                                                /// Unfortunately, we may get nested square brackets, e.g., 
                                                /// [Philadelphia, Pennsylvania, Yale [University, New Haven, Connecticut]]
                                                /// "Eat" nested pairs, as in:
                                                /// [Philadelphia, Pennsylvania, Yale University, New Haven, Connecticut]
                                                /// Only outer one matters...
                                                numberOfCloseSquareBracketsToEat++;
                                            }
                                            else if (targetString == "]")
                                            {
                                                if (numberOfCloseSquareBracketsToEat > 0)
                                                    numberOfCloseSquareBracketsToEat--;
                                                if (numberOfCloseSquareBracketsToEat == 0)
                                                {
                                                    isWithinSquareBrackets = false; // on ], end the treatment of word(s) as being within [], but don't touch consideringSBPrefix
                                                                                    // as consideringSBPrefix doesn't get cleared until the word AFTER the ]

                                                    // Have all this text be part of "clean" name for current Entry.
                                                    transcriptOffset = foundOffset + 1;         // move past the ending ] in transcript
                                                    currentEntry.Text += " " + targetString;    // don't assume we're done yet, e.g., Martin [Luther] King is allowed
                                                }
                                            }
                                            else
                                            {
                                                // If we're considering the prefix for a NE, perhaps update the type if we don't have one yet....
                                                if (consideringSBPrefix)
                                                {
                                                    if (currentType == EntityType.Unset && TypeForText(linePieces[1]) != currentType)
                                                        currentType = TypeForText(linePieces[1]);
                                                    /// !!!TBD!!! later revisit to see if sequence of LOC PERSON within [] becomes PERSON,
                                                    /// if PERSON ORG becomes ORG, or whatever the rules are for mixed types within [].
                                                    /// Also, downgrade the confidence if we get mixed types that don't make sense....  !!!TBD!!!
                                                }
                                                // Collect words within [ and ] as marked in Stanford NER into the same current entry:
                                                currentEntry.Text += " " + targetString;
                                                transcriptOffset = foundOffset + targetString.Length; // move past this target string in transcript
                                            }
                                        }
                                        else if ( useSourceTextNewLinesAsHardEntityBreaks 
                                               && transcript.Substring(currentEntry.StartOffset, foundOffset + targetString.Length - currentEntry.StartOffset).Contains("\n\n"))
                                        {
                                            /// By our heuristic, we cannot lump this next text into what we have for the same entity, even
                                            /// if it is a \n\n[stuff] sequence, because the source text contains a hard line break \n\n 
                                            /// marking a new paragraph/piece and the rule is that a single entity does not span this 
                                            /// line break gap.  Close out the formerly collected entity.
                                            currentEntry.Length = transcriptOffset - currentEntry.StartOffset;
                                            currentEntry.Type = currentType;
                                            currentEntry.ContextualizedText = transcript.Substring(currentEntry.StartOffset, currentEntry.Length);
                                            fillingCurrentEntry = false;
                                        }
                                        else if (targetString == "[" && useSquareBracketHinting)
                                        {   // collect all of [string] and put it with this current entity
                                            isWithinSquareBrackets = true;  // note: don't update consideringSBPrefix here, because if it's false then
                                                                            // we have an acknowledged entity before the [, if true we have [seq]...[seq2+]
                                            currentEntry.Text += " " + targetString;
                                            transcriptOffset = foundOffset + 1; // move past this target string in transcript
                                        }
                                        else
                                        {   // processing non-square bracket text or we are giving no special treatment to [sequence] denotation.

                                            // If we are considering a [prefix] sequence, then if consideringSBPrefix we will take this first
                                            // non-square-bracketed type (what's in linePieces[1]) as THE type for the entity.  
                                            if (consideringSBPrefix)
                                            {
                                                consideringSBPrefix = false; // consideration concludes once we hit entry following the [sequence]
                                                if (TypeForText(linePieces[1]) != currentType)
                                                {
                                                    if (TypeForText(linePieces[1]) == EntityType.Unset)
                                                    {
                                                        // The [stuff] on its own is an entity as it is typed (i.e., currentType != Unset)
                                                        currentEntry.Length = transcriptOffset - currentEntry.StartOffset;
                                                        currentEntry.ContextualizedText = transcript.Substring(currentEntry.StartOffset, currentEntry.Length);
                                                        fillingCurrentEntry = false;
                                                        // Next line already queued up -- process it again in outer loop...  Don't move transcriptOffset past this queued up line...
                                                    }
                                                    else
                                                    {
                                                        /// !!!TBD!!! Revisit heuristic as needed: for now supporting
                                                        /// -LSB-	O
                                                        /// US LOCATION
                                                        /// Congressman O
                                                        /// William PERSON
                                                        /// -RSB - PERSON
                                                        /// Dawson PERSON
                                                        /// ...by overwriting the type with the entry following the ], i.e., all of
                                                        /// [US Congressman William] Dawson gets tagged as PERSON.
                                                        currentType = TypeForText(linePieces[1]);
                                                        /// tacking on targetString to the current entry occurs in logic below,
                                                        /// within the else for if (fillingCurrentEntry) {if (types different)...else...} logic.
                                                        /// 
                                                        /// Don't conclude the entry yet, i.e., keep fillingCurrentEntry as true,
                                                        /// since we may have [Dr. Martin] Luther King Jr. all as 1 person entry...
                                                    }
                                                }
                                                else if (currentType == EntityType.Unset)
                                                {
                                                    /// Handle case of [text] nexttext having no spotted entity.  Back out of presumption
                                                    /// that square bracketed text was a prefix to qualify a named entity.  The partially
                                                    /// filled in currentEntry should be ignored - there is no entity here.

                                                    transcriptOffset = foundOffset + targetString.Length;   // move past this target string in transcript
                                                                                                            // Dismiss this line without further consideration.
                                                    oneStanfordNERLine = stanford_sr.ReadLine();
                                                    fillingCurrentEntry = false;
                                                    // (no extra logic needed as currentType == NE_Row.NE_Type.Unset denotes this throwaway case...)
                                                }
                                            }

                                            if (fillingCurrentEntry)
                                            {
                                                // decide what to do with current line as that has not yet been decided...
                                                if (TypeForText(linePieces[1]) != currentType)
                                                {
                                                    // End collection of the prior type.
                                                    currentEntry.Length = transcriptOffset - currentEntry.StartOffset;
                                                    currentEntry.ContextualizedText = transcript.Substring(currentEntry.StartOffset, currentEntry.Length);
                                                    fillingCurrentEntry = false;
                                                    // Next line already queued up -- process it again in outer loop...
                                                }
                                                else
                                                {
                                                    // Same type, so move transcriptOffset forward as we will use this transcript text span, expanding
                                                    // on what we have in hand already
                                                    currentEntry.Text += " " + targetString;
                                                    transcriptOffset = foundOffset + targetString.Length; // move past this target as it will get put into currentEntry
                                                }
                                            }
                                        } // Stanford NER line processing
                                        
                                    } // end of parsing line with exactly two fields as expected from Stanford NER dump (text \t label-or-0)
                                } // end of parsing line
                            } // looping through filling up a named entity entry

                            if (currentType != EntityType.Unset)
                            {
                                // Save the collected entry if it has a valid type
                                currentEntry.Type = currentType;
                                entries.Add(currentEntry);
                            }
                        } // end of a marked named entity's processing
                    }
                } // end of while statement
            } // end of using statement

            return entries;
        }

        /// <summary>
        /// Returns source form for given text
        /// </summary>
        /// <param name="givenText">derived text, e.g., from a NER system</param>
        /// <returns>source form for much of the text as it would be in the source transcript, blanked out empty string for most punctuation</returns>
        /// <remarks>
        ///   This turns much punctuation into a "no operation" blank "" 
        ///   since some punctuation like '' or " in transcript may appear as a string of: `` or, worse, some like "Jr. " is doubled with 
        ///   Stanford NE output showing "Jr." followed by "." despite there being just one period in the transcript.  
        ///   Deal with ambiguity and duplication by giving up/opting out on source forms for some punctuation.
        ///   Also, if Stanford NER is quirky regarding [ or ] with punctuation, preserve such [ and ] as we may use them
        ///   for other NE processing hinting.
        /// </remarks>
        static string GetTranscriptSourceForm(string givenText)
        {
            string retVal;

            if (givenText == "-LSB-")
                retVal = "[";
            else if (givenText == "-RSB-")
                retVal = "]";
            else if (givenText == "-LRB-")
                retVal = "(";
            else if (givenText == "-RRB-")
                retVal = ")";
            else if (givenText == "-LCB-")
                retVal = "{";
            else if (givenText == "-RCB-")
                retVal = "}";
            else if (givenText == ",")
            {
                /// comma is VERY important to break about lists of named entities, 
                /// so keep it and assume it is never doubled in Stanford NER list,
                /// i.e., that walking the Stanford NER list will also walk the transcript cleanly
                retVal = ","; 
            }
            else
            {
                /// Unfortunately, Stanford NER tokenizer will "dirty" things up a bit in unusual ways,
                /// such that for example :) might become :-RRB- ...clean up by first replacing out the
                /// tokens as listed above, and then as needed deciding there's nothing of worth in the
                /// string to return to the caller.
                /// This will take some processing time, but will let us work through all tokenized outputs without error
                /// so that we can walk through Stanford tokenized output (!!!TBD!!! as of June 2019 ONLY!) and walk through transcript, too.
                string workVal = givenText.Replace("-LSB-", "[");
                workVal = workVal.Replace("-RSB-", "]");
                workVal = workVal.Replace("-LRB-", "(");
                workVal = workVal.Replace("-RRB-", ")");
                workVal = workVal.Replace("-LCB-", "{");
                workVal = workVal.Replace("-RCB-", "}");
                if (HasAlphanumeric(workVal))
                {
                    if (HasNonASCII(workVal))
                        workVal = MapToASCII(workVal);
                    retVal = workVal;
                }
                else if (workVal.Contains("["))
                    retVal = "["; // NOTE: Stanford NER may "lose" [ in punctuation
                else if (workVal.Contains("]"))
                    retVal = "]"; // NOTE: Stanford NER does "lose" ] in punctuation such as :] for story 15/3666
                else // this handles workVal == "``" || "''" || "`" || "'" and lots of other goofy punctuations including , . ? etc.
                    retVal = "";
            }

            return retVal;
        }

        /// <summary>
        /// Stanford NER tokenizer sometimes introduces noise with respect to text that will
        /// let us walk the Stanford NER token lines to get offsets from the source text.
        /// Note if the string has nonASCII text, despite the source text being all ASCII.
        /// </summary>
        /// <param name="givenString"></param>
        /// <returns>true if a non-ASCII character found...</returns>
        /// <remarks>
        ///  Three real data files where this noise was found:
        ///  Collection ID 107, story ID 7962, (###) ###-#### phone number pattern with space not an ASCII space
        ///  Collection ID 137, 3734 with 6 1/2 not having space before fraction
        ///  Collection ID 72, 9789 with 33 1/3 not having space before fraction
        /// </remarks>
        static bool HasNonASCII(string givenString)
        {
            int charIntValue;
            if (string.IsNullOrEmpty(givenString))
                return false;

            for (int i = 0; i < givenString.Length; i++)
            {
                charIntValue = (int)givenString[i];
                if (charIntValue > 255)
                {   // NOTE: typically the reported value is 65533 indicating some other encoding (Unicode)
                    // used rather than ASCII - may not be able to "see" the value well so leave WARNING vague.
                    Logger.Warning("StanfordNERPolisher: Non-ASCII value at offset {0} within: '{1}'", i ,givenString);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Problematic mapping of Stanford NER token to an all-ASCII string as it
        /// appears in the source text (transcript).  We do not know all the mappings.
        /// For now, we are pushing most out-of-bounds characters to " ".
        /// </summary>
        /// <param name="givenString"></param>
        /// <returns>A string where all characters are in the 7-bit ASCII text range.</returns>
        /// <remarks>Since this is slow, use HasNonASCII to gate when it must be called.</remarks>
        static string MapToASCII(string givenString)
        {
            if (string.IsNullOrEmpty(givenString))
                return givenString;

            string correctedString = givenString;
            for (int i = 0; i < givenString.Length; i++)
            {
                if (((int)givenString[i]) > 255)
                {
                    // !!!TBD!!! NOTE: this is too simplifying, perhaps, to take all out-of-bounds chars as space ' '
                    correctedString = givenString.Replace(givenString[i], ' ');
                }
            }

            return correctedString;
        }

        /// <summary>
        /// Returns true iff parameter has at least one character in [a-zA-Z0-9] (alphanumeric) range.
        /// </summary>
        /// <param name="givenString"></param>
        /// <returns></returns>
        static bool HasAlphanumeric(string givenString)
        {
            if (string.IsNullOrEmpty(givenString))
                return false;

            for (int i = 0; i < givenString.Length; i++)
            {
                if ((char.IsLetter(givenString[i])) || ((char.IsNumber(givenString[i]))))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Converts Stanford entity type string value to an enumeration value.
        /// </summary>
        /// <param name="givenText">Stanford entity type as string.</param>
        /// <returns>EntityType enumeration value.</returns>
        static EntityType TypeForText(string givenText)
        {
            EntityType typeOfNamedEntity = EntityType.Unset;
            if (givenText == "PERSON")
                typeOfNamedEntity = EntityType.Person;
            else if (givenText == "LOCATION")
                typeOfNamedEntity = EntityType.Loc;
            else if (givenText == "ORGANIZATION")
                typeOfNamedEntity = EntityType.Org;
            return typeOfNamedEntity;
        }

    }
}
