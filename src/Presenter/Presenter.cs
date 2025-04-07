using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NDesk.Options;

using SdfComparator.Model.Db;
using SdfComparator.DecoratedText;

namespace SdfComparator.Presenter {

    class Presenter {
        private readonly Model.Settings settings;
        private readonly View.IView view;

        public Presenter(Model.Settings settings, View.IView view) {
            this.settings = settings;
            this.view = view;

            var prgm_name = System.IO.Path.GetFileName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
            var version = typeof(SdfComparator).Assembly.GetName().Version.ToString();
            this.settings.PrgmName = prgm_name;
            this.settings.Version = version;
        }

        public string GetPrgmName() {
            return settings.PrgmName;
        }

        public string GetHelpText() {
            return settings.HelpText;
        }

        public bool DoColors() {
            return settings.DoColors;
        }

        public int ParseArgs(string[] args) {
            settings.ShowHelp = false;
            settings.ShowVersion = false;
            settings.ShowCols = false;
            settings.ShowRows = false;
            settings.ShowTables = false;
            settings.DoColors = true;

            var p = new OptionSet() {
                {"h|help", "Show this help message and exit", v => settings.ShowHelp = v != null},
                {"v|version", "Print the version and exit", v => settings.ShowVersion = v != null},
                {"c|col", "Print column differences", v => settings.ShowCols = v != null},
                {"r|row", "Print each row differences", v => settings.ShowRows = v != null},
                {"t|table", "Print table differences", v => settings.ShowTables = v != null},
                {"no-color", "Disable colored output", v => settings.DoColors = v != null},
            };
            var filepaths = p.Parse(args);

            var buffer = new System.IO.StringWriter();
            p.WriteOptionDescriptions(buffer);
            settings.HelpText = buffer.ToString();

            if (settings.ShowHelp) {
                view.ShowHelp(this);
                return 0;
            } else if (settings.ShowVersion) {
                view.ShowMessage(this, $"{settings.PrgmName} v{settings.Version}");
                return 0;
            } else if (filepaths.Count != 2) {
                view.ShowMessage(this, "Error: 2 files required");
                view.ShowHelp(this);
                return 1;
            }

            settings.Filepaths = filepaths.ToArray();
            foreach (var fpath in settings.Filepaths) {
                if (!System.IO.File.Exists(fpath)) {
                    view.ShowMessage(this, $"Error: \"{fpath}\" is not a file");
                    view.ShowHelp(this);
                    return 1;
                }
            }
            if (!settings.ShowTables && !settings.ShowCols && !settings.ShowRows) {
                settings.ShowAll = true;
            } else {
                settings.ShowAll = false;
            }

            return -1;
        }

        public bool DoShowTables() {
            return settings.ShowAll || settings.ShowTables;
        }

        public bool DoShowColumns() {
            return settings.ShowAll || settings.ShowCols;
        }

        public bool DoShowRows() {
            return settings.ShowAll || settings.ShowRows;
        }

        public void Run() {
            var db_tup = new DatabaseTuple(settings.Filepaths[0], settings.Filepaths[1]);

            view.ShowDiffs(this, db_tup);
        }

        public List<DecoratedTextLine> InitHeaders(DatabaseTuple db_tup) {
            var headers = new List<DecoratedTextLine>() {
                    new DecoratedTextLine($"--- {db_tup.Orig.Filepath}"),
                    new DecoratedTextLine($"+++ {db_tup.Dest.Filepath}"),
                    new DecoratedTextLine("")
                    };
            return headers;
        }

        public List<DecoratedTextLine> GetTableDiffLines(
                DatabaseTuple db_tup,
                List<DecoratedTextLine> lines = null) {

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

        public List<DecoratedTextLine> GetTableRowDiffLines(
                DatabaseTuple db_tup,
                List<DecoratedTextLine> headers) {

            var lines = new List<DecoratedTextLine>(headers);
            var potential_lines_start = lines.Count;
            foreach (var table_tup in db_tup.MatchedTables) {
                lines.AddRange(new DecoratedTextLine[] {
                        new DecoratedTextLine(""),
                        new DecoratedTextLine($"---- {table_tup.OrigName}"),
                        new DecoratedTextLine($"++++ {table_tup.DestName}")
                        });
                potential_lines_start = lines.Count;

                if (DoShowColumns()) {
                    var col_lines = GetColDiffLines(table_tup);
                    lines.AddRange(col_lines);
                    if (col_lines.Count > 0) {
                        potential_lines_start = lines.Count;
                    }
                }

                if (DoShowRows()) {
                    var changes = table_tup.GetRowChanges();
                    lines.AddRange(new DecoratedTextLine[] {
                            new DecoratedTextLine(""),
                            new DecoratedTextLine("  Rows:")
                            });
                    var s = "  ";

                    //Initialize max_len_map to the header name length
                    var max_len_map = new List<int>();
                    foreach (var item in table_tup.MatchedCols.Select(
                            (col_tup, i) => new { i, col_tup }
                            )) {
                        max_len_map.Add(item.col_tup.Names.Select(v => v.Length).Max());
                    }
                    var row_lines = GetRowDiffLines(changes, max_len_map);
                    foreach (var item in table_tup.MatchedCols.Select(
                            (col_tup, i) => new { i, col_tup }
                            )) {
                        var name = item.col_tup.Names[0];
                        s += $" | {name.PadRight(max_len_map[item.i])}";
                    }
                    s += " |";
                    lines.AddRange(new DecoratedTextLine[] {
                            new DecoratedTextLine(""),
                            new DecoratedTextLine(s)
                            });
                    if (row_lines.Count > 0) {
                        lines.AddRange(row_lines);
                        potential_lines_start = lines.Count;
                    }
                }

                if (lines.Count > potential_lines_start) {
                    lines.RemoveRange(
                            potential_lines_start,
                            lines.Count - potential_lines_start);
                }
            }

            return lines;
        }

        public static string GetPaddedString(
                Row r,
                List<int> max_len_map,
                char diff_marker) {

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

        public static List<DecoratedTextLine> GetRowDiffLines(
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

        public List<DecoratedTextLine> GetColDiffLines(
                TableTuple table_tup,
                List<DecoratedTextLine> lines = null) {

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
