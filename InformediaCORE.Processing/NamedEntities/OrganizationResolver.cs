using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using InformediaCORE.Common;
using InformediaCORE.Common.Config;

/**
 * NOTES: (CHRISTEL)
 * Unlike Organizations and Locations which work on combined spaCy and Stanford results, dates only 
 * care about spaCy. Because of this, date processing occurs earlier in the pipeline and maintains it's 
 * own spaCy parsing logic instead of sharing the "pass 3" combined results used by the remainder of 
 * the resolvers. 
 * 
 * 
 * This Pass 4 program works only with organizations and currently only resolves to those Library of 
 * Congress CorporateName named authority entries which via other helper programs were teased out into 
 * the data input file CorporateNameLookup.txt (which is admittedly very large, 90 MB of 1,592,072 
 * entries of form unique-name and LOC-name where a LOC-name like n79054102 can be used in a URL 
 * http://id.loc.gov/authorities/names/n79054102.html to get to further details on that organization, 
 * including alternate forms). 
 * 
 * On a match (success), the program bumps up organization reference confidences, and adds in canonical 
 * named entity ID (from LOC authority name table). This pass is ONLY for organizations as a subset of 
 * CorporateName (which does cover the vast majority of organizations detected by spaCy and/or the 
 * Stanford NLP NER and recognized as "organization" type). 
 * 
 * NOTE WELL: Pass 3 input file MUST be sorted on collection ID, then story ID, then start offset 
 * ascending to keep stories together and entries in offset order. 
 * 
 * Heuristics for Pass 4 are noted in comments for ProcessEntriesForStory(). 
 * 
 * The attempt is made to work across all the organizations, not just those with highest confidence of 
 * 3 from "Pass 3." 
 *
 * Order of work:
 * (1) Get all organizations for a story (all confidences for now....).
 * (2) For each, try to make into a recognized organization (using ProcessEntriesForStory heuristics).
 * (3) In outputting entries, add in new column for LOC-NameID and update confidence as appropriate.
 *     Some jump +1 (found a matching LOC name); others may jump more (formerly low confidence 
 *     that matches another entry in this same story so bump up to all be the same highest confidence 
 *     in that story).
 *
 * NOTE: this does NOT yet consider hints/context from higher level text such as tape abstracts, 
 * session abstracts, biography abstracts, biography metadata like organizations in a description.
 * This only considers the story transcript processing via what is already in our "Pass 3" output file.
 */

namespace InformediaCORE.Processing.NamedEntities
{
    /// <summary>
    /// Holds values from "Pass 3" along with an extra slot for LOC name ID (may be "" for not applicable).
    /// Input data has form: collection-ID, story-ID, name, contextualized-name, startoffset, length, 
    /// spaCy-type, "Org", and confidence. We do not save the "Org" overall type indicating Organization - 
    /// it is assumed by the nature of this "Pass 4 for Organizations" program.
    /// </summary>
    public class OrganizationalEntity : NamedEntity
    {
        public int Count { get; set; } = 1;
        public string LOCNameID { get; set; } = "";

        public OrganizationalEntity() { }

        public OrganizationalEntity(NamedEntity entity)
        {
            this.Text = entity.Text;
            this.ContextualizedText = entity.ContextualizedText;
            this.StartOffset = entity.StartOffset;
            this.Length = entity.Length;
            this.SpacyType = entity.SpacyType;
            this.Type = entity.Type;
            this.Confidence = entity.Confidence;
        }
    }

    /// <summary>
    /// Take as input a list of Library of Congress (LOC) corporate names and ids (input file), an alternate corporate names input file
    /// of synonyms (e.g., NAACP for National Association for the Advancement of Colored People),
    /// plus the organizations "Pass 3" output.  Update confidences and add in LOC 
    /// ID where possible for these organizations, with the "Pass 4" output disambiguating hopefully most of the organizations.
    /// </summary>
    /// <remarks>
    /// 12-Aug-2019:    Added in "University of ..." parsing.  Did not add in special casing for possessives ("'s") because there may
    /// be organizations, such as Macy's, where the 's is part of the organization name.  Hopefully for stories where the organization is referred
    /// to with a possessive form it is also notes in a non-possessive form.
    /// 
    /// The input from Pass 3 has columns from 9 fields:
    /// collection-ID, story-ID, name, contextualized-name, startoffset, length, spaCy-type, "Org", and confidence.
    /// (Instead of entity-type, "Org" is listed to denote eighth field always "Org" for organization), e.g.,
    /// 10209	664272	Carnegie Mellon University	Carnegie Mellon University [Pittsburgh, Pennsylvania]	435	26	ORG	Org	3
    /// </remarks>
    public static class OrganizationResolver
    {
        // NOTE: important data files within dataPath, each with a header line describing fields to start
        // the file before data lines commence: 
        // CorporateNameLookup.txt, tab-delimited rows of Name, LOC-NameID (one header line to start)
        static readonly string dataPath = Settings.Current.EntityResolutionTask.DataPath;

        private static readonly Dictionary<string, string> authorizedCorporateName = LoadAuthorizedCorporateNames();
        private static readonly Dictionary<string, string> providedCorporateSynonym = LoadAlternateCorporateNames();

        /// <summary>
        /// Resolve possible organizations within the given list of named entities.
        /// </summary>
        /// <param name="pass3Entities">A list of pass 3 named entities.</param>
        /// <returns>A list of organizational entities which were successfully resolved to an authoritative id.</returns>
        /// <remarks>This logic comes from the Main method in the original source code.</remarks>
        public static List<OrganizationalEntity> Resolve(List<NamedEntity> pass3Entities, out List<OrganizationalEntity> unresolvedOrganizations)
        {
            List<OrganizationalEntity> processedStoryEntities = ProcessEntriesForStory(pass3Entities);

            // Output each entry into one of two buckets: resolved or unresolved:
            unresolvedOrganizations = new List<OrganizationalEntity>();
            Dictionary<string, OrganizationalEntity> resolvedOrganizations = new Dictionary<string, OrganizationalEntity>();

            // Example: a story has lots of "Georgia State" but one of those mentions "Georgia State 
            // University" to resolve to a real non-empty entity. Make all be that entity. A check 
            // is also done to be sure that in the same story, the same text, e.g., Georgia State, 
            // does not map to two entities, e.g., Georgia State Univ. and Savannah State Univ. That 
            // might happen in actuality, but will trigger a warning message. 

            Dictionary<string, string> orgNameResolvedEntity = new Dictionary<string, string>(); // used to map all "X" to resolved entity X' if one had enough context to do so

            // Build dictionary of resolved entities and check for conflicts
            foreach (OrganizationalEntity entity in processedStoryEntities)
            {
                if (orgNameResolvedEntity.ContainsKey(entity.Text))
                {   // The given organization name (from prior named entity organization identification passes) is already noted.
                    if (orgNameResolvedEntity[entity.Text] != entity.LOCNameID)
                    {
                        if (orgNameResolvedEntity[entity.Text] == "")
                        {
                            orgNameResolvedEntity[entity.Text] = entity.LOCNameID;
                        }
                        else if (entity.LOCNameID != "")
                        {
                            Logger.Error($"Two or more entities with the name '{entity.Text}' resolved to different IDs.");
                            // Bail out now, Mike's code did not allow for continued processing upon detecting duplicates
                            return resolvedOrganizations.Values.ToList();   
                        }
                    }
                }
                else
                {
                    // add in the current entry, even with perhaps an unresolved blank LOCNameID.
                    orgNameResolvedEntity.Add(entity.Text, entity.LOCNameID);
                }
            }

            // Do a first clean-up pass on LOCNameID: same orgName gets the same LOCNameID with all orgNames as keys already in place
            foreach (OrganizationalEntity entity in processedStoryEntities)
            {
                entity.LOCNameID = orgNameResolvedEntity[entity.Text];
            }

            // Output collected story information whereby a resolved org name has been propagated within 
            // the story to all other of the same name in that story (in case some did not have enough 
            // context to resolve, done via use of currentStoryOrgNameResolvedEntity).
            foreach (OrganizationalEntity entity in processedStoryEntities)
            {
                if (entity.LOCNameID != "")
                {   
                    // Keep track of each unique non-empty organization mentioned in the story.  If 
                    // already listed, update its count  and keep the maximum of the confidences.
                    if (resolvedOrganizations.ContainsKey(entity.LOCNameID))
                    {
                        // Already here.
                        resolvedOrganizations[entity.LOCNameID].Count++;
                        resolvedOrganizations[entity.LOCNameID].Confidence = Math.Max(entity.Confidence, resolvedOrganizations[entity.LOCNameID].Confidence);
                    }
                    else
                    {
                        resolvedOrganizations.Add(entity.LOCNameID, entity);
                    }
                }
                else
                {
                    // put into unresolved bucket, using "n/a" in LOC name ID field to indicate this is unresolved
                    entity.LOCNameID = "n/a";
                    unresolvedOrganizations.Add(entity);
                }
            }

            // Boost Confidence
            foreach (string oneNameID in resolvedOrganizations.Keys)
            {
                if (resolvedOrganizations[oneNameID].Count >= 2)
                {    
                    // Boost the confidence up +1
                    resolvedOrganizations[oneNameID].Confidence += 1;
                }
            }

            return resolvedOrganizations.Values.ToList();
        }

        /// <summary>
        /// Apply heuristics and various helper data to resolve organization names to their LOC name ID.
        /// If a name resolves, update its confidence.
        /// </summary>
        /// <param name="namedEntities">Processed "pass 3" spaCy entities combined with Stanford "pass 2" entities.</param>
        /// <remarks>If context has form of organization-name org-name-in-different-form then use that to disambiguate.  Parsing is tricky because:
        /// (a) "name" is fuzzily extracted; trust the contextualized name if the name does not produce a hit
        /// (b) Both can have [ and/or ] add in noise.  Use these as candidate name separators, e.g., to find name in "Princeton [University" or
        ///     "AFL-CIO [American Federation of Labor - Congress of Industrial Organizations]" -- consider text delineated by [ and ] as a name by itself
        ///     but if no matches, then replace [ and ] with space, put " " before/after
        ///     every &, remove extra spaces, and do lookup on the whole.  
        /// (c) context of [] words may bring in other noise so organization may just be a piece in a smaller whole as in
        ///     "Harvard St. George [School, Chicago, Illinois]" -- stop after certain code words for organization name ending such as 
        ///     School, University, Bank, House, Academy, Church, Command, Incorporated, Inc., A&M, A&P, A&T, LLC, Office
        /// (d) Some locations have common pseudodyms like "IBM" for "International Business Machines."
        ///     Expand out to "proper" form as needed (via hard-coded data in this program).
        /// </remarks>
        private static List<OrganizationalEntity> ProcessEntriesForStory(List<NamedEntity> namedEntities)
        {
            // Filter list to organizations and convert type.
            List<OrganizationalEntity> organizationalEntities = namedEntities
                .Where(x => x.Type == EntityType.Org)
                .Select(x => new OrganizationalEntity(x))
                .ToList();

            int workVal;
            bool hasContext;
            string presumedOrgName;

            // First pass: resolve each entry separately, using only its context as necessary.
            foreach(var organizationalEntity in organizationalEntities)
            {
                // First try: parse just the name as detected from earlier work.
                ParseOrganizationNameCandidate(organizationalEntity.Text, out string resolvedLOCNameID);

                if (resolvedLOCNameID == "")
                {
                    hasContext = (organizationalEntity.Text != organizationalEntity.ContextualizedText);
                    if (hasContext)
                    { // Try to look up the organization via either just the context text, or context with redundant name stripped away.
                        ParseOrganizationNameCandidate(organizationalEntity.ContextualizedText, out resolvedLOCNameID);
                        if (resolvedLOCNameID == "")
                        {
                            workVal = organizationalEntity.ContextualizedText.IndexOf(organizationalEntity.Text);
                            if (workVal >= 0 && (workVal + organizationalEntity.Text.Length < organizationalEntity.ContextualizedText.Length - 1))
                            {
                                // have name as "leadin" and "[leadin more-context]" context information; get to the "...more-context]" part
                                presumedOrgName = organizationalEntity.ContextualizedText.Substring(workVal + organizationalEntity.Text.Length).Trim();
                                ParseOrganizationNameCandidate(presumedOrgName, out resolvedLOCNameID);
                            }
                        }
                    } // End of having context more than just the given name
                }

                // Move updates of confidence and name ID back into the entry
                organizationalEntity.LOCNameID = resolvedLOCNameID;
                if (resolvedLOCNameID != "")
                {
                    organizationalEntity.Confidence++; // if we can look up the org name successfully, increase the confidence that it is a true name
                }
            }

            return organizationalEntities;
        }

        /// <summary>
        /// Parse out an organization candidate from strings of form ORG *or* ORGa [ORGb] *or* ORGc (ORGd) with ending ] and ) optional.
        /// </summary>
        /// <param name="givenText">text with hoped-for form of organization name, plus extras</param>
        /// <param name="discoveredLOCNameID">discovered name ID for the candidate, "" if not found</param>
        static void ParseOrganizationNameCandidate(string givenText, out string discoveredLOCNameID)
        {
            bool squareBracketContextGiven = false;
            bool parentheticalContextGiven = false;
            int workVal;
            string presumedOrgName = TrimOrganizationName(givenText); // handles both ORG as good name and ORGa [ORGb] with ORGa ORGb making a good name together

            discoveredLOCNameID = LookupViaSynonyms(presumedOrgName);
            if (discoveredLOCNameID == "")
                discoveredLOCNameID = LookupViaNameAuthority(presumedOrgName);

            if (discoveredLOCNameID == "")
            {
                // see if it has form of ORGa [ORGb]
                workVal = givenText.IndexOf("[");
                if (workVal >= 2 && workVal < givenText.Length - 1)
                {
                    squareBracketContextGiven = true;
                    // Try ORGa first.
                    presumedOrgName = TrimOrganizationName(givenText.Substring(0, workVal));
                    discoveredLOCNameID = LookupViaSynonyms(presumedOrgName);
                    if (discoveredLOCNameID == "")
                        discoveredLOCNameID = LookupViaNameAuthority(presumedOrgName);
                    if (discoveredLOCNameID == "")
                    {
                        // Try ORGb from "ORGa [ORGb" where TrimOrganizationName will treat ending ] as a no-op (can be there or not, doesn't matter)
                        presumedOrgName = TrimOrganizationName(givenText.Substring(workVal + 1));
                        discoveredLOCNameID = LookupViaSynonyms(presumedOrgName);
                        if (discoveredLOCNameID == "")
                            discoveredLOCNameID = LookupViaNameAuthority(presumedOrgName);
                    }
                }
                if (discoveredLOCNameID == "")
                {
                    // Try form ORGc (ORGd) with ending ) optional
                    workVal = givenText.IndexOf("(");
                    if (workVal >= 2 && workVal < givenText.Length - 1)
                    {
                        parentheticalContextGiven = true;
                        // Try ORGc first.
                        presumedOrgName = TrimOrganizationName(givenText.Substring(0, workVal));
                        discoveredLOCNameID = LookupViaSynonyms(presumedOrgName);
                        if (discoveredLOCNameID == "")
                            discoveredLOCNameID = LookupViaNameAuthority(presumedOrgName);
                        if (discoveredLOCNameID == "")
                        {
                            // Try ORGd from "ORGc (ORGd" where optional ending ) taken off here...
                            presumedOrgName = TrimOrganizationName(givenText.Substring(workVal + 1)).Trim();
                            if (presumedOrgName.EndsWith(")"))
                                presumedOrgName = presumedOrgName.Substring(0, presumedOrgName.Length - 1);
                            discoveredLOCNameID = LookupViaSynonyms(presumedOrgName);
                            if (discoveredLOCNameID == "")
                                discoveredLOCNameID = LookupViaNameAuthority(presumedOrgName);
                        }
                    }
                    if (discoveredLOCNameID == "")
                    {
                        // Special-purpose college/university processing to reclaim mention of college/university in the
                        // [] or () supplemental text or even if the full text if context on its own is not helping.
                        if (squareBracketContextGiven)
                            discoveredLOCNameID = ParseCollegeUniversityMention(givenText, "[");
                        if (discoveredLOCNameID == "" && parentheticalContextGiven)
                            discoveredLOCNameID = ParseCollegeUniversityMention(givenText, "(");
                        if (discoveredLOCNameID == "")
                            // Try one last time without separating out a defined "context", i.e., consider all the given text:
                            discoveredLOCNameID = ParseCollegeUniversityMention(givenText, "");
                    }
                }
            }
        }

        /// <summary>
        /// return organization ID if found, "" if not
        /// </summary>
        /// <param name="givenKeyCandidate">key to use for lookup</param>
        /// <returns>dictionary value if found, "" if not</returns>
        static string LookupViaSynonyms(string givenKeyCandidate)
        {
            string retVal = "";
            string presumedOrgName;

            if (providedCorporateSynonym.ContainsKey(givenKeyCandidate))
                retVal = providedCorporateSynonym[givenKeyCandidate];
            else if (givenKeyCandidate.Contains("United States"))
            { // in synonyms file, the various U.S. agencies are listed just with U.S., not with full form United States
                presumedOrgName = givenKeyCandidate.Replace("United States", "U.S.");
                if (providedCorporateSynonym.ContainsKey(presumedOrgName))
                    retVal = providedCorporateSynonym[presumedOrgName];
            }
            else if (givenKeyCandidate.StartsWith("The ") || givenKeyCandidate.StartsWith("the "))
            { // in synonyms file, only have Washington Post, not "The Washington Post", etc.
                presumedOrgName = givenKeyCandidate.Substring(4);
                if (providedCorporateSynonym.ContainsKey(presumedOrgName))
                    retVal = providedCorporateSynonym[presumedOrgName];
            }
            else if (givenKeyCandidate.StartsWith("later "))
            { // this is a specific corpus convention, to state that the organization is "later NNN" with NNN the actual name
                presumedOrgName = givenKeyCandidate.Substring(6); // get to the NNN part after the "later " prefix
                if (providedCorporateSynonym.ContainsKey(presumedOrgName))
                    retVal = providedCorporateSynonym[presumedOrgName];
            }
            return retVal;
        }

        /// <summary>
        /// return organization ID if found, "" if not
        /// </summary>
        /// <param name="givenKeyCandidate">key to use for lookup</param>
        /// <returns>dictionary value if found, "" if not</returns>
        static string LookupViaNameAuthority(string givenKeyCandidate)
        {
            string retVal = "";
            string presumedOrgName;

            if (authorizedCorporateName.ContainsKey(givenKeyCandidate))
                retVal = authorizedCorporateName[givenKeyCandidate];
            else if (givenKeyCandidate.StartsWith("The ") || givenKeyCandidate.StartsWith("the "))
            { // in authority file, often only have term like Boston Globe, not "The Boston Globe", etc.
                presumedOrgName = givenKeyCandidate.Substring(4);
                if (authorizedCorporateName.ContainsKey(presumedOrgName))
                    retVal = authorizedCorporateName[presumedOrgName];
            }
            else if (givenKeyCandidate.StartsWith("later "))
            { // this is a specific corpus convention, to state that the organization is "later NNN" with NNN the actual name
                presumedOrgName = givenKeyCandidate.Substring(6); // get to the NNN part after the "later " prefix
                if (authorizedCorporateName.ContainsKey(presumedOrgName))
                    retVal = authorizedCorporateName[presumedOrgName];
            }
            else if (givenKeyCandidate.StartsWith("UC "))
            { // in authority file, have "University of California, Berkeley" and so on rather than "UC Berkeley" so look up that instead
                presumedOrgName = "University of California, " + givenKeyCandidate.Substring(3);
                if (authorizedCorporateName.ContainsKey(presumedOrgName))
                    retVal = authorizedCorporateName[presumedOrgName];
            }
            else if ((presumedOrgName = RecastProfessionalSportsTeamMention(givenKeyCandidate)) != "")
            {
                if (authorizedCorporateName.ContainsKey(presumedOrgName))
                    retVal = authorizedCorporateName[presumedOrgName];
            }

            // NOTE: the Library of Congress named authority file has too mixed of formatting to attempt any additional parsing such as assuming
            // U.S. or United States - only do lookup based on the key given without further parsing.

            return retVal;
        }

        /// <summary>
        /// If given text has specific forms like "Buffalo Bills football team" return a form that is how the authority file
        /// key is formatted, e.g., Buffalo Bills (Football team)" - only do this for baseball, football, basketball, hockey.
        /// </summary>
        /// <param name="givenText">given text which may be a team reference</param>
        /// <returns>"" if name is not a recognized team reference, else a format of that team reference as likely to be used in authority name list</returns>
        static string RecastProfessionalSportsTeamMention(string givenText)
        {
            // If given string is of the form "NAME (professional ** baseball/football/basketball/hockey team)" where ( and ) are optional, word "professional" 
            // is optional, ** can be empty or one of {American League, National League, Negro League} for baseball, one of {American} for football,
            // ...then return string of the form "NAME (Sport team)" where Sport is the associated Baseball, Football, Basketball, Hockey.

            // Assumes that givenText already went through a TrimOrganizationName() step so that it has no [ or ].
            const int MIN_SPORTS_NAME_SOUGHT = 3; // for NAME in comment above
            const int MIN_SPORT_NAME_LENGTH = 6; // for "hockey" -- see comment

            string retVal = "";
            int workOffset = -1;
            int sportNameOffset;
            string sportsNameCandidate;
            string workString;

            if (givenText.EndsWith(" team"))
                workOffset = givenText.Length - 5;
            else if (givenText.EndsWith(" team)"))
                workOffset = givenText.Length - 6;
            if (workOffset > 0)
            {
                workString = givenText.Substring(0, workOffset).Trim();
                // perhaps have the "team" sports name for one of the sports.  Back up further in the string and see.
                sportNameOffset = workString.LastIndexOf(' ');
                if (sportNameOffset > MIN_SPORTS_NAME_SOUGHT && sportNameOffset <= workString.Length - MIN_SPORT_NAME_LENGTH)
                {
                    sportsNameCandidate = workString.Substring(sportNameOffset + 1).ToLower();
                    if (sportsNameCandidate.StartsWith("("))
                        sportsNameCandidate = workString.Substring(1).Trim();
                    if (sportsNameCandidate == "basketball")
                        retVal = workString.Substring(0, sportNameOffset).Trim() + " (Basketball team)";
                    else if (sportsNameCandidate == "hockey")
                        retVal = workString.Substring(0, sportNameOffset).Trim() + " (Hockey team)";
                    else if (sportsNameCandidate == "football")
                    {
                        workString = workString.Substring(0, sportNameOffset).Trim();
                        if (workString.EndsWith("American"))
                            workString = workString.Substring(0, workString.Length - 8).Trim(); // take off American and trim whitespace
                        if (workString.EndsWith("professional"))
                            workString = workString.Substring(0, workString.Length - 12).Trim(); // take off professional and trim whitespace
                        retVal = workString + " (Football team)";
                    }
                    else if (sportsNameCandidate == "baseball")
                    {
                        workString = workString.Substring(0, sportNameOffset).Trim();
                        if (workString.EndsWith("American League"))
                            workString = workString.Substring(0, workString.Length - 15).Trim(); // take off American League and trim whitespace
                        else if (workString.EndsWith("National League"))
                            workString = workString.Substring(0, workString.Length - 15).Trim(); // take off National League and trim whitespace
                        else if (workString.EndsWith("Negro League"))
                            workString = workString.Substring(0, workString.Length - 12).Trim(); // take off Negro League and trim whitespace
                        if (workString.EndsWith("professional"))
                            workString = workString.Substring(0, workString.Length - 12).Trim(); // take off professional and trim whitespace
                        retVal = workString + " (Baseball team)";
                    }
                }
            }
            return retVal;
        }

        /// <summary>
        /// Look specifically for "College" or "University" cues to return a college/university name.
        /// </summary>
        /// <param name="givenText">candidate text to check</param>
        /// <param name="contextMarker">punctuation such as [ or ( marking context supplemental text, "" for no such context</param>
        /// <returns>discovered name ID for the candidate text, "" if not found</returns>
        static string ParseCollegeUniversityMention(string givenText, string contextMarker)
        {
            string discoveredLOCNameID = "";
            int workVal;
            string prefixStub, suffixStub;
            string presumedOrgName;

            if (contextMarker != "")
            { // ORGa [ORGb for contextMarker of [ for example
                workVal = givenText.IndexOf(contextMarker);
                if (workVal >= 2 && workVal < givenText.Length - 1)
                {
                    prefixStub = givenText.Substring(0, workVal).Trim();
                    suffixStub = givenText.Substring(workVal + 1);
                    if (!prefixStub.Contains("College") && !prefixStub.Contains("University"))
                    {
                        presumedOrgName = "";
                        // if ORGb starts as College or University, then consider org name as ORGa College/University
                        if (suffixStub.IndexOf("University") == 0)
                            presumedOrgName = TrimOrganizationName(prefixStub) + " University";
                        else if (suffixStub.IndexOf("College") == 0)
                            presumedOrgName = TrimOrganizationName(prefixStub) + " College";
                        if (presumedOrgName.Length > 0)
                        {
                            discoveredLOCNameID = LookupViaSynonyms(presumedOrgName);
                            if (discoveredLOCNameID == "")
                                discoveredLOCNameID = LookupViaNameAuthority(presumedOrgName);
                        }
                        if (discoveredLOCNameID == "")
                        {
                            presumedOrgName = "";
                            // if ORGb contains "ORGa College" or "ORGa University" or "University of ORGa" use that...
                            if (suffixStub.Contains(prefixStub + " University"))
                                presumedOrgName = prefixStub + " University";
                            else if (suffixStub.Contains("University of " + prefixStub))
                                presumedOrgName = "University of " + prefixStub;
                            else if (suffixStub.Contains(prefixStub + " College"))
                                presumedOrgName = prefixStub + " College";
                            if (presumedOrgName.Length > 0)
                            {
                                discoveredLOCNameID = LookupViaSynonyms(presumedOrgName);
                                if (discoveredLOCNameID == "")
                                    discoveredLOCNameID = LookupViaNameAuthority(presumedOrgName);
                            }
                        }
                    }
                }
                else
                    suffixStub = givenText;
            }
            else
                suffixStub = givenText;

            if (discoveredLOCNameID == "")
            {
                // Most drastic: if context (or full given string) gives us University of ... or ... College or ...University then use that as presumed organization name, 
                // as in Georgia State College [Savannah State University, Savannah, Georgia] (would use Savannah State University), or in 
                // Savannah State University, Savannah, Georgia.
                presumedOrgName = "";
                workVal = suffixStub.IndexOf("University of");
                if (workVal > 0 && workVal + 13 < suffixStub.Length)
                {
                    presumedOrgName = "University of " + ExtractPotentialNameFromTrailer(suffixStub, workVal + 13);
                }
                workVal = suffixStub.IndexOf(" University");
                if (workVal > 0)
                    presumedOrgName = ExtractPotentialName(suffixStub, workVal) + " University";
                else
                {
                    workVal = suffixStub.IndexOf(" College");
                    if (workVal > 0)
                        presumedOrgName = ExtractPotentialName(suffixStub, workVal) + " College";
                }
                if (presumedOrgName.Length > 0)
                {
                    discoveredLOCNameID = LookupViaSynonyms(presumedOrgName);
                    if (discoveredLOCNameID == "")
                        discoveredLOCNameID = LookupViaNameAuthority(presumedOrgName);
                }
            }
            return discoveredLOCNameID;
        }

        /// <summary>
        /// Returns trimmed string from the given starting index up to the first "[", "(", ";", or "," or end of string.
        /// </summary>
        /// <param name="givenText">given text</param>
        /// <param name="startingIndex">starting index (if not nonzero, then "" is returned)</param>
        /// <returns>trimmed substring of the given string past the given starting index</returns>
        static string ExtractPotentialNameFromTrailer(string givenText, int startingIndex)
        {
            string retVal = "";
            int firstSB, firstComma, firsttSemicolon, firstParen;
            int firstIndexToToss;
            if (startingIndex >= 0)
            {
                retVal = givenText.Substring(startingIndex);
                firstSB = retVal.IndexOf('[');
                firstComma = retVal.IndexOf(',');
                firsttSemicolon = retVal.IndexOf(';');
                firstParen = retVal.IndexOf('(');
                firstIndexToToss = retVal.Length;
                if (firstSB >= 0 && firstSB < firstIndexToToss)
                    firstIndexToToss = firstSB;
                if (firstComma >= 0 && firstComma < firstIndexToToss)
                    firstIndexToToss = firstComma;
                if (firsttSemicolon >= 0 && firsttSemicolon < firstIndexToToss)
                    firstIndexToToss = firsttSemicolon;
                if (firstParen >= 0 && firstParen < firstIndexToToss)
                    firstIndexToToss = firstParen;
                if (firstIndexToToss < retVal.Length)
                    retVal = retVal.Substring(0, firstIndexToToss);
            }
            return retVal.Trim();
        }

        /// <summary>
        /// Returns trimmed string from just after the first "[", "(", ";", "sic ", "sic. " or "," found before the given concluding index, up to the concluding index.
        /// </summary>
        /// <param name="givenText">given text</param>
        /// <param name="concludingIndex">concluding index (if not positive, then "" is returned)</param>
        /// <returns>trimmed substring of the given string never at or past the given concluding index</returns>
        static string ExtractPotentialName(string givenText, int concludingIndex)
        {
            string retVal = "";
            int lastSB, lastComma, lastSemicolon, lastParen, lastSic;
            int lastIndexToToss;
            if (concludingIndex > 0)
            {
                retVal = givenText.Substring(0, concludingIndex);
                lastSB = retVal.LastIndexOf('[');
                lastComma = retVal.LastIndexOf(',');
                lastSemicolon = retVal.LastIndexOf(';');
                lastParen = retVal.LastIndexOf('(');
                lastSic = retVal.LastIndexOf("sic. ");
                if (lastSic == -1)
                {
                    lastSic = retVal.LastIndexOf("sic ");
                    if (lastSic >= 0)
                        lastSic += 3; // move to end of found string as others are just 1-character strings
                }
                else
                    lastSic += 4; // move to end of found string as others are just 1-character strings
                lastIndexToToss = lastSB;
                if (lastComma > lastIndexToToss)
                    lastIndexToToss = lastComma;
                if (lastSemicolon > lastIndexToToss)
                    lastIndexToToss = lastSemicolon;
                if (lastParen > lastIndexToToss)
                    lastIndexToToss = lastParen;
                if (lastSic > lastIndexToToss)
                    lastIndexToToss = lastSic;
                if (lastIndexToToss >= 0)
                    retVal = retVal.Substring(lastIndexToToss + 1);
            }
            return retVal.Trim();
        }

        /// <summary>
        /// Return given name with all double spaces reduced to 1 and & opened up to " & " with no ending :, &, ;, or comma,
        /// any [ or ] always replaced by space with opening "sic" removed as well.
        /// </summary>
        /// <param name="givenOrganizationCandidate">organization candidate (often multiple words)</param>
        /// <returns>trimmed name cleaning punctuation</returns>
        static string TrimOrganizationName(string givenOrganizationCandidate)
        {
            string presumedOrganizationName = givenOrganizationCandidate.Replace("[", " ").Replace("]", " ").Replace("&", " & ");
            presumedOrganizationName = presumedOrganizationName.Replace("  ", " ");
            presumedOrganizationName = presumedOrganizationName.Trim();
            if (presumedOrganizationName.StartsWith("sic. "))
                presumedOrganizationName = presumedOrganizationName.Substring(5); // move past opening "sic. "
            else if (presumedOrganizationName.StartsWith("sic "))
                presumedOrganizationName = presumedOrganizationName.Substring(4); // move past opening "sic "

            while (presumedOrganizationName.Length > 0 &&
                (presumedOrganizationName.EndsWith(":") || presumedOrganizationName.EndsWith(";") || presumedOrganizationName.EndsWith(",")))
                presumedOrganizationName = presumedOrganizationName.Substring(0, presumedOrganizationName.Length - 1).Trim();
            return presumedOrganizationName;
        }

        /// <summary>
        /// Use a data input file to fill out synonyms to LOC authoritative ID mapping, keyed by synonym names (backward from what
        /// might be expected).
        /// </summary>
        /// <remarks>Data file format is tab-separated synonym, authoritative name (not preserved here), LOC-NameID per line after a heading line.
        /// </remarks>
        static Dictionary<string, string> LoadAlternateCorporateNames()
        {
            char[] lineSeparatorChars = { '\t' };
            string synonymsFileName = Path.Combine(dataPath, @"AlternateCorporateNames.txt");

            Logger.Write("LoadAlternateCorporateNames: Loading data from {0}", synonymsFileName);
            var corporateSynonyms = new Dictionary<string, string>();

            // Carnegie Mellon	Carnegie-Mellon University	n79054102
            // etc., tab-delimited

            using (var reader = new StreamReader(synonymsFileName))
            { 
                if (reader.ReadLine() == null)
                {
                    throw new Exception($"LoadAlternateCorporateNames: Required input file is missing or empty: {synonymsFileName}");
                }
                // else successfully read away the opening header line in the input file
                
                string oneInputLine;
                while ((oneInputLine = reader.ReadLine()) != null)
                {
                    string[] linePieces = oneInputLine.Split(lineSeparatorChars);
                    if (linePieces.Length == 3 && linePieces[0].Length > 0 && linePieces[2].Length > 0)
                    {
                        if (corporateSynonyms.ContainsKey(linePieces[0]))
                        {
                            Logger.Warning("LoadAlternateCorporateNames: Ignoring this redundant organization synonym: {0}", oneInputLine);
                        }
                        else
                        {
                            corporateSynonyms.Add(linePieces[0], linePieces[2]);
                        }
                    }
                    else
                    {
                        Logger.Warning("LoadAlternateCorporateNames: Ignoring this organization synonym due to bad format: {0}", oneInputLine);
                    }
                }
            }

            Logger.Write("LoadAlternateCorporateNames: {0} corporate synonyms loaded", corporateSynonyms.Count());
            return corporateSynonyms;
        }

        /// <summary>
        /// Load table which maps corporate names to Library of Congress (LoC) authoritative ID mapping, 
        /// keyed by corporate names (backward from what might be expected).
        /// </summary>
        /// <remarks>
        /// Data file format is tab-separated name, LOC-NameID per line after a heading line.
        /// </remarks>
        static Dictionary<string, string> LoadAuthorizedCorporateNames()
        {
            char[] lineSeparatorChars = { '\t' };
            string fullOrganizationsFileName = Path.Combine(dataPath, @"CorporateNameLookup.txt");

            Logger.Write("LoadAuthorizedCorporateNames: Loading data from {0}", fullOrganizationsFileName);
            var corporateNames = new Dictionary<string, string>();

            // Carnegie-Mellon University   n79054102
            // etc., tab-delimited

            using (var reader = new StreamReader(fullOrganizationsFileName))
            { 
                // Check header
                if (reader.ReadLine() == null)
                {
                    throw new Exception($"LoadAuthorizedCorporateNames: Required input file missing or empty: {fullOrganizationsFileName}");
                }
                // else successfully read away the opening header line in the input file
                
                string oneInputLine;
                while ((oneInputLine = reader.ReadLine()) != null)
                {
                    var linePieces = oneInputLine.Split(lineSeparatorChars);
                    if (linePieces.Length == 2)
                    {
                        var readableName = linePieces[0].Trim();
                        var nameID = linePieces[1].Trim();
                        if (readableName.Length > 0 && nameID.Length > 0)
                        {
                            if (corporateNames.ContainsKey(readableName))
                            {
                                Logger.Warning("LoadAuthorizedCorporateNames: Ignoring this redundant organization: {0}", oneInputLine);
                            }
                            else
                            {
                                corporateNames.Add(readableName, nameID);
                            }
                        }
                        else
                        {
                            Logger.Warning("LoadAuthorizedCorporateNames: Ignoring this organization due to empty data: {0}", oneInputLine);
                        }
                    }
                    else
                    {
                        Logger.Warning("LoadAuthorizedCorporateNames: ignoring this organization due to bad format: {0}", oneInputLine); 
                    }
                }                
            }

            Logger.Write("LoadAuthorizedCorporateNames: {0} organizational names loaded.", corporateNames.Count());
            return corporateNames;
        }
    }
}
