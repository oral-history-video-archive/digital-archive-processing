using System;
using System.IO;
using System.Reflection;
using NLog;
using NLog.Config;

namespace InformediaCORE.Common
{
    /// <summary>
    /// A static class exposing a common set of logging functions.
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Cache the starting time for the log file.
        /// </summary>
        private static DateTime _startTime;

        /// <summary>
        /// The NLog singleton which handles the log entries.
        /// </summary>
        private static readonly NLog.Logger NLogger;

        /// <summary>
        /// Static constructor initializes singleton.
        /// </summary>
        static Logger()
        {
            try
            {
                // Find location and name of NLog configuration file
                var thisAssembly = Assembly.GetExecutingAssembly();
                var configFile = Path.ChangeExtension(thisAssembly.Location, ".nlog");

                // Instantiate an instance using the configuration above.
                var config = new XmlLoggingConfiguration(configFile);
                LogManager.Configuration = config;
                NLogger = LogManager.GetCurrentClassLogger();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Writes a start banner to the log file.
        /// </summary>
        public static void Start()
        {
            _startTime = DateTime.Now;

            // Method for determining the build date from the version number
            // https://stackoverflow.com/questions/3982953/visualstudio-translating-a-build-version-to-a-calendar-date
            var version = Assembly.GetEntryAssembly().GetName().Version;
            DateTime buildDate = 
                new DateTime(2000, 1, 1)                        // Visual Studio epoch
                + new TimeSpan(version.Build, 0, 0, 0)          // Days since Jan 1, 2000
                + TimeSpan.FromSeconds(version.Revision * 2);   // Seconds since midnight / 2


            Write("================================================================================");
            Write($"{Assembly.GetEntryAssembly().GetName().Name} v{buildDate:yyyy.MM.dd} (rev {version.Revision}) started.");
            Write("================================================================================");
        }

        /// <summary>
        /// Writes an end banner to the log file.
        /// </summary>
        public static void End()
        {
            var duration = DateTime.Now.Subtract(_startTime);

            Write("================================================================================");
            Write($"{Assembly.GetEntryAssembly().GetName().Name} ended. Run time: {duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}");
            Write("================================================================================");
        }
        
        /// <summary>
        /// Writes messages to the log file at the INFO level.
        /// </summary>
        /// <param name="message">The string message to be written.</param>
        public static void Write(string message)
        {
            NLogger.Info(message);
        }

        /// <summary>
        /// Writes a formatted string to the log file at INFO level.
        /// </summary>
        /// <param name="message">The message with substition variables.</param>
        /// <param name="args">The substitution values to be formatted into the string.</param>
        public static void Write(string message, params object[] args)
        {
            NLogger.Info(message, args);
        }

        /// <summary>
        /// Writes messages to the log file at the WARN level.
        /// </summary>
        /// <param name="message">The string message to be written.</param>
        public static void Warning(string message)
        {
            NLogger.Warn(message);
        }

        /// <summary>
        /// Writes a formatted string to the log file at WARN level.
        /// </summary>
        /// <param name="message">The message with substition variables.</param>
        /// <param name="args">The substitution values to be formatted into the string.</param>
        public static void Warning(string message, params object[] args)
        {
            NLogger.Warn(message, args);
        }

        /// <summary>
        /// Writes messages to the log file at the ERROR level.
        /// </summary>
        /// <param name="message">The string message to be written.</param>
        public static void Error(string message)
        {
            NLogger.Error(message);
        }

        /// <summary>
        /// Writes a formatted string to the log file at ERROR level.
        /// </summary>
        /// <param name="message">The message with substition variables.</param>
        /// <param name="args">The substitution values to be formatted into the string.</param>
        public static void Error(string message, params object[] args)
        {
            NLogger.Error(message, args);
        }

        /// <summary>
        /// Writes formatted exception information to the log file.
        /// </summary>
        /// <param name="ex">The exception object to be logged.</param>
        public static void Exception(Exception ex)
        {
            NLogger.Error(ex, ex?.Message);
        }
    }
}
