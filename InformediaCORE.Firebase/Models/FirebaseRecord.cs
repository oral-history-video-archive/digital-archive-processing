using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InformediaCORE.Firebase.Models
{
    /// <summary>
    /// Model for deserializing JSON from Firebase database.
    /// </summary>
    class FirebaseRecord
    {
        public string Accession { get; set; }

        /// <summary>
        /// A unix timestamp indicating last time tags were modified by a user.
        /// </summary>
        public int LastModified { get; set; }

        public string[] Tags { get; set; }
    }
}
