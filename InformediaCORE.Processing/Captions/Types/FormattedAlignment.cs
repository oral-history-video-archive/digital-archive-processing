using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using InformediaCORE.Common;
using InformediaCORE.Processing.Gentle.Models;

namespace InformediaCORE.Processing.Captions.Types
{
    /// <summary>
    /// A class containing a cleaned, adjusted, and formatted version
    /// of the alignment data for use by the closed captioner.
    /// </summary>
    public class FormattedAlignment
    {
        #region == CONSTANTS
        /// <summary>
        /// The caption used when there are no spoken words in the transcript.
        /// </summary>
        public const string NO_NARRATION = "(no narration)";
        #endregion CONSTANTS

        /// <summary>
        /// A list of paragraphs and associated timing data.
        /// </summary>
        public readonly List<AlignedParagraph> Paragraphs;

        /// <summary>
        /// Instantiates a FormattedAlignment instance with the given parameters.
        /// </summary>
        /// <param name="paragraphs">A list of paragraphs with alignment data.</param>
        public FormattedAlignment(List<AlignedParagraph> paragraphs)
        {
            Paragraphs = paragraphs;
        }
    }
}
