using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InformediaCORE.Azure
{
    /// <summary>
    /// The list of valid digital archive specifiers
    /// </summary>
    public enum DigitalArchiveSpecifier
    {
        /// <summary>
        /// Specifies the Processing (a.k.a. QA) Digital Archive
        /// </summary>
        Processing,
        /// <summary>
        /// Specifies the Prodution (a.k.a. Live) Digtial Archive
        /// </summary>
        Production
    }
}
