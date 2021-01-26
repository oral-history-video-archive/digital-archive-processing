using System;
using System.Globalization;
using System.IO;
using System.Linq;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net.NetworkInformation;

using InformediaCORE.Common.Config;
using System.Net;

namespace InformediaCORE.Common
{
    public static class Utilities
    {
        /// <summary>
        /// Use US-English as the culture for all globalized functions.
        /// </summary>
        private static readonly CultureInfo en_US = new CultureInfo("en-US");

        /// <summary>
        /// Converts the time portion of a DateTime structure into milliseconds.
        /// </summary>
        /// <param name="dt">The DateTime structure as parsed from an XML file.</param>
        /// <returns>An integer representing a length of time as milliseconds.</returns>
        public static int DateTimeToMS(DateTime dt)
        {
            return (dt.Hour * 3600000 + dt.Minute * 60000 + dt.Second * 1000 + dt.Millisecond);
        }

        /// <summary>
        /// Converts a string representation of time into milliseconds.
        /// </summary>
        /// <param name="hhmmss">A string in the format of HH:MM:SS.ss</param>
        /// <returns>An integer representing the length of time as milliseconds.</returns>
        public static int HHMMSStoMS(string hhmmss)
        {
            string[] fields = hhmmss.Split(':');
            return (int)(Convert.ToInt32(fields[0], en_US) * 3600000 +
                         Convert.ToInt32(fields[1], en_US) * 60000 +
                         Convert.ToDouble(fields[3], en_US) * 1000);
        }

        /// <summary>
        /// Get the extension for the given file.
        /// </summary>
        /// <param name="filename">The fully or partially qualified file name to </param>
        /// <returns>The file extension without the "."</returns>
        public static string GetFileExtension(string filename)
        {
            var extension = filename == null ? string.Empty : Path.GetExtension(filename).ToLower(en_US);
            if (extension.Length > 1)
                extension = extension.Substring(1);

            return extension;
        }

        /// <summary>
        /// Generates a fully path qualified filename for use as a temporary file.
        /// </summary>
        /// <param name="sourceFile">A partial for fully qualified file name whose name 
        /// will be used as the basename for the temp file.</param>
        /// <param name="extension">The extension which should be applied to the file.</param>
        /// <returns>A fully qualified filename.</returns>
        public static string GetTemporaryFilename(string sourceFile, string extension)
        {
            return Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(sourceFile) + extension);
        }

        /// <summary>
        /// Return the fully qualified data path for the given segment.
        /// </summary>
        /// <param name="segment">A valid Segment instance.</param>
        /// <returns>The given segment's data path upon success; null otherwise.</returns>
        public static string GetSegmentDataPath(Database.Segment segment)
        {
            if (segment == null) return null;

            return Path.Combine(
                Settings.Current.BuildPath,"Data",
                segment.CollectionID.ToString(en_US),
                segment.SegmentID.ToString(en_US)
            );
        }

        /// <summary>
        /// Return the fully qualified build path for the given segment.
        /// </summary>
        /// <param name="segment">A valid Segment instance.</param>
        /// <returns>The given segment's build (media) path upon success; null otherwise.</returns>
        public static string GetSegmentBuildPath(Database.Segment segment)
        {
            if (segment == null) return null;

            return Path.GetDirectoryName(segment.MediaPath);
        }

        /// <summary>
        /// Get user confirmation prior to performing an operation.
        /// </summary>
        /// <returns>Returns true if user wants to proceed; false otherwise.</returns>
        public static bool GetUserConfirmation(string message)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;

            Console.WriteLine();
            ConsoleKey response;
            do
            {
                Console.Write(message);
                response = Console.ReadKey(false).Key;
                if (response != ConsoleKey.Enter) Console.WriteLine();
            } while (response != ConsoleKey.Y && response != ConsoleKey.N);

            Console.ForegroundColor = originalColor;
            return (response == ConsoleKey.Y);
        }

        /// <summary>
        /// Writes a blank line to the console.
        /// </summary>
        public static void NewLine()
        {
            Console.WriteLine();
        }

        /// <summary>
        /// Write the given message to the console using the default console color.
        /// </summary>
        /// <param name="message">Message to output.</param>
        public static void Write(string message)
        {
            Write(message, Console.ForegroundColor);
        }

        /// <summary>
        /// Write the given message to the console using the specified color.
        /// </summary>
        /// <param name="message">Message to output.</param>
        /// <param name="color">Color to use.</param>
        public static void Write(string message, ConsoleColor color)
        {
            var originalColor = Console.ForegroundColor;

            Console.ForegroundColor = color;
            Console.Write(message);
            Console.ForegroundColor = originalColor;
        }

        /// <summary>
        /// Write the given message plus line terminator to the console using the default console color.
        /// </summary>
        /// <param name="message">Message to output.</param>
        public static void WriteLine(string message)
        {
            WriteLine(message, Console.ForegroundColor);
        }

        /// <summary>
        /// Write the given message plus line terminator to the console using the specified color.
        /// </summary>
        /// <param name="message">Message to output.</param>
        /// <param name="color">Color to use.</param>
        public static void WriteLine(string message, ConsoleColor color)
        {
            var originalColor = Console.ForegroundColor;

            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        /// <summary>
        /// Upper-cases the given accession if necessary.
        /// Logs warnings if the conversion is required.
        /// </summary>
        /// <param name="accession">The accession to be converted.</param>
        /// <returns>The upper cased version of the given accession.</returns>
        public static string UpperCaseWithWarnings(string accession)
        {
            if (accession.Any(char.IsLower))
            {
                Logger.Warning($"Given accession '{accession}' contains lowercase characters, converting to uppercase per accession rules.");
                accession = accession?.ToUpper(en_US);
            }

            return accession;
        }

        /// <summary>
        /// Sends an email to the processing team.
        /// </summary>
        /// <param name="subject">Email subject.</param>
        /// <param name="body">Email message body.</param>
        public static void SendEmail(string subject, string body, bool isError = false)
        {
            var config = Settings.Current.Email;

            // Filter messages based on level
            if ( config.MessageLevel == EmailMessageLevel.None ||
                (config.MessageLevel == EmailMessageLevel.ErrorsOnly && !isError))
            {
                Logger.Warning($"Email suppressed by MessageLevel: {config.MessageLevel}");
                return;
            }

            var hostname = Dns.GetHostName(); 

            // Follows Quick Start - Hello Email example at:
            // https://github.com/sendgrid/sendgrid-csharp/
            var client = new SendGridClient(config.SendGridApiKey);
            var from = new EmailAddress($"{config.SenderAddress}", $"Digital Archive Processing System on {hostname}");
            var tos = config.Recipients.Select(address => new EmailAddress(address)).ToList();
            var htmlContent = $"<pre>{body}</pre>";
            var msg = MailHelper.CreateSingleEmailToMultipleRecipients(from, tos, subject, body, htmlContent);

            var response = client.SendEmailAsync(msg).Result;

            if (response?.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                Logger.Warning("Email failed with the following status code: {0}", response?.StatusCode);
                Logger.Warning("Response body: {0}", response?.Body.ReadAsStringAsync().Result);
            }
         }

        /// <summary>
        /// Extension method truncates strings on word boundaries adding ellipsis as necessary.
        /// </summary>
        /// <param name="value">The string</param>
        /// <param name="maxChars">Maximum length before truncation occurs.</param>
        /// <returns>The original string if less than maxChars; a truncated string otherwise.</returns>
        public static string Truncate(this string value, int maxChars)
        {
            if (value?.Length > maxChars)
            {
                var pos = value.IndexOf(" ", maxChars - 3, StringComparison.Ordinal);
                if (pos > 0)
                {
                    value = value.Substring(0, pos) + "...";
                }
            }

            return value;
        }

        /// <summary>
        /// Writes given content to the specified file.
        /// </summary>
        /// <param name="content">The content to be written to file.</param>
        /// <param name="filename">The fully qualified filename.</param>
        public static void WriteToFile(string content, string filename)
        {
            using (var writer = new StreamWriter(filename))
            {
                writer.Write(content);
                writer.Flush();
                writer.Close();
            }
        }

        /// <summary>
        /// Retrieves the contents of the given file.
        /// </summary>
        /// <param name="filename">Fully qualified path to file.</param>
        /// <returns>Contents of file as a string.</returns>
        public static string ReadFromFile(string filename)
        {
            using (var reader = new StreamReader(filename))
            {
                var content = reader.ReadToEnd();
                reader.Close();
                return content;
            }
        }
    }
}
