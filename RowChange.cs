using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SDF_comparator {
    class RowChange {
        private Row orig_row;
        private Row dst_row;
        private List<int> row_diff_idxs;

        public Row Orig { get { return orig_row; } }
        public Row Dst { get { return dst_row; } }

        public RowChange() : this(null, null) {}

        public RowChange(Row orig, Row dst) {
            orig_row = orig;
            dst_row = dst;
            row_diff_idxs = new List<int>();
        }
    }
}
