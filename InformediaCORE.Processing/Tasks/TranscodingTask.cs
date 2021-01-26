using System;
using System.IO;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Common.Media;

namespace InformediaCORE.Processing.Tasks
{
    /// <summary>
    /// Transcodes the movie-level video into smaller web-ready segment videos.
    /// </summary>
    /// <remarks>
    /// History:
    /// 2013-03-11: bm3n
    ///     Fixed A bug where NULL or invalid MediaPath would prevent TranscodingTask from running even with RunAlways condition set to True.
    /// </remarks>
    public class TranscodingTask : AbstractTask
    {
        private int _targetWidth;
        private int _targetHeight;

        #region Constructors
        /// <summary>
        /// Instantiates an instance of the TranscodingTask class from the given segment id.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>
        public TranscodingTask(int segmentID, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segmentID, condition) { }

        /// <summary>
        /// Instantiates an instance of the TranscodingTask class from the given segment name.
        /// </summary>
        /// <param name="segmentName">A valid segment name.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>
        public TranscodingTask(string segmentName, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segmentName, condition) { }

        /// <summary>
        /// Instantiates an instance of the TranscodingTask class from the given segment.
        /// </summary>
        /// <param name="segment">A valid segment.</param>
        /// <param name="condition">Specifies whether the task should be run regardless of previous run condition.</param>
        public TranscodingTask(Segment segment, RunConditionValue condition = RunConditionValue.AsNeeded) : base(segment, condition) { }
        #endregion Constructors

        /// <summary>
        /// Checks that the necessary input requirements are met prior to running the task.
        /// </summary>
        internal override void CheckRequirements()
        {
            Movie movie = Database.GetMovie(Segment.MovieID);

            if (movie == null)
                throw new TaskRequirementsException($"Could not retrieve parent movie with id={Segment.MovieID}.");

            if (!File.Exists(movie.MediaPath))
                throw new TaskRequirementsException($"Could not find required input file {movie.MediaPath}.");

            var mediaInfo = MediaTools.MediaInfo(movie.MediaPath);
            var resolutionMapping = GetResolutionMapping(mediaInfo);
            if (resolutionMapping == null)
                throw new TaskRequirementsException($"No mapping exists for input file resolution {mediaInfo.Width}x{mediaInfo.Height}.");

            _targetWidth = resolutionMapping.Target.Width;
            _targetHeight = resolutionMapping.Target.Height;
        }

        /// <summary>
        /// Purges prior TranscodingTask results for the associated 
        /// segment from the database and file system.
        /// </summary>
        internal override void Purge()
        {
            // Only delete the existing media if the run condition is Always.  Sometimes we already
            // have valid media and all we need is to get the metadata into the database.
            if (File.Exists(Segment.MediaPath) && RunCondition == RunConditionValue.Always)
            {
                Logger.Write("Deleting existing segment video {0}.", Segment.MediaPath);
                File.Delete(Segment.MediaPath);
            }

            // ============================================================
            Logger.Write("Setting media related fields to null.");
            Segment.MediaPath = null;
            Segment.Duration = null;
            Segment.FPS = null;
            Segment.Height = null;
            Segment.Width = null;

            Database.UpdateSegment(Segment);

            // ============================================================
            // Reload the updated segment from the database
            Segment = Database.GetSegment(SegmentID);
        }

        /// <summary>
        /// Runs the video transcoding task for the associated segment.
        /// </summary>
        internal override void Run()
        {
            Movie movie = Database.GetMovie(Segment.MovieID);

            // ============================================================
            // Determine output directory and create it.
            string transcodePath = Path.Combine(Settings.BuildPath, "WebVideo", movie.MovieName);

            if (!Directory.Exists(transcodePath))
                Directory.CreateDirectory(transcodePath);

            if (!Directory.Exists(transcodePath))
                throw new TaskRunException($"Failed to create output directory {transcodePath}.");

            string segmentFile = Path.Combine(transcodePath, $"{Segment.SegmentName}.mp4");

            // ============================================================
            // Transcode Segment Video.
            if (File.Exists(segmentFile) && RunCondition != RunConditionValue.Always)
            {
                Logger.Write("Keeping existing segment video {0}.", segmentFile);
            }
            else
            {
                // Typically Purge() will remove prior instances of the video, but under
                // certain circumstances the prior encoding path may not be the same as the
                // current encoding path so we need to be certain there is nothing here to
                // get in the way.
                if (File.Exists(segmentFile) && RunCondition == RunConditionValue.Always)
                {
                    Logger.Write("Deleting existing segment video {0}.", segmentFile);
                    File.Delete(segmentFile);
                }

                Logger.Write("Transcoding segment video {0}", segmentFile);
                MediaTools.EncodeSegment(movie.MediaPath, Segment.StartTime, Segment.EndTime, _targetWidth, _targetHeight, segmentFile);

                // Does the output file exist?
                if (!File.Exists(segmentFile))
                    throw new TaskRunException($"Failed to produce expected output file {segmentFile}.");
            }


            // ============================================================
            // Analyze the segment's video duration.
            Logger.Write("Analyzing the segment video {0}", segmentFile);

            MediaInfo segmentInfo = MediaTools.MediaInfo(segmentFile);
            Logger.Write("Duration={0}ms; Frames={1}; FPS={2}; Width={3}; Heigth={4}",
                segmentInfo.Duration, segmentInfo.Frames, segmentInfo.FPS, segmentInfo.Width, segmentInfo.Height);

            int expectedDuration = (Segment.EndTime - Segment.StartTime);
            int difference = segmentInfo.Duration - expectedDuration;

            Logger.Write("Expected Duration={0}ms; Allowable Difference={1}ms; Actual Difference={2}ms", expectedDuration, Settings.TranscodingTask.MaximumAllowableDeltaMS, difference);

            // Is the duration correct?
            if (Math.Abs(difference) > Settings.TranscodingTask.MaximumAllowableDeltaMS)
                throw new TaskRunException($"Transcoded video file is too {((difference < 0) ? "short" : "long")}.");

            Logger.Write("Segmented video generated successfully.");

            // ============================================================
            Logger.Write("Updating database.");
            Segment.MediaPath = segmentInfo.MediaFile;
            Segment.Duration = segmentInfo.Duration;
            Segment.FPS = segmentInfo.FPS;
            Segment.Height = segmentInfo.Height;
            Segment.Width = segmentInfo.Width;

            if (Database.UpdateSegment(Segment) == false)
                throw new TaskRunException("Failed to write segment media info to database.");
        }

        /// <summary>
        /// Get the resolution map for the given input resolution.
        /// </summary>
        /// <param name="mediaInfo">The media info of the source file.</param>
        /// <returns>A valid resolution mapping on success; null if input resolution is unknown.</returns>
        private ResolutionMapping GetResolutionMapping(MediaInfo mediaInfo)
        {
            ResolutionMapping resolutionMapping = null;
            
            foreach (var rm in Settings.TranscodingTask.ResolutionMappings)
            {
                if (mediaInfo.Width == rm.Source.Width && mediaInfo.Height == rm.Source.Height)
                {
                    resolutionMapping = rm;
                    break;
                }
            }

            return resolutionMapping;
        }
    }
}
