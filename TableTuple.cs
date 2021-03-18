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

        public List<RowChange> get_row_changes() {
            /* A list of dictionaries each containing a collection of rows with matching column values
             * row_dicts[1][col_val][3] means "get the 4th row whose 2nd column has a value of col_var"
             */
            Parent.Orig.Connection.Open();
            var row_dicts = build_row_dicts(OrigReader);
            Parent.Orig.Connection.Close();

            Parent.Dest.Connection.Open();
            var dest_rows = prune_full_matches(DestReader, row_dicts);
            Parent.Dest.Connection.Close();

            return build_row_changes(row_dicts, dest_rows);
        }

        private List<Dictionary<object, List<Row>>> build_row_dicts(SqlCeDataReader rdr) {
            var row_dicts = new List<Dictionary<object, List<Row>>>();
            object[] raw_row = new object[rdr.FieldCount];

            /* Create the empty dictionaries, even if there is no row in the table,
                * just so we don't have to deal with that edge case elsewhere
                */
            for (int i = 0; i < rdr.FieldCount; i++) {
                row_dicts.Add(new Dictionary<object, List<Row>>());
            }

            while (rdr.Read()) {
                rdr.GetValues(raw_row);
                var cur_row = new Row(raw_row);
                for (int j = 0; j < cur_row.Length; j++) {
                    if (!row_dicts[j].ContainsKey(cur_row[j])) {
                        row_dicts[j].Add(cur_row[j], new List<Row>());
                    }
                    row_dicts[j][cur_row[j]].Add(cur_row);
                }
            }

            return row_dicts;
        }

        private List<Row> prune_full_matches(SqlCeDataReader rdr, List<Dictionary<object, List<Row>>> row_dicts) {
            var dest_rows = new List<Row>();
            object[] raw_row = new object[rdr.FieldCount];
            while (rdr.Read()) {
                rdr.GetValues(raw_row);
                var cur_row = new Row(raw_row);

                /* Remove the first full match
                 * A full match is that of the first column matches that also matches every other column
                 * We can't just check for a match of every column since multiple rows could each match on a single value
                 */
                var pot_full_matches = new List<Row>();
                if (row_dicts[0].TryGetValue(cur_row[0], out List<Row> first_col_matches)) {
                    bool b_had_full_match = false;
                    foreach (var pot_match in first_col_matches) {
                        bool b_is_full_match = true;
                        for (int j = 1; j < pot_match.Length; j++) {
                            if (!pot_match[j].Equals(cur_row[j])) {
                                b_is_full_match = false;
                                break;
                            }
                        }

                        //If full match, remove match from every column dict
                        if (b_is_full_match) {
                            b_had_full_match = true;
                            for (int j = 0; j < row_dicts.Count; j++) {
                                row_dicts[j][cur_row[j]].Remove(pot_match);
                            }
                            break;
                        }
                    }

                    if (!b_had_full_match) {
                        dest_rows.Add(cur_row);
                    }
                } else {
                    dest_rows.Add(cur_row);
                }
            }
            return dest_rows;
        }

        private List<RowChange> build_row_changes(List<Dictionary<object, List<Row>>> row_dicts, List<Row> dest_rows) {
            var changes = new List<RowChange>();

            var pot_matches = new HashSet<Row>();
            var unmatched_dst_rows = new List<Row>();

            foreach (var dst_row in dest_rows) {
                //For each row value, gather all rows that match on at least that column value, without duplicates
                pot_matches.Clear();
                for (int i = 0; i < row_dicts.Count; i++) {
                    var row_dict = row_dicts[i];
                    if (row_dict.TryGetValue(dst_row[i], out List<Row> idx_rows)) {
                        pot_matches.UnionWith(idx_rows);
                    }
                }

                Row best_match = null;
                List<int> best_match_idxs = null;
                var col_matches = new List<int>();
                foreach (var pot_match in pot_matches) {
                    col_matches.Clear();
                    for (int i = 0; i < pot_match.Length; i++) {
                        if (dst_row[i].Equals(pot_match[i])) {
                            col_matches.Add(i);
                        }
                    }
                    if (col_matches.Count > 0
                            && (best_match_idxs is null || best_match_idxs.Count < col_matches.Count)) {
                        best_match_idxs = new List<int>(col_matches);
                        best_match = pot_match;
                    }
                }
                if (best_match is null) {
                    unmatched_dst_rows.Add(dst_row);
                } else {
                    var diff_idxs = new List<int>();

                    int start_idx = 0;
                    foreach (var match_idx in best_match_idxs.Union(new int[] { best_match_idxs.Count })) {
                        for (int i = start_idx; i < match_idx; i++) {
                            diff_idxs.Add(i);
                        }
                        start_idx = match_idx + 1;
                    }

                    changes.Add(new RowChange(best_match, dst_row, diff_idxs));
                    for (int i = 0; i < row_dicts.Count; i++) {
                        //use best_match[i] instead of dst_row[i] since we're doing partial matches, so the match is indexed elsewhere in some columns
                        row_dicts[i][best_match[i]].Remove(best_match);
                    }
                }
            }

            /* row_dicts is a list of {column_count} dictionaries of rows, indexed by their values for each column
             * Here, we just want to add all rows that have at least one change to changes, so we just go through the first dictionary
             * (because it doesn't matter which one we use).
             */
            foreach (var row_pair in row_dicts[0]) {
                foreach (var row in row_pair.Value) {
                    changes.Add(new RowChange(row, null));
                }
            }

            foreach (var row in unmatched_dst_rows) {
                changes.Add(new RowChange(null, row));
            }
            return changes;
        }
    }
}
