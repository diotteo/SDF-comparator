using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SDF_comparator {
    class TableTuple {
        public List<string> Names { get; private set; }
        public List<ColumnTuple> diff_cols;
        public List<ColumnTuple> col_tuples;
        private DatabaseTuple db_tup;
        public TableTuple(DatabaseTuple parent, string orig_table_name, string dest_table_name) {
            Names = new List<string>();
            Names.Add(orig_table_name);
            Names.Add(dest_table_name);
            col_tuples = new List<ColumnTuple>();
            diff_cols = new List<ColumnTuple>();
            db_tup = parent;

            if (orig_table_name != null && dest_table_name != null) {
                match_cols();
            }
        }
        class ColCount {
            public int count;
            public bool b_in_orig;

            public ColCount(bool b_in_orig) {
                count = 1;
                this.b_in_orig = b_in_orig;
            }
        }
        private void match_cols() {
            var col_counts = new Dictionary<string, ColCount>();

            var orig_col_d = db_tup.Orig.tables[Names[0]].columns;
            var dest_col_d = db_tup.Dest.tables[Names[1]].columns;

            foreach (var col_dict in new Dictionary<string, CachedColumn>[] {
                    orig_col_d,
                    dest_col_d}) {
                foreach (var table_keypair in col_dict) {
                    var name = table_keypair.Key;
                    if (!col_counts.ContainsKey(name)) {
                        col_counts.Add(name, new ColCount(col_dict == orig_col_d));
                    } else {
                        col_counts[name].count++;
                    }
                }
            }

            foreach (var kp in col_counts) {
                var name = kp.Key;
                var count = kp.Value.count;
                bool b_in_orig = kp.Value.b_in_orig;
                switch (count) {
                case 1:
                    string orig_name = null;
                    string dest_name = null;
                    if (b_in_orig) {
                        orig_name = name;
                    } else {
                        dest_name = name;
                    }
                    diff_cols.Add(new ColumnTuple(this, orig_name, dest_name));
                    break;
                case 2:
                    col_tuples.Add(new ColumnTuple(this, name, name));
                    break;
                default:
                    throw new Exception($"{name} has {count} occurences");
                }
            }
        }
    }
}
