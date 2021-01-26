using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

using InformediaCORE.Azure;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace TestConfiguration
{
    class Program
    {
        /// <summary>
        /// Shorthand references to settings
        /// </summary>
        static readonly Settings settings = Settings.Current;

        static void Main()
        {
            Logger.Start();

            TestLoggingTargets();
            TestDatabaseConfiguration();
            TestBuildPath();
            TestPowerShell();
            TestGentleAligner();
            TestFFmpeg();
            TestSpacyNLP();
            TestStanfordNER();
            TestFirebase();
            TestEmailConfiguration();
            TestProcessingArchive();
            TestProductionArchive();

            Logger.End();
        }

        /// <summary>
        /// Tests each one of the logging targets.
        /// </summary>
        private static void TestLoggingTargets()
        {
            try
            {
                Logger.Write("IGNORE: JUST A TEST");
                Logger.Warning("IGNORE: JUST A TEST");
                Logger.Error("IGNORE: JUST A TEST");
                Logger.Exception(new Exception("IGNORE: JUST A TEST (EXCEPTION HANDLER)"));
            }
            catch (Exception ex)
            {
                Logger.Error("Logger: FAILED - {0}", ex.Message);
                return;
            }

            Logger.Write("Logger: OK");
        }

        /// <summary>
        /// Tests database connection.
        /// </summary>
        private static void TestDatabaseConfiguration()
        {
            try
            {
                using (var context = DataAccess.GetDataContext(settings.ConnectionString))
                {
                    var count = context.Collections.Count();
                }
            } catch(Exception ex)
            {
                Logger.Error("ConnectionString: FAILED - {0}", ex.Message);
                return;
            }

            Logger.Write("ConnectionString: OK");
        }

        /// <summary>
        /// Tests build path
        /// </summary>
        /// <returns>True if build path exists and permissions are correct; false otherwise.</returns>
        private static void TestBuildPath()
        {

            if (!Directory.Exists(settings.BuildPath))
            {
                Logger.Error("BuildPath: FAILED - specified BuildPath does not exist.");
                return;
            }

            var filename = Path.Combine(settings.BuildPath, "__configuration.test");

            try
            {
                var test = File.CreateText(filename);
                test.WriteLine("{0:HH:mm:ss} on {0:MM/dd/yyyy}", DateTime.Now);
                test.Close();
            }
            catch (Exception)
            {
                Logger.Error("BuildPath: FAILED - unable to write to BuildPath.");
                return;
            }

            try
            {
                File.Delete(filename);
            }
            catch (Exception)
            {
                Logger.Error("BuildPath: FAILED - unable to delete from BuildPath");
            }

            Logger.Write("BuildPath: OK");
        }

        /// <summary>
        /// Tests if PowerShell is installed and running correctly.
        /// </summary>
        public static void TestPowerShell()
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(settings.ExternalTools.PowerShellPath, "pwsh.exe"),
                        Arguments = "-v",
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };

                    if (process.Start())
                    {
                        var stdout = process.StandardOutput.ReadToEnd();

                        process.WaitForExit();
                        process.Close();

                        if (!stdout.Contains("PowerShell"))
                        {
                            throw new Exception("Process did not output expected value.");
                        }
                    }
                    else
                    {
                        throw new Exception("Process failed to start.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PowerShell: FAILED - {0}", ex.Message);
                return;
            }

            Logger.Write("PowerShell: OK");
        }

        /// <summary>
        /// Test Gentle Forced Aligner
        /// </summary>
        private static void TestGentleAligner()
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(settings.ExternalTools.PowerShellPath, "pwsh.exe"),
                        Arguments = "-Command wsl python3 ~/gentle/align.py ~/gentle/examples/data/lucier.mp3 ~/gentle/examples/data/lucier.txt",
                        UseShellExecute = false,                        
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    if (process.Start())
                    {
                        var stderr = process.StandardError.ReadToEnd();

                        process.WaitForExit();
                        process.Close();

                        if (!stderr.Contains("INFO:root:after 2nd pass: 4 unaligned words (of 105)"))
                        {
                            throw new Exception("Process did not output expected value.");
                        }
                    }
                    else
                    {
                        throw new Exception("Process failed to start.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Gentle Forced Aligner: FAILED - {0}", ex.Message);
                return;
            }

            Logger.Write("Gentle Forced Aligner: OK");
        }

        /// <summary>
        /// Tests if FFmpeg is installed and configured correctly.
        /// </summary>
        public static void TestFFmpeg()
        {
            try
            { 
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(settings.ExternalTools.FFmpegPath , "ffmpeg.exe"),
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };

                    if (process.Start())
                    {
                        // For whatever reason, FFmpeg outputs the valuable
                        // information to StdErr, go figure.
                        var stderr = process.StandardError.ReadToEnd();

                        process.WaitForExit();
                        process.Close();

                        if (!stderr.Contains("ffmpeg"))
                        {
                            throw new Exception("Process did not output expected value.");
                        }
                    }
                    else
                    {
                        throw new Exception("Process failed to start.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("FFmpeg: FAILED - {0}", ex.Message);
                return;
            }

            Logger.Write("FFmpeg: OK");
        }

        /// <summary>
        /// Tests if spaCy NLP is installed and configured correctly.
        /// </summary>
        private static void TestSpacyNLP()
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(settings.ExternalTools.SpacyPath, "python.exe"),
                        Arguments = $"example.py",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        WorkingDirectory = settings.ExternalTools.SpacyPath
                    };

                    if (process.Start())
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        process.Close();

                        if (!output.Contains("earlier this week 306 323 DATE"))
                        {
                            throw new Exception("Process did not produce expected output");
                        }
                    }
                    else
                    {
                        throw new Exception("Process failed to start.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("spaCy NLP: FAILED! - {0}", ex.Message);
                return;
            }

            Logger.Write("spaCy NLP: OK");
        }

        /// <summary>
        /// Tests if Stanford NER is installed and configured correctly.
        /// </summary>
        private static void TestStanfordNER()
        {
            try { 
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(settings.ExternalTools.JavaPath, "java.exe"),
                        Arguments = $"-mx1000m -cp stanford-ner.jar;lib/* edu.stanford.nlp.ie.crf.CRFClassifier -loadClassifier classifiers\\english.all.3class.distsim.crf.ser.gz  -outputFormat tsv -textFile sample.txt",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        WorkingDirectory = settings.ExternalTools.SNERPath
                    };

                    if (process.Start())
                    {
                        var stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        process.Close();

                        if (!stderr.Contains("CRFClassifier tagged 85 words"))
                        {
                            throw new Exception("Process did not produce expected output");
                        }
                    }
                    else
                    {
                        throw new Exception("Process failed to start.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Stanford NER: FAILED! - {0}", ex.Message);
                return;
            }

            Logger.Write("Stanford NER: OK");
        }

        /// <summary>
        /// Tests if Firebase is configuration is correct.
        /// </summary>
        private static void TestFirebase()
        {
            try
            {
                var firebaseURI = new Uri(settings.ExternalTools.FirebaseURL);
                var requestURI = new Uri(firebaseURI, "gs/0.json");

                HttpClient client = new HttpClient();
                HttpResponseMessage response = client.GetAsync(requestURI).Result;

                if (response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsStringAsync().Result;
                    if (content != "null")
                    {
                        throw new Exception($"Response does not contain expected content '{content}'");
                    }

                }
                else
                {
                    throw new Exception($"Response failed with status code {response.StatusCode}: {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Firebase: FAILED! - {0}", ex.Message);
                return;
            }

            Logger.Write("Firebase: OK");
        }

        /// <summary>
        /// Tests the email configuration
        /// </summary>
        private static void TestEmailConfiguration()
        {
            try
            {
                const string subject = "Test Email";
                const string body = "This is a test email from the configuration test utility.";
                Utilities.SendEmail(subject, body, true);
            }
            catch (Exception ex)
            {
                Logger.Error("Email: FAILED - {0}", ex.Message);
                return;
            }

            Logger.Write("Email: OK - (check your inbox)");
        }

        /// <summary>
        /// Tests Azure resources related to the processing Digital Archive.
        /// </summary>
        private static void TestProcessingArchive()
        {
            if (AzureContentManager.TestSearchService(DigitalArchiveSpecifier.Processing))
            {
                Logger.Write("Processing (Test) Search Service: OK");
            }
            else
            {
                Logger.Error("Processing (Test) Search Service: FAILED!");
            }

            if (AzureContentManager.TestStorageService(DigitalArchiveSpecifier.Processing))
            {
                Logger.Write("Processing (Test) Storage Service: OK");
            }
            else
            {
                Logger.Error("Processing (Test) Storage Service: FAILED!");
            }
        }

        /// <summary>
        /// Tests Azure resources related to the production Digital Archive.
        /// </summary>
        private static void TestProductionArchive()
        {
            if (AzureContentManager.TestSearchService(DigitalArchiveSpecifier.Production))
            {
                Logger.Write("Production (Live) Search Service: OK");
            }
            else
            {
                Logger.Error("Production (Live) Search Service: FAILED!");
            }

            if (AzureContentManager.TestStorageService(DigitalArchiveSpecifier.Production))
            {
                Logger.Write("Production (Live) Storage Service: OK");
            }
            else
            {
                Logger.Error("Production (Live) Storage Service: FAILED!");
            }
        }
    }
}
