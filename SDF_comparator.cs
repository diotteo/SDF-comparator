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


        private static void print_row_diffs(List<RowChange> changes) {
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

        static void print_table_diffs(DatabaseTuple db_tup) {
            var added_tables = new SortedSet<string>();
            var removed_tables = new SortedSet<string>();
            foreach (var diff_table_tup in db_tup.UnmatchedTables) {
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
            foreach (var diff_col_tup in table_tup.UnmatchedCols) {
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

        private static void printWithHeader(string s) {
            foreach (var line in header) {
                Utils.WriteLine(line);
            }
            header.Clear();
            if (s != null) {
                Utils.WriteLine(s);
            }
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

            var p = new OptionSet() {
                {"h|help", "Show this help message and exit", v => b_print_help = v != null},
                {"v|version", "Print the version and exit", v => b_print_version = v != null},
                {"c|col", "Print column differences", v => b_print_cols = v != null},
                {"r|row", "Print each row differences", v => b_print_rows = v != null},
                {"t|table", "Print table differences", v => b_print_tables = v != null},
            };
            var filepaths = p.Parse(args);

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

            header.Add($"--- {db_tup.Orig.Filepath}\n+++ {db_tup.Dest.Filepath}\n");

            if (b_print_all || b_print_tables) {
                print_table_diffs(db_tup);
            }
            foreach (var table_tup in db_tup.MatchedTables) {
                header.Add($"\n---- {table_tup.OrigName}\n++++ {table_tup.DestName}");

                if (b_print_all || b_print_cols) {
                    print_col_diffs(table_tup);
                }

                if (b_print_all || b_print_rows) {
                    var changes = table_tup.get_row_changes();
                    header.Add("\n  Rows:");
                    var s = "  ";
                    var prefix = " |";
                    foreach (var col_tup in table_tup.MatchedCols) {
                        s += $"{prefix} {col_tup.Names[0]}";
                    }
                    s += " |";
                    header.Add(s);
                    print_row_diffs(changes);
                }

                //If the header still has data (we did not print anything), don't print the header
                header.Clear();
            }

            return 0;
        }
    }
}