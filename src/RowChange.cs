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
        public List<int> Diffs { get { return row_diff_idxs; } }


        public RowChange() : this(null, null) {}

        public RowChange(Row orig, Row dst) : this(orig, dst, null) {}

        public RowChange(Row orig, Row dst, List<int> diff_idxs) {
            orig_row = orig;
            dst_row = dst;

            if (diff_idxs is null) {
                row_diff_idxs = new List<int>();
            } else {
                row_diff_idxs = new List<int>(diff_idxs);
            }
        }
    }
}
