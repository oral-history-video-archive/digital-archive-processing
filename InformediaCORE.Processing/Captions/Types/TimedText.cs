using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InformediaCORE.Processing.Captions.Types
{
    /// <summary>
    /// Timed sections within a paragraph.
    /// </summary>
    public class TimedText
    {
        /// <summary>
        /// Backing store for OffsetStart field.
        /// </summary>
        private int? _offsetStart = null;

        /// <summary>
        /// Backing store for OffsetEnd field.
        /// </summary>
        private int? _offsetEnd = null;

        /// <summary>
        /// Interpolation outcome, one of: aligned | unaligned | ignore
        /// </summary>
        public string Case { get; set; }

        /// <summary>
        /// Starting character offset relative to containing paragraph.
        /// </summary>
        public int OffsetStart 
        {
            get 
            { 
                return _offsetStart ?? 0; 
            }

            set 
            {
                if ( _offsetStart == null) { OriginalStartOffset = value; } // Store intial value
                _offsetStart = value;
            }
        }

        /// <summary>
        /// Ending character offset relative to containing paragraph.
        /// </summary>
        public int OffsetEnd 
        { 
            get
            {
                return _offsetEnd ?? 0;
            } 

            set
            {
                if (_offsetEnd == null ) { OriginalEndOffset = value;  }    // Store intial value
                _offsetEnd = value;
            } 
        }

        /// <summary>
        /// Character length of text.
        /// </summary>
        public int Length
        {
            get
            {
                return OffsetEnd - OffsetStart;
            }
        }

        /// <summary>
        /// Start time in milliseconds relative to the full segment video.
        /// </summary>
        public int TimeStart { get; set; }

        /// <summary>
        /// End time in milliseconds relative to the full segment video.
        /// </summary>
        public int TimeEnd { get; set; }

        /// <summary>
        /// Duration in milliseconds
        /// </summary>
        public int Duration 
        { 
            get
            {
                return TimeEnd - TimeStart;
            }
        }

        /// <summary>
        /// All text (word, punctuation, and whitespace) from the end of the
        /// prior word (or start of paragraph) up to the beginning of the next 
        /// word (or end of paragraph).
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// One of: no-narration | word
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The start offset as it relates back to the original transcript.
        /// </summary>
        public int OriginalStartOffset { get; private set; }

        /// <summary>
        /// The end offset as it relates back to the original transcript.
        /// </summary>
        public int OriginalEndOffset { get; private set; }
    }
}
