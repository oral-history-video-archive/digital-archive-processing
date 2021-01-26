using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace InformediaCORE.Common.Database
{
    /// <summary>
    /// Represents one (char_position,video_time) transcript synchronization pair.
    /// </summary>
    /// <remarks>
    /// !!! DO NOT MODIFY THIS CLASS UNDER ANY CIRCUMSTANCES !!!
    /// This class is used (de)serialize data to and from the database.  If the
    /// signature changes in any way, then existing data will not deserialize
    /// properly.
    /// </remarks>
    [Serializable]
    public class TSyncPair
    {
        /// <summary>
        /// The absolute character offset from the beginning of the document.
        /// </summary>
        public int offset;

        /// <summary>
        /// The absolute time in milliseconds since the beginning of the video stream.
        /// </summary>
        public int time;

        /// <summary>
        /// Parameterless constructor to facilitate serialization.
        /// </summary>
        public TSyncPair() { }

        /// <summary>
        /// Instantiates an instance of the TSync class.
        /// </summary>
        /// <param name="offset">The absolute character offset of the word from the beginning of the transcript.</param>
        /// <param name="time">The absolute time of occurance from the beginning of the video stream.</param>
        public TSyncPair(int offset, int time)
        {
            this.offset = offset;
            this.time = time;
        }

        /// <summary>
        /// A comparison function for sorting lists of TSyncPairs
        /// </summary>
        public static int Compare(TSyncPair x, TSyncPair y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    // If x is null and y is null, they're equal. 
                    return 0;
                }
                // If x is null and y is not null, y is greater. 
                return -1;
            }
            // If x is not null...
            if (y == null)
            {
                // ...and y is null, x is greater.
                return 1;
            }
            // ...and y is not null, compare the offsets
            return x.offset.CompareTo(y.offset);
        }

    }

    /// <summary>
    /// A serializable array of TSyncPair.
    /// </summary>
    [Serializable]
    public class TranscriptSync
    {
        public List<TSyncPair> SyncPairs;

        /// <summary>
        /// Create database-ready binary TranscriptSync data from TSyncPairs.
        /// </summary>
        /// <returns>A Linq.Binary object which can be stored in the Segment.TranscriptSync field.</returns>
        public Binary ToBinary()
        {
            // Serialize the list using a binary formatter
            var formatter = new BinaryFormatter();
            using (var buffer = new MemoryStream())
            {
                formatter.Serialize(buffer, SyncPairs);
                return buffer.ToArray();
            }
        }

        /// <summary>
        /// Populate TSyncPairs by deserializing the binary TranscriptSync from the database.
        /// </summary>
        /// <param name="transcriptSync">The Linq.Binary data from the Segment.TranscriptSync field.</param>
        public void FromBinary(Binary transcriptSync)
        {
            // Deserialize the list assuming binary format
            using (var buffer = new MemoryStream(transcriptSync.ToArray()))
            {
                var formatter = new BinaryFormatter();
                SyncPairs = (List<TSyncPair>)formatter.Deserialize(buffer);
            }
        }

        /// <summary>
        /// An empty constructor to facilitate serialization.
        /// </summary>
        public TranscriptSync()
        {
            SyncPairs = new List<TSyncPair>();
        }

        /// <summary>
        /// Instantiates an instance of the TranscriptSync array..
        /// </summary>
        /// <param name="transcriptSync">A binary data stream to be deserialized into a TranscriptSync array.</param>
        public TranscriptSync(Binary transcriptSync)
        {
            FromBinary(transcriptSync);
        }
    }
}
