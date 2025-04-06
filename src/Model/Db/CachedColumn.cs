using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SdfComparator.Model.Db {
    class CachedColumn {
        public string Name { get; private set; }
        public string DataType { get; private set; }
        public int Index { get; private set; }
        public int MaxLength { get; private set; }


        public CachedColumn(string name, string data_type, int idx)
            : this(name, data_type, idx, -1) {}

        public CachedColumn(string name, string data_type, int idx, int maxlen) {
            Name = name;
            DataType = data_type;
            Index = idx;
            MaxLength = maxlen;
        }
    }
}
