using System;

using InformediaCORE.Common;
using InformediaCORE.Common.Database;
using InformediaCORE.Processing.Tasks;

namespace InformediaCORE.Processing
{
    public class TaskManager
    {
        /// <summary>
        /// Runs a single task.
        /// </summary>
        /// <param name="task">The task to be run.</param>
        public void Run(AbstractTask task)
        {
            Logger.Write("QUEUEING: {0} on segment {1}.", task.Name, task.SegmentID);

            try
            {
                // Make sure task isn't already running.
                if (task.State == TaskStateValue.Running)
                {
                    Logger.Write("SKIPPING: {0}. The database claims this task is currently running on segment {1}.", task.Name, task.SegmentID);
                }
                else
                {
                    // Check if task has already been run
                    if (task.State == TaskStateValue.Complete && task.RunCondition != AbstractTask.RunConditionValue.Always)
                    {
                        // Task has already be run, and the Always flag was not specified.
                        Logger.Write("SKIPPING: {0}.  This task was run previously for segment {1}.", task.Name, task.SegmentID);
                    }
                    else
                    {
                        Logger.Write("Initializing {0}.", task.Name);
                        task.State = TaskStateValue.Running;

                        Logger.Write("Checking requirements.");
                        task.CheckRequirements();
                        
                        Logger.Write("Purging prior {0} results.", task.Name);
                        task.Purge();

                        Logger.Write("Running {0}.", task.Name);
                        task.Run();
                        
                        task.State = TaskStateValue.Complete;
                        Logger.Write("{0} completed successfully.", task.Name);
                    }
                }

            }
            catch (TaskRequirementsException ex)
            {
                Logger.Warning(ex.Message);
                Logger.Warning("{0} will not be run.", task.Name);
                task.State = TaskStateValue.Pending;             }
            catch (TaskRunException ex)
            {
                Logger.Error(ex.Message);
                Logger.Error("{0} failed.", task.Name);
                task.State = TaskStateValue.Failed;
            }
            catch (Exception ex)
            {
                // Unexpected error log full exception for diagnostic purposes.
                Logger.Exception(ex);
                Logger.Error($"{task.Name} failed to run due to unexpected error.");
                task.State = TaskStateValue.Failed;
            }
        }
    }
}
