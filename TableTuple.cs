using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data.SqlServerCe;

namespace SDF_comparator {
    class TableTuple {
        public string OrigName { get; private set; }
        public string DestName { get; private set; }
        public List<string> Names => new string[] { OrigName, DestName }.ToList<string>();
        public List<ColumnTuple> MatchedCols { get; private set; }
        public List<ColumnTuple> UnmatchedCols { get; private set; }
        public DatabaseTuple Parent { get; private set; }

        /* TODO: instead of returning readers, we should have an Enumerable interface that spits out rows
         * Then have another method that gives out a ready-made List<RowChange>
         * Effectively, we'd be moving build_row_dicts(), prune_full_matches() and build_row_changes() in here
         */
        public SqlCeDataReader OrigReader => get_reader_from_table_and_cols(Parent.Orig[OrigName], MatchedCols, true);
        public SqlCeDataReader DestReader => get_reader_from_table_and_cols(Parent.Dest[DestName], MatchedCols, false);
        public List<SqlCeDataReader> Readers => new SqlCeDataReader[] { OrigReader, DestReader }.ToList<SqlCeDataReader>();

        public TableTuple(DatabaseTuple parent, string orig_table_name, string dest_table_name) {
            OrigName = orig_table_name;
            DestName = dest_table_name;
            MatchedCols = new List<ColumnTuple>();
            UnmatchedCols = new List<ColumnTuple>();
            this.Parent = parent;

            if (OrigName != null && DestName != null) {
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

            var orig_col_d = Parent.Orig[OrigName].columns;
            var dest_col_d = Parent.Dest[DestName].columns;

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
                    UnmatchedCols.Add(new ColumnTuple(this, orig_name, dest_name));
                    break;
                case 2:
                    MatchedCols.Add(new ColumnTuple(this, name, name));
                    break;
                default:
                    throw new Exception($"{name} has {count} occurences");
                }
            }
        }

        private static SqlCeDataReader get_reader_from_table_and_cols(CachedTable table, List<ColumnTuple> col_tuples, bool b_is_src) {
            var conn = table.ParentDb.Connection;
            SqlCeCommand cmd = conn.CreateCommand();
            var s = "SELECT ";
            var prefix = "";

            foreach (var col in col_tuples) {
                s += $"{prefix}[{col.Names[b_is_src ? 0 : 1]}]";
                prefix = ", ";
            }
            s += $" FROM {table.Name};";
            cmd.CommandText = s;
            return cmd.ExecuteReader();
        }
    }
}
