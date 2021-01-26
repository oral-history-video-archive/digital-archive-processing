using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DocToXMLCleanup
{
    /// <summary>
    /// Convert folder of input .doc and/or .txt files into .xml files suitable for 
    /// ingesting into IDVL repository using an MDF data store.
    /// </summary>
    /// <remarks>See TxtToXMLGeneration class for detailed comments on input requirements.</remarks>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DocumentToXMLCleanupForm());
        }
    }
}
