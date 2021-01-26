using System;
using System.Collections.Generic;
using System.Text;

namespace DocToXMLCleanup
{
    public class ConfigSettings
    {
        public int UseHistoryMakersConventions = 0;
        public int AllowFrameCountInTimeCode = 1;
        public string WorldsPartitionsXMLFilename = "";
        public string PortraitPath = "";
        public string VideoPath = "";
        public int MinLegalSegmentLengthInSecs = 0;
        public int MaxLegalSegmentLengthInSecs = 0;
    }
}
