using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlServerCe;
using System.Diagnostics;
using NDesk.Options;

namespace SDF_comparator {
    class SDF_comparator {
        private static readonly string PRGM = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private static List<string> header = new List<string>();


        private static List<DecoratedTextLine> GetRowDiffLines(List<RowChange> changes, List<DecoratedTextLine> lines = null) {
            if (lines is null) {
                lines = new List<DecoratedTextLine>();
            }

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
                if (s != null) {
                    lines.Add(new DecoratedTextLine(new TextBlock(s)));
                }
                if (del_s != null) {
                    lines.Add(new DecoratedTextLine(new DecoratedTextBlock(del_s).WithDecoration("color", "red")));
                }
                if (add_s != null) {
                    lines.Add(new DecoratedTextLine(new DecoratedTextBlock(add_s).WithDecoration("color", "green")));
                }
            }

            return lines;
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
                        lines.Add(new DecoratedTextLine(new DecoratedTextBlock($"{prefix}{table_name}").WithDecoration("color", color)));
                    }
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
                        lines.Add(new DecoratedTextLine(new DecoratedTextBlock($"{prefix}{col_name}").WithDecoration("color", color)));
                    }
                }
            }

            return lines;
        }

        private static void PrintDecoratedLine(DecoratedTextLine line, bool b_do_colors) {
            foreach (var block in line) {
                if (b_do_colors && block.TryGetValue("color", out var color)) {
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
                if (b_do_colors) {
                    Console.ResetColor();
                }
            }
            Console.Write("\n");
        }

        private static void PrintDecoratedLines(List<DecoratedTextLine> lines, bool b_do_colors) {
            foreach (var line in lines) {
                PrintDecoratedLine(line, b_do_colors);
            }
        }

        private static void PrintDecoratedSection(string header, List<DecoratedTextLine> lines, bool b_do_colors) {
            if (lines.Count > 0) {
                Console.WriteLine(header);
            }
            PrintDecoratedLines(lines, b_do_colors);
        }

        private static void PrintHelp(OptionSet opts) {
            Console.WriteLine($"Usage: {PRGM} [options] {{path/to/file1.sdf}} {{path/to/file2.sdf}}");
            opts.WriteOptionDescriptions(Console.Out);
        }

        static int Main(string[] args) {
            var b_print_help = false;
            var b_print_version = false;
            var b_print_cols = false;
            var b_print_rows = false;
            var b_print_tables = false;
            bool b_print_all = false;
            string color_opt = "auto";
            bool b_do_colors = true;

            var p = new OptionSet() {
                {"h|help", "Show this help message and exit", v => b_print_help = v != null},
                {"v|version", "Print the version and exit", v => b_print_version = v != null},
                {"c|col", "Print column differences", v => b_print_cols = v != null},
                {"r|row", "Print each row differences", v => b_print_rows = v != null},
                {"t|table", "Print table differences", v => b_print_tables = v != null},
                {"color", "Enable/disable colored output. Possible values are auto, on and off. Default is auto", v => color_opt = (v is null ? "auto" : v) },
            };
            var filepaths = p.Parse(args);
            switch (color_opt) {
            case "on":
            case "auto":
                b_do_colors = true;
                break;
            default:
                b_do_colors = false;
                break;
            }


            if (b_print_help) {
                PrintHelp(p);
                return 0;
            } else if (b_print_version) {
                Console.WriteLine($"{PRGM} v{typeof(SDF_comparator).Assembly.GetName().Version}");
                return 0;
            } else if (filepaths.Count != 2) {
                Console.WriteLine("Error: 2 files required");
                PrintHelp(p);
                return 1;
            }
            foreach (var fpath in filepaths) {
                if (!File.Exists(fpath)) {
                    Console.WriteLine($"Error: \"{fpath}\" is not a file");
                    PrintHelp(p);
                    return 1;
                }
            }
            if (!b_print_tables && !b_print_cols && !b_print_rows) {
                b_print_all = true;
            }

            var db_tup = new DatabaseTuple(
                    new CachedDatabase(filepaths[0]),
                    new CachedDatabase(filepaths[1]));

            string header;
            if (b_print_all || b_print_tables) {
                header = $"--- {db_tup.Orig.Filepath}\n+++ {db_tup.Dest.Filepath}\n";
                var lines = GetTableDiffLines(db_tup);
                PrintDecoratedSection(header, lines, b_do_colors);
            }
            foreach (var table_tup in db_tup.MatchedTables) {
                header = $"\n---- {table_tup.OrigName}\n++++ {table_tup.DestName}";

                if (b_print_all || b_print_cols) {
                    var lines = GetColDiffLines(table_tup);
                    PrintDecoratedSection(header, lines, b_do_colors);
                }

                if (b_print_all || b_print_rows) {
                    var changes = table_tup.get_row_changes();
                    header = "\n  Rows:";
                    var s = "  ";
                    var prefix = " |";
                    foreach (var col_tup in table_tup.MatchedCols) {
                        s += $"{prefix} {col_tup.Names[0]}";
                    }
                    s += " |";
                    header += "\n" + s;
                    var lines = GetRowDiffLines(changes);
                    PrintDecoratedSection(header, lines, b_do_colors);
                }
            }

            return 0;
        }
    }
}