using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;
using InformediaCORE.Common.Database;

namespace InformediaCORE.Processing.Captions.Types
{
    /// <summary>
    /// Contains the text captions for a single segment video along
    /// with methods for exporting in various useful formats.
    /// </summary>
    public class TextCaptions
    {
        /// <summary>
        /// List of cues representing the complete set of captions
        /// </summary>
        public List<CaptionCue> Cues { get; set; }

        /// <summary>
        /// Export caption data as a VTT-formatted closed caption file.
        /// </summary>
        /// <returns>VTT-formated document which can be stored in the database.</returns>
        public string ToVTT()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("WEBVTT");
            sb.Append("\n\n");
            sb.Append("NOTE iCore Captioner v2020.09.28");

            foreach (var cue in Cues)
            {
                sb.Append("\n\n");
                sb.Append($"{FormatTime(cue.TimeStart)} --> {FormatTime(cue.TimeEnd)}");
                foreach(var line in cue.Lines)
                {
                    // Escape illegal characters per:
                    // https://developer.mozilla.org/en-US/docs/Web/API/WebVTT_API
                    var text = line.Text.Replace("&", "&amp;");
                    text = text.Replace("<", "&lt;");
                    text = text.Replace(">", "&gt;");

                    sb.Append("\n");
                    sb.Append($"<v {line.SpeakerID}>{text}");
                }
            }

            return sb.ToString();            
        }

        /// <summary>
        /// Export caption data as transcript timing pairs (tsync).
        /// </summary>
        /// <param name="endOffset">The last char in the transcript.</param>
        /// <param name="endTime">The end time of the segment in milliseconds.</param>
        /// <returns>A TranscriptSync instance populated with timing data.</returns>
        public TranscriptSync ToTSync(int endOffset, int endTime)
        {
            TranscriptSync tsync = new TranscriptSync();

            // First timing pair is always at zero-zero
            tsync.SyncPairs.Add(new TSyncPair(0, 0));

            foreach (var cue in Cues)
            {
                // Add start of caption
                tsync.SyncPairs.Add( new TSyncPair(cue.TranscriptOffsetStart, cue.TimeStart) );
            }

            // The final timing pair is at the very end of the segment.
            tsync.SyncPairs.Add(new TSyncPair(endOffset, endTime));
            return tsync;
        }

        /// <summary>
        /// Exports an index enumerated diagnostic dump for debugging purposes
        /// </summary>
        /// <returns>A plain text document with diagnostic information.</returns>
        public string ToPlainText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("DIAGNOSTIC DUMP");

            for(int i = 0; i < Cues.Count; i++)
            {
                var cue = Cues[i];
                sb.Append("\n\n");
                sb.Append($"cue[{i}] - Duration: {cue.Duration:#####}ms - {FormatTime(cue.TimeStart)} --> {FormatTime(cue.TimeEnd)}");
                for(int j = 0; j < cue.Lines.Count; j++)
                {
                    var line = cue.Lines[j];
                    sb.Append("\n");
                    sb.Append($"  line[{j}]: {line.SpeakerID}:{line.Text}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Instantiates an empty instance of the TextCaptions class.
        /// </summary>
        public TextCaptions()
        {
            Cues = new List<CaptionCue>();
        }

        /// <summary>
        /// Formats the given time as MM:SS.sss (ex: 01:59.123)
        /// </summary>
        /// <param name="ms">Time in milliseconds</param>
        /// <returns>A string containing the formatted time expression.</returns>
        private string FormatTime(int ms)
        {
            double seconds = (double)ms / 1000;
            int minutes = (int)(seconds / 60);
            seconds -= minutes * 60;

            return $"{minutes:00}:{seconds:00.000}";
        }
    }

    /// <summary>
    /// A caption cue which may contain one or more lines of text.
    /// </summary>
    public class CaptionCue
    {
        /// <summary>
        /// Start time in milliseconds
        /// </summary>
        public int TimeStart
        {
            get
            {
                int value = 0;

                if (Lines?.Count > 0)
                {
                    value = Lines[0].TimeStart;
                }

                return value;
            }
        }

        /// <summary>
        /// End time in milliseconds.
        /// </summary>
        public int TimeEnd
        {
            get
            {
                int value = 0;

                if (Lines?.Count > 0)
                {
                    value = Lines[Lines.Count - 1].TimeEnd;
                }

                return value;
            }
        }

        /// <summary>
        /// Total duration of cue in milliseconds.
        /// </summary>
        public int Duration
        {
            get
            {
                return TimeEnd - TimeStart;
            }
        }

        /// <summary>
        /// Returns the start offset of this cue as it relates back to the original transcript.
        /// </summary>
        public int TranscriptOffsetStart
        {
            get
            {
                int value = 0;

                if (Lines?.Count > 0 && Lines[0].Words?.Count > 0)
                {
                    value = Lines[0].Words[0].OriginalStartOffset;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the end offset of this cue as it relates back to the original transcript.
        /// </summary>
        public int TranscriptOffsetEnd
        {
            get
            {
                int value = 0;

                if (Lines?.Count > 0)
                {
                    int lastLine = Lines.Count - 1;
                    if (Lines[lastLine].Words?.Count > 0)
                    {
                        int lastWord = Lines[lastLine].Words.Count - 1;
                        value = Lines[lastLine].Words[lastWord].OriginalEndOffset;
                    }
                }

                return value;
            }
        }

        /// <summary>
        /// A list of timed and speaker-attributed lines of text.
        /// </summary>
        public List<CueText> Lines { get; set; }

        /// <summary>
        /// Instantiates an empty instance of this class.
        /// </summary>
        public CaptionCue()
        {
            Lines = new List<CueText>();
        }
    }

    /// <summary>
    /// A single timed text caption.
    /// </summary>
    public class CueText
    {
        /// <summary>
        /// Label (i.e S1 or S2) identifying the speaker.
        /// </summary>
        public string SpeakerID { get; private set; }

        /// <summary>
        /// Start time in milliseconds.
        /// </summary>
        public int TimeStart
        {
            get
            {
                int value = 0;

                if (Words?.Count > 0)
                {
                    value = Words[0].TimeStart;
                }

                return value;
            }
        }

        /// <summary>
        /// End time in milliseconds.
        /// </summary>
        public int TimeEnd

        { 
            get 
            {
                int value = 0;

                if (Words?.Count > 0)
                {
                    value = Words[Words.Count - 1].TimeEnd;
                }

                return value;
            } 
        }

        /// <summary>
        /// Total duration of caption in milliseconds.
        /// </summary>
        public int Duration
        {
            get
            {
                return TimeEnd - TimeStart;
            }
        }

        /// <summary>
        /// Total length of the caption in characters
        /// </summary>
        public int Length
        {
            get
            {
                int length = 0;

                if (Words?.Count > 0)
                {
                    length = Words[Words.Count - 1].OffsetEnd - Words[0].OffsetStart;
                }

                return length;
            }
        }

        /// <summary>
        /// Full caption text.
        /// </summary>
        public string Text 
        { 
            get
            {
                string text = "";
                foreach(var word in Words)
                {
                    text += word.Text;
                }

                return text.Trim();
            } 
        }

        /// <summary>
        /// The words associated with this caption cue.
        /// </summary>
        public List<TimedText> Words { get; set; }

        /// <summary>
        /// Instantiates a TextCaption instance with the given speakerID.
        /// <param name="speakerID">String identifying the speaker.</param>
        public CueText(string speakerID) : this(speakerID, null) { }

        /// <summary>
        /// Instantiates a TextCaption instance with the given speakerID and list of words.
        /// </summary>
        /// <param name="speakerID">String identifying the speaker.</param>
        /// <param name="words">A list of TimeText (words).</param>
        public CueText(string speakerID, List<TimedText> words)
        {
            SpeakerID = speakerID;
            
            if (words == null)
            {
                Words = new List<TimedText>();
            }
            else
            {
                // Create a new list from the existing list
                Words = new List<TimedText>(words); 
            }
        }
    }
}
