using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InformediaCORE.Processing.Gentle.Models
{
    /// <summary>
    /// A single word and associated aligment data.
    /// </summary>
    public class WordResult
    {
        /// <summary>
        /// Aligment outcome, one of:
        /// success | not-found-in-audio | not-found-in-transcript
        /// </summary>
        public string Case { get; set; }

        /// <summary>
        /// Audio end time in seconds.
        /// </summary>
        public float End { get; set; }

        /// <summary>
        /// End character offset within transcript.
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// Audio start time in seconds.
        /// </summary>
        public float Start { get; set; }

        /// <summary>
        /// Start character offset within transcript.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// Aligned word as it appears in the transcript.
        /// </summary>
        public string Word { get; set; }
    }
}
