namespace InformediaCORE.Processing.NamedEntities
{
    /// <summary>
    /// Specifies the type of named entity.
    /// </summary>
    public enum EntityType
    {
        Unset = 0,
        Person,
        Loc,
        Org,
        Year,
        YearPerhaps,
        SomethingToIgnore,
        SomethingElse
    }

    /// <summary>
    /// Using a static class avoids need to cast enumeration to int and allows more flexibility
    /// in deriving child classes.
    /// </summary>
    public static class EntityConfidence
    {
        public const int None = 0;
        public const int Some = 1;
        public const int Good = 2;
        public const int Better = 3;
    }

    /// <summary>
    /// A singlular Named Entity including confidence and contextualized information.
    /// </summary>
    public class NamedEntity
    {
        /// <summary>
        /// The detected entity text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Often longer version of "Value", not necessarily in a canonical form
        /// </summary>
        public string ContextualizedText { get; set; }

        /// <summary>
        /// Character offset relative to story transcript.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// Length of entity text.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Enumeration specifying the type of entity as detected by NER.
        /// </summary>
        public EntityType Type { get; set; }

        /// <summary>
        /// Might help with location/organization disambiguation, as we get GPE, LOC, FAC 
        /// </summary>
        public string SpacyType { get; set; }

        /// <summary>
        /// True if entity was detected by both spaCy NLP and Stanford NER.
        /// </summary>
        public bool ReceivedDuelCoverage { get; set; } = false;

        /// <summary>
        /// Hueristic confidence level
        /// </summary>
        public int Confidence { get; set; }
    }
}
