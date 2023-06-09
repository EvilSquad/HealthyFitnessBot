using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace TGFitnessBot.Models {
    public class Exercise {
        public string name { get; set; }
        public string type { get; set; }
        public string muscle { get; set; }
        public string equipment { get; set; }
        public string difficulty { get; set; }
        public string instructions { get; set; }
        public int id { get; set; }
        public long userid { get; set; }
    }
}
