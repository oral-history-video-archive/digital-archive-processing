using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InformediaCORE.Processing.Gentle.Models
{
    /// <summary>
    /// Contains the output of Gentle Forced Aligner.
    /// </summary>
    public class AlignmentResult
    {
        /// <summary>
        /// The transcript given for alignment.
        /// </summary>
        public string Transcript { get; set; }

        /// <summary>
        /// List of words with their resulting aligment data.
        /// </summary>
        public WordResult[] Words { get; set; }
    }
}
