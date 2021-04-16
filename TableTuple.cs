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
        public DatabaseTuple DbTuple { get; private set; }

        /* TODO: instead of returning readers, we should have an Enumerable interface that spits out rows
         * Then have another method that gives out a ready-made List<RowChange>
         * Effectively, we'd be moving build_row_dicts(), prune_full_matches() and build_row_changes() in here
         */
        public SqlCeDataReader OrigReader => get_reader_from_table_and_cols(DbTuple.Orig[OrigName], MatchedCols, true);
        public SqlCeDataReader DestReader => get_reader_from_table_and_cols(DbTuple.Dest[DestName], MatchedCols, false);
        public List<SqlCeDataReader> Readers => new SqlCeDataReader[] { OrigReader, DestReader }.ToList<SqlCeDataReader>();

        public TableTuple(DatabaseTuple parent, string orig_table_name, string dest_table_name) {
            OrigName = orig_table_name;
            DestName = dest_table_name;
            MatchedCols = new List<ColumnTuple>();
            UnmatchedCols = new List<ColumnTuple>();
            this.DbTuple = parent;

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

            var orig_col_d = DbTuple.Orig[OrigName].columns;
            var dest_col_d = DbTuple.Dest[DestName].columns;

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
            DbTuple.Orig.Connection.Open();
            var row_dicts = build_row_dicts(OrigReader);
            DbTuple.Orig.Connection.Close();

            DbTuple.Dest.Connection.Open();
            var dest_rows = prune_full_matches(DestReader, row_dicts);
            DbTuple.Dest.Connection.Close();

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


        /* Each time a row from reader fully matches a row in row_dicts, remove that
         * row from every row_dicts subdictionary and skip adding it to the returned List.
         * Every other row is added to the result.
         */
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

        class RowMatch {
            public List<int> Cols { get; private set; }
            public Row Src { get; private set; }
            public Row Dst { get; private set; }
            public int Count => Cols.Count();

            public RowMatch(List<int> col_idxs, Row src, Row dst) {
                Cols = new List<int>(col_idxs);
                Src = src;
                Dst = dst;
            }
        }


        private List<RowChange> build_row_changes(List<Dictionary<object, List<Row>>> row_dicts, List<Row> dest_rows) {
            var changes = new List<RowChange>();

            var unmatched_dst_rows = new List<Row>();
            var pot_unmatched_dst_rows = new Dictionary<Row, bool>();
            int nb_rows = row_dicts.Count;

            /* TODO: think of a way to rank order partial string matches.
             * It would be better for AAAAAAAB to match AAAAAAAA rather than CCCCCCCB for example.
             * Then again, is 2 near-perfect matches better than 1 perfect match + a very poor match?
             * How do we score row match with multiple partial matches?
             * Perhaps we only score string fields and get a "partial score" that’s only used to differentiate
             * candidates within the same rank order?
             */
            var ranked_matches = new SortedDictionary<int, List<RowMatch>>(new ReverseIntComparer());
            foreach (var dst_row in dest_rows) {
                //For each row value, gather all rows that match on at least that column value, without duplicates
                var pot_matches = new HashSet<Row>();
                for (int i = 0; i < row_dicts.Count; i++) {
                    var row_dict = row_dicts[i];
                    if (row_dict.TryGetValue(dst_row[i], out List<Row> idx_rows)) {
                        pot_matches.UnionWith(idx_rows);
                    }
                }

                var col_matches = new List<int>();
                foreach (var pot_match in pot_matches) {
                    col_matches.Clear();
                    for (int i = 0; i < pot_match.Length; i++) {
                        if (dst_row[i].Equals(pot_match[i])) {
                            col_matches.Add(i);
                        }
                    }

                    int match_count = col_matches.Count;

                    /* We need to keep track of all potentially-matched rows since it's possible
                     * for a row to be the second-best match for many rows and thus actually be unmatched
                     * TFW no match dest row PepeHands
                     */
                    if (!pot_unmatched_dst_rows.TryGetValue(dst_row, out var b_matched)) {
                        pot_unmatched_dst_rows.Add(dst_row, true);
                    }

                    if (!ranked_matches.TryGetValue(match_count, out var list)) {
                        list = new List<RowMatch>();
                        ranked_matches.Add(match_count, list);
                    }
                    list.Add(new RowMatch(col_matches, pot_match, dst_row));
                }
            }

            /* It’s easier to just mark matched src rows rather than looking for them through row_dicts
             * (since we need to check if first_match_idx exists and then look for the match.Src in that list)
             */
            var matched_src_rows = new Dictionary<Row, bool>();
            /* Cycle through all matches, starting with the best matches (highest rank) first
             */
            foreach (var match_keypair in ranked_matches) {
                int rank = match_keypair.Key;
                var matches = match_keypair.Value;

                foreach (var match in matches) {
                    /* A given row may match multiple output rows slightly differently so we first need to
                     * make sure the row hasn't already matched (in which case it'll have been removed from row_dicts)
                     */
                    int first_match_idx = match.Cols[0];
                    if (pot_unmatched_dst_rows[match.Dst]
                            && !matched_src_rows.ContainsKey(match.Src)) {
                        //Go from matching column indexes to difference column indexes
                        var diff_idxs = new List<int>();
                        int start_idx = 0;
                        foreach (var match_idx in match.Cols.Union(new int[] { nb_rows })) {
                            for (int i = start_idx; i < match_idx; i++) {
                                diff_idxs.Add(i);
                            }
                            start_idx = match_idx + 1;
                        }

                        pot_unmatched_dst_rows[match.Dst] = false;
                        matched_src_rows[match.Src] = true;

                        changes.Add(new RowChange(match.Src, match.Dst, diff_idxs));

                        //Remove the src row from every row_dicts subdictionary
                        for (int i = 0; i < row_dicts.Count; i++) {
                            row_dicts[i][match.Src[i]].Remove(match.Src);
                        }
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

            var new_rows = from kp in pot_unmatched_dst_rows where kp.Value select kp.Key;
            foreach (var row in new_rows) {
                changes.Add(new RowChange(null, row));
            }
            return changes;
        }
    }
}
