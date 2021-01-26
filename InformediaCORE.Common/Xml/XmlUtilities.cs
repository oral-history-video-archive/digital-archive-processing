using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace InformediaCORE.Common.Xml
{   
    public static class XmlUtilities
    {
        public enum XmlClassNames
        {
            AnnotationType,
            Collection,
            Movie,
            World,
            Unknown
        }

        /// <summary>
        /// Loads an object from disk using the XML deserializer.
        /// </summary>
        /// <typeparam name="T">The class type of the object to deserialize.</typeparam>
        /// <param name="xmlFilename">The XML file to be read.</param>
        /// <returns>An object of type T.</returns>
        public static T Read<T>(string xmlFilename)
        {
            // Create an instance of the XmlSerializer to read the XML document
            var serializer = new XmlSerializer(typeof(T));

            // Set the validation settings.
            var settings = new XmlReaderSettings {ValidationType = ValidationType.Schema};
            settings.ValidationEventHandler += ValidationCallBack;

            // Create the XmlReader object.
            using (var reader = XmlReader.Create(xmlFilename, settings))
            {
                // Return the deserialized data.
                return (T)serializer.Deserialize(reader);
            }
        }

        /// <summary>
        /// Serializes an object to disk using the XML serializer.
        /// </summary>
        /// <typeparam name="T">The class type of the object to serialize.</typeparam>
        /// <param name="xmlObject">The object to serialize.</param>
        /// <param name="xmlFilename">The XML file to be created.</param>
        public static void Write<T>(T xmlObject, string xmlFilename)
        {
            // Create an instance of the XmlSerializer to read the XML document
            var serializer = new XmlSerializer(typeof(T));

            var settings = new XmlWriterSettings
            {
                Encoding = System.Text.Encoding.UTF8,
                NewLineHandling = NewLineHandling.Entitize,
                Indent = true
            };

            using (var writer = XmlWriter.Create(xmlFilename, settings))
            {
                serializer.Serialize(writer, xmlObject);
            }
        }

        /// <summary>
        /// Gets the name of the class type represented by the root element of the given XML file.
        /// </summary>
        /// <param name="xmlFilename">The XML file to parse.</param>
        /// <returns>The name of the class.</returns>
        public static XmlClassNames GetClassName(string xmlFilename)
        {
            var className = XmlClassNames.Unknown;

            try
            {
                // Open XML Document
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlFilename);
                
                // Get RootElement
                var rootElement = xmlDoc.DocumentElement?.Name ?? string.Empty;
                
                // Process Root Element
                switch (rootElement.ToLower())
                {
                    case "xmlannotationtype":
                        className = XmlClassNames.AnnotationType;
                        break;
                    case "xmlcollection":
                        className = XmlClassNames.Collection;
                        break;
                    case "xmlmovie":
                        className = XmlClassNames.Movie;
                        break;
                    case "xmlworld":
                        className = XmlClassNames.World;
                        break;
                    default:
                        // If this happens, the XML is malformed.
                        className = XmlClassNames.Unknown;
                        Logger.Write("Unknown root element: {0}.", rootElement);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }

            return className;
        }

        /// <summary>
        /// Display any schema warnings or errors encountered during XML deserialization.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void ValidationCallBack(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
                Logger.Write("\tWarning: Matching schema not found.  No validation occurred. {0}.", args.Message);
            else
                Logger.Error("\tValidation error: {0}", args.Message);
        }
    }
}
