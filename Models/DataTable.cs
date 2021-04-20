using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Load_Test_Visualiser.Models
{
    public class DataTable
    {
        public List<Column> Columns { get; set; }
        public List<Row> Rows { get; set; }
    }
}
