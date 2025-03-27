using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlServerCe;
using System.Diagnostics;
using NDesk.Options;

namespace SdfComparator {
    class SdfComparator {
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

        private static bool PrintDecoratedSection(List<string> headers, List<DecoratedTextLine> lines, bool b_do_colors) {
            bool b_print = (lines.Count > 0);

            if (b_print) {
                foreach (var header in headers) {
                    Console.WriteLine(header);
                }
            }
            PrintDecoratedLines(lines, b_do_colors);

            return b_print;
        }

        private static void PrintHelp(OptionSet opts) {
            Console.WriteLine($"Usage: {PRGM} [options] {{path/to/file1.sdf}} {{path/to/file2.sdf}}");
            opts.WriteOptionDescriptions(Console.Out);
        }

        static int Main(string[] args) {
            string PRGM = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var model = new Model.Model();
            model.PrgmName = PRGM;
            model.ParseArgs(args);

            var view = new View.ConsoleView(model);
            var ctlr = new Controller.Controller(model, view);

            var db_tup = new DatabaseTuple(
                    new CachedDatabase(filepaths[0]),
                    new CachedDatabase(filepaths[1]));

            var headers = new List<string>() { $"--- {db_tup.Orig.Filepath}\n+++ {db_tup.Dest.Filepath}\n" };
            if (b_print_all || b_print_tables) {
                var lines = GetTableDiffLines(db_tup);
                if (PrintDecoratedSection(headers, lines, b_do_colors)) {
                    headers.Clear();
                }
            }

            int header_table_start;
            foreach (var table_tup in db_tup.MatchedTables) {
                header_table_start = headers.Count;
                headers.Add($"\n---- {table_tup.OrigName}\n++++ {table_tup.DestName}");

                if (b_print_all || b_print_cols) {
                    var lines = GetColDiffLines(table_tup);
                    if (PrintDecoratedSection(headers, lines, b_do_colors)) {
                        headers.Clear();
                    }
                }

                if (b_print_all || b_print_rows) {
                    var changes = table_tup.get_row_changes();
                    headers.Add("\n  Rows:");
                    var s = "  ";
                    var prefix = " |";
                    foreach (var col_tup in table_tup.MatchedCols) {
                        s += $"{prefix} {col_tup.Names[0]}";
                    }
                    s += " |";
                    headers.Add("\n" + s);
                    var lines = GetRowDiffLines(changes);
                    if (PrintDecoratedSection(headers, lines, b_do_colors)) {
                        headers.Clear();
                    }
                }

                if (headers.Count > header_table_start) {
                    headers.RemoveRange(header_table_start, headers.Count - header_table_start);
                }
            }

            return 0;
        }
    }
}