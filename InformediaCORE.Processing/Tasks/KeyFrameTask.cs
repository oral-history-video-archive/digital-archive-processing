using System;
using System.IO;
using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Common.Media;

namespace InformediaCORE.Processing.Tasks
{
    /// <summary>
    /// Extracts a representitive key frame from the segment video and stores it in the database.
    /// </summary>
    public class KeyFrameTask : AbstractTask
    {
        #region Constructors
        /// <summary>
        /// Instantiates an instance of the KeyFrameTask class from the given segment id.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>
        public KeyFrameTask(int segmentID, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segmentID, condition) { }
        
        /// <summary>
        /// Instantiates an instance of the KeyFrameTask class from the given segment name.
        /// </summary>
        /// <param name="segmentName">A valid segment name.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>
        public KeyFrameTask(string segmentName, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segmentName, condition) { }

        /// <summary>
        /// Instantiates an instance of the KeyFrameTask class from the given segment.
        /// </summary>
        /// <param name="segment">A valid segment.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>
        public KeyFrameTask(Segment segment, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segment, condition) { }
        #endregion Constructors

        /// <summary>
        /// Checks that the necessary input requirements are met prior to running the task.
        /// </summary>
        internal override void CheckRequirements()
        {
            if (string.IsNullOrEmpty(Segment.MediaPath))
                throw new TaskRequirementsException("Required media path is null or empty.");

            if (!File.Exists(Segment.MediaPath))
                throw new TaskRequirementsException($"Could not find required input file {Segment.MediaPath}.");
        }

        /// <summary>
        /// Purges prior KeyFrameTask results for the associated segment from the database.
        /// </summary>
        internal override void Purge()
        {
            Logger.Write("Setting segment keyframe to null.");
            Segment.Keyframe = null;
            Database.UpdateSegment(Segment);

            // ============================================================
            // Reload the updated segment from the database
            Segment = Database.GetSegment(SegmentID);
        }

        /// <summary>
        /// Executes that actual key frame extraction logic.
        /// </summary>
        internal override void Run()
        {
            // Post log update
            Logger.Write("Extracting key frame for segment {0} '{1}'.", Segment.SegmentID, Segment.SegmentName);

            // Attempt to extract key frame
            Byte[] keyframe = MediaTools.GetFrame(Segment.MediaPath, Segment.Duration.GetValueOrDefault() / 2);  // Midpoint of video in milliseconds.

            // ============================================================
            Segment.Keyframe = keyframe ?? throw new TaskRunException("Failed to extract key frame for specified segment.");

            Logger.Write("Updating database.");
            if (Database.UpdateSegment(Segment) == false)
                throw new TaskRunException("Failed to write keyframe to database.");
        }
    }
}
