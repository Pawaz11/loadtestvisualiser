using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Load_Test_Visualiser.Models.DataTable
{
    public class DataTable
    {
        public List<Column> cols { get; set; }
        public List<Row> rows { get; set; }
    }
}
