using System;

namespace InformediaCORE.UpdateDatabase
{
    /// <summary>
    /// Exception raised when values in spreadsheet cannot be parsed.
    /// </summary>
    internal class ParsingException : Exception
    {
        public ParsingException() { }

        public ParsingException(string message) : base(message) { }
    }
}
