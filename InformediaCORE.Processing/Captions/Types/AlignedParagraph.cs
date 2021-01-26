using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InformediaCORE.Processing.Captions.Types
{
    /// <summary>
    /// A paragraph within the larger transcript complete with timing data.
    /// </summary>
    public class AlignedParagraph
    {
        /// <summary>
        /// Start offset of paragraph as it relates to the original transcript.
        /// </summary>
        public int OriginalStartOffset { get; private set; }

        /// <summary>
        /// End offset of the paragraph as it relates to the original transcript.
        /// </summary>
        public int OriginalEndOffset { get; private set; }

        /// <summary>
        /// The text as it relates to the original transcript.
        /// </summary>
        public string OriginalText { get; private set; }

        /// <summary>
        /// Aligned words within the paragraph.
        /// </summary>
        public List<TimedText> Words { get; private set; }

        /// <summary>
        /// Full paragraph text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Duration of paragraph in milliseconds.
        /// </summary>
        public int Duration
        {
            get
            {
                int value = 0;

                if (Words?.Count > 0)
                {
                    value = Words[Words.Count - 1].TimeEnd - Words[0].TimeStart;
                }

                return value;
            }
        }

        /// <summary>
        /// Number of characters in paragraph text.
        /// </summary>
        public int Length
        {
            get
            {
                return (Text?.Length ?? 0);
            }
        }

        /// <summary>
        /// Instantiate a new AlignedParagraph with given start and end offsets.
        /// </summary>
        /// <param name="startOffset">Start offset within original transcript.</param>
        /// <param name="endOffset">End offset within original transcript.</param>
        public AlignedParagraph(int startOffset, int endOffset, string text)
        {
            OriginalStartOffset = startOffset;
            OriginalEndOffset = endOffset;
            OriginalText = Text;

            // Initialize remaining properties
            Text = text;
            Words = new List<TimedText>();
        }
    }
}
