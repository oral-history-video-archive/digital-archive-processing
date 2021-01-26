using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace InformediaCORE.Common.Media
{
    /// <summary>
    /// Tools for analyzing and producing media.
    /// </summary>
    /// <remarks>
    /// History
    /// 2019-Dec-05: bm3n
    ///     Updated FFmpeg to latest official build with modernized encoding parameters.
    /// 2013-Aug-06: bm3n
    ///     Updated FFmpeg parameters in transcode to coincide with new FFmpeg binary and profile settings
    ///     for iOS/iPad compatibility.  Need for qt-faststart eliminated by new FFmpeg so WebOptimize was
    ///     removed as well. Changed MediaInfo to use FFprobe instead of FFmpeg.
    /// 2013-Feb-03: bm3n
    ///     Changed GetFrame to accept milliseconds instead of seconds to be consistent with other media methods.
    ///     Fixed potential bug in EncodeSegment which may have truncated start and duration due to impicit conversion to int.
    ///     Updated FFmpeg parameters in Transcode to improve seeking accuracy.
    ///     Fixed bug in MediaInfo which caused duration to come up short.
    /// </remarks>
    public static class MediaTools
    {
        /// <summary>
        /// Use US-English as the culture for all globalized functions.
        /// </summary>
        private static readonly CultureInfo en_US = new CultureInfo("en-US");

        private static readonly string ffmpegPath = Config.Settings.Current.ExternalTools.FFmpegPath;

        /// <summary>
        /// Uses FFmpeg to derive duration, framerate, width and height of the given video.
        /// </summary>
        /// <param name="mediaFile">The fully qualified path and filename of the video to analyze.</param>
        /// <returns>A MediaInfo object containing the results of the analysis.</returns>
        public static MediaInfo MediaInfo(string mediaFile)
        {
            // Default values
            var milliseconds = 0;
            var width = 0;
            var height = 0;
            double fps = 0;

            string ffmpegOutput = "";

            // Run FFmpeg and wait for completion
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(ffmpegPath, "ffprobe.exe"),
                    Arguments = $"-i \"{mediaFile}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                if (process.Start())
                {
                    // For whatever reason, FFmpeg outputs the valuable
                    // information to StdErr, go figure.
                    ffmpegOutput = process.StandardError.ReadToEnd();

                    process.WaitForExit();
                    process.Close();
                }
            }

            // Get duration in form HH:MM:SS.SS and convert to milliseconds.
            var regex = new Regex("[D|d]uration:.((\\d|:|\\.)*)");
            var match = regex.Match(ffmpegOutput);

            if (match.Success)
            {
                var duration = match.Groups[1].Value;
                var fields = duration.Split(':', '.');
                if (fields.Length == 4)
                {
                    var timespan = new TimeSpan(0, Convert.ToInt16(fields[0], en_US), Convert.ToInt16(fields[1], en_US), Convert.ToInt16(fields[2], en_US), Convert.ToInt16(fields[3], en_US) * 10);
                    milliseconds = (int)timespan.TotalMilliseconds;
                }
            }

            // Get width and height.
            regex = new Regex("(\\d{2,3})x(\\d{2,3})");
            match = regex.Match(ffmpegOutput);

            if (match.Success)
            {
                _ = int.TryParse(match.Groups[1].Value, out width);
                _ = int.TryParse(match.Groups[2].Value, out height);
            }

            // Get fps from tbr value.
            regex = new Regex("(\\d*\\.?\\d*)\\s*tbr");
            match = regex.Match(ffmpegOutput);

            if (match.Success)
            {
                _ = double.TryParse(match.Groups[1].Value, out fps);
            }

            // Estimate frames based on duration and fps.
            var frames = (int)(milliseconds * fps / 1000);

            // And finally the output
            return  new MediaInfo(mediaFile, milliseconds, frames, fps, width, height);
        }

        /// <summary>
        /// Extract the frame at the time specified and return it as a JPEG image.
        /// </summary>
        /// <param name="sourceFile">Fully qualified path to the source video file.</param>
        /// <param name="milliseconds">Number of milliseconds into the video where the desired image exists.</param>
        /// <returns>A byte array representing a JPEG image.</returns>
        public static byte[] GetFrame(string sourceFile, int milliseconds)
        {
            // Path to temp file - use png to preserve image quality.
            var tempFile = Utilities.GetTemporaryFilename(sourceFile, ".png");

            // WARNING: -ss must come before -i or FFMpeg will buffer overflow on large start time values.
            var arguments =  $"-ss {milliseconds / 1000.00D} -i \"{sourceFile}\" -f image2 -r 1 -vframes 1 -y \"{tempFile}\"";

            byte[] frame = null;

            try
            {
                // Use FFMpeg to extract a frame to a temp file.
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                        Arguments = arguments,
                        UseShellExecute = false
                    };

                    if (process.Start())
                    {
                        process.WaitForExit();
                        process.Close();

                        // Return the byte array comprising the data.
                        frame = LoadImageAsJpeg(tempFile);
                    }
                }

            }
            finally
            {
                // Clean-up temp file regardless of success or failure.
                File.Delete(tempFile);
            }

            return frame;
        }

        /// <summary>
        /// Serializes the given image as a database compatible byte array.
        /// </summary>
        /// <param name="image">The image to serialize.</param>
        /// <returns>A LINQ compatible array of bytes.</returns>
        public static System.Data.Linq.Binary ImageToLinqBinary(Image image)
        {
            if (image == null) return null;

            // Refer to XmlImporter.LoadImageAsJpeg as well as StackOverflow:
            // http://stackoverflow.com/questions/3548401/how-to-save-image-in-database-using-c-sharp
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, image.RawFormat);
                ms.Seek(0, SeekOrigin.Begin);
                int size = (int)ms.Length;
                byte[] bytes = new byte[size];
                ms.Read(bytes, 0, size);
                return (System.Data.Linq.Binary)bytes;
            }
        }

        /// <summary>
        /// Deserializes a Linq.Binary array in to a System.Drawing.Image.
        /// </summary>
        /// <param name="imagedata">The binary data to be deserialized.</param>
        /// <returns>A System.Drawing.Image instance.</returns>
        public static Image LinqBinaryToImage(System.Data.Linq.Binary imagedata)
        {
            // Loading the image takes some work, refer to StackOverflow
            // http://stackoverflow.com/questions/19733451/image-from-database-display-in-picutrebox-using-linq-query
            if (imagedata == null) return null;

            using (var ms = new MemoryStream(imagedata.ToArray()))
            using (var image = Image.FromStream(ms))
            {
                return image.Clone() as Image;
            }
        }

        /// <summary>
        /// Loads the specified image file into memory and converts it to a jpeg byte stream.
        /// </summary>
        /// <remarks>
        /// MSDN documentation states that the FromFile method supports BMP, GIF, JPEG, PNG,
        /// and TIFF file formats.  Other types will throw and OutOfMemoryException.
        /// </remarks>
        /// <param name="imageFile">The fully qualified path to an image file.</param>
        /// <param name="quality">JPEG compression quality metric specified as a value between 0 (worst) and 100 (best). Defaults to 75.</param>
        /// <returns>A byte array containing a JPEG image encoded to the specified quality level.</returns>
        public static byte[] LoadImageAsJpeg(string imageFile, long quality = 75L)
        {
            using (var image = Image.FromFile(imageFile))
            {
                try
                {
                    // Specify the quality level.
                    var qualityParameter = new EncoderParameter(Encoder.Quality, quality);

                    // Create an EncoderParameters object to control compression quality.
                    using (var encoderParameters = new EncoderParameters(1) { Param = { [0] = qualityParameter } })
                    using (var stream = new MemoryStream())
                    {
                        // Save the stream using the jpeg encoder and given quality metric.
                        image.Save(stream, GetEncoder(ImageFormat.Jpeg), encoderParameters);

                        // Return the resulting image as byte array.
                        return stream.GetBuffer();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Loads the the specified image into a byte array.
        /// </summary>
        /// <param name="filename">A valid image file.</param>
        /// <returns>A byte array containing the image data on success; null otherwise.</returns>
        public static System.Data.Linq.Binary LoadImage(string filename)
        {
            // Fails if file does not exist.
            if (!File.Exists(filename)) return null;

            using (var reader = new BinaryReader(File.Open(filename, FileMode.Open)))
            {
                var length = (int)reader.BaseStream.Length;
                var bytes = reader.ReadBytes(length);
                reader.Close();

                return bytes;
            }
        }

        /// <summary>
        /// Saves the given image data to a file.
        /// </summary>
        /// <param name="data">The binary image data.</param>
        /// <param name="imageFile">Fully qualified filename.</param>
        public static void SaveImage(byte[] data, string imageFile)
        {
            using (var stream = new MemoryStream(data))
            {
                var image = Image.FromStream(stream);
                image.Save(imageFile);
            }
        }

        /// <summary>
        /// Finds the image encoder for the given image type.
        /// </summary>
        /// <remarks>
        /// Pinched from MSDN documentation.
        /// See: http://msdn.microsoft.com/en-us/library/hwkztaft.aspx        
        /// </remarks>
        /// <param name="format">A valid System.Drawing.Imaging.ImageFormat specifier.</param>
        /// <returns>The requested ImagedCodecInfo object on success, null otherwise.</returns>
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();

            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        /// <summary>
        /// Generates a web ready video segment from the given source.
        /// </summary>
        /// <param name="sourceFile">The fully qualified path to the source video.</param>
        /// <param name="startTime">The start time of the segment within the source video specified in milliseconds.</param>
        /// <param name="endTime">The duration of the segment to encode specified in milliseconds.</param>
        /// <param name="width">Width of output in pixels.</param>
        /// <param name="height">Height of output in pixels.</param>
        /// <param name="targetFile">The fully qualified path and name for the segment video.</param>
        /// <remarks>
        /// Source file must be properly sized and watermarked prior to segmentation.  Segmented videos
        /// will have the same size, aspect ratio, and frame rate of the source video.
        /// </remarks>
        public static void EncodeSegment(string sourceFile, int startTime, int endTime, int width, int height, string targetFile)
        {
            // Transcode to H.264
            Transcode(sourceFile, startTime / 1000.00D, (endTime - startTime) / 1000.00D, width, height, targetFile);
        }

        /// <summary>
        /// A wrapper around FFmpeg which generates an H.264 encoded MP4 file from the given source.
        /// </summary>
        /// <param name="sourceFile">Source video containing one or more story segments to encode.</param>
        /// <param name="startTime">The start time of the segment within the source video specified in seconds.</param>
        /// <param name="duration">The duration of the segment specified in seconds.</param>
        /// <param name="width">Width of output in pixels.</param>
        /// <param name="height">Height of output in pixels.</param>
        /// <param name="targetFile">The fully qualified path and filename for web-ready output file.</param>
        /// <returns>The FFmpeg process exit code.</returns>
        /// <remarks>
        /// <para>
        /// The current FFmpeg settings were assembled using the FFmpeg and x264 Encoding Guide with input
        /// from Apple's recommendations for iOS compatible encodings, specifically the use of the h.264
        /// Baseline profile and stereo 22khz 40kbs AAC audio.  Apple also recommends a key frame interval
        /// of 90, I assumed this means GOP size and set the -g value accordingly.  
        /// <href>https://trac.ffmpeg.org/wiki/x264EncodingGuide</href>
        /// <href>https://developer.apple.com/library/ios/#documentation/NetworkingInternet/Conceptual/StreamingMediaGuide/UsingHTTPLiveStreaming/UsingHTTPLiveStreaming.html#//apple_ref/doc/uid/TP40008332-CH102-SW8</href>
        /// </para>
        /// <para>
        /// UPDATE 05-DEC-2019 (bm3n): While updating FFmpeg to v4.2.1 (Sep 2019) I took the liberty of
        /// reviewing the encoding settings outlined above. Upon reread, I realize the recommendations
        /// from Apple were in regards to HTTP LiveStreaming NOT progressively downloaded content like 
        /// what we use. With improvements in browser and device support for h.264 and VP9, we should 
        /// probably revisit these settings.
        /// </para>
        /// <para>
        /// </para>
        /// UPDATE 18-JUN-2020 (bm3n): Added scaling filter to force final video into a specific 
        /// resolution as specified in the main configuration file.  The final videos will have
        /// clean SAR values of 1:1 and known aspect ratios like 4:3 and 16:9.
        /// </remarks>
        private static int Transcode(string sourceFile, double startTime, double duration, int width, int height, string targetFile)
        {
            int exitCode = -1;

            using (var process = new Process())
            {
                // Refer to FFmpeg H.264 Video Encoding Guide for details:
                // https://trac.ffmpeg.org/wiki/Encode/H.264
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                    Arguments = $"-ss {startTime} -i \"{sourceFile}\" -t {duration} -filter_complex \"scale={width}:{height},setsar=1:1; aresample=async=1000\" -c:v libx264 -profile:v baseline -level 3.0 -preset slow -crf 23 -maxrate 320k -bufsize 640k -c:a aac -ar 22050 -ac 2 -b:a 40k -movflags +faststart -y \"{targetFile}\"",
                    UseShellExecute = false
                };

                Logger.Write("Initializing FFmpeg with the following arguments:");
                Logger.Write(process.StartInfo.Arguments);

                if (process.Start())
                {
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                    process.Close();
                }
                else
                {
                    throw new Exception("FAILED to start process.");
                }
            }

            return exitCode;
        }
    }
}
