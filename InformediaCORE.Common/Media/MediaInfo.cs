namespace InformediaCORE.Common.Media
{
    /// <summary>
    /// Contains information regarding basic properties describing a video.
    /// </summary>
    public class MediaInfo
    {
        /// <summary>
        /// The fully qualified path to the file described by this MediaInfo instance.
        /// </summary>
        public string MediaFile { get; }

        /// <summary>
        /// Duration of media in milliseconds.
        /// </summary>
        public int Duration { get; }

        /// <summary>
        /// Duration of media in frames.
        /// </summary>
        public int Frames { get; }

        /// <summary>
        /// Frames per second.
        /// </summary>
        public double FPS { get; }

        /// <summary>
        /// Pixel height of video frame.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Pixel width of video frame.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Instantiates an instance of the MediaInfo class.
        /// </summary>
        /// <param name="mediaFile">Fully qualified path to media file.</param>
        /// <param name="duration">Duration of media in milliseconds.</param>
        /// <param name="frames">Duration of the media in frames.</param>
        /// <param name="fps">Frames per second.</param>
        /// <param name="width">Pixel width of video frame.</param>
        /// <param name="height">Pixel height of video frame.</param>
        public MediaInfo(string mediaFile, int duration, int frames, double fps, int width, int height)
        {
            MediaFile = mediaFile;
            Duration = duration;
            Frames = frames;
            FPS = fps;
            Width = width;
            Height = height;
        }
    }
}
