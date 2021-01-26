namespace InformediaCORE.Common.Database
{
    /// <summary>
    /// The list of valid task state values.
    /// </summary>
    public enum TaskStateValue
    {
        Complete = 'C',
        Pending = 'P',
        Queued = 'Q',
        Failed = 'F',
        Running = 'R',
        Unknown = 'U'
    }

    /// <summary>
    /// The list of valid segment ready state values.
    /// </summary>
    public enum ReadyStateValue
    {
        Ready = 'Y',
        Failed = 'F',
        NotReady = 'N'
    }

    /// <summary>
    /// The integers used by the Named Entity Recognizer to identify the source field of an entity.
    /// </summary>
    public enum GeoFieldID
    {
        Transcript = 1,
        Title = 2
    }

    /// <summary>
    /// The list of valid publishing phases for Collections and Sessions
    /// </summary>
    public enum PublishingPhase
    {
        /// <summary>
        /// Draft phase - processing incomplete.
        /// </summary>
        Draft = 'D',
        /// <summary>
        /// Review phase - processing complete, ready for QA review.
        /// </summary>
        Review = 'R',
        /// <summary>
        /// Published phase - passed QA review and published to production site.
        /// </summary>
        Published = 'P'
    }

    /// <summary>
    /// Defines valid values for NamedEntity types.
    /// </summary>
    public static class NamedEntityType
    {
        public const string Year = "Year";
        public const string Decade = "Decade";
        public const string Organization = "Organization";
        public const string State = "USState";
        public const string Country = "Country";
    }

    public static class PublishingPhaseExtensions
    {
        /// <summary>
        /// Compares sort order of two PublishingPhase enumeration values.
        /// <param name="a">The left-hand argument in the comparison</param>
        /// <param name="b">The right-hand argument in the comparison</param>
        /// <returns>-1 if a less than b; 0 if a equals b; 1 if a gt b</returns>
        /// <remarks>
        /// Using IComparer.Compare method as standard for output values.
        /// </remarks>
        public static int Compare(this PublishingPhase a, PublishingPhase b)
        {
            var valA = Ordinal(a);
            var valB = Ordinal(b);

            return valA.CompareTo(valB);
        }

        /// <summary>
        /// Compares sort order of a PublishingPhase enumeration value to a char.
        /// </summary>
        /// <param name="a">The left-hand argument in the comparison.</param>
        /// <param name="b">The right-hand argument in the comparison.</param>
        /// <returns>-1 if a less than b; 0 if a equals b; 1 if a gt b</returns>
        public static int Compare(this PublishingPhase a, char b)
        {
            return Compare(a, (PublishingPhase)b);
        }

        /// <summary>
        /// Compares sort order of a char to a PublishingPhase enumeration value.
        /// </summary>
        /// <param name="a">The left-hand argument in the comparison.</param>
        /// <param name="b">The right-hand argument in the comparison.</param>
        /// <returns>-1 if a less than b; 0 if a equals b; 1 if a gt b</returns>
        public static int Compare(this char a, PublishingPhase b)
        {
            return Compare((PublishingPhase)a, b);
        }

        /// <summary>
        /// Assigns a numeric value to the enumeration for comparison purposes.
        /// </summary>
        /// <param name="p">The char value to compare</param>
        /// <returns>An integer in the range of 1 to 3 inclusive on success; -1 otherwise.</returns>
        private static int Ordinal(PublishingPhase p)
        {
            switch (p)
            {
                case PublishingPhase.Draft:
                    return 1;
                case PublishingPhase.Review:
                    return 2;
                case PublishingPhase.Published:
                    return 3;
                default:
                    throw new System.ArgumentException($"Given value '{p}' is not a valid PublishingPhase enumeration.");
            }
        }


    }
}
