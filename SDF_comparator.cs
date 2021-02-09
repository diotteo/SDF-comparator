using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlServerCe;
using System.Diagnostics;

namespace SDF_comparator {
    class SDF_comparator {
        private static List<string> header = new List<string>();
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
        private static void print_row_diffs(List<RowChange> changes, string[] filepaths) {
            foreach (var change in changes) {
                string s;
                string del_s = null;
                string add_s = null;
                if (change.Orig is null) {
                    s = null;
                    add_s = $" + {change.Dst}";
                } else if (change.Dst is null) {
                    s = null;
                    del_s = $" - {change.Orig}";
                } else {
                    int[] cols_pos = new int[change.Orig.Length];

                    int col_idx = 0;
                    s = "   | ";
                    del_s = " -  ";
                    add_s = " +  ";
                    string prefix = "";
                    foreach (var idx in change.Diffs.Union(new int[] { change.Orig.Length })) {
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
                    s += " |";
                }
                printWithHeader(null);
                if (s != null) {
                    Utils.WriteLine(s);
                }
                if (del_s != null) {
                    Utils.WriteDiffLine(del_s, false);
                }
                if (add_s != null) {
                    Utils.WriteDiffLine(add_s, true);
                }
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

            if (row_dicts.Count > 0) {
                foreach (var row_pair in row_dicts[0]) {
                    foreach (var row in row_pair.Value) {
                        changes.Add(new RowChange(row, null));
                    }
                }
            }
            foreach (var row in unmatched_dst_rows) {
                changes.Add(new RowChange(null, row));
            }
            return changes;
        }
        static void print_table_diffs(DatabaseTuple db_tup) {
            var added_tables = new SortedSet<string>();
            var removed_tables = new SortedSet<string>();
            foreach (var diff_table_tup in db_tup.diff_tables) {
                if (diff_table_tup.Names[1] is null) {
                    removed_tables.Add(diff_table_tup.Names[0]);
                } else {
                    added_tables.Add(diff_table_tup.Names[1]);
                }
            }

            bool b_is_first = true;
            foreach (var tup in new Tuple<SortedSet<string>, bool>[] {
                    new Tuple<SortedSet<string>, bool>(added_tables, true),
                    new Tuple<SortedSet<string>, bool>(removed_tables, false) }) {
                var set = tup.Item1;
                var b_is_add = tup.Item2;
                if (set.Count > 0) {
                    foreach (var table_name in set) {
                        if (b_is_first) {
                            b_is_first = false;
                            printWithHeader($"Tables:");
                        }
                        var prefix = b_is_add ? "+ " : "- ";
                        Utils.WriteDiffLine($"{prefix}{table_name}", b_is_add);
                    }
                }
            }
        }
        private static bool print_col_diffs(TableTuple table_tup) {
            var added_cols = new SortedSet<string>();
            var removed_cols = new SortedSet<string>();
            foreach (var diff_col_tup in table_tup.diff_cols) {
                if (diff_col_tup.Names[1] is null) {
                    removed_cols.Add(diff_col_tup.Names[0]);
                } else {
                    added_cols.Add(diff_col_tup.Names[1]);
                }
            }

            bool b_is_first = true;
            foreach (var tup in new Tuple<SortedSet<string>, bool>[] {
                        new Tuple<SortedSet<string>, bool>(added_cols, true),
                        new Tuple<SortedSet<string>, bool>(removed_cols, false)}) {
                var set = tup.Item1;
                var b_is_add = tup.Item2;
                if (set.Count > 0) {
                    foreach (var col_name in set) {
                        if (b_is_first) {
                            b_is_first = false;
                            printWithHeader($"  Columns:");
                        }
                        var prefix = b_is_add ? "+   " : "-   ";
                        Utils.WriteDiffLine($"{prefix}{col_name}", b_is_add);
                    }
                }
            }
            return true;
        }
        private static void print_help() {
            //string prgm = System.IO.Path.GetRelativePath(System.IO.Directory.GetCurrentDirectory(), System.Reflection.Assembly.GetExecutingAssembly().Location);
            string prgm = System.Reflection.Assembly.GetExecutingAssembly().Location;
            Utils.WriteLine($"Usage: {prgm} {{path/to/orig.sdf}} {{path/to/dest.sdf}}");
        }
        private static void printWithHeader(string s) {
            foreach (var line in header) {
                Utils.WriteLine(line);
            }
            header.Clear();
            if (s != null) {
                Utils.WriteLine(s);
            }
        }
        static int Main(string[] args) {
            string[] filepaths = new string[2];

            if (args.Length == 2) {
                filepaths[0] = args[0];
                filepaths[1] = args[1];
            } else {
                print_help();
                return 1;
            }

            var db_tup = new DatabaseTuple(
                    new CachedDatabase(filepaths[0]),
                    new CachedDatabase(filepaths[1]));

            header.Add($"--- {filepaths[0]}\n+++ {filepaths[1]}\n");
            print_table_diffs(db_tup);
            foreach (var table_tup in db_tup.table_tuples) {
                header.Add($"\n---- {table_tup.Names[0]}\n++++ {table_tup.Names[1]}");
                print_col_diffs(table_tup);

                /* A list of dictionaries each containing a collection of rows with matching column values
                 * row_dicts[1][col_val][3] means "get the 4th row whose 2nd column has a value of col_var"
                 */
                List<Dictionary<object, List<Row>>> row_dicts = null;
                List<Row> dest_rows = null;

                table_tup.parent.Orig.GetConnection().Open();
                var orig_table_name = table_tup.Names[0];
                var orig_table = table_tup.parent.Orig.tables[orig_table_name];
                var rdr = get_reader_from_table_and_cols(orig_table, table_tup.col_tuples);
                row_dicts = build_row_dicts(rdr);
                table_tup.parent.Orig.GetConnection().Close();

                table_tup.parent.Dest.GetConnection().Open();
                var dest_table_name = table_tup.Names[1];
                var dest_table = table_tup.parent.Dest.tables[dest_table_name];
                rdr = get_reader_from_table_and_cols(dest_table, table_tup.col_tuples);
                dest_rows = prune_full_matches(rdr, row_dicts);
                table_tup.parent.Dest.GetConnection().Close();

                var changes = build_row_changes(row_dicts, dest_rows);
                header.Add("\n  Rows:");
                var s = "  ";
                var prefix = " |";
                foreach (var col_tup in table_tup.col_tuples) {
                    s += $"{prefix} {col_tup.Names[0]}";
                }
                s += " |";
                header.Add(s);
                print_row_diffs(changes, filepaths);
            }

            return 0;
        }

        private static SqlCeDataReader get_reader_from_table_and_cols(CachedTable table, List<ColumnTuple> col_tuples) {
            var conn = table.ParentDb.GetConnection();
            SqlCeCommand cmd = conn.CreateCommand();
            var s = "SELECT ";
            var prefix = "";

            //FIXME: should only use Names[0] for orig reader
            foreach (var col in col_tuples) {
                s += $"{prefix}[{col.Names[0]}]";
                prefix = ", ";
            }
            s += $" FROM {table.Name};";
            cmd.CommandText = s;
            return cmd.ExecuteReader();
        }
    }
}