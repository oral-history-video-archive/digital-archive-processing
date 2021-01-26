using System;
using System.Diagnostics;
using System.Net;

using InformediaCORE.Common.Database;
using InformediaCORE.Processing.Database;

namespace InformediaCORE.Processing
{
    public class SemaphoreRequestException : Exception
    {
        public int SegmentID { get; }

        public SemaphoreRequestException(int segmentID) : base($"Database lock could not be obtained for segment {segmentID}") 
        {
            SegmentID = segmentID;
        }
    }

    public class SemaphoreManager
    {
        #region =========================     Class Scope (Static)     =========================
        /// <summary>
        /// Get the single instance of the SemaphoreManager.
        /// </summary>
        public static SemaphoreManager Instance { get; } = new SemaphoreManager();
        #endregion ======================     Class Scope (Static)     =========================

        
        #region ========================= Instance Scope (Non-Static)  =========================

        /// <summary>
        /// A cached instance of the DataAccess class used for communicating with the database.
        /// </summary>
        private readonly DataAccessExtended _database = new DataAccessExtended();

        /// <summary>
        /// The id of the process running this instance.
        /// </summary>
        private readonly int _pid = Process.GetCurrentProcess().Id;

        /// <summary>
        /// The hostname of the machine where this instance is running.
        /// </summary>
        private readonly string _hostname = Dns.GetHostName().ToLower();

        #region Constructor
        /// <summary>
        /// Private constructor prevents clients from instantiating
        /// their own instance of the SemaphoreManager class.
        /// </summary>
        private SemaphoreManager() { }
        #endregion Constructor

        /// <summary>
        /// Requests a semaphore lock on the given segment.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <returns>True on success, false on failure.</returns>
        public bool Request(int segmentID)
        {
            // Attempt to create the semaphore...
            Semaphore semaphore = _database.InsertSemaphore(segmentID, _pid, _hostname);

            // Report results
            return (semaphore != null);
        }

        /// <summary>
        /// Releases a semaphore lock on the given segment.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        public void Release(int segmentID)
        {
            _database.DeleteSemaphore(segmentID, _pid, _hostname);
        }

        #endregion ====================== Instance Scope (Non-Static)  =========================
    }
}
