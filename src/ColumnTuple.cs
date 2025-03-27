using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SdfComparator {
    class ColumnTuple {
        public string OrigName { get; private set; }
        public string DestName { get; private set; }
        public List<string> Names => new string[] { OrigName, DestName }.ToList<string>();
        public TableTuple TableTuple { get; private set; }


        public ColumnTuple(TableTuple parent, string orig_name, string dest_name) {
            OrigName = orig_name;
            DestName = dest_name;
            TableTuple = parent;
        }
    }
}
