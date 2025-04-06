using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using SdfComparator.DecoratedText;
using SdfComparator.Model.Db;

namespace SdfComparator.View {
    class ConsoleView: IView {
        public ConsoleView() {
        }

        public void ShowHelp(Presenter.Presenter pstr) {
            Console.WriteLine($"Usage: {pstr.GetPrgmName()} [options] {{path/to/file1.sdf}} {{path/to/file2.sdf}}");
            Console.WriteLine(pstr.GetHelpText());
        }

        public void ShowMessage(Presenter.Presenter pstr, string msg) {
            Console.WriteLine(msg);
        }

        public void ShowDecoratedLine(Presenter.Presenter pstr, DecoratedTextLine line) {
            foreach (var block in line) {
                if (pstr.DoColors() && block.TryGetValue("color", out var color)) {
                    switch (color) {
                        case "green":
                            Console.ForegroundColor = ConsoleColor.Green;
                            break;
                        case "red":
                            Console.ForegroundColor = ConsoleColor.Red;
                            break;
                    }
                }
                Console.Write(block.Text);
                if (pstr.DoColors()) {
                    Console.ResetColor();
                }
            }
            Console.Write("\n");
        }

        private void ShowDecoratedLines(
                Presenter.Presenter pstr,
                List<DecoratedTextLine> lines) {
            foreach (var line in lines) {
                ShowDecoratedLine(pstr, line);
            }
        }

        private bool ShowDecoratedSection(
                Presenter.Presenter pstr,
                List<string> headers,
                List<DecoratedTextLine> lines) {
            bool b_print = (lines.Count > 0);

            if (b_print) {
                foreach (var header in headers) {
                    ShowMessage(pstr, header);
                }
            }
            ShowDecoratedLines(pstr, lines);

            return b_print;
        }

        public void ShowDiffs(Presenter.Presenter pstr, DatabaseTuple db_tup) {
            var headers = new List<string>() {
                    $"--- {db_tup.Orig.Filepath}",
                    $"+++ {db_tup.Dest.Filepath}",
                    ""
                    };

            if (pstr.DoShowTables()) {
                var lines = GetTableDiffLines(db_tup);
                if (ShowDecoratedSection(pstr, headers, lines)) {
                    headers.Clear();
                }
            }

            int header_table_start;
            foreach (var table_tup in db_tup.MatchedTables) {
                header_table_start = headers.Count;
                headers.AddRange(new string[] {
                        "",
                        $"---- {table_tup.OrigName}",
                        $"++++ {table_tup.DestName}"
                        });

                if (pstr.DoShowColumns()) {
                    var lines = GetColDiffLines(table_tup);
                    if (ShowDecoratedSection(pstr, headers, lines)) {
                        headers.Clear();
                    }
                }

                if (pstr.DoShowRows()) {
                    var changes = table_tup.GetRowChanges();
                    headers.AddRange(new string[] {
                            "",
                            "  Rows:"
                            });
                    var s = "  ";

                    //Initialize max_len_map to the header name length
                    var max_len_map = new List<int>();
                    foreach (var item in table_tup.MatchedCols.Select(
                            (col_tup, i) => new { i, col_tup }
                            )) {
                        max_len_map.Add(item.col_tup.Names.Select(v => v.Length).Max());
                    }
                    var lines = GetRowDiffLines(changes, max_len_map);
                    foreach (var item in table_tup.MatchedCols.Select(
                            (col_tup, i) => new { i, col_tup }
                            )) {
                        var name = item.col_tup.Names[0];
                        s += $" | {name.PadRight(max_len_map[item.i])}";
                    }
                    s += " |";
                    headers.AddRange(new string[] {
                            "",
                            s
                            });

                    if (ShowDecoratedSection(pstr, headers, lines)) {
                        headers.Clear();
                    }
                }

                if (headers.Count > header_table_start) {
                    headers.RemoveRange(header_table_start, headers.Count - header_table_start);
                }
            }
        }

        static List<DecoratedTextLine> GetTableDiffLines(DatabaseTuple db_tup, List<DecoratedTextLine> lines = null) {
            if (lines is null) {
                lines = new List<DecoratedTextLine>();
            }

            var added_tables = new SortedSet<string>();
            var removed_tables = new SortedSet<string>();
            foreach (var diff_table_tup in db_tup.UnmatchedTables) {
                if (diff_table_tup.Names[1] is null) {
                    removed_tables.Add(diff_table_tup.Names[0]);
                } else {
                    added_tables.Add(diff_table_tup.Names[1]);
                }
            }

            foreach (var tup in new Tuple<SortedSet<string>, bool>[] {
                    new Tuple<SortedSet<string>, bool>(added_tables, true),
                    new Tuple<SortedSet<string>, bool>(removed_tables, false) }) {
                var set = tup.Item1;
                var b_is_add = tup.Item2;
                if (set.Count > 0) {
                    foreach (var table_name in set) {
                        string prefix;
                        string color;

                        if (b_is_add) {
                            prefix = "+ ";
                            color = "green";
                        } else {
                            prefix = "- ";
                            color = "red";
                        }
                        lines.Add(new DecoratedTextLine(
                                new DecoratedTextBlock($"{prefix}{table_name}")
                                    .WithDecoration("color", color)
                                ));
                    }
                }
            }

            return lines;
        }

        private static string GetPaddedString(
                Row r,
                List<int> max_len_map,
                char diff_marker
                ) {
            var diff_s = $" {diff_marker} | ";
            var prefix = "";
            foreach (var (val, i) in ((IEnumerable<object>)r).Select(
                    (i, value) => (i, value))) {
                var pad_len = max_len_map[i];
                diff_s += $"{prefix}{val.ToString().PadRight(pad_len)}";
                prefix = " | ";
            }
            diff_s += " |";
            return diff_s;
        }

        private static List<DecoratedTextLine> GetRowDiffLines(
                List<RowChange> changes,
                List<int> max_len_map,
                List<DecoratedTextLine> lines = null
                ) {
            if (lines is null) {
                lines = new List<DecoratedTextLine>();
            }

            //Figure out the length of each col, so every row is aligned
            foreach (var change in changes) {
                for (int i = 0; i < changes.Count; i++) {
                    var orig_len = 0;
                    var dst_len = 0;
                    if (!(change.Orig is null)) {
                        orig_len = change.Orig[i].ToString().Length;
                    } else if (!(change.Dst is null)) {
                        dst_len = change.Dst[i].ToString().Length;
                    }
                    max_len_map[i] = new int[] {
                            max_len_map[i],
                            orig_len,
                            dst_len }.Max();
                }
            }

            foreach (var change in changes) {
                string s;
                string del_s = null;
                string add_s = null;
                if (change.Orig is null) {
                    s = null;
                    add_s = GetPaddedString(change.Dst, max_len_map, '+');
                } else if (change.Dst is null) {
                    s = null;
                    del_s = GetPaddedString(change.Orig, max_len_map, '-');
                } else {
                    int[] cols_pos = new int[change.Orig.Length];

                    s = "   | ";
                    del_s = " -  ";
                    add_s = " +  ";
                    string prefix = "";

                    /*
                     * from 0 to each diff index, calculate the total substring length of the
                     * orig line up to that diff, so that we can pad each diff line (added and removed)
                     * and thus line all 3 of them together.
                     */
                    int col_idx = 0;
                    foreach (var idx in change.Diffs.Union(new int[] { change.Orig.Length })) {
                        for (int i = col_idx; i < idx && i < change.Orig.Length; i++) {
                            cols_pos[i] = -1;
                            var pad_len = max_len_map[i];
                            s += $"{prefix}{change.Orig[i].ToString().PadRight(pad_len)}";
                            prefix = " | ";
                        }
                        col_idx = idx + 1;
                        if (idx < change.Orig.Length) {
                            var pad_len = max_len_map[idx];
                            cols_pos[idx] = s.Length;
                            var orig_s = change.Orig[idx].ToString().PadRight(pad_len);
                            var dst_s = change.Dst[idx].ToString().PadRight(pad_len);

                            s += prefix;
                            del_s = del_s.PadRight(s.Length, ' ') + orig_s;
                            add_s = add_s.PadRight(s.Length, ' ') + dst_s;
                            s = s.PadRight(s.Length + Math.Max(orig_s.Length, dst_s.Length), ' ');
                            prefix = " | ";
                        }
                    }
                    s += " |";
                }
                if (s != null) {
                    lines.Add(new DecoratedTextLine(new TextBlock(s)));
                }
                if (del_s != null) {
                    lines.Add(new DecoratedTextLine(
                            new DecoratedTextBlock(del_s)
                            .WithDecoration("color", "red")));
                }
                if (add_s != null) {
                    lines.Add(new DecoratedTextLine(
                            new DecoratedTextBlock(add_s)
                            .WithDecoration("color", "green")));
                }
            }

            return lines;
        }

        private static List<DecoratedTextLine> GetColDiffLines(TableTuple table_tup, List<DecoratedTextLine> lines = null) {
            if (lines is null) {
                lines = new List<DecoratedTextLine>();
            }

            var added_cols = new SortedSet<string>();
            var removed_cols = new SortedSet<string>();
            foreach (var diff_col_tup in table_tup.UnmatchedCols) {
                if (diff_col_tup.Names[1] is null) {
                    removed_cols.Add(diff_col_tup.Names[0]);
                } else {
                    added_cols.Add(diff_col_tup.Names[1]);
                }
            }

            foreach (var tup in new Tuple<SortedSet<string>, bool>[] {
                    new Tuple<SortedSet<string>, bool>(added_cols, true),
                    new Tuple<SortedSet<string>, bool>(removed_cols, false)}) {
                var set = tup.Item1;
                var b_is_add = tup.Item2;
                if (set.Count > 0) {
                    foreach (var col_name in set) {
                        string prefix;
                        string color;
                        if (b_is_add) {
                            prefix = "+   ";
                            color = "green";
                        } else {
                            prefix = "-   ";
                            color = "red";
                        }
                        lines.Add(new DecoratedTextLine(
                                new DecoratedTextBlock($"{prefix}{col_name}")
                                .WithDecoration("color", color)
                                ));
                    }
                }
            }

            return lines;
        }
    }
}
