using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using InformediaCORE.Common;
using InformediaCORE.Common.Config;

/**
 * NOTES: (CHRISTEL)
 * Pass 4 works only with locations and currently only resolves to Populated Place, Military, and some 
 * School (colleges or universities) in the U.S., or the US states plus DC. As of July 2019 we have 
 * 166,672 Populated Place entities for disambiguating place names. As of Feb. 2020, we brought in 
 * Military (2788 entities) and some School (colleges or universities) entities, bringing up the count 
 * to 203,689 entities for disambiguatings place names. Of course this "truth" table from USGS (USGS 
 * file date of 01012020) could be trimmed/tweaked further, but we keep it as is for Feb. 2020 and 
 * beyond. 
 * 
 * On a match (success), the program bumps up location reference confidences, and adds in canonical 
 * named entity ID (from USGS table). This pass is ONLY for Populated Place, Military, and some School 
 * in U.S. (not country name yet, nor parks, locales, hospitals, etc.). 
 * 
 * NOTE WELL: Pass 3 input file MUST be sorted on collection ID, then story ID, then start offset 
 * ascending to keep stories together and entries in offset order. 
 * 
 * Heuristics for Pass 4 are noted in comments for ProcessEntriesForStory(). 
 * 
 * The attempt is made to work across all the locations, not just those with highest confidence of 3 
 * from "Pass 3." 
 * 
 * 
 * 
 * Order of work:
 * (1) Get all locations for a story (all confidences for now....).
 * (2) For each, try to make into a US place/state pair (using ProcessEntriesForStory heuristics).
 * (3) In outputting entries, add in new column for USGS ID as well as USGS state ID for the container 
 *     state (if applicable) and update confidence as appropriate.  Also add in the US country code (840).
 *     Some jump +1 (state) or +2 (state and place); others may jump more (formerly low confidence 
 *     that matches another entry in this same story so bump up to all be the same highest confidence in 
 * 	that story).
 * 
 * NOTE: this does NOT yet consider hints/context from higher level text such as tape abstracts, 
 * session abstracts, biography abstracts, biography metadata like birth city. This only considers the 
 * story transcript processing via what is already in our "Pass 3" output file. 
 * 
 * Feb. 2020 Update: Locations like Mississippi River, Lake Michigan, Wyoming Avenue mistakenly tagged 
 * to the state. Add in place/context check. If place [place context] or place [Lake place] (e.g., Lake 
 * Michigan) or place context or context place (just Lake place) has context as one of {Lake, River, 
 * Avenue, Street, Road, Boulevard, Ave., Blvd., Blvd} with only { Lake } allowed before or after, 
 * others must be suffixes, then do NOT tag that as the given place. Also, trimmed away some WA entries 
 * that either should have been DC or are too ambiguous (sometimes a person named Washington!), via 
 * ConsiderWashingtonAsDCorWA() heuristics. Finally, resolve "the city of Detroit" to Detroit and 
 * "[Chicago]" to Chicago via better clean-up of place name in ProperUSGSNameForPlace(). 
 */

namespace InformediaCORE.Processing.NamedEntities
{
    class CityHint
    {
        // input data of the tab-separated format: Akron OH	39	1064305
        // How it's used: dictionary entry where the name (e.g., 'Akron') is used as the key
        // Remaining for value with this key: state-name, state-ID, USGS-ID (of which we drop out 
        // the state-name as redundant)
        public int StateNumeric { get; set; }
        public int USGS_ID { get; set; }

        public CityHint(int stateCode, int usgsID)
        {
            StateNumeric = stateCode;
            USGS_ID = usgsID;
        }
    }

    class US_StateInfo
    {
        // Typical: 1779780 State of Connecticut Civil   CT  9   Connecticut, Conn.
        // What's kept: USGS ID (1779780), two-letter form (CT), list of strings as likely pseudonyms 
        // (Connecticut, Conn., etc.)
        // How it's used: dictionary entry where the USGS state ID (9 for CT) is used as the key

        public int USGS_ID { get; set; }
        public string StateAlpha { get; set; }
        public string[] NameOptions { get; set; }

        public const int DC_ID = 11; // entry for District of Columbia
        public const int WA_ID = 53; // entry for state of Washington (used to correct skewing of data from DC to WA on "Washington")

        public US_StateInfo(int usgsID, string stateAlpha, string[] nameOptions)
        {
            USGS_ID = usgsID;
            StateAlpha = stateAlpha;
            NameOptions = nameOptions;
        }
    }

    /// <summary>
    /// Holds values from "Pass 3" along with an extra slot for USGS place ID and USGS state ID (may be 0 
    /// for not applicable).
    /// 
    /// Input data has form: 
    /// collection-ID, story-ID, name, contextualized-name, startoffset, length, spaCy-type, "Loc", and confidence.
    /// 
    /// We do not save the "Loc" overall type indicating Location - it is assumed by the nature of this 
    /// "Pass 4 for Locations" program.
    /// </summary>
    public class LocationEntity : NamedEntity
    {
        /// <summary>
        /// The country code for the United States from 3-digit country codes list is 840
        /// </summary>
        private const int US_COUNTRY_CODE = 840;

        /// <summary>
        /// ISO 3166-1 standard county code
        /// </summary>
        public int CountryCode { get; set; } = US_COUNTRY_CODE;

        /// <summary>
        /// USGS State Code
        /// </summary>
        public int StateCode { get; set; } = 0;

        /// <summary>
        /// USGS City/Region Place Code
        /// </summary>
        public int PlaceID { get; set; } = 0;
        public int Count { get; set; } = 1;

        public LocationEntity() { }

        public LocationEntity(NamedEntity namedEntity)
        {
            Text = namedEntity.Text;
            ContextualizedText = namedEntity.ContextualizedText;
            StartOffset = namedEntity.StartOffset;
            Length = namedEntity.Length;
            SpacyType = namedEntity.SpacyType;
            Confidence = namedEntity.Confidence;
        }
    }

    /// <summary>
    /// Take as input a US 51 states+DC (actually, coded in here as lookup dictionary) and a list of 
    /// place names that have a default state container (input file), and a list of USGS ids and place 
    /// names with their state containers (input file), plus the locations "Pass 3" output.  Update 
    /// confidences and add in USGS ID where possible for these locations, with the "Pass 4" output 
    /// disambiguating hopefully most of the US locations.
    /// </summary>
    /// <remarks>
    /// Knowledge of how USGS lists a canonical entry is coded in here for the most common cities, 
    /// e.g., "Los Angeles" and "Saint Louis" vs. LA and St. Louis.  
    /// "New York" without additional NY/New York or "City" is tagged to the state, "New York NY" or 
    /// "New York City" to the city.
    /// 
    /// The input from Pass 3 has columns from 9 fields:
    /// collection-ID, story-ID, name, contextualized-name, startoffset, length, spaCy-type, "Loc", and confidence.
    /// (Instead of entity-type, "Loc" is listed to denote eighth field always "Loc" for location.), e.g.,
    /// 1	8190	South	South	411	5	LOC	Loc	3
    /// </remarks>
    public static class DomesticLocationResolver
    {

        // NOTE: important data files within dataPath, each with a header line describing fields to start 
        // the file before data lines commence:
        // USGS_Places_Table.txt, tab-delimited rows of USGS ID, Name, State-ID (one header line to start)
        // DefaultStatesForSomeLocations.txt, tab-delimited rows of Name, State, State-ID, USGS-ID (one header line to start)
        static readonly string dataPath = Settings.Current.EntityResolutionTask.DataPath;

        static readonly Dictionary<int, US_StateInfo> StateInfo = InitializeStateDictionary();
        static readonly Dictionary<string, CityHint> CityHintingInfo = LoadCityHintingInfo();

        /// <summary>
        /// Each state (plus DC, so 51) has its list of cities/places.  That list will be a dictionary 
        /// with key == place name, value == USGS ID.
        /// </summary>
        static readonly Dictionary<int, Dictionary<string, int>> PlacesInState = LoadPlacesInStates(StateInfo);

        /// <summary>
        /// Resolve possible domestic locations within the given list of named entities.
        /// </summary>
        /// <param name="pass3Entities">List of pass 3 named entities.</param>
        /// <param name="unresolvedLocations">A list of locations which couldn't be resolved.</param>
        /// <returns>A list of resolved entities as a tuple.</returns>
        /// <remarks>This logic comes from the Main method of the original source code.</remarks>
        public static List<LocationEntity> Resolve(List<NamedEntity> pass3Entities, out List<LocationEntity> unresolvedLocations)
        {
            List<LocationEntity> processedStoryEntities = ProcessEntriesForStory(pass3Entities);

            // Output each entry into one of two buckets: resolved or unresolved:
            unresolvedLocations = new List<LocationEntity>();

            // NOTE: Resolved location list is reduced to unique places with counts, and highest occuring confidence.
            // If a location is marked up 3+ times, boost its confidence again by +1.
            Dictionary<int, LocationEntity> resolvedLocations = new Dictionary<int, LocationEntity>();

            foreach (LocationEntity entity in processedStoryEntities)
            {
                if (entity.StateCode != 0)
                { 
                    // Keep track of each unique non-empty place mentioned in the story.  If already listed, update its count 
                    // and keep the maximum of the confidences.
                    if (resolvedLocations.ContainsKey(entity.PlaceID))
                    {
                        // Already here.
                        resolvedLocations[entity.PlaceID].Count++;
                        resolvedLocations[entity.PlaceID].Confidence = Math.Max(entity.Confidence, resolvedLocations[entity.PlaceID].Confidence);
                        // NOTE: this indeed is always true: oneEntry.StateNumeric == currentThinnedData[oneEntry.PlaceID].StateNumeric
                    }
                    else
                    {
                        resolvedLocations.Add(entity.PlaceID, entity);
                    }
                }
                else
                {
                    // Put into unresolved bucket, using "0" in the country code to reinforce the 0 from 
                    // place and state that this is unresolved
                    entity.PlaceID = entity.StateCode = entity.CountryCode = 0;
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
        /// Converts the USGS numeric id to the official two-character state abbreviation.
        /// </summary>
        /// <param name="stateCode">USGS state identifier.</param>
        /// <returns>Official two character state abbreviation on success; "unknown" otherwise.</returns>
        public static string CodeToAlpha(int stateCode)
        {
            if (StateInfo.ContainsKey(stateCode))
            {
                return StateInfo[stateCode].StateAlpha;
            }
            else
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Apply heuristics and various helper data to resolve U.S. place names to their USGS place ID
        /// and container states (if in a US state or DC). If a location resolves, update its confidence.
        /// </summary>
        /// <param name="oneStoryEntries">entries within story, in startOffset ascending order</param>
        /// <param name="keptEntries">
        /// list of overall kept entries with updated confidences and USGS place and state IDs
        /// </param>
        /// <remarks>
        /// If within the same story a name resolves, resolve that name across all mentions, e.g., if in
        /// a story there is Buffalo NY and later just Buffalo, make both be the Buffalo NY city entry.
        /// 
        /// If context has form of place US-state then use that to disambiguate.  Parsing is tricky
        /// because:
        /// 
        /// (a) US.-state referred to in many ways: CA, Calif., California, etc. 
        ///     Capture anticipated ways and see if other ways are missed by looking at 
        ///     "left on the table" data.
        /// (b) Both place (e.g., St.Louis or Saint Louis) and state (e.g., North Carolina) can be 
        ///     multi-word. If comma is there, make use of that to help break into 2.
        /// (c) context of [] words may bring in other noise so location may just be a piece in a 
        ///     smaller whole (use the named location field to find the right piece in the context)
        /// (d) if for a story there is a location at offset X to its length, and another location at 
        ///     offset X+length+epsilon or less for small epsilon, then try to bring those two together.
        ///     This will help bring together cases like Buffalo NY where both are made separate location
        ///     entries by spaCy.
        /// (e) Some cities have common pseudodyms like "LA" for Los Angeles, "Philly" or "Phila." for 
        ///     Philadelphia, etc.  Expand out to "proper" form as needed (via hard-coded data in this program).
        /// (f) some cities are so distinctive, they do not need a state qualifier (e.g., New Orleans or 
        ///     Chicago or Los Angeles) - for these, fill in the state via additional input data "hint" file.
        ///     This list of default states for cities can be updated as needed based on the "left on the table" data.
        /// (g) Some "places" are counties or schools or playing venues where the transcriber notes the city
        ///     state in square brackets in the form [...possible extra words... city, state] -- code such places
        ///     as city, state (such as PS. 140 [P.S. X140 The Eagle School, Bronx, New York]
        ///     or Wrigley Field [Chicago, Illinois].
        /// (h) Even before turning to the context in [] as in place [more context] the "place" may be parsed 
        ///     with extra text at its end and/or contain a place, state pair.
        ///     Examples include Iowa City, Iowa] and Tuscaloosa, Alabama] -- most often a mistake perhaps from 
        ///     Pass 3 and earlier keeping around "]" at end and preserving the place, state pair.  Try to parse 
        ///     that first, looking only for "," as place-state separator.
        /// </remarks>
        static List<LocationEntity> ProcessEntriesForStory(List <NamedEntity> namedEntities)
        {
            // Filter list down to locations and convert type
            List<LocationEntity> locCandidates = namedEntities
                .Where(x => x.Type == EntityType.Loc)
                .Select(x => new LocationEntity(x))
                .ToList();

            string presumedStateName, presumedPlaceName;
            int resolvedStateNumeric, resolvedPlaceNumeric;
            int updatedConfidence;
            int workVal;
            LocationEntity onePlaceEntry;
            const int adjacencyEpsilon = 4; // allow punctuation to gap place placeContainer by up to 4 more characters, e.g., Buffalo -- NY
            bool hasContext;

            // First pass: resolve each entry separately, using only its context as necessary.
            for (int i = 0; i < locCandidates.Count; i++)
            {
                onePlaceEntry = locCandidates[i];
                resolvedStateNumeric = 0;
                resolvedPlaceNumeric = 0;
                updatedConfidence = onePlaceEntry.Confidence;
                if (onePlaceEntry.StateCode == 0)
                { // Not resolved yet -- try to do so here...

                    // First try: if this is a form that is a street or body of water without further 
                    // qualification, e.g., Wyoming Avenue, give up early rather than allow it to be 
                    // tagged as Wyoming the state.  If not a general location like Lake Michigan or 
                    // Michigan Avenue, continue onward with attempts for place-state qualification.
                    if (!IsGeneralLocation(locCandidates[i].Text, locCandidates[i].ContextualizedText))
                    {
                        // Next: parse a given place-comma-container place name into a resolved U.S. place.
                        // The presence of a comma supercedes all other tests (so no U.S. place name is "allowed" to have a comma).
                        workVal = onePlaceEntry.Text.IndexOf(",");
                        if (workVal > 0)
                        {
                            ParsePlaceNameAndStateCandidates(onePlaceEntry.Text, out presumedPlaceName, out resolvedStateNumeric);

                            if (resolvedStateNumeric != 0)
                            {
                                if (PlacesInState[resolvedStateNumeric].ContainsKey(presumedPlaceName))
                                {
                                    resolvedPlaceNumeric = PlacesInState[resolvedStateNumeric][presumedPlaceName];
                                    updatedConfidence += 2; // give bump of 2 for city/state resolving
                                }
                                else
                                {
                                    // One more hope: we have long place, something like "Annapolis [ U.S. Naval Academy , Maryland ]" where
                                    // text before "[" may resolve fine to the state discovered within the latter [] context.
                                    workVal = onePlaceEntry.Text.IndexOf("[");
                                    if (workVal > 0)
                                    {
                                        presumedPlaceName = onePlaceEntry.Text.Substring(0, workVal).Trim();
                                        presumedPlaceName = ProperUSGSNameForPlace(presumedPlaceName);
                                        if (PlacesInState[resolvedStateNumeric].ContainsKey(presumedPlaceName))
                                        { // This is a keeper: use the city, state information in the context to resolve this place location.
                                            resolvedPlaceNumeric = PlacesInState[resolvedStateNumeric][presumedPlaceName];
                                            updatedConfidence += 2; // give bump of 2 for city/state resolving
                                        }
                                    }
                                    if (resolvedPlaceNumeric == 0)
                                    { // just use the discovered state rather than pushing more to located a place within the state

                                        if (resolvedStateNumeric == US_StateInfo.WA_ID)
                                        {
                                            ConsiderWashingtonAsDCorWA(onePlaceEntry.ContextualizedText, out resolvedStateNumeric);
                                            // NOTE: resolvedStateNumeric might get reset to 0 (context too weak to say DC or WA),
                                            // or be WA or be DC.
                                        }
                                        if (resolvedStateNumeric != 0)
                                        {
                                            resolvedPlaceNumeric = StateInfo[resolvedStateNumeric].USGS_ID; // the place is the state
                                            updatedConfidence += 1; // give bump of 1 for state resolving
                                        }
                                    }
                                }
                            }
                        }
                        if (resolvedPlaceNumeric == 0)
                        {
                            hasContext = (onePlaceEntry.Text != onePlaceEntry.ContextualizedText);
                            if (hasContext)
                            { // Try to look up the state immediately after the given place using the context.  Do this by
                              // finding the place in the context, then looking at the immediate suffix word(s).  States can be multi-word, as can place names.
                                workVal = onePlaceEntry.ContextualizedText.IndexOf(onePlaceEntry.Text);
                                if (workVal >= 0 && (workVal + onePlaceEntry.Text.Length < onePlaceEntry.ContextualizedText.Length - 1))
                                    // have "place [place more-context]" type information, get to the "...more-context]" part
                                    presumedStateName = onePlaceEntry.ContextualizedText.Substring(workVal + onePlaceEntry.Text.Length).Trim();
                                else
                                {
                                    // consider the final bracketed context [] as a candidate for a state name
                                    presumedStateName = onePlaceEntry.ContextualizedText;
                                    workVal = presumedStateName.LastIndexOf('[');
                                    if (workVal >= 0)
                                        presumedStateName = presumedStateName.Substring(workVal + 1);
                                    workVal = presumedStateName.LastIndexOf(']');
                                    if (workVal >= 0)
                                        presumedStateName = presumedStateName.Substring(0, workVal);
                                }
                                resolvedStateNumeric = LookUpState(presumedStateName, false); // allow inexact matching...
                                if (resolvedStateNumeric != 0)
                                {
                                    presumedPlaceName = ProperUSGSNameForPlace(onePlaceEntry.Text.Trim());
                                    // Verify that place name is in this state from the context text
                                    if (PlacesInState[resolvedStateNumeric].ContainsKey(presumedPlaceName))
                                    {
                                        resolvedPlaceNumeric = PlacesInState[resolvedStateNumeric][presumedPlaceName];
                                        updatedConfidence += 2; // give bump of 2 for city/state resolving
                                    }
                                    else // back out of the context setting the state
                                        resolvedStateNumeric = 0;
                                }
                                if (resolvedPlaceNumeric == 0)
                                {   // Do more work trying to parse out not just a state hint but a city, state hint within the context.
                                    // Don't require that the place name be repeated in the context, i.e., start with just contextualizedPlaceName.
                                    // There are many entries, perhaps 27873 of 1,086,000 entries from the originally processed ~149,000 stories,
                                    // which have the pattern of place [more description perhaps then city, state] or 
                                    // place [more description perhaps then state] for places like Wrigley Field, Hardeman County, Beale Street, etc.
                                    // Use that context to resolve this location to a specific city if possible, or a state if not.
                                    // First, try to parse out an ending ...city, state] from onePlaceEntry.contextualizedPlaceName
                                    ParsePlaceNameAndStateCandidates(onePlaceEntry.ContextualizedText, out presumedPlaceName, out resolvedStateNumeric);

                                    if (resolvedStateNumeric != 0)
                                    { // Verify that place name is in this state from the context text
                                        if (PlacesInState[resolvedStateNumeric].ContainsKey(presumedPlaceName))
                                        { // This is a keeper: use the city, state information in the context to resolve this place location.
                                            resolvedPlaceNumeric = PlacesInState[resolvedStateNumeric][presumedPlaceName];
                                            updatedConfidence += 2; // give bump of 2 for city/state resolving
                                        }
                                        else
                                        {
                                            // One more hope: we have something like "Annapolis [ U.S. Naval Academy , Maryland ]" where
                                            // original place name may resolve fine to the state discovered within the [] context.
                                            presumedPlaceName = ProperUSGSNameForPlace(onePlaceEntry.Text.Trim());
                                            if (PlacesInState[resolvedStateNumeric].ContainsKey(presumedPlaceName))
                                            { // This is a keeper: use the city, state information in the context to resolve this place location.
                                                resolvedPlaceNumeric = PlacesInState[resolvedStateNumeric][presumedPlaceName];
                                                updatedConfidence += 2; // give bump of 2 for city/state resolving
                                            }
                                            else
                                            {
                                                // We only have a state, but that may be all we were given, e.g., "Hardeman County [Tennessee]".

                                                // If the "state" is "Washington" that is ambiguous and may mean DC or WA.
                                                // Check first for contextual clues that it means DC.
                                                if (resolvedStateNumeric == US_StateInfo.WA_ID)
                                                {
                                                    ConsiderWashingtonAsDCorWA(onePlaceEntry.ContextualizedText, out resolvedStateNumeric);
                                                    // NOTE: resolvedStateNumeric might get reset to 0 (context too weak to say DC or WA),
                                                    // or be WA or be DC.
                                                }

                                                if (resolvedStateNumeric != 0)
                                                {
                                                    // Use the container state only (no city clearly found).
                                                    resolvedPlaceNumeric = StateInfo[resolvedStateNumeric].USGS_ID; // the place is the state
                                                    updatedConfidence += 1; // give bump of 1 for state resolving
                                                }
                                            }
                                        }
                                    }
                                }
                            } // End of having context more than just the given place name
                            if (resolvedStateNumeric == 0)
                            {
                                presumedPlaceName = ProperUSGSNameForPlace(onePlaceEntry.Text.Trim());
                                // Peek ahead.  If the next entry resolves to just a state and its offset means it is adjacent to this place name, 
                                // then have that be the state container for this location, if it resolves from our tables.
                                if (i < locCandidates.Count - 1)
                                {
                                    if (locCandidates[i + 1].StartOffset <= adjacencyEpsilon + onePlaceEntry.StartOffset + onePlaceEntry.Length)
                                    {
                                        // It's close enough to be considered two place names for the same location, e.g., Cairo and Illinois.
                                        // If the latter place resolves to a state, and the former resolves to a place within that state, then do just that:
                                        // make both entries be the same place.
                                        resolvedStateNumeric = LookUpState(locCandidates[i + 1].Text);
                                        if (resolvedStateNumeric != 0)
                                        {
                                            // OK, we have equivalent of "place, state" - see if place resolves inside that state
                                            if (PlacesInState[resolvedStateNumeric].ContainsKey(presumedPlaceName))
                                            {
                                                resolvedPlaceNumeric = PlacesInState[resolvedStateNumeric][presumedPlaceName];
                                                updatedConfidence += 2; // give bump of 2 for city/state resolving
                                                                        // Only at this point, when we confirm "place, state" - do we mark upcoming entry as "finished" in a way
                                                                        // by duplicating what we have found here
                                                locCandidates[i + 1].StateCode = resolvedStateNumeric;
                                                locCandidates[i + 1].PlaceID = resolvedPlaceNumeric;
                                                locCandidates[i + 1].Confidence += 2; // give bump of 2 for city/state resolving
                                            }
                                            else // back away from using next entry for any help
                                                resolvedStateNumeric = 0;
                                        }
                                    }
                                }
                            }

                            if (resolvedStateNumeric == 0)
                            {
                                // No further clues in this entry's context. If a "city-needing-no-hinting", like "New Orleans", great!
                                presumedPlaceName = ProperUSGSNameForPlace(onePlaceEntry.Text.Trim());
                                if (CityHintingInfo.ContainsKey(presumedPlaceName))
                                {
                                    resolvedStateNumeric = CityHintingInfo[presumedPlaceName].StateNumeric;
                                    resolvedPlaceNumeric = CityHintingInfo[presumedPlaceName].USGS_ID;
                                    updatedConfidence += 2; // give bump of 2 for city/state resolving
                                }
                                else
                                {
                                    resolvedStateNumeric = LookUpState(presumedPlaceName);
                                    if (resolvedStateNumeric != 0)
                                    {
                                        // If the "state" is "Washington" that is ambiguous and may mean DC or WA.
                                        // Check for additional contextual clues that it clearly means DC or WA.
                                        if (resolvedStateNumeric == US_StateInfo.WA_ID)
                                        {
                                            ConsiderWashingtonAsDCorWA(presumedPlaceName, out resolvedStateNumeric);
                                            // NOTE: resolvedStateNumeric might get reset to 0 (context too weak to say DC or WA),
                                            // or be WA or be DC.
                                        }

                                        if (resolvedStateNumeric != 0)
                                        {
                                            resolvedPlaceNumeric = StateInfo[resolvedStateNumeric].USGS_ID; // the place is the state
                                            updatedConfidence += 1; // give bump of 1 for state resolving
                                        }
                                        else
                                        {
                                            resolvedPlaceNumeric = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // Move updates of confidence and name IDs back into the entry
                    locCandidates[i].PlaceID = resolvedPlaceNumeric;
                    locCandidates[i].StateCode = resolvedStateNumeric;
                    locCandidates[i].Confidence = updatedConfidence;
                }
                // else we already cleaned up oneStoryEntries[i] back when it was oneStoryEntries[i + 1] during a look-ahead operation
            }
            // Second pass: for all the names represented in oneStoryEntries, if there are times when some entries have no StateNumeric but others do,
            // then turn all the unmarked unresolved ones into resolved ones.
            for (int i = 0; i < locCandidates.Count; i++)
            {
                if (locCandidates[i].StateCode == 0)
                {
                    // Check out other entries and see if any of them have the same place.
                    for (int j = 0; j < locCandidates.Count; j++)
                    {
                        if (j != i && locCandidates[i].Text == locCandidates[j].Text && locCandidates[j].StateCode != 0)
                        { // Resolve to this first discovered one with state info.
                            locCandidates[i].PlaceID = locCandidates[j].PlaceID;
                            locCandidates[i].StateCode = locCandidates[j].StateCode;
                            locCandidates[i].Confidence = locCandidates[j].Confidence;
                            break; // done fixing up entry i via entry j
                        }
                    }
                }
            }

            return locCandidates;
        }

        static bool IsGeneralLocation(string givenText, string givenContext)
        {
            // For a street name like Wyoming Avenue, we may have Wyoming [Avenue], Wyoming [Wyoming Avenue] or Wyoming Avenue [Wyoming Avenue] 
            // and of course Avenue may be Ave., Boulevard, Blvd, Blvd., Street, St., Road, Lane, Lake, River....
            // If that is all the context we have, return true, so that we do not qualify this general location to a city or state.
            bool isGeneralLocation = false;
            string myWorkContext = givenContext;
            if (myWorkContext == null || myWorkContext.Trim().Length == 0)
                myWorkContext = givenText;
            string workVal = myWorkContext.Replace("]", "").TrimEnd(); // remove ] as needed (may not be there)
            if (workVal.Length > 0 && workVal[workVal.Length - 1] == '.')
                workVal = workVal.Substring(0, workVal.Length - 1); // trim off ending '.' to simplify matching to trailing Ave. or Ave, Blvd. or Blvd, etc.

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
                    // No other potential text for a Lake entry like Lake Michigan; give up as too "general" 
                    // since big lakes won't qualify to a single state (leaving open the possibility for context to provide a qualifier
                    // like Lake Michigan [shore by Chicago, IL] or Lake Tahoe [California] which will be taken as long as there a nonempty context hint).
                    isGeneralLocation = true;
            }
            return isGeneralLocation;
        }

        /// <summary>
        /// Parse out a state candidate from given string.
        /// </summary>
        /// <param name="givenText">text with context cues as for state being WA or DC (or perhaps too ambiguous to tell)</param>
        /// <param name="StateNumeric">will be set to 0 (inconclusive) or DC_ID or WA_ID</param>
        static void ConsiderWashingtonAsDCorWA(string givenText, out int StateNumeric)
        {
            StateNumeric = 0; // must get more context to make it DC or WA
            string workVal = givenText.ToLower();
            if (workVal.Contains("d.c.") || workVal.Contains("district of columbia") || workVal.Contains("metro washington") || workVal.Contains("metro [washington"))
                StateNumeric = US_StateInfo.DC_ID;
            if (workVal.Contains("king county") || workVal.Contains("seattle") || workVal.Contains("spokane") ||
                workVal.Contains("yakima") || workVal.Contains("tacoma") || workVal.Contains("pasco") ||
                workVal.Contains("fort lewis") || workVal.Contains("mcchord") || workVal.Contains("fairchild air force base") ||
                workVal.Contains("kitsap") || workVal.Contains("state of washington") || workVal.Contains("washington state"))
                StateNumeric = US_StateInfo.WA_ID;
        }

        /// <summary>
        /// Parse out a city/place name candidate and a state candidate from strings of form city, state and city [state].
        /// </summary>
        /// <param name="givenText">text with hoped-for form of city, state or city [state] plus extras</param>
        /// <param name="cityCandidate">candidate place name in state, "" if not found</param>
        /// <param name="StateNumeric">candidate state name, 0 if not found</param>
        static void ParsePlaceNameAndStateCandidates(string givenText, out string cityCandidate, out int StateNumeric)
        {
            int workVal;
            string presumedStateName = "";
            string presumedPlaceName = "";

            workVal = givenText.LastIndexOf(","); // look first for city COMMA state
            if (workVal >= 2 && (workVal < givenText.Length - 2))
            { // Enough characters before , for city and after , to be a two-letter state code or longer.  Go backward from , to reclaim a "city" candidate.
                presumedStateName = givenText.Substring(workVal + 1).Trim();
                if (presumedStateName.EndsWith("]"))
                    presumedStateName = presumedStateName.Substring(0, presumedStateName.Length - 1).Trim();
                presumedPlaceName = TrimPlaceName(givenText.Substring(0, workVal));
            }
            else
            { // Try the city [state] pattern
                workVal = givenText.LastIndexOf("[");
                if (workVal >= 2 && (workVal < givenText.Length - 2))
                { // Enough characters before [ for city and after [ to be a two-letter state code or longer.  Go backward from [ to reclaim a "city" candidate.
                    presumedStateName = givenText.Substring(workVal + 1).Trim();
                    if (presumedStateName.EndsWith("]"))
                        presumedStateName = presumedStateName.Substring(0, presumedStateName.Length - 1).Trim();
                    presumedPlaceName = TrimPlaceName(givenText.Substring(0, workVal));
                }
            }
            cityCandidate = ProperUSGSNameForPlace(presumedPlaceName);
            StateNumeric = LookUpState(presumedStateName, false); // state name parameter already trimmed above to take off ending ] if found
        }

        /// <summary>
        /// If given candidate has prefix text ending with any of "[sic ", "[sic. ", "[", ",", ";" ":" strip it off (can't do "." as that is in some place names).
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
            // Consider punctuation prefixes as well to clean up the "sloppy" context from named entity extraction and extraneous square-bracketed notes,
            // e.g., [St. Joseph Missionary Baptist Church, Birmingham, Alabama]
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
        /// If given name is a state name or variant like Calif., return the USGS ID for that state.
        /// </summary>
        /// <param name="givenName">name to resolve</param>
        /// <param name="exactMatchNeeded">
        /// optional, defaults to true, if true match all of givenName else match any piece of it for
        /// most of the variants but still require an exact match for the two-letter postal codes given 
        /// prevalence of HI, IN, etc., in corpora
        /// </param>
        /// <returns>0 if not found, or the state numeric code (from USGS) if located</returns>
        /// <remarks>
        /// There was too much noise of the form of last name "Washington" becoming "WA", and so do not
        /// match to "Washington" imprecisely - too noisy. Force an exact match on that particular variant.
        /// </remarks>
        static int LookUpState(string givenName, bool exactMatchNeeded = true)
        {
            string WASHINGTON_EXCEPTION = "Washington"; // the exception to allowing inexact matches -- see remarks above
            int foundVal = 0;
            if (givenName.Length > 1)
            {
                // NOTE: this currently is SLOWED by not trusting just two-letter identifiers but also variants like Calif. and Ill.
                foreach (int oneStateID in StateInfo.Keys)
                {
                    if (givenName == StateInfo[oneStateID].StateAlpha)
                    { // NOTE: regardless of setting for exactMatchNeeded, only match to two-letter code exactly
                        foundVal = oneStateID;
                        break;
                    }
                    foreach (string nameVariant in StateInfo[oneStateID].NameOptions)
                    {
                        if (givenName == nameVariant || (!exactMatchNeeded && givenName.Contains(nameVariant) && nameVariant != WASHINGTON_EXCEPTION))
                        {
                            foundVal = oneStateID;
                            if (!exactMatchNeeded && oneStateID == 51) // 51 == Virginia
                            { // Protect against edge case of "...West Virginia..." returning an imprecise match to "Virginia" rather than "West Virginia"
                                if (givenName.Contains("West Virginia"))
                                    foundVal = 54; // West Virginia
                            }
                            break;
                        }
                    }
                }
            }
            return foundVal;
        }

        /// <summary>
        /// Returns a dictionary loaded with info regarding known states.
        /// </summary>
        static Dictionary<int, US_StateInfo> InitializeStateDictionary()
        {
            /**
            // Set up dictionary keyed by USGS integer code for 50 states plus DC,
            // with the entry being the USGS code for that particular entity.  
            // The comment header shows more details, as taken from the May 1, 2019 USGS data source file
            // with 6 fields shown for each of the 51 entries:
            // USGS ID	Name	Class	State	State-ID	State-Simple-Name
            // USGS_ID, full "name", "Civil" as the feature class, two-letter abbreviation, state code as
            // in USGS tables, typical "name" for the state
            // 785533 State of Alaska Civil   AK  2   Alaska
            // 1779775 State of Alabama Civil   AL  1   Alabama
            // 68085   State of Arkansas Civil   AR  5   Arkansas
            // 1779777 State of Arizona Civil   AZ  4   Arizona
            // 1779778 State of California Civil   CA  6   California
            // 1779779 State of Colorado Civil   CO  8   Colorado
            // 1779780 State of Connecticut Civil   CT  9   Connecticut
            // 1702382 District of Columbia Civil   DC  11  District of Columbia
            // 1779781 State of Delaware Civil   DE  10  Delaware
            // 294478  State of Florida Civil   FL  12  Florida
            // 1705317 State of Georgia Civil   GA  13  Georgia
            // 1779782 State of Hawaii Civil   HI  15  Hawaii
            // 1779785 State of Iowa Civil   IA  19  Iowa
            // 1779783 State of Idaho Civil   ID  16  Idaho
            // 1779784 State of Illinois Civil   IL  17  Illinois
            // 448508  State of Indiana Civil   IN  18  Indiana
            // 481813  State of Kansas Civil   KS  20  Kansas
            // 1779786 Commonwealth of Kentucky Civil   KY  21  Kentucky
            // 1629543 State of Louisiana Civil   LA  22  Louisiana
            // 606926  Commonwealth of Massachusetts Civil   MA  25  Massachusetts
            // 1714934 State of Maryland Civil   MD  24  Maryland
            // 1779787 State of Maine Civil   ME  23  Maine
            // 1779789 State of Michigan Civil   MI  26  Michigan
            // 662849  State of Minnesota Civil   MN  27  Minnesota
            // 1779791 State of Missouri Civil   MO  29  Missouri
            // 1779790 State of Mississippi Civil   MS  28  Mississippi
            // 767982  State of Montana Civil   MT  30  Montana
            // 1027616 State of North Carolina Civil NC  37  North Carolina
            // 1779797 State of North Dakota   Civil ND  38  North Dakota
            // 1779792 State of Nebraska Civil   NE  31  Nebraska
            // 1779794 State of New Hampshire  Civil NH  33  New Hampshire
            // 1779795 State of New Jersey Civil NJ  34  New Jersey
            // 897535  State of New Mexico Civil NM  35  New Mexico
            // 1779793 State of Nevada Civil   NV  32  Nevada
            // 1779796 State of New York   Civil NY  36  New York
            // 1085497 State of Ohio Civil   OH  39  Ohio
            // 1102857 State of Oklahoma Civil   OK  40  Oklahoma
            // 1155107 State of Oregon Civil   OR  41  Oregon
            // 1779798 Commonwealth of Pennsylvania Civil   PA  42  Pennsylvania
            // 1219835 State of Rhode Island and Providence Plantations Civil   RI  44  Rhode Island
            // 1779799 State of South Carolina Civil SC  45  South Carolina
            // 1785534 State of South Dakota   Civil SD  46  South Dakota
            // 1325873 State of Tennessee Civil   TN  47  Tennessee
            // 1779801 State of Texas Civil   TX  48  Texas
            // 1455989 State of Utah Civil   UT  49  Utah
            // 1779803 Commonwealth of Virginia Civil   VA  51  Virginia
            // 1779802 State of Vermont Civil   VT  50  Vermont
            // 1779804 State of Washington Civil   WA  53  Washington
            // 1779806 State of Wisconsin Civil   WI  55  Wisconsin
            // 1779805 State of West Virginia  Civil WV  54  West Virginia
            // 1779807 State of Wyoming Civil   WY  56  Wyoming
            */

            Dictionary<int, US_StateInfo> stateInfo = new Dictionary<int, US_StateInfo>
            {
                { 1, new US_StateInfo(1779775, "AL", new string[] { "Alabama", "State of Alabama" }) },
                { 2, new US_StateInfo(1785533, "AK", new string[] { "Alaska", "State of Alaska" }) },
                { 4, new US_StateInfo(1779777, "AZ", new string[] { "Arizona", "State of Arizona" }) },
                { 5, new US_StateInfo(68085, "AR", new string[] { "Arkansas", "Ark.", "State of Arkansas" }) },
                { 6, new US_StateInfo(1779778, "CA", new string[] { "California", "Calif.", "State of California" }) },
                { 8, new US_StateInfo(1779779, "CO", new string[] { "Colorado", "Colo.", "State of Colorado" }) },
                { 9, new US_StateInfo(1779780, "CT", new string[] { "Connecticut", "Conn.", "State of Connecticut" }) },
                { 10, new US_StateInfo(1779781, "DE", new string[] { "Delaware", "Dela.", "Del.", "State of Delaware" }) },
                { 11, new US_StateInfo(1702382, "DC", new string[] { "District of Columbia", "D.C.", "the District of Columbia" }) },
                { 12, new US_StateInfo(294478, "FL", new string[] { "Florida", "Fla.", "State of Florida" }) },
                { 13, new US_StateInfo(1705317, "GA", new string[] { "Georgia", "State of Georgia" }) },
                { 15, new US_StateInfo(1779782, "HI", new string[] { "Hawaii", "State of Hawaii" }) },
                { 16, new US_StateInfo(1779783, "ID", new string[] { "Idaho", "State of Idaho" }) },
                { 17, new US_StateInfo(1779784, "IL", new string[] { "Illinois", "Ill.", "State of Illinois" }) },
                { 18, new US_StateInfo(448508, "IN", new string[] { "Indiana", "State of Indiana" }) },
                { 19, new US_StateInfo(1779785, "IA", new string[] { "Iowa", "State of Iowa" }) },
                { 20, new US_StateInfo(481813, "KS", new string[] { "Kansas", "State of Kansas" }) },
                { 21, new US_StateInfo(1779786, "KY", new string[] { "Kentucky", "State of Kentucky" }) },
                { 22, new US_StateInfo(1629543, "LA", new string[] { "Louisiana", "State of Louisiana" }) },
                { 23, new US_StateInfo(1779787, "ME", new string[] { "Maine", "State of Maine" }) },
                { 24, new US_StateInfo(1714934, "MD", new string[] { "Maryland", "State of Maryland" }) },
                { 25, new US_StateInfo(606926, "MA", new string[] { "Massachusetts", "Mass.", "State of Massachusetts" }) },
                { 26, new US_StateInfo(1779789, "MI", new string[] { "Michigan", "Mich.", "State of Michigan" }) },
                { 27, new US_StateInfo(662849, "MN", new string[] { "Minnesota", "Minn.", "State of Minnesota" }) },
                { 28, new US_StateInfo(1779790, "MS", new string[] { "Mississippi", "State of Mississippi" }) },
                { 29, new US_StateInfo(1779791, "MO", new string[] { "Missouri", "State of Missouri" }) },
                { 30, new US_StateInfo(767982, "MT", new string[] { "Montana", "State of Montana" }) },
                { 31, new US_StateInfo(1779792, "NE", new string[] { "Nebraska", "Neb.", "State of Nebraska" }) },
                { 32, new US_StateInfo(1779793, "NV", new string[] { "Nevada", "Nev.", "State of Nevada" }) },
                { 33, new US_StateInfo(1779794, "NH", new string[] { "New Hampshire", "N.H.", "State of New Hampshire" }) },
                { 34, new US_StateInfo(1779795, "NJ", new string[] { "New Jersey", "N.J.", "State of New Jersey" }) },
                { 35, new US_StateInfo(897535, "NM", new string[] { "New Mexico", "N.M.", "State of New Mexico" }) },
                { 36, new US_StateInfo(1779796, "NY", new string[] { "New York", "N.Y.", "NY State", "New York State", "N.Y. State", "State of New York" }) },
                { 37, new US_StateInfo(1027616, "NC", new string[] { "North Carolina", "N.C.", "State of North Carolina" }) },
                { 38, new US_StateInfo(1779797, "ND", new string[] { "North Dakota", "N.D.", "State of North Dakota" }) },
                { 39, new US_StateInfo(1085497, "OH", new string[] { "Ohio", "State of Ohio" }) },
                { 40, new US_StateInfo(1102857, "OK", new string[] { "Oklahoma", "Okla.", "State of Oklahoma" }) },
                { 41, new US_StateInfo(1155107, "OR", new string[] { "Oregon", "State of Oregon" }) },
                { 42, new US_StateInfo(1779798, "PA", new string[] { "Pennsylvania", "Penn.", "State of Pennsylvania" }) },
                { 44, new US_StateInfo(1219835, "RI", new string[] { "Rhode Island", "R.I.", "State of Rhode Island" }) },
                { 45, new US_StateInfo(1779799, "SC", new string[] { "South Carolina", "S.C.", "State of South Carolina" }) },
                { 46, new US_StateInfo(1785534, "SD", new string[] { "South Dakota", "S.D.", "State of South Dakota" }) },
                { 47, new US_StateInfo(1325873, "TN", new string[] { "Tennessee", "Tenn.", "State of Tennessee" }) },
                { 48, new US_StateInfo(1779801, "TX", new string[] { "Texas", "Tex.", "State of Texas" }) },
                { 49, new US_StateInfo(1455989, "UT", new string[] { "Utah", "State of Utah" }) },
                { 50, new US_StateInfo(1779802, "VT", new string[] { "Vermont", "State of Vermont" }) },
                { 51, new US_StateInfo(1779803, "VA", new string[] { "Virginia", "State of Virginia" }) },
                { 53, new US_StateInfo(1779804, "WA", new string[] { "Washington", "Washington State", "State of Washington" }) },
                { 54, new US_StateInfo(1779805, "WV", new string[] { "West Virginia", "W.V.", "State of West Virginia" }) },
                { 55, new US_StateInfo(1779806, "WI", new string[] { "Wisconsin", "Wisc.", "State of Wisconsin" }) },
                { 56, new US_StateInfo(1779807, "WY", new string[] { "Wyoming", "State of Wyoming" }) }
            };

            return stateInfo;
        }

        /// <summary>
        /// Use a data input file to fill out cityHintingInfo.
        /// </summary>
        static Dictionary<string, CityHint> LoadCityHintingInfo()
        {
            string fullHintFileName = Path.Combine(dataPath, @"DefaultStatesForSomeLocations.txt");
            char[] lineSeparatorChars = { '\t' };

            // Akron	OH	39	1064305
            // Albany NY  36  977310
            // Naval Station Pearl Harbor  HI  15  2511939
            // etc., tab-delimited
            var cityHintingInfo = new Dictionary<string, CityHint>();

            using (var reader = new StreamReader(fullHintFileName))
            { 
                if (reader.ReadLine() == null)
                {
                    throw new Exception($"LoadCityHintingInfo: Required input file is missing or empty: {fullHintFileName}");
                }
                // else successfully read away the opening header line in the input file

                string oneInputLine;
                while ((oneInputLine = reader.ReadLine()) != null)
                {
                    string[] linePieces = oneInputLine.Split(lineSeparatorChars);
                    if (linePieces.Length == 4 && int.TryParse(linePieces[2], out int stateID) && int.TryParse(linePieces[3], out int cityID))
                    {
                        if (cityHintingInfo.ContainsKey(linePieces[0]))
                        {
                            Logger.Warning("LoadCityHintingInfo: Ignoring repeated city/place hint: {0}", oneInputLine);
                        }
                        else
                        { 
                            cityHintingInfo.Add(linePieces[0], new CityHint(stateID, cityID));
                        }
                    }
                    else
                    {
                        Logger.Warning("LoadCityHintingInfo: Ignoring this malformed city/place hint: {0}", oneInputLine);
                    }
                }
            }

            Logger.Write("LoadCityHintingInfo: {0} city hints loaded.", cityHintingInfo.Count());

            return cityHintingInfo;
        }

        /// <summary>
        /// Use a data input file to fill out all 51 entries of placesInState (keyed by the USGS state ID, e.g., 42 is Pennsylvania.
        /// </summary>
        /// <remarks>Data file format is tab-separated USGS_ID, name, state_ID, e.g.: 1418109	Adak	2
        /// </remarks>
        static Dictionary<int, Dictionary<string, int>> LoadPlacesInStates(Dictionary<int, US_StateInfo> stateInfo)
        {
            string fullPlacesFileName = Path.Combine(dataPath, @"USGS_Places_Table.txt");
            char[] lineSeparatorChars = { '\t' };

            Dictionary<int, Dictionary<string, int>> placesInState = new Dictionary<int, Dictionary<string, int>>();

            // Create entry for each known state
            foreach (var state in stateInfo) 
            { 
                placesInState.Add(state.Key, new Dictionary<string, int>()); 
            }

            // 1418109	Adak	2
            // etc., tab-delimited
            using (var reader = new StreamReader(fullPlacesFileName))
            {
                if (reader.ReadLine() == null)
                {
                    throw new Exception($"LoadPlacesInStates: Required input file is missing or empty: {fullPlacesFileName}");
                }
                // else successfully read away the opening header line in the input file

                string oneInputLine;
                while ((oneInputLine = reader.ReadLine()) != null)
                {
                    var linePieces = oneInputLine.Split(lineSeparatorChars);
                    if (linePieces.Length == 3 &&
                        int.TryParse(linePieces[0], out int placeID) && int.TryParse(linePieces[2], out int stateID) &&
                        placesInState.ContainsKey(stateID))
                    {
                        if (placesInState[stateID].ContainsKey(linePieces[1]))
                        {
                            Logger.Warning("LoadPlacesInStates: Ignoring redundant place: {0}", oneInputLine);
                        }
                        else
                        {
                            placesInState[stateID].Add(linePieces[1], placeID);
                        }
                    }
                    else
                    {
                        Logger.Warning("LoadPlacesInStates: Ignoring this place due to bad format: {0}", oneInputLine);
                    }
                }
            }

            return placesInState;
        }

        /// <summary>
        /// Adjust given name to a "proper" name as needed, typically just returning the given name.
        /// </summary>
        /// <param name="givenCandidate">candidate</param>
        /// <returns>name in a form more suitable for lookup against the USGS data tables</returns>
        /// <remarks>
        /// With addition of Military types into the lookup list along with Populated Place and 
        /// colleges/universities, expand out AFB if found to "Air Force Base".
        /// </remarks>
        static string ProperUSGSNameForPlace(string givenCandidate)
        {
            string retVal = givenCandidate.Replace('[', ' ').Replace(']', ' ').Replace(" AFB", " Air Force Base").Trim();
            if (retVal.ToLower().StartsWith("the city of "))
                retVal = retVal.Substring(12); // remove verbose preface that may be in various upper/lower case mixes
            else if (retVal.ToLower().StartsWith("city of "))
                retVal = retVal.Substring(8); // remove verbose preface that may be in various upper/lower case mixes

            if (retVal.Contains("Ft.") && retVal.Length > 4)
                retVal = retVal.Replace("Ft.", "Fort");

            if (retVal.EndsWith(" St.") && retVal.Length > 4)
                retVal = retVal.Substring(0, retVal.Length - 4) + " Street";
            else if (retVal.Contains("St."))
                // USGS has Saint Louis, Saint Paul, etc., throughout except for one solitary
                // case of St. John, KS (USGS ID 473574) - don't worry about losing that as it 
                // is likely that updates to USGS will update this entry to be Saint John, Kansas too.
                retVal = retVal.Replace("St.", "Saint");
            else
            {
                if (retVal == "Philly" || retVal == "Phila.")
                    retVal = "Philadelphia";
                else if (retVal == "LA" || retVal == "L.A." || retVal == "L.A. Los Angeles")
                    retVal = "Los Angeles";
                else if (retVal == "N.Y. City" || retVal == "NY City" || retVal == "NYC" || retVal == "New York City")
                    retVal = "New York City";
                else if (retVal == "Pearl Harbor") {
                    // !!!TBD!!! NOTE: this preface could be done for other naval stations named NNN 
                    // as long as NNN is not ambiguous such as Naval Station New York -- for now only
                    // Pearl Harbor given such attention.
                    retVal = "Naval Station Pearl Harbor";                                                           
                }
                else if (retVal == "Vegas")
                    retVal = "Las Vegas";
            }
            return retVal;
        }
    }
}
