using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlServerCe;
using System.Diagnostics;

namespace SDF_comparator {
    class SDF_comparator {
        const string VERSION_STR = "0.2";
        private static SqlCeConnection sqlce_from_filepath(string filepath) {
            return new SqlCeConnection("Data Source = " + filepath + ";");
        }
        private static void write_line(string s) {
#if DEBUG
            Debug.WriteLine(s);
#else
            Console.WriteLine(s);
#endif
        }
        private static SqlCeDataReader get_sqlce_reader(string filepath, out SqlCeConnection conn) {
            SqlCeDataReader rdr = null;
            conn = sqlce_from_filepath(filepath);
            conn.Open();

            SqlCeCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM SEEDS;";
            rdr = cmd.ExecuteReader();

            return rdr;
        }
        private static List<Dictionary<object, List<Row>>> build_row_dicts(SqlCeDataReader rdr) {
            var row_dicts = new List<Dictionary<object, List<Row>>>();
            object[] raw_row = new object[rdr.FieldCount];
            while (rdr.Read()) {
                rdr.GetValues(raw_row);
                var cur_row = new Row(raw_row);
                for (int j = 0; j < cur_row.Length; j++) {
                    if (row_dicts.Count <= j) {
                        row_dicts.Insert(j, new Dictionary<object, List<Row>>());
                    }
                    //row_dicts[j].TryGetValue()
                    if (!row_dicts[j].ContainsKey(cur_row[j])) {
                        row_dicts[j].Add(cur_row[j], new List<Row>());
                    }
                    row_dicts[j][cur_row[j]].Add(cur_row);
                }
            }

            return row_dicts;
        }
        private static List<Row> prune_full_matches(SqlCeDataReader rdr, List<Dictionary<object, List<Row>>> row_dicts) {
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
        private static void print_diffs(List<RowChange> changes, string[] filepaths) {
            write_line($"--- {filepaths[0]}\n+++ {filepaths[1]}\n");
            foreach (var change in changes) {
                string s;
                if (change.Orig is null) {
                    s = $"+ {change.Dst}";
                } else if (change.Dst is null) {
                    s = $"- {change.Orig}";
                } else {
                    //TODO: handle differing column schemes (that'll be interesting)
                    int[] cols_pos = new int[change.Orig.Length];

                    int col_idx = 0;
                    s = "   | ";
                    string del_s = " -  ";
                    string add_s = " +  ";
                    string prefix = "";
                    var col_idxs = new List<int>(change.Diffs);
                    col_idxs.Add(change.Orig.Length);
                    foreach (var idx in col_idxs) {
                        for (int i = col_idx; i < idx && i < change.Orig.Length; i++) {
                            cols_pos[i] = -1;
                            s += $"{prefix}{change.Orig[i]}";
                            prefix = " | ";
                        }
                        col_idx = idx+1;
                        if (idx < change.Orig.Length) {
                            cols_pos[idx] = s.Length;
                            var orig_s = change.Orig[idx].ToString();
                            var dst_s = change.Dst[idx].ToString();

                            s += prefix;
                            del_s = del_s.PadRight(s.Length, ' ') + orig_s;
                            add_s = add_s.PadRight(s.Length, ' ') + dst_s;
                            s = s.PadRight(s.Length + Math.Max(orig_s.Length, dst_s.Length), ' ');
                            prefix = " | ";
                        }
                    }
                    s = $"{s}\n{del_s}\n{add_s}\n";
                }
                write_line(s);
            }
        }
        private static List<RowChange> build_row_changes(List<Dictionary<object, List<Row>>> row_dicts, List<Row> dest_rows) {
            var changes = new List<RowChange>();

            var pot_matches = new HashSet<Row>();
            var unmatched_dst_rows = new List<Row>();
            foreach (var dst_row in dest_rows) {
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
        static void Main(string[] args) {
            string[] filepaths = { "../../seeds1.sdf", "../../seeds2.sdf" };

            /* A list of dictionaries each containing a collection of rows with matching column values
             * row_dicts[1][col_val][3] means "get the 4th row whose 2nd column has a value of col_var"
             */
            List<Dictionary<object, List<Row>>> row_dicts = null;
            List<Row> dest_rows = null;
            SqlCeConnection conn = null;
            try {
                for (int i = 0; i < filepaths.Length; i++) {
                    var rdr = get_sqlce_reader(filepaths[i], out conn);
                    if (i == 0) {
                        row_dicts = build_row_dicts(rdr);
                    } else {
                        dest_rows = prune_full_matches(rdr, row_dicts);
                    }
                    conn.Close();
                }
            } finally {
                conn.Close();
            }

            var changes = build_row_changes(row_dicts, dest_rows);
            print_diffs(changes, filepaths);
        }
    }
}