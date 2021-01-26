using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing.NamedEntities;
using Newtonsoft.Json;

namespace InformediaCORE.Processing.Tasks
{
    /// <summary>
    /// Resolves named entities through combined analysis of spaCy NLP and Stanford NER results.
    /// </summary>
    public class EntityResolutionTask : AbstractTask
    {
        private string spacyFile;
        private string stanfordFile;
        
        #region Constructors
        /// <summary>
        /// Instantiates an instance of the EntityResolutionTask class from the given segment id.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>

        public EntityResolutionTask(int segmentID, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segmentID, condition) { }

        /// <summary>
        /// Instantiates an instance of the EntityResolutionTask class from the given segment name.
        /// </summary>
        /// <param name="segmentName">A valid segment name.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>

        public EntityResolutionTask(string segmentName, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segmentName, condition) { }

        /// <summary>
        /// Instantiates an instance of the EntityResolutionTask class from the given segment.
        /// </summary>
        /// <param name="segment">A valid segment.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>
        public EntityResolutionTask(Segment segment, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segment, condition) { }
        #endregion Constructors

        #region Internal Overrides
        /// <summary>
        /// Checks that the necessary input requirements are met prior to running the task.
        /// </summary>
        internal override void CheckRequirements()
        {
            if (Segment.TranscriptText == null)
                throw new TaskRequirementsException("Transcript is null.");

            if (Segment.TranscriptText == String.Empty)
                throw new TaskRequirementsException("Transcript is empty.");

            spacyFile = Path.Combine(DataPath, $"{SegmentID}.spacy.txt");
            if (!File.Exists(spacyFile))
                throw new TaskRequirementsException($"Could not find required input file {spacyFile}.");

            stanfordFile = Path.Combine(DataPath, $"{SegmentID}.stanford.txt");
            if (!File.Exists(stanfordFile))
                throw new TaskRequirementsException($"Could not find required input file {stanfordFile}.");
        }

        /// <summary>
        /// Purges prior EntityResolutionTask results for the associated segment from the database.
        /// </summary>
        internal override void Purge()
        {
            Logger.Write("Deleting associated entities from the database.");
            Database.DeleteNamedEntities(SegmentID);

            // ============================================================
            // Reload the updated segment from the database
            Segment = Database.GetSegment(SegmentID);
        }

        /// <summary>
        /// Resolves named entities to canonical values.
        /// </summary>
        internal override void Run()
        {
            //////////////////////////////////////////////////
            ///// DATE RESOLUTION
            var dates = DateResolver.Resolve(spacyFile, Segment.TranscriptText);
            if (dates != null)
            {
                int datesInserted = 0;
                foreach (var date in dates)
                {
                    if (Regex.IsMatch(date.Value, @"[1|2]\d\d\ds?"))
                    {
                        datesInserted++;
                        if (date.Value.Length == 4)
                        {
                            Database.InsertNamedEntity(new Common.Database.NamedEntity
                            {
                                SegmentID = this.SegmentID,
                                Type = NamedEntityType.Year,
                                Value = date.Value
                            });
                        }

                        Database.InsertNamedEntity(new Common.Database.NamedEntity
                        {
                            SegmentID = this.SegmentID,
                            Type = NamedEntityType.Decade,
                            Value = $"{date.Value.Substring(0, 3)}0"
                        });
                    }
                }
                Logger.Write("{0} date entities resolved.", datesInserted);
            }

            //////////////////////////////////////////////////
            ///// PRE-PROCESS             
            var pass2Entities = StanfordNERPolisher.Polish(stanfordFile, Segment.TranscriptText);
            if (pass2Entities == null) return;

            var pass3Entities = SpacyNERPolisher.Polish(spacyFile, Segment.TranscriptText, pass2Entities);
            if (pass3Entities == null) return;

            //////////////////////////////////////////////////
            ///// ORGANIZATION RESOLUTION
            var organizations = OrganizationResolver.Resolve(pass3Entities, out var _);
            if (organizations != null)
            {
                foreach (var org in organizations)
                {
                    Database.InsertNamedEntity(new Common.Database.NamedEntity
                    {
                        SegmentID = this.SegmentID,
                        Type = NamedEntityType.Organization,
                        Value = org.LOCNameID
                    });
                }
                Logger.Write("{0} organizational entities resolved.", organizations.Count);
            }

            //////////////////////////////////////////////////
            ///// LOCATION RESOLUTION
            var domesticLocations = DomesticLocationResolver.Resolve(pass3Entities, out var pass4UnresolvedLocations);
            if (domesticLocations != null)
            {
                foreach (var loc in domesticLocations)
                {
                    Database.InsertNamedEntity(new Common.Database.NamedEntity
                    {
                        SegmentID = this.SegmentID,
                        Type = NamedEntityType.State,
                        Value = DomesticLocationResolver.CodeToAlpha(loc.StateCode)
                    });
                }
                Logger.Write("{0} domestic locations resolved.", domesticLocations.Count);
            }

            //////////////////////////////////////////////////
            ///// LOCATION RESOLUTION
            var internationalLocations = InternationalLocationResolver.Resolve(pass4UnresolvedLocations);
            if (internationalLocations != null)
            {
                foreach (var country in internationalLocations)
                {
                    Database.InsertNamedEntity(new Common.Database.NamedEntity
                    {
                        SegmentID = this.SegmentID,
                        Type = NamedEntityType.Country,
                        Value = country.CountryCode.ToString()
                    });
                }
                Logger.Write("{0} international locations resolved.", internationalLocations.Count);
            }
        }
        #endregion Internal Overrides
    }
}
