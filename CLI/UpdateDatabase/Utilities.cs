using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using InformediaCORE.Common;
using InformediaCORE.Common.Config;
using InformediaCORE.Common.Database;

namespace InformediaCORE.UpdateDatabase
{
    /// <summary>
    /// 
    /// </summary>
    internal enum DataType
    {
        Collection,
        Session
    };

    internal static class Utilities
    {
        /// <summary>
        /// Get the gender specifier from the given string.
        /// </summary>
        /// <param name="value">The string to be parsed.</param>
        /// <returns>M or F upon success.</returns>
        /// <remarks>Throws and exception if given an illegal value.</remarks>
        internal static char GetGenderFromString(string value)
        {
            switch (value)
            {
                case "Female":
                    return 'F';
                case "Male":
                    return 'M';
                default:
                    throw new ParsingException($"Gender value could not be parsed: '{value}'");
            }
        }

        /// <summary>
        /// Get the date from the given string.
        /// </summary>
        /// <param name="value">A string containing a date.</param>
        /// <returns>A valid DateTime for parsable values; null otherwise.</returns>
        /// <remarks>Throws and exception for unparsable values.</remarks>
        internal static DateTime? GetDateFromString(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            if (DateTime.TryParse(value, out DateTime date))
            {
                return date;
            }

            throw new ParsingException($"Date value could not be parsed: '{value}'");
        }

        /// <summary>
        /// Uses OleDB to load an Excel spreadsheet into a datatable for.
        /// </summary>
        /// <param name="pathName">Fully qualified path to an Excel spreadsheet.</param>
        /// <param name="sheetName">The name of the worksheet containing the data.</param>
        /// <returns>A DataTable containing the spreadsheet data.</returns>
        /// <remarks>
        /// See: https://social.msdn.microsoft.com/Forums/vstudio/en-US/3a4fae14-6764-4ccc-9781-845e3d035683/read-xls-file-to-import-to-datatable-but-no-need-to-install-any-com-component-dlls-etc-in-windows?forum=csharpgeneral
        /// </remarks>
        internal static DataTable ExcelToDataTable(string pathName, string sheetName)
        {
            var file = new FileInfo(pathName);
            var extension = file.Extension;

            string strConn;
            switch (extension)
            {
                case ".xls":
                    strConn = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + pathName + ";Extended Properties='Excel 8.0;HDR=Yes;IMEX=1;'";
                    break;
                case ".xlsx":
                    strConn = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + pathName + ";Extended Properties='Excel 12.0;HDR=Yes;IMEX=1;'";
                    break;
                default:
                    strConn = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + pathName + ";Extended Properties='Excel 8.0;HDR=Yes;IMEX=1;'";
                    break;
            }

            var cnnxls = new OleDbConnection(strConn);
            var oda = new OleDbDataAdapter($"select * from [{sheetName}$]", cnnxls);

            var tbContainer = new DataTable();
            oda.Fill(tbContainer);
            return tbContainer;
        }

        /// <summary>
        /// Checks the given DataTable for the existence of all the given columns.
        /// </summary>
        /// <param name="data">The DataTable to be validated.</param>
        /// <param name="columns">A list of column names.</param>
        /// <returns>True if all columns exist; false otherwise.</returns>
        internal static bool ValidateColumnNames(DataTable data, List<string> columns)
        {
            var isValid = true;

            foreach (var column in columns)
            {
                if (data.Columns.Contains(column)) continue;
                Logger.Warning("*** MISSING COLUMN: {0}", column);
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// Put the specified collection into the cloud update queue.
        /// </summary>
        /// <param name="accession"></param>
        internal static void QueueUpdate(string accession)
        {
            using (var context = DataAccess.GetDataContext(Settings.Current.ConnectionString, true))
            {
                if (context.QueuedUpdates.Any(u => u.Accession == accession))
                {
                    Logger.Write($"Collection {accession} already in digital archive update queue.");
                }
                else
                {
                    context.QueuedUpdates.InsertOnSubmit(new QueuedUpdate { Accession = accession });
                    context.SubmitChanges();
                    Logger.Write($"Collection {accession} added to digital archive update queue.");
                }
            }
        }
    }
}
