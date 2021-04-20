using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Load_Test_Visualiser.Models
{
    public class NormalisedSamples
    {
        public string ThreadName { get; set; }
        public string Label { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public bool Failure { get; set; }
        public string ResponseMessage { get; set; }
    }
}
