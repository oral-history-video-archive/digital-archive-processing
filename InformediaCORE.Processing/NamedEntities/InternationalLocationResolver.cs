using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using InformediaCORE.Common;
using InformediaCORE.Common.Config;

/**
 * NOTES: (CHRISTEL) Pass 5 works only with those yet-to-be-disambiguated locations not resolved to 
 * U.S., and currently only resolves to given list of cities or countries. As of February 2020, the 
 * cities come from Simplemaps excluding those whose first "letter" was less than A in a sort (just 5) 
 * and excluding the U.S. cities, leaving 8090 candidates. More specifically, the free data set from 
 * https://simplemaps.com/data/world-cities (labeled "Basic") was downloaded Feb. 19, 2020 and the 
 * Basic World Cities Database is licensed under the Creative Commons Attribution 4.0 license as 
 * described at: https://creativecommons.org/licenses/by/4.0/. 
 * 
 * The countries are from file GENC_ED3U10_GEC_XWALK.xlsx from 
 * http://geonames.nga.mil/gns/html/countrycodes.html with numeric country codes used to record the 
 * country location. Additional names were added in to better resolve to country names, see worker file 
 * for complete details (e.g., Great Britain and England and U.K. for United Kingdom, etc.), and those 
 * with a comma in them had the comma form removed in favor of a non-comma alternate (e.g., North Korea 
 * instead of "Korea, North" as we don't parse a location as having a comma within it (instead, we look 
 * for commas to resolve a location into a city, country pair). Also, the unusual "Entity 1", "Entity 
 * 2" etc. were taken out (again, see worker Excel file if curious as to where the country names came 
 * from). 
 * 
 * On a match (success), the program bumps up location reference confidences, and adds in our own city 
 * ID from our table, if resolved to a city, and the numeric country code (from input table). This pass 
 * is ONLY for "significant" international cities and country names (not ALL international locations, 
 * etc.). 
 * 
 * NOTE WELL: of special interest for first runs of data: what place names are given a country that 
 * "works" yet the place name will not resolve given the truth table. 
 * 
 * NOTE WELL: Pass 3 input file MUST be sorted on collection ID, then story ID, then start offset 
 * ascending to keep stories together and entries in offset order. That collection/story/startoffset 
 * grouping/order must be kept in the Pass4-non-U.S. input file list. 
 * 
 * Heuristics for Pass 5 are similar to Pass 4 instead of "U.S. city, U.S. state" pairs, looking for 
 * "international city, country" pairs, as noted in comments for ProcessEntriesForStory(). 
 * 
 * For now, try to work across all the locations, not just those with highest confidence of 3 from 
 * "Pass 3." 
 * 
 * Order of work: 
 * (1) Get all still-not-resolved locations for a story (all confidences for now....). FYI, 320,280 
 *     such entries from Feb. 2020 processing of 148,665 stories. 
 * 
 * 
 * (2) For each, try to make into a city/country pair or just a country (using ProcessEntriesForStory 
 *     heuristics). 
 * 
 * 
 * (3) In outputting entries, add in new column for THMDA-City-ID as well as country numeric code for 
 *     the container country (if applicable) or just the country code for country-only resolutions, 
 * 	and update confidence as appropriate. Some jump +1 (found country) or +2 (found city and country); 
 *     others may jump more (formerly low confidence that matches another entry in this same story so 
 * 	bump up to all be the same highest confidence in that story). 
 * 
 * 
 * 
 * NOTE: this does NOT yet consider hints/context from higher level text such as tape abstracts, 
 * session abstracts, biography abstracts, biography metadata like birth city. This only considers the 
 * story transcript processing via what is already in our "Pass 3" output file. "Pass 4" processing to 
 * pull out the U.S. cities/states produces a file of still-unresolved locations (including those just 
 * to U.S. without further qualification). Resolve the non-U.S. countries and cities in non-U.S. 
 * countries here. 
 * 
 * Feb. 2020 Update: Locations like Jamaica Avenue mistakenly tagged to the country. Add in 
 * place/context check. If place [place context] or place [Lake place] (e.g., Lake Michigan) or place 
 * context or context place (just Lake place) has context as one of {Lake, River, Avenue, Street, Road, 
 * Boulevard, Ave., Blvd., Blvd} with only { Lake } allowed before or after, others must be suffixes, 
 * then do NOT tag that as the given place. Finally, resolve "the city of Dakar" to Dakar and "[Dakar]" 
 * to Dakar via better clean-up of place name in ProperNameForPlace(). 
 */

namespace InformediaCORE.Processing.NamedEntities
{
    class City_Hinting_Row
    {
        // input data of the tab-separated format: Montreal	124	4564
        // How it's used: dictionary entry where the name (e.g., 'Montreal') is used as the key
        // Remaining for value with this key: country-ID, THMDA-City-ID
        public int countryNumeric;
        public int THMDACityID;

        public City_Hinting_Row(int givencountryNumeric, int givenTHMDACityID)
        {
            countryNumeric = givencountryNumeric;
            THMDACityID = givenTHMDACityID;
        }
    }

    class PlaceNameInfo
    {
        // Typical: Montreal	124	4564
        // How it's used: dictionary entry where the name (e.g., Montreal) is used as the key for a list of country-code and city-code values.
        // NOTE WELL: a name would be a poor key choice if we wanted to match an exact city, since a name like Cambridge or London may be
        // in multiple countries.  So, have a name like "London" be a key for a List of PlaceNameInfos where usually the List count is 1, but 
        // perhaps we have entries for the U.K., Canada, etc., making there be more than 1 places with a given name.

        public int countryNumeric;
        public int THMDACityID;

        public PlaceNameInfo(int givenCountryCode, int givenCityID)
        {
            countryNumeric = givenCountryCode;
            THMDACityID = givenCityID;
        }
    }

    /// <summary>
    /// Take as input a country name list (input file), a list of place names that have a default 
    /// country container (input file), and a list of international city names with their country 
    /// containers (input file), plus the locations "Pass 4" output.  Update confidences and add in city
    /// and/or country ID where possible for these locations, with the "Pass 5" output disambiguating 
    /// hopefully most of the international locations.
    /// </summary>
    /// <remarks>
    /// The input from is from "pass 3".
    /// Furthermore, because these locations remain unresolved, the values for PlaceID, StateCode, and
    /// CountryCode are 0.  This program OVERRIDES these two of these three fields to now be THMDA-City-Code 
    /// (0 if unresolved), state left as 0, and Country-Code (0 if unresolved).
    /// </remarks>
    public static class InternationalLocationResolver
    {
        // NOTE: important data files within dataPath each with a header line to start before the data:
        // CountryNameListWithCodes.txt, tab-delimited rows of numeric-code and country-name-plus-variants,
        // so may have many names for any one code WorldCitiesWithCodes.txt, tab-delimited rows of place-name,
        // country-number-code, and THMDA-city-ID DefaultCountriesForSomeLocations.txt, tab-delimited rows of
        // name, country-number-code, THMDA-city-ID
        static readonly string dataPath = Settings.Current.EntityResolutionTask.DataPath;

        private static readonly Dictionary<string, int> countryInfo = LoadCountryInfo();
        private static readonly Dictionary<string, City_Hinting_Row> cityHintingInfo = LoadCityHintingInfo();
        private static readonly Dictionary<string, List<PlaceNameInfo>> placeInfo = LoadPlaceNameDictionary();

        /// <summary>
        /// Resolve possible international locations within the given list of location entities.
        /// </summary>
        /// <param name="pass4UnresolvedLocations">A list of previously unresolved location entities.</param>
        /// <returns>A list of resolved location entities.</returns>
        /// <remarks>This logic comes from the Main method of the original source code.</remarks>
        public static List<LocationEntity> Resolve(List<LocationEntity> pass4UnresolvedLocations)
        {
            List<LocationEntity> processedStoryEntities = ProcessEntriesForStory(pass4UnresolvedLocations);

            // Output each entry into one of two buckets: resolved or unresolved:
            List<LocationEntity> unresolvedLocations = new List<LocationEntity>();

            // NOTE: we also want to output ResolvedThinned which removes the many duplicate entries
            // per story for a location name produced by slightly different contextual texts or name 
            // texts.  Instead of listing say Chicago 8 times for a story, list it once, keeping it 
            // at highest confidence.
            // If a location is marked up 3+ times, boost its confidence again by +1.
            // For ResolvedThinned, output: collectionID, storyID, place-id, 0 for the state-id, 
            // country-id, max-updated-confidence.
            Dictionary<int, LocationEntity> resolvedLocations = new Dictionary<int, LocationEntity>(); // places within a country

            foreach (LocationEntity entity in processedStoryEntities)
            {
                if (entity.CountryCode != 0)
                {   
                    if (resolvedLocations.ContainsKey(entity.PlaceID))
                    {
                        // Already here.
                        resolvedLocations[entity.PlaceID].Count++;
                        resolvedLocations[entity.PlaceID].Confidence = Math.Max(entity.Confidence, resolvedLocations[entity.PlaceID].Confidence);
                    }
                    else
                    {
                        resolvedLocations.Add(entity.PlaceID, entity);
                    }
                }
                else
                { 
                    // Put into unresolved bucket
                    unresolvedLocations.Add(entity);
                }
            }

            // Boost Confidence
            foreach (int onePlaceID in resolvedLocations.Keys)
            {
                if (resolvedLocations[onePlaceID].Count > 3)
                {                    
                    // Boost the confidence up +1
                    resolvedLocations[onePlaceID].Confidence += 1;
                }
            }

            return resolvedLocations.Values.ToList();
        }

        /// <summary>
        /// Apply heuristics and various helper data to resolve international (non-U.S.) place names to
        /// their "city-id" and container country-code identifiers (where a location that resolves to a 
        /// country but not a known location within the country from our admittedly thin list of 8090 
        /// place names will get a city-id of 0 for unresolved but a filled in non-zero country code).
        /// If a location resolves, update its confidence.
        /// </summary>
        /// <param name="oneStoryEntries">entries within story, in startOffset ascending order</param>
        /// <param name="keptEntries">list of overall kept entries with updated confidences and place and country IDs</param>
        /// <remarks>
        /// If within the same story a name resolves, resolve that name across all mentions, e.g., if in a
        /// story there is Toronto Canada and later just Toronto, make both be the Toronto Canada city entry.
        /// If context has form of "place country" then use that to disambiguate.  Parsing is tricky because:
        /// (a) Country referred to in many ways: U.K., Great Britain, England, etc. 
        ///     Capture anticipated ways via input data file, and see if other ways are missed by looking at 
        ///     "left on the table" data.
        /// (b) Both place and country can be multi-word. If comma is there, make use of that to help break 
        ///     into 2.  This means country names have been adjusted to NOT allow comma within them 
        ///     (e.g., "North Korea" instead of "Korea, North").
        /// (c) Context of [] words may bring in other noise so location may just be a piece in a smaller 
        ///     whole (use the named location field to find the right piece in the context)
        /// (d) if for a story there is a location at offset X to its length, and another location at 
        ///     offset X+length+epsilon or less for small epsilon, then try to bring those two together.
        ///     This will help bring together cases like Toronto Canada where both are made separate 
        ///     location entries by spaCy.
        /// (e) some cities are so distinctive, they do not need a country qualifier (e.g., Montreal or 
        ///     Toronto) - for these, fill in the country via additional input data "hint" file.
        /// (f) Some "places" are schools or playing venues where the transcriber notes the city country
        ///     in square brackets in the form [...possible extra words... city, country] -- code such 
        ///     places as city, country.
        /// (g) Even before turning to the context in [] as in place [more context] the "place" may be 
        ///     parsed with extra text at its end and/or contain a place, country pair. Examples include 
        ///     Toronto, Canada] -- most often a mistake perhaps from Pass 3 and earlier keeping around "]"
        ///     at end and preserving the place, country pair.  Try to parse that first, looking only 
        ///     for "," as place-country separator.
        /// </remarks>
        static List<LocationEntity> ProcessEntriesForStory(List<LocationEntity> oneStoryEntries)
        {
            string presumedCountryName, presumedPlaceName;
            int resolvedCountryID, resolvedPlaceID;
            int updatedConfidence;
            int workVal;
            LocationEntity onePlaceEntry;
            const int adjacencyEpsilon = 4; // allow punctuation to gap place placeContainer by up to 4 more characters, e.g., Toronto -- Canada
            bool hasContext;

            // First pass: resolve each entry separately, using only its context as necessary.
            for (int i = 0; i < oneStoryEntries.Count; i++)
            {
                onePlaceEntry = oneStoryEntries[i];
                resolvedCountryID = 0;
                resolvedPlaceID = 0;
                updatedConfidence = onePlaceEntry.Confidence;
                if (onePlaceEntry.CountryCode == 0)
                {   // Not resolved yet -- try to do so here...

                    // First try: if this is a form that is a street or body of water without further 
                    // qualification, e.g., Jamaica Avenue, give up early rather than allow it to be 
                    // tagged as Jamaica the country.  If not a general location like Lake Jamaica or 
                    // Jamaica Avenue, continue onward with attempts for place-country qualification.
                    if (!IsGeneralLocation(oneStoryEntries[i].Text, oneStoryEntries[i].ContextualizedText))
                    {
                        // First try: parse a given place-comma-container place name into a resolved international place.
                        // The presence of a comma supercedes all other tests (so no international place name is "allowed" to have a comma).
                        workVal = onePlaceEntry.Text.IndexOf(",");
                        if (workVal > 0)
                        {
                            ParsePlaceNameAndCountryCandidates(onePlaceEntry.Text, out presumedPlaceName, out resolvedCountryID);

                            if (resolvedCountryID != 0)
                            {
                                if (PlaceFoundInCountry(presumedPlaceName, resolvedCountryID, out resolvedPlaceID))
                                { // note when PlaceFoundInCountry returns true, resolvedPlaceID is a non-zero value
                                    updatedConfidence += 2; // give bump of 2 for city/country resolving
                                }
                                else
                                {
                                    // One more hope: we have long place, something like "Montreal [ Expos, Canada ]" where
                                    // text before "[" may resolve fine to the country discovered within the latter [] context.
                                    workVal = onePlaceEntry.Text.IndexOf("[");
                                    if (workVal > 0)
                                    {
                                        presumedPlaceName = onePlaceEntry.Text.Substring(0, workVal).Trim();
                                        if (PlaceFoundInCountry(presumedPlaceName, resolvedCountryID, out resolvedPlaceID))
                                        { // note when PlaceFoundInCountry returns true, resolvedPlaceID is a non-zero value
                                            updatedConfidence += 2; // give bump of 2 for city/country resolving
                                        }
                                    }
                                    if (resolvedPlaceID == 0)
                                    {   // just use the discovered country rather than pushing more to 
                                        // located a place within the country
                                        // NOTE: resolvedPlaceID stays as 0 to indicate this imprecise 
                                        // location (only resolvedCountryID is nonzero).
                                        updatedConfidence += 1; // give bump of 1 for country resolving
                                    }
                                }
                            }
                        }
                        if (resolvedCountryID == 0)
                        {
                            hasContext = (onePlaceEntry.Text != onePlaceEntry.ContextualizedText);
                            if (hasContext)
                            { // Try to look up the country immediately after the given place using the context.  Do this by
                              // finding the place in the context, then looking at the immediate suffix word(s).  Country names can be multi-word, as can place names.
                                workVal = onePlaceEntry.ContextualizedText.IndexOf(onePlaceEntry.Text);
                                if (workVal >= 0 && (workVal + onePlaceEntry.Text.Length < onePlaceEntry.ContextualizedText.Length - 1))
                                    // have "place [place more-context]" type information, get to the "...more-context]" part
                                    presumedCountryName = onePlaceEntry.ContextualizedText.Substring(workVal + onePlaceEntry.Text.Length).Trim();
                                else
                                {
                                    // consider the final bracketed context [] as a candidate for a country name
                                    presumedCountryName = onePlaceEntry.ContextualizedText;
                                    workVal = presumedCountryName.LastIndexOf('[');
                                    if (workVal >= 0)
                                        presumedCountryName = presumedCountryName.Substring(workVal + 1);
                                    workVal = presumedCountryName.LastIndexOf(']');
                                    if (workVal >= 0)
                                        presumedCountryName = presumedCountryName.Substring(0, workVal);
                                }
                                if (countryInfo.ContainsKey(presumedCountryName))
                                    resolvedCountryID = countryInfo[presumedCountryName];

                                if (resolvedCountryID != 0)
                                {
                                    presumedPlaceName = ProperNameForPlace(onePlaceEntry.Text.Trim());
                                    if (PlaceFoundInCountry(presumedPlaceName, resolvedCountryID, out resolvedPlaceID))
                                    { // note when PlaceFoundInCountry returns true, resolvedPlaceID is a non-zero value
                                        updatedConfidence += 2; // give bump of 2 for city/country resolving
                                    }
                                    else // back out of the context setting the country
                                        resolvedCountryID = 0;
                                }
                                if (resolvedCountryID == 0)
                                {   // Do more work trying to parse out not just a country hint but a 
                                    // city, country hint within the context. Don't require that the 
                                    // place name be repeated in the context, i.e., start with just 
                                    // contextualizedPlaceName. This allows for the pattern of place 
                                    // [more description perhaps then city, country] or place
                                    // [more description perhaps then country]. Use that context to 
                                    // resolve this location to a specific city if possible, or a country
                                    // if not.  First, try to parse out an ending ...city, country] 
                                    // from onePlaceEntry.contextualizedPlaceName
                                    ParsePlaceNameAndCountryCandidates(onePlaceEntry.ContextualizedText, out presumedPlaceName, out resolvedCountryID);

                                    if (resolvedCountryID != 0)
                                    {   // Verify that place name is in this country from the context text
                                        if (PlaceFoundInCountry(presumedPlaceName, resolvedCountryID, out resolvedPlaceID))
                                        { // note when PlaceFoundInCountry returns true, resolvedPlaceID is a non-zero value
                                            updatedConfidence += 2; // give bump of 2 for city/country resolving
                                        }
                                        else
                                        {
                                            // One more hope: we have something like "Montreal [ Quebec, Canada]" where
                                            // original place name may resolve fine to the country discovered within the [] context.
                                            presumedPlaceName = onePlaceEntry.Text.Trim();
                                            if (PlaceFoundInCountry(presumedPlaceName, resolvedCountryID, out resolvedPlaceID))
                                            {   // note when PlaceFoundInCountry returns true, resolvedPlaceID is a non-zero value
                                                updatedConfidence += 2; // give bump of 2 for city/country resolving
                                            }
                                            else
                                            {
                                                // We only have a country, but that may be all we were given.
                                                // Use that only (just the container country)
                                                resolvedPlaceID = 0;
                                                // NOTE: resolvedPlaceID stays as 0 to indicate this 
                                                // imprecise location (only resolvedCountryID is nonzero).
                                                updatedConfidence += 1; // give bump of 1 for country resolving
                                            }
                                        }
                                    }
                                }
                            } // End of having context more than just the given place name
                            if (resolvedCountryID == 0)
                            {
                                presumedPlaceName = onePlaceEntry.Text.Trim();
                                // Peek ahead.  If the next entry resolves to just a country and its
                                // offset means it is adjacent to this place name, then have that be the
                                // country container for this location, if it resolves from our tables.
                                if (i < oneStoryEntries.Count - 1)
                                {
                                    if (oneStoryEntries[i + 1].StartOffset <= adjacencyEpsilon + onePlaceEntry.StartOffset + onePlaceEntry.Length)
                                    {
                                        // It's close enough to be considered two place names for the 
                                        // same location, e.g., Toronto and Canada. If the latter place 
                                        // resolves to a country, and the former resolves to a place within
                                        // that country, then do just that:
                                        // make both entries be the same place.
                                        presumedCountryName = oneStoryEntries[i + 1].Text;
                                        if (countryInfo.ContainsKey(presumedCountryName))
                                            resolvedCountryID = countryInfo[presumedCountryName];

                                        if (resolvedCountryID != 0)
                                        {
                                            // OK, we have equivalent of "place, country" - see if place resolves inside that country
                                            if (PlaceFoundInCountry(presumedPlaceName, resolvedCountryID, out resolvedPlaceID))
                                            {   // note when PlaceFoundInCountry returns true, resolvedPlaceID is a non-zero value
                                                updatedConfidence += 2; // give bump of 2 for city/country resolving
                                                                        // Only at this point, when we confirm "place, country" - do we mark upcoming entry as "finished" in a way
                                                                        // by duplicating what we have found here
                                                oneStoryEntries[i + 1].CountryCode = resolvedCountryID;
                                                oneStoryEntries[i + 1].PlaceID = resolvedPlaceID;
                                                oneStoryEntries[i + 1].Confidence += 2; // give bump of 2 for city/country resolving
                                            }
                                            else // back away from using next entry for any help
                                                resolvedCountryID = 0;
                                        }
                                    }
                                }
                            }

                            if (resolvedCountryID == 0)
                            {
                                // No further clues in this entry's context. If a "city-needing-no-hinting", 
                                // like "Montreal", great!
                                presumedPlaceName = onePlaceEntry.Text.Trim();
                                if (cityHintingInfo.ContainsKey(presumedPlaceName))
                                {
                                    resolvedCountryID = cityHintingInfo[presumedPlaceName].countryNumeric;
                                    resolvedPlaceID = cityHintingInfo[presumedPlaceName].THMDACityID;
                                    updatedConfidence += 2; // give bump of 2 for city/country resolving
                                }
                                else
                                {
                                    if (countryInfo.ContainsKey(presumedPlaceName))
                                        resolvedCountryID = countryInfo[presumedPlaceName];
                                    if (resolvedCountryID != 0)
                                    {
                                        resolvedPlaceID = 0; // indicates a match just to the country
                                        updatedConfidence += 1; // give bump of 1 for country resolving
                                    }
                                }
                            }
                        }
                    }

                    // Move updates of confidence and name IDs back into the entry
                    oneStoryEntries[i].PlaceID = resolvedPlaceID;
                    oneStoryEntries[i].CountryCode = resolvedCountryID;
                    oneStoryEntries[i].Confidence = updatedConfidence;
                }
                // else we already cleaned up oneStoryEntries[i] back when it was oneStoryEntries[i + 1] 
                // during a look-ahead operation
            }
            // Second pass: for all the names represented in oneStoryEntries, if there are times when 
            // some entries have no countryID but others do, then turn all the unmarked unresolved ones
            // into resolved ones.
            int indexForGivenPlaceName;
            for (int i = 0; i < oneStoryEntries.Count; i++)
            {
                if (oneStoryEntries[i].CountryCode == 0)
                {
                    // Check out other entries and see if any of them have the same place.
                    indexForGivenPlaceName = SearchForPlaceName(oneStoryEntries[i].Text, i, oneStoryEntries);
                    if (indexForGivenPlaceName != -1)
                    {   // Resolve to this first discovered one with at least country info and perhaps place info too.
                        oneStoryEntries[i].PlaceID = oneStoryEntries[indexForGivenPlaceName].PlaceID;
                        oneStoryEntries[i].CountryCode = oneStoryEntries[indexForGivenPlaceName].CountryCode;
                        oneStoryEntries[i].Confidence = oneStoryEntries[indexForGivenPlaceName].Confidence;
                    }
                }
            }

            // NOTE: Returning original data structure, which is actually unecessary, however it 
            // preserves the semantics of the prior Resolver classes. Just know that the original
            // list has been modified.
            return oneStoryEntries;
        }

        static bool IsGeneralLocation(string givenText, string givenContext)
        {
            // For a street name like Jamaica Avenue, we may have Jamaica [Avenue], Jamaica [Jamaica Avenue]
            // or Jamaica Avenue [Jamaica Avenue] and of course Avenue may be Ave., Boulevard, Blvd, Blvd., 
            // Street, St., Road, Lane, Lake, River....
            // If that is all the context we have, return true, so that we do not qualify this general 
            // location to a city or country.
            bool isGeneralLocation = false;
            string myWorkContext = givenContext;
            if (myWorkContext == null || myWorkContext.Trim().Length == 0)
                myWorkContext = givenText;
            string workVal = myWorkContext.Replace("]", "").TrimEnd(); // remove ] as needed (may not be there)
            if (workVal.Length > 0 && workVal[workVal.Length - 1] == '.')
            {
                // trim off ending '.' to simplify matching to trailing Ave. or Ave, Blvd. or Blvd, etc.
                workVal = workVal.Substring(0, workVal.Length - 1); 
            }

            char[] markerForLastContextWord = { '[', ' ' }; // get to final word
            int lastWordStart = workVal.LastIndexOfAny(markerForLastContextWord);
            if (lastWordStart >= 0)
                lastWordStart += 1;
            else
                lastWordStart = 0;
            string finalWord = workVal.Substring(lastWordStart);
            if (finalWord == "Avenue" || finalWord == "Ave" || finalWord == "Boulevard" || finalWord == "Blvd" ||
                finalWord == "Street" || finalWord == "St" || finalWord == "Road" || finalWord == "Lane" || finalWord == "Lake" || finalWord == "River")
                isGeneralLocation = true;
            else if (givenText.StartsWith("Lake "))
            {
                workVal = myWorkContext.Replace("[", "").Replace("]", "").Trim();
                if (workVal.StartsWith("Lake ") && workVal.Length <= givenText.Length)
                { 
                    // No other potential text for a Lake entry like Lake Jamaica; give up as too 
                    // "general" since big lakes won't qualify to a single state (leaving open the 
                    // possibility for context to provide a qualifier like Lake Jamaica [New York] 
                    // which will be taken as long as there a nonempty context hint).
                    isGeneralLocation = true;
                }
            }

            return isGeneralLocation;
        }

        /// <summary>
        /// Return first index within list where the place name is found AND it has a nonzero place ID.
        /// If that is not possible, return the first time the place name is found with a nonzero 
        /// country ID.  Else, return -1.
        /// </summary>
        /// <param name="givenPlaceName">place name to check</param>
        /// <param name="givenIndex">index of place name in entry list</param>
        /// <param name="oneStoryEntries">place entries</param>
        /// <returns></returns>
        static int SearchForPlaceName(string givenPlaceName, int givenIndex, List<LocationEntity> oneStoryEntries)
        {
            int indexForPlace = -1;
            int indexForCountryOnlyDetail = -1;

            for (int j = 0; j < oneStoryEntries.Count; j++)
            {
                if (j != givenIndex && oneStoryEntries[j].Text == givenPlaceName)
                {
                    if (oneStoryEntries[j].PlaceID != 0)
                    {
                        indexForPlace = j;
                        break; // no need to iterate further
                    }
                    else if (indexForCountryOnlyDetail == -1 && oneStoryEntries[j].CountryCode != 0)
                        indexForCountryOnlyDetail = j; // might have to resort to just a country-only match, but keep iterating
                }
            }
            if (indexForPlace == -1)
                indexForPlace = indexForCountryOnlyDetail; // make use of a country-only match (if one happened) - better than nothing
            return indexForPlace;
        }

        /// <summary>
        /// Returns true IFF given place is in the given country
        /// </summary>
        /// <param name="givenPlaceName">place name to check</param>
        /// <param name="givenCountryID">ID of country container</param>
        /// <param name="qualifiedPlaceID">
        /// set to the place ID for this name in this country if true is returned, 0 otherwise
        /// </param>
        /// <returns></returns>
        static Boolean PlaceFoundInCountry(string givenPlaceName, int givenCountryID, out int qualifiedPlaceID)
        {
            bool isGoodPlaceInGivenCountry = false;
            qualifiedPlaceID = 0;
            List<PlaceNameInfo> placesWithThisName;

            if (placeInfo.ContainsKey(givenPlaceName))
            {
                placesWithThisName = placeInfo[givenPlaceName];
                foreach (PlaceNameInfo onePlace in placesWithThisName)
                {
                    if (onePlace.countryNumeric == givenCountryID)
                    {
                        qualifiedPlaceID = onePlace.THMDACityID;
                        isGoodPlaceInGivenCountry = true;
                        break;
                    }
                }
            }
            return isGoodPlaceInGivenCountry;
        }

        /// <summary>
        /// Parse out a city/place name candidate and a country candidate from strings of form city, 
        /// country and city [country].
        /// </summary>
        /// <param name="givenText">text with hoped-for form of city, country or city [country] plus extras</param>
        /// <param name="cityCandidate">candidate place name in country, "" if not found</param>
        /// <param name="countryCode">candidate country code, 0 if not found</param>
        static void ParsePlaceNameAndCountryCandidates(string givenText, out string cityCandidate, out int countryCode)
        {
            int workVal;
            string presumedCountryName = "";
            string presumedPlaceName = "";

            workVal = givenText.LastIndexOf(","); // look first for city COMMA country
            if (workVal >= 2 && (workVal < givenText.Length - 2))
            {  // Enough characters before , for city and after , to be a two-letter country name or 
               // longer.  Go backward from , to reclaim a "city" candidate.
                presumedCountryName = givenText.Substring(workVal + 1).Trim();
                if (presumedCountryName.EndsWith("]"))
                    presumedCountryName = presumedCountryName.Substring(0, presumedCountryName.Length - 1).Trim();
                presumedPlaceName = TrimPlaceName(givenText.Substring(0, workVal));
            }
            else
            { // Try the city [country] pattern
                workVal = givenText.LastIndexOf("[");
                if (workVal >= 2 && (workVal < givenText.Length - 2))
                {  // Enough characters before [ for city and after [ to be a two-letter country name or 
                   // longer.  Go backward from [ to reclaim a "city" candidate.
                    presumedCountryName = givenText.Substring(workVal + 1).Trim();
                    if (presumedCountryName.EndsWith("]"))
                        presumedCountryName = presumedCountryName.Substring(0, presumedCountryName.Length - 1).Trim();
                    presumedPlaceName = TrimPlaceName(givenText.Substring(0, workVal));
                }
            }
            cityCandidate = presumedPlaceName;
            countryCode = 0;
            if (countryInfo.ContainsKey(presumedCountryName))
                countryCode = countryInfo[presumedCountryName];
        }

        /// <summary>
        /// If given candidate has prefix text ending with any of "[sic ", "[sic. ", "[", ",", ";" ":" 
        /// strip it off (can't do "." as that is in some place names).
        /// </summary>
        /// <param name="givenPlaceCandidate">place candidate (may be multiple words like Kansas City)</param>
        /// <returns>trimmed name removing prefix text marked by punctuation or "sic "</returns>
        static string TrimPlaceName(string givenPlaceCandidate)
        {
            string presumedPlaceName = givenPlaceCandidate.Trim();
            int cityNameStart = presumedPlaceName.LastIndexOf("[sic. ");
            if (cityNameStart >= 0)
                presumedPlaceName = presumedPlaceName.Substring(cityNameStart + 6); // move past "[sic. "
            else
            {
                cityNameStart = presumedPlaceName.LastIndexOf("[sic ");
                if (cityNameStart >= 0)
                    presumedPlaceName = presumedPlaceName.Substring(cityNameStart + 5); // move past "[sic "
                else
                {
                    cityNameStart = presumedPlaceName.LastIndexOf(" sic ");
                    if (cityNameStart >= 0)
                        presumedPlaceName = presumedPlaceName.Substring(cityNameStart + 5); // move past " sic "
                }
            }
            // Consider punctuation prefixes as well to clean up the "sloppy" context from named entity 
            // extraction and extraneous square-bracketed notes, e.g., 
            // [St. Joseph Missionary Baptist Church, Birmingham, Alabama]
            cityNameStart = presumedPlaceName.LastIndexOf("[");
            if (cityNameStart >= 0)
                presumedPlaceName = presumedPlaceName.Substring(cityNameStart + 1);
            cityNameStart = presumedPlaceName.LastIndexOf(":");
            if (cityNameStart >= 0)
                presumedPlaceName = presumedPlaceName.Substring(cityNameStart + 1);
            cityNameStart = presumedPlaceName.LastIndexOf(";");
            if (cityNameStart >= 0)
                presumedPlaceName = presumedPlaceName.Substring(cityNameStart + 1);
            cityNameStart = presumedPlaceName.LastIndexOf(",");
            if (cityNameStart >= 0)
                presumedPlaceName = presumedPlaceName.Substring(cityNameStart + 1);
            return presumedPlaceName.Trim(); // remove any leading whitespace before returning the value
        }

        /// <summary>
        /// Check if given string is a U.S. state name.
        /// </summary>
        /// <param name="textToCheck">text to check</param>
        /// <returns>true iff text matches a U.S. state name (case-insensitive)</returns>
        static bool MatchToUSState(string textToCheck)
        {
            // !!!TBD!!! Strong CANDIDATE TO DELETE ENTIRELY once input file is locked in place and noted as having no U.S. state names.
            // FYI, this removes Florida and Montana from "common" international location lists.
            bool matchesStateName = false;
            if (textToCheck != null)
            {
                string workString = textToCheck.ToLower();
                string[] USStateNames = { "alaska", "alabama", "arkansas", "arizona", "california", "colorado", "connecticut", "district of columbia", "delaware", "florida",
                         "georgia", "hawaii", "iowa", "idaho", "illinois", "indiana", "kansas", "kentucky", "louisiana", "massachusetts",
                         "maryland", "maine", "michigan", "minnesota", "missouri", "mississippi", "montana", "north carolina", "north dakota", "nebraska",
                         "new hampshire", "new jersey", "new mexico", "nevada", "new york", "ohio", "oklahoma", "oregon", "pennsylvania", "rhode island",
                         "south carolina", "south dakota", "tennessee", "texas", "utah", "virginia", "vermont", "washington", "wisconsin", "west virginia", "wyoming" };

                matchesStateName = USStateNames.Contains(workString);
            }
            return matchesStateName;
        }

        /// <summary>
        /// Adjust given name to a "proper" name as needed, typically just returning the given name.
        /// </summary>
        /// <param name="givenCandidate">candidate</param>
        /// <returns>name in a form more suitable for lookup against the hinting and lookup data tables</returns>
        static string ProperNameForPlace(string givenCandidate)
        {
            string retVal = givenCandidate.Replace('[', ' ').Replace(']', ' ').Trim();
            string lowerRetVal = retVal.ToLower();
            if (lowerRetVal.StartsWith("the city of "))
                retVal = retVal.Substring(12); // remove verbose preface that may be in various upper/lower case mixes
            else if (lowerRetVal.StartsWith("city of "))
                retVal = retVal.Substring(8); // remove verbose preface that may be in various upper/lower case mixes
            else if (lowerRetVal.StartsWith("the capital city of "))
                retVal = retVal.Substring(20); // remove verbose preface that may be in various upper/lower case mixes
            else if (lowerRetVal.StartsWith("capital city of "))
                retVal = retVal.Substring(16); // remove verbose preface that may be in various upper/lower case mixes

            if (givenCandidate.EndsWith(" St.") && retVal.Length > 4)
                retVal = retVal.Substring(0, retVal.Length - 4) + " Street";
            else if (givenCandidate.Contains("St."))
                // lookup file has Saint Kitts throughout, i.e., no "St." instead of "Saint"
                retVal = givenCandidate.Replace("St.", "Saint");

            return retVal;
        }

        /// <summary>
        /// Use a data input file to fill out countryInfo.
        /// </summary>
        static Dictionary<string, int> LoadCountryInfo()
        {
            char[] lineSeparatorChars = { '\t' };
            string fullCountryInfoFileName = Path.Combine(dataPath, @"CountryNameListWithCodes.txt");

            Logger.Write("LoadCountryInfo: Loading data from {0}", fullCountryInfoFileName);
            Dictionary<string, int> countryInfo = new Dictionary<string, int>();

            // 4	Afghanistan
            // 900  Akrotiri
            // 8    Albania
            // 12   Algeria
            // etc., tab-delimited

            using (var reader = new StreamReader(fullCountryInfoFileName))
            { 
                if (reader.ReadLine() == null)
                {
                    throw new Exception($"LoadCountryInfo: Required input file is missing or empty: {fullCountryInfoFileName}");
                }
                // else successfully read away the opening header line in the input file

                string oneInputLine;
                while ((oneInputLine = reader.ReadLine()) != null)
                {
                    var linePieces = oneInputLine.Split(lineSeparatorChars);
                    if (linePieces.Length == 2 && int.TryParse(linePieces[0], out int countryID) && !countryInfo.ContainsKey(linePieces[1]))
                    {
                        countryInfo.Add(linePieces[1], countryID);
                    }
                    else
                    {
                        Logger.Warning("LoadCountryInfo: Ignoring this country name variant as it is already in place: {0}", oneInputLine);
                    }
                }            
            }

            Logger.Write("LoadCountryInfo: {0} country names with codes loaded", countryInfo.Count);
            return countryInfo;
        }

        /// <summary>
        /// Use a data input file to fill out cityHintingInfo.
        /// </summary>
        static Dictionary<string, City_Hinting_Row> LoadCityHintingInfo()
        {            
            char[] lineSeparatorChars = { '\t' };
            string fullHintFileName = Path.Combine(dataPath, @"DefaultCountriesForSomeLocations.txt");

            Logger.Write("");
            Dictionary<string, City_Hinting_Row> cityHintingInfo = new Dictionary<string, City_Hinting_Row>();

            // Montreal	4564    124
            // etc., tab-delimited

            using (var reader = new StreamReader(fullHintFileName))
            {
                if (reader.ReadLine() == null)
                {
                    throw new Exception($"LoadCityHintingInfo: Required input file missing or empty: {fullHintFileName}");
                }
                // else successfully read away the opening header line in the input file (stating name city-id country-id as order of data)

                string oneInputLine;
                while ((oneInputLine = reader.ReadLine()) != null)
                {
                    var linePieces = oneInputLine.Split(lineSeparatorChars);
                    if (linePieces.Length == 3
                        && int.TryParse(linePieces[1], out int cityID)
                        && int.TryParse(linePieces[2], out int countryID))
                    {
                        cityHintingInfo.Add(linePieces[0], new City_Hinting_Row(countryID, cityID));
                    }
                    else
                    {
                        Logger.Warning("LoadCityHintingInfo: Ignoring this city hint: {0}", oneInputLine);
                    }
                }
            }

            Logger.Write("LoadCityHintingInfo: {0} city hints loaded.", cityHintingInfo.Count);
            return cityHintingInfo;
        }

        /// <summary>
        /// Use a data input file to fill out the lookup dictionary.
        /// </summary>
        /// <remarks>Data file format is tab-separated place-name, country-number-code, and THMDA-city-ID
        /// </remarks>
        static Dictionary<string, List<PlaceNameInfo>> LoadPlaceNameDictionary()
        {
            char[] lineSeparatorChars = { '\t' };
            string fullPlacesFileName = Path.Combine(dataPath, @"WorldCitiesWithCodes.txt");

            Logger.Write("LoadPlaceNameDictionary: Loading data from {0}", fullPlacesFileName);
            var placeInfo = new Dictionary<string, List<PlaceNameInfo>>();

            // 1418109	Adak	2
            // etc., tab-delimited

            using (var reader = new StreamReader(fullPlacesFileName))
            {
                if (reader.ReadLine() == null)
                {
                    throw new Exception($"LoadPlaceNameDictionary: Required input file is missing or empty: {fullPlacesFileName}");
                }
                // else successfully read away the opening header line in the input file

                string oneInputLine;
                while ((oneInputLine = reader.ReadLine()) != null)
                {
                    var linePieces = oneInputLine.Split(lineSeparatorChars);
                    if (linePieces.Length == 3 
                        && int.TryParse(linePieces[1], out int countryID)
                        && int.TryParse(linePieces[2], out int placeID))
                    {
                        if (MatchToUSState(linePieces[0]))
                        {
                            Logger.Warning("LoadPlaceNameDictionary: Ignoring U.S. state match: {0}", linePieces[0]);
                        }
                        else
                        {
                            if (placeInfo.ContainsKey(linePieces[0]))
                            { 
                                // Add this specific place details to the list already indexed by the place name.
                                placeInfo[linePieces[0]].Add(new PlaceNameInfo(countryID, placeID));
                            }
                            else 
                            {
                                // Make new lookup entry for this name.
                                placeInfo.Add(linePieces[0], new List<PlaceNameInfo>());
                                placeInfo[linePieces[0]].Add(new PlaceNameInfo(countryID, placeID));
                            }
                        }
                    }
                    else
                    {
                        Logger.Warning("LoadPlaceNameDictionary: Ignoring this place with bad input formatting: {0}", oneInputLine); 
                    }
                }
            }

            Logger.Write("LoadPlaceNameDictionary: {0} places loaded.");
            return placeInfo;
        }
    }
}

