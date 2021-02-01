using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SDF_comparator {
    class ColumnTuple {
        public List<string> Names { get; private set; }
        private TableTuple table;
        public ColumnTuple(TableTuple parent, string orig_name, string dest_name) {
            Names = new List<string>();
            Names.Add(orig_name);
            Names.Add(dest_name);
            table = parent;
        }
    }
}
