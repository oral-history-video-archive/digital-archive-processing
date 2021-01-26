using System;
using System.IO;

using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing.Database;

namespace InformediaCORE.Processing.Tasks
{
    /// <summary>
    /// Abstract base class for processing tasks.
    /// </summary>
    public abstract class AbstractTask
    {
        #region == Declarations
        /// <summary>
        /// Static reference to current configuration settings.
        /// </summary>
        internal static readonly Settings Settings = Settings.Current;

        /// <summary>
        /// Backing storage for DataPath property.
        /// </summary>
        private string _dataPath;

        /// <summary>
        /// A list of possible run conditions.
        /// </summary>
        public enum RunConditionValue
        {
            /// <summary>
            /// Run the task only if it has not been run previously.
            /// </summary>
            AsNeeded,
            /// <summary>
            /// Run the task regardless of whether it has been run previously.
            /// </summary>
            Always
        }

        /// <summary>
        /// The data access object used for communicating with the database.
        /// </summary>
        internal DataAccessExtended Database = new DataAccessExtended();

        /// <summary>
        /// The segment to be updated by this task.
        /// </summary>
        internal Segment Segment;

        /// <summary>
        /// Gets or sets the corresponding value in the TaskStates table.
        /// </summary>
        internal TaskStateValue State 
        {
            get
            {
                return Database.GetTaskState(SegmentID, Name);
            }

            set
            {
                Database.UpdateTaskState(SegmentID, Name, value);
            }
        }

        /// <summary>
        /// Returns the name of the instantiated task type.
        /// </summary>
        internal string Name => GetType().Name;

        /// <summary>
        /// Returns a name of the path for intermediate segment data.
        /// </summary>
        internal string DataPath
        {
            get
            {
                if (String.IsNullOrEmpty(_dataPath))
                {
                    _dataPath = Path.Combine(
                        Settings.BuildPath,
                        "Data",
                        CollectionID.ToString(),
                        SegmentID.ToString()
                    );
                }
                return _dataPath;
            }
        }

        /// <summary>
        /// Returns the collection id of this task.
        /// </summary>
        internal int CollectionID
        {
            get
            {
                if (Segment == null)
                    return 0;
                else
                    return Segment.CollectionID;
            }
        }

        /// <summary>
        /// Returns the segment id of this task.
        /// </summary>
        internal int SegmentID
        {
            get 
            {
                if (Segment == null)
                    return 0;
                else
                    return Segment.SegmentID; 
            }
        }

        /// <summary>
        /// Gets or sets the condition under which this task should run.
        /// </summary>
        internal RunConditionValue RunCondition { get; set; }
        #endregion Declarations

        #region == Constructors
        /// <summary>
        /// Instantiates an instance from the given segment id.
        /// </summary>
        /// <param name="segmentID">A valid segment id.</param>
        /// <param name="condition">Sets the conditions for when the task should be run.</param>
        protected AbstractTask(int segmentID, RunConditionValue condition = RunConditionValue.AsNeeded)
        {
            Segment = Database.GetSegment(segmentID);
            RunCondition = condition;
        }

        /// <summary>
        /// Instantiates an instance from the given segment name.
        /// </summary>
        /// <param name="segmentName">A valid segment name.</param>
        /// <param name="condition">Sets the conditions for when the task should be run.</param>
        protected AbstractTask(string segmentName, RunConditionValue condition = RunConditionValue.AsNeeded)
        {
            Segment = Database.GetSegment(segmentName);
            RunCondition = condition;
        }

        /// <summary>
        /// Instantiates an instance from the given segment.
        /// </summary>
        /// <param name="segment">A database Segment.</param>
        /// <param name="condition">Sets the conditions for when the task should be run.</param>
        protected AbstractTask(Segment segment, RunConditionValue condition = RunConditionValue.AsNeeded)
        {
            Segment = segment;
            RunCondition = condition;
        }
        #endregion Constructors

        #region == Abstract Methods
        /// <summary>
        /// Checks that the necessary input requirements are met prior to running the task.
        /// </summary>
        internal abstract void CheckRequirements();

        /// <summary>
        /// Purges prior task results for the associated segment from the database.
        /// </summary>
        internal abstract void Purge();

        /// <summary>
        /// The method that performs the magic.
        /// </summary>
        internal abstract void Run();
        #endregion Abstract Methods
    }

    /// <summary>
    /// Exception raised when a task fails to find the necessary inputs.
    /// </summary>
    public class TaskRequirementsException : Exception
    {
        public TaskRequirementsException(string message) : base(message) { }
    }

    /// <summary>
    /// Exception raised when a task fails to generate the expected output.
    /// </summary>
    public class TaskRunException : Exception
    {
        public TaskRunException(string message) : base(message) { }
    }
}
