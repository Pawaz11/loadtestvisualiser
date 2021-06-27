using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Load_Test_Visualiser.Models.DataTable
{
    public class ColumnValues
    {
        public string ThreadName { get; set; }
        public string Label { get; set; }
        public long StartEpochMilli { get; set; }
        public long EndEpochMilli { get; set; }
    }
}
