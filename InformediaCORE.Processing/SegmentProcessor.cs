using System;
using System.Linq;

using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing.Database;
using InformediaCORE.Processing.Tasks;

namespace InformediaCORE.Processing
{
    /// <summary>
    /// Runs segments through 
    /// </summary>
    public class SegmentProcessor
    {
        private readonly DataAccessExtended _db = new DataAccessExtended();

        /// <summary>
        /// If true, application will begin graceful shutdown procedure.
        /// </summary>
        public bool Halt { get; set; } = false;

        /// <summary>
        /// Initializes an instance of the SegmentProcessor class.
        /// </summary>
        public SegmentProcessor() { }

        #region =========================      PUBLIC PROPERTIES       =========================
        /// <summary>
        /// Runs TranscodingTask if true.
        /// </summary>
        public bool RunVideoTranscoder { get; set; }

        /// <summary>
        /// Runs KeyFrameTask if true.
        /// </summary>
        public bool RunKeyFrameExtractor { get; set; }

        /// <summary>
        /// Runs AlignmentTask if true.
        /// </summary>
        public bool RunForcedAligner { get; set; }

        /// <summary>
        /// Runs CaptioningTask if true.
        /// </summary>
        public bool RunCaptionGenerator { get; set; }

        /// <summary>
        /// Runs the SpaCy Natural Language Processor if true.
        /// </summary>
        public bool RunSpacyNLP { get; set; }

        /// <summary>
        /// Runs the Stanford Named Entity Recognizer if true.
        /// </summary>
        public bool RunStanfordNER { get; set; }

        /// <summary>
        /// Runs EntityResolutionTask if true.
        /// </summary>
        public bool RunNamedEntityRecognizer { get; set; }

        /// <summary>
        /// If true, specified tasks will be run regardless of prior state.
        /// </summary>
        public bool ForceRerun { get; set; }
        #endregion ======================      PUBLIC PROPERTIES       =========================

        #region =========================        PUBLIC METHODS        =========================
        /// <summary>
        /// Process segment specified by given segment name.
        /// </summary>
        /// <param name="segmentName">A valid segment name.</param>
        public void ProcessSegment(string segmentName)
        {
            var segment = _db.GetSegment(segmentName);

            if (segment == null)
            {
                Logger.Error("Could not retrieve segment with name = \"{0}\". Processing aborted.", segmentName);
            }
            else
            {
                ProcessSegment(segment);
            }
        }

        /// <summary>
        /// Process segment specified by given segment id.
        /// </summary>
        /// <param name="segmentID">A valid segment id</param>
        public void ProcessSegment(int segmentID)
        {
            var segment = _db.GetSegment(segmentID);
            
            if (segment == null)
            {
                Logger.Error("Could not retrieve segment with id = \"{0}\". Processing aborted.", segmentID);
            }
            else
            {
                ProcessSegment(segment);
            }
        }

        /// <summary>
        /// Process all segments in the work queue.
        /// </summary>
        public void ProcessSegments()
        {
            while (!Halt)
            {
                var segment = _db.GetNextSegmentToProcess(ForceRerun);

                if (segment == null)
                    break;  // No more segments to process

                ProcessSegment(segment);
            }
        }
        #endregion ======================        PUBLIC METHODS        =========================

        /// <summary>
        /// Process the given segment.
        /// </summary>
        /// <remarks>
        /// This is the workhorse that performs all the processing logic.  
        /// </remarks>
        /// <param name="segment">The DataContext Segment object to be processed.</param>
        private void ProcessSegment(Segment segment)
        {
            var semaphores = SemaphoreManager.Instance;

            if (!semaphores.Request(segment.SegmentID))
            {
                Logger.Error("Could not obtain lock on segment {0}, processing aborted.", segment.SegmentID);
                return;
            }

            try
            {
                var taskMan = new TaskManager();

                Logger.Write("Segment {0} \"{1}\" locked for processing.", segment.SegmentID, segment.SegmentName);

                // Convert ForceRerun to a proper RunStateValue
                var runCondition =
                    (ForceRerun) ? AbstractTask.RunConditionValue.Always : AbstractTask.RunConditionValue.AsNeeded;

                // Generate web-ready media file
                if (RunVideoTranscoder)
                {
                    Logger.Write("--------------------------------------------------------------------------------");
                    taskMan.Run(new TranscodingTask(segment.SegmentID, runCondition));
                }

                // Generate keyframe
                if (RunKeyFrameExtractor)
                {
                    Logger.Write("--------------------------------------------------------------------------------");
                    taskMan.Run(new KeyFrameTask(segment.SegmentID, runCondition));
                }

                // Align transcript
                if (RunForcedAligner)
                {
                    Logger.Write("--------------------------------------------------------------------------------");
                    taskMan.Run(new AlignmentTask(segment.SegmentID, runCondition));
                }

                // Generate Closed Captions and TranscriptSync
                if (RunCaptionGenerator)
                {
                    Logger.Write("--------------------------------------------------------------------------------");
                    taskMan.Run(new CaptioningTask(segment.SegmentID, runCondition));
                }

                // Detect named entities via spaCy NLP
                if (RunSpacyNLP)
                {
                    Logger.Write("--------------------------------------------------------------------------------");
                    taskMan.Run(new SpacyTask(segment.SegmentID, runCondition));
                }

                // Detect named entities via Stanford NER
                if (RunStanfordNER)
                {
                    Logger.Write("--------------------------------------------------------------------------------");
                    taskMan.Run(new StanfordTask(segment.SegmentID, runCondition));
                }

                // Extract named entities
                if (RunNamedEntityRecognizer)
                {
                    Logger.Write("--------------------------------------------------------------------------------");
                    taskMan.Run(new EntityResolutionTask(segment.SegmentID, runCondition));
                }

                // Update the Ready state
                Logger.Write("--------------------------------------------------------------------------------");
                Logger.Write("Segment Ready flag set to: {0}", UpdateSegmentReadyState(segment.SegmentID));

                // Publish to review site as needed
                AutoPublisher.CheckSession(segment.SessionID);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
            finally
            {
                semaphores.Release(segment.SegmentID);
                Logger.Write("Semaphore lock on segment {0} released.", segment.SegmentID);
                Logger.Write("Processing Complete.");
                Logger.Write("--------------------------------------------------------------------------------");
            }
        }

        /// <summary>
        /// Updates the given segment's Ready state flag based on the current processing status.
        /// </summary>
        /// <param name="segmentID">The id of the segment currently being processed.</param>
        /// <returns>The updated ReadyStateValue.</returns>
        private ReadyStateValue UpdateSegmentReadyState(int segmentID)
        {
            var segment =  _db.GetSegment(segmentID);
            var taskStates = _db.GetTaskStates(segmentID);

            if (taskStates.Values.Any(v => v == (char)TaskStateValue.Failed))
            {
                segment.Ready = (char)ReadyStateValue.Failed;
            }
            else if (taskStates.Values.All(v => v == (char)TaskStateValue.Complete))
            {
                segment.Ready = (char)ReadyStateValue.Ready;
            }
            else
            {
                segment.Ready = (char)ReadyStateValue.NotReady;
            }
                    
            _db.UpdateSegment(segment);

            return (ReadyStateValue)segment.Ready;
        }
    }
}
