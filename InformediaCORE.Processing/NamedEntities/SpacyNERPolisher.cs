using System.IO;
using System.Collections.Generic;
using System.Linq;

using InformediaCORE.Common;

/**
 * NOTES: (CHRISTEL)
 * Some stories, like collection 1 story 8193, have no named entities resulting in a zero-length *.pass2.txt file.
 * SpaCy states they have tokens that are verbatim in transcript, but at least " in transcript messes up such tokens,
 * so ignore tokens having " in them if they are not found verbatim in the given transcript location.
 * Very rarely (once in 148,665 stories!) SpaCy may include a tab character \b in its entity text.  This messes
 * up tab-separated fields later.  So, if SpaCy insists on a tab, give a warning and skip it.
 */

namespace InformediaCORE.Processing.NamedEntities
{
    /// <summary>
    /// Pass 3, i.e., improve, the Named Entity Recognition results from 2 NER systems run across a corpus:
    /// (A) Stanford NER (https://nlp.stanford.edu/software/CRF-NER.html)
    /// (B) spaCy (https://spacy.io/)
    /// In Pass 2, Stanford NER was cleaned up a bit and disambiguation context entended to include square 
    /// bracket markup from the source text (e.g., Buffalo [New York]).  Here, we take spaCy output and use
    /// it to refine what is the named entity, while keeping the larger context intact in case it can serve
    /// for disambiguation.
    /// </summary>
    /// <remarks>
    /// Specifically, this Pass3 does the following:
    /// 
    /// (a) break apart long sequences (typically with 1 or more [] sequences) in Pass 2 into multiple named entities
    /// (b) add in confidences: low if an entity is just in Pass 2 (just from Stanford NER) or just in spaCy, 
    ///     higher if in both but mismatched types, highest if in both and both agree on person/location/organization
    /// (c) might be done via (b), but make sure to water down person entity in a story from Pass 2 if spaCy says it 
    ///     is something else, like Work of Art (e.g., collection ID 10, story 2871 has 'Finding Buck McHenry' as Work of Art).
    /// (d) shrink the cleaned entity name from the longer name-with-context (typically text in square brackets) 
    ///     based on spaCy start/end points
    /// 
    /// From https://spacy.io/api/annotation there are these types, with notation here on which we skip over:
    /// 
    /// PERSON      As-is           People, including fictional.
    /// NORP        treated as ORG  Nationalities or religious or political groups.
    /// FAC         treated as LOC  Buildings, airports, highways, bridges, etc.
    /// ORG         As-is           Companies, agencies, institutions, etc.
    /// GPE         treated as LOC  Countries, cities, states.
    /// LOC         As-is           Non-GPE locations, mountain ranges, bodies of water.
    /// PRODUCT     ignored*        Objects, vehicles, foods, etc. (Not services.)
    /// EVENT       ignored*        Named hurricanes, battles, wars, sports events, etc.
    /// WORK_OF_ART ignored*        Titles of books, songs, etc.
    /// LAW         ignored*        Named documents made into laws.
    /// LANGUAGE    ignored*        Any named language.
    /// DATE        year-check      Absolute or relative dates or periods.
    /// TIME        ignored*        Times smaller than a day.
    /// PERCENT     ignored         Percentage, including ”%“.
    /// MONEY       ignored         Monetary values, including unit.
    /// QUANTITY    ignored         Measurements, as of weight or distance.
    /// ORDINAL     ignored         “first”, “second”, etc.
    /// CARDINAL    year-check      Numerals that do not fall under another type.
    /// 
    /// * Those marked "ignored*" will water down any Stanford NER entries because they will not be considered
    /// universally recognized people, places, or organizations.  
    /// </remarks>
    public static class SpacyNERPolisher
    {
        /// <summary>
        /// For a given path that is confirmed to have the necessary transcript, spaCy NER output, and Pass 2 (via Stanford NER) output files,
        /// generate a list of named entity candidates to a pass3.txt file, with lines in tab-delimited fields as follows:
        /// name    name-with-context source-text-start-offset length  type  spaCy-type  confidence
        /// where:
        /// name-with-context brings in broader source-text context (via Pass 2 heuristics)
        /// type is one of [0,3] for 0=name, 1=location, 2=organization, 3=year (yes, Year is brought in, as we get that from spaCy...)
        /// confidence is one of [0,3] for 
        ///     0=none/default, 1=a bit (e.g., one of the NER systems), 
        ///     2=good (spaCy and Stanford NE both recognize something in intersecting spans but either spans don't overlap well and/or
        ///     the two NE systems tag the text differently)
        ///     3=better (e.g., both Stanford and spaCy agree on tag type, and one (typically spaCy) is fully within the other.
        /// Further enhancement of confidence or qualification of people/locations may be likely with additional processing, e.g., 
        /// using a geocoding lookup service to beef up sources to confidence of 4=trustworthy or 5=best if they are found, or 
        /// if they are noted in the parent collection's abstract.
        /// </summary>
        /// <param name="currentCollectionID">collection identifier</param>
        /// <param name="currentStoryID">story identifier</param>
        /// <param name="pathAndBaseName">path and base name to NER and transcript data files</param>
        /// <returns>true on no errors, false if error found during processing</returns>
        public static List<NamedEntity> Polish(string spacyFile, string transcript, List<NamedEntity> pass2Entities)
        {
            List<NamedEntity> entries = new List<NamedEntity>();
            NamedEntity currentEntry;
            EntityType currentType;

            int foundOffset;
            string oneSpaCyLine = null;
            string[] linePieces;
            char[] spaCyLineSeparatorChars = { ',' };
            bool giveUpEarlyOnError = false;
            string targetString;
            int activeEntryForPass2Processing = 0;
            bool useSpaCyNameEntry;
            bool sideStepDoubleQuoteIssue;
            bool sideStepTabIssue;

            if (pass2Entities == null)
            {
                Logger.Error("SpacyNERPolisher: Pass2 entity list is null. Cannot proceed.");
                return null;
            }

            using (var spacy_sr = new StreamReader(spacyFile))
            {
                // Discard header row
                _ = spacy_sr.ReadLine();

                while ((oneSpaCyLine = spacy_sr.ReadLine()) != null)
                {
                    currentEntry = null;
                    linePieces = oneSpaCyLine.Split(spaCyLineSeparatorChars);
                    targetString = ""; // in case we have no data, blank this out...
                    if (linePieces.Count() == 4 && linePieces[0].Length > 0)
                        targetString = GetTranscriptSourceForm(linePieces[0]);
                    if (targetString.Length > 0)
                    {
                        currentType = TypeForSpaCyType(linePieces[3]);

                        if (currentType != EntityType.Unset && currentType != EntityType.SomethingToIgnore)
                        {
                            if (int.TryParse(linePieces[1], out int givenOffset) && int.TryParse(linePieces[2], out int givenEndOffset) &&
                                givenEndOffset > givenOffset)
                            {
                                sideStepDoubleQuoteIssue = false;
                                sideStepTabIssue = false;

                                /// Check for lining up with transcript only for the most important entity types (because some "distractors"
                                /// like WORK_OF_ART that are considered as EntityType.SomethingElse have as a token text like 
                                /// "the ""Little Sparrow"
                                /// even though transcript text is something like: the "Little Sparrow." -- don't want to surface such punctuation differences
                                if (currentType == EntityType.Person || currentType == EntityType.Org || currentType == EntityType.Loc)
                                {
                                    // Roll the transcriptOffset forward because we may do something with this entry
                                    foundOffset = transcript.IndexOf(targetString, givenOffset);
                                    if (foundOffset != givenOffset)
                                    {
                                        /// NOTE: use of punctuation such as " throws off spaCy, e.g., 'minister--"I ' gets seen as this entity:
                                        /// "minister--""I" (no enclosing quotes - spaCy notes a " then m ... etc., which of course are NOT in the transcript).
                                        /// Walk back from an entity with a " in it and see if this works:
                                        if (targetString.Contains('"'))
                                            sideStepDoubleQuoteIssue = true; // just skip
                                        else
                                        {
                                            Logger.Error("SpacyNERPolisher: Transcript did not have this text from SpaCy NER at given SpaCy offset {0}: {1}", givenOffset, targetString);
                                            giveUpEarlyOnError = true;
                                        }
                                    }
                                }

                                if (targetString.Contains("\t"))
                                {
                                    Logger.Warning($"SpacyNERPolisher: Skipping entry with tab in: '{0}'", targetString);
                                    sideStepTabIssue = true;
                                }

                                if (!giveUpEarlyOnError && !sideStepDoubleQuoteIssue && !sideStepTabIssue)
                                {
                                    /// Cases to cover within the if clause:
                                    /// (A) This entry's [givenOffset, givenEndOffset) overlaps at least a bit with a Pass 2 entry. Up confidence and mark that
                                    ///     Pass 2 entry as being considered.
                                    /// (B) This entry's [givenOffset, givenEndOffset) is ahead of Pass 2 entry(ies).  Output those Pass 2 entries not yet considered
                                    ///     at a low confidence (since they are covered by just the Pass 2 Stanford NER and not confirmed with a spaCy overlap).
                                    ///     If ahead of ALL Pass 2 entries, do the same as (C)....
                                    /// (C) This entry's [givenOffset, givenEndOffset) is behind Pass 2 current entry.  Consider it as low confidence since it is 
                                    ///     not getting confirming support from Pass 2.

                                    // Note that with SpaCy we are now at offsets of givenOffset or greater.  
                                    // Move up activeEntryForPass2Processing and consideration of pass2Entries accordingly.
                                    while (activeEntryForPass2Processing < pass2Entities.Count)
                                    {
                                        if (pass2Entities[activeEntryForPass2Processing].StartOffset +
                                            pass2Entities[activeEntryForPass2Processing].Length <= givenOffset)
                                        {
                                            // This Pass 2 entry can no longer overlap with spaCy work.  Move past it....
                                            if (!pass2Entities[activeEntryForPass2Processing].ReceivedDuelCoverage)
                                            { // It is not considered in any way yet in the output set.  Do so now.
                                                entries.Add(new NamedEntity
                                                {
                                                    Text = pass2Entities[activeEntryForPass2Processing].Text,
                                                    ContextualizedText = pass2Entities[activeEntryForPass2Processing].ContextualizedText,
                                                    StartOffset = pass2Entities[activeEntryForPass2Processing].StartOffset,
                                                    Length = pass2Entities[activeEntryForPass2Processing].Length,
                                                    SpacyType = string.Empty,
                                                    Type = (EntityType)pass2Entities[activeEntryForPass2Processing].Type,
                                                    Confidence = EntityConfidence.Some
                                                });
                                            }
                                            activeEntryForPass2Processing++; // move beyond this entry, i.e., move up in Pass 2 entry list
                                        }
                                        else if (pass2Entities[activeEntryForPass2Processing].StartOffset < givenEndOffset)
                                        { // There is some overlap between the currently considered spaCy entry from [givenOffset, givenEndOffset)
                                          // and this Pass 2 entry.  
                                            if (pass2Entities[activeEntryForPass2Processing].Type == currentType)
                                            {
                                                // Best case! This entry confirms what we have in Pass 2.
                                                pass2Entities[activeEntryForPass2Processing].ReceivedDuelCoverage = true;
                                                useSpaCyNameEntry = true; // might get reset to false below for a few special cases...
                                                int computedConfidence = EntityConfidence.Better;
                                                if (givenOffset >= pass2Entities[activeEntryForPass2Processing].StartOffset &&
                                                    givenEndOffset <= pass2Entities[activeEntryForPass2Processing].StartOffset +
                                                    pass2Entities[activeEntryForPass2Processing].Length)
                                                {
                                                    useSpaCyNameEntry = true;
                                                }
                                                else if (pass2Entities[activeEntryForPass2Processing].StartOffset >= givenOffset &&
                                                    pass2Entities[activeEntryForPass2Processing].StartOffset +
                                                    pass2Entities[activeEntryForPass2Processing].Length <= givenEndOffset)
                                                {
                                                    // Less likely: using tighter bounds from Pass 2 regarding the actual name for the entity.
                                                    useSpaCyNameEntry = false;
                                                }
                                                else
                                                {
                                                    // not so sure what text to use for entry or whether to lower confidence; 
                                                    // for now, lower confidence a bit and use the longest entry not having [ within it or
                                                    // longest entry if they both have [...
                                                    computedConfidence = EntityConfidence.Good;
                                                    if (pass2Entities[activeEntryForPass2Processing].Text.Contains("[") &&
                                                        !targetString.Contains("["))
                                                    {
                                                        useSpaCyNameEntry = true;
                                                    }
                                                    else if (!pass2Entities[activeEntryForPass2Processing].Text.Contains("[") &&
                                                        targetString.Contains("["))
                                                    {
                                                        useSpaCyNameEntry = false;
                                                    }
                                                    else
                                                        useSpaCyNameEntry = ((givenEndOffset - givenOffset) >= pass2Entities[activeEntryForPass2Processing].Length);
                                                }

                                                if (useSpaCyNameEntry)
                                                {
                                                    // Typical: using tighter bounds from spaCy regarding the actual name for the entity.
                                                    entries.Add(new NamedEntity
                                                    {
                                                        Text = targetString,
                                                        ContextualizedText = pass2Entities[activeEntryForPass2Processing].ContextualizedText,
                                                        StartOffset = givenOffset,
                                                        Length = givenEndOffset - givenOffset,
                                                        SpacyType = linePieces[3],
                                                        Type = currentType,
                                                        Confidence = computedConfidence
                                                    });
                                                }
                                                else
                                                {
                                                    // Less likely: using Pass 2 regarding the actual name for the entity.
                                                    entries.Add(new NamedEntity
                                                    {
                                                        Text = pass2Entities[activeEntryForPass2Processing].Text,
                                                        ContextualizedText = pass2Entities[activeEntryForPass2Processing].ContextualizedText,
                                                        StartOffset = pass2Entities[activeEntryForPass2Processing].StartOffset,
                                                        Length = pass2Entities[activeEntryForPass2Processing].Length,
                                                        SpacyType = linePieces[3],
                                                        Type = (EntityType)pass2Entities[activeEntryForPass2Processing].Type,
                                                        Confidence = computedConfidence
                                                    });
                                                }
                                            }
                                            else
                                            {
                                                // Types don't line up, so log with slightly lower confidence.
                                                currentEntry = new NamedEntity
                                                {
                                                    Text = targetString,
                                                    ContextualizedText = pass2Entities[activeEntryForPass2Processing].ContextualizedText,
                                                    StartOffset = givenOffset, 
                                                    Length = givenEndOffset - givenOffset, 
                                                    SpacyType = linePieces[3],
                                                    Type = currentType, 
                                                    Confidence = EntityConfidence.Good
                                                };
                                                // Check whether to keep currentEntry below...
                                            }
                                            break; // no need to move Pass 2 ahead as we have overlap (and might find a few named entities in this single Pass 2 entry)
                                        }
                                        else
                                        {
                                            // spaCy entry is before the active Pass 2 entry, i.e., it is an entity only found by spaCy with no overlap
                                            currentEntry = new NamedEntity
                                            {
                                                Text = targetString,
                                                ContextualizedText = targetString,
                                                StartOffset = givenOffset,
                                                Length = givenEndOffset - givenOffset, 
                                                SpacyType = linePieces[3],
                                                Type = currentType, 
                                                Confidence = EntityConfidence.Some
                                            };
                                            // Check whether to keep currentEntry below...
                                            break; // no need to move Pass 2 ahead as it's ahead of spaCy already
                                        }
                                    }
                                    if (activeEntryForPass2Processing >= pass2Entities.Count)
                                    {
                                        // No confirming evidence or extra context from Pass 2 for this spaCy entry, just treat it as its own.
                                        currentEntry = new NamedEntity
                                        {
                                            Text = targetString, 
                                            ContextualizedText = targetString, 
                                            StartOffset = givenOffset,
                                            Length = givenEndOffset - givenOffset,
                                            SpacyType = linePieces[3],
                                            Type = currentType,
                                            Confidence = EntityConfidence.Some 
                                        };
                                        // Check whether to keep currentEntry below...
                                    }
                                    if (currentEntry != null)
                                    {
                                        // Need to decide whether to keep the current entry, perhaps with a bit more processing.
                                        if (currentEntry.Type == EntityType.YearPerhaps || currentEntry.Type == EntityType.Year)
                                        {
                                            // If we find 16xx, 17xx, 18xx, 19xx, 20xx, 21xx with xx numerals too then tag this as "Year" and continue.
                                            if (FoundYearEvidence(targetString, out int yearCandidate))
                                            {
                                                currentEntry.Text = yearCandidate.ToString();
                                                currentEntry.Type = EntityType.Year;
                                            }
                                            else
                                                currentEntry.Type = EntityType.SomethingToIgnore;
                                        }
                                        if (currentEntry.Type != EntityType.Unset && currentEntry.Type != EntityType.SomethingElse &&
                                                currentEntry.Type != EntityType.SomethingToIgnore)
                                        {
                                            entries.Add(currentEntry);
                                        }
                                        // else forget this entry as meaningless in Pass 3
                                    }
                                }
                            }
                            else
                            {
                                // Badly formatted spaCy input line
                                Logger.Error($"SpacyNERPolisher: SpaCy NER output line offsets could not be parsed: {0}", oneSpaCyLine);
                                return null;
                            }
                        }
                    }
                } // end of processing input file
            } // end of using statement

            return entries;
        }

        /// <summary>
        /// Return true iff given string contains a sequence as one of 15xx, 16xx, 17xx, 18xx, 19xx, 20xx, 21xx with xx a meaningful numeral in range 00-99.
        /// </summary>
        /// <param name="givenString">given string</param>
        /// <param name="yearValue">output year value of given string</param>
        /// <returns></returns>
        static bool FoundYearEvidence(string givenString, out int yearValue)
        {
            char[] leadingYearNumber = { '1', '2' };
            int startIndex = 0;
            int foundIndex;

            bool foundYear = false;
            yearValue = 0;

            while (startIndex < givenString.Length - 4)
                if ((foundIndex = givenString.IndexOfAny(leadingYearNumber, startIndex)) >= 0 && foundIndex < givenString.Length - 4)
                {
                    if (int.TryParse(givenString.Substring(foundIndex, 4), out yearValue))
                        if (yearValue >= 1500 && yearValue <= 2199)
                        {
                            foundYear = true;
                            break;
                        }
                    startIndex = foundIndex + 1;
                }
                else
                    break;

            return foundYear;
        }

        /// <summary>
        /// Returns source form for given text
        /// </summary>
        /// <param name="givenText">derived text, e.g., from a NER system</param>
        /// <returns>source form for much of the text as it would be in the source transcript</returns>
        /// <remarks>SpaCy claims no change in source text form, so just return it.</remarks>
        static string GetTranscriptSourceForm(string givenText)
        {
            return givenText; // SpaCy claims no change in source text form, so just return it.
        }

        static EntityType TypeForSpaCyType(string givenText)
        {
            // From https://spacy.io/api/annotation there are these types, with notation here on which we skip over:
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
            // universally recognized people, places, or organizations.  
            EntityType typeOfNamedEntity = EntityType.Unset;
            if (givenText == "PERSON")
                typeOfNamedEntity = EntityType.Person;
            else if (givenText == "LOC" || givenText == "GPE" || givenText == "FAC")
                typeOfNamedEntity = EntityType.Loc;
            else if (givenText == "ORG" || givenText == "NORP")
                typeOfNamedEntity = EntityType.Org;
            else if (givenText == "EVENT" || givenText == "DATE" || givenText == "CARDINAL")
                typeOfNamedEntity = EntityType.YearPerhaps;
            else if (givenText == "PRODUCT" || givenText == "WORK_OF_ART" || givenText == "LAW" ||
                givenText == "LANGUAGE" || givenText == "TIME")
                typeOfNamedEntity = EntityType.SomethingElse;
            else if (givenText == "PERCENT" || givenText == "MONEY" || givenText == "QUANTITY" ||
                givenText == "ORDINAL")
                typeOfNamedEntity = EntityType.SomethingToIgnore;
            return typeOfNamedEntity;
        }
    }
}

