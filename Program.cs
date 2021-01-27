using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlServerCe;
using System.Diagnostics;

namespace SDF_creation_test {
    class Program {
        private static SqlCeConnection sqlce_from_filepath(string filepath)
        {
            return new SqlCeConnection("Data Source = " + filepath + ";");
        }
        
        private static void write_line(string s)
        {
            #if DEBUG
                Debug.WriteLine(s);
            #else
                Console.WriteLine(s);
            #endif
        }
        static void Main(string[] args) {
            string[] filepaths = { "../../seeds1.sdf", "../../seeds2.sdf" };
            List<object[]>[] rows = { new List<object[]>(), new List<object[]>() };

            /* A list of dictionaries containing a collection of rows with matching column values
             * row_dicts[1][col_val][3] means "get the 4th row whose 2nd column has a value of col_var"
             */
            var row_dicts = new List<Dictionary<object, List<object[]>>>();
            var dest_rows = new List<object[]>();
            SqlCeConnection conn = null;
            try {
                for (int i = 0; i < filepaths.Length; i++)
                {
                    conn = sqlce_from_filepath(filepaths[i]);
                    conn.Open();

                    SqlCeCommand cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT * FROM SEEDS;";
                    var rdr = cmd.ExecuteReader();
                    object[] cur_row = new object[rdr.FieldCount];
                    while (rdr.Read())
                    {
                        rdr.GetValues(cur_row);
                        var cloned_row = cur_row.Clone() as object[];
                        if (i == 0)
                        {
                            for (int j = 0; j < cur_row.Length; j++)
                            {
                                if (row_dicts.Count <= j)
                                {
                                    row_dicts.Insert(j, new Dictionary<object, List<object[]>>());
                                }
                                //row_dicts[j].TryGetValue()
                                if (!row_dicts[j].ContainsKey(cur_row[j]))
                                {
                                    row_dicts[j].Add(cur_row[j], new List<object[]>());
                                }
                                row_dicts[j][cur_row[j]].Add(cloned_row);
                            }
                        } else {
                            /* Remove the first full match
                             * A full match is that of the first column matches that also matches every other column
                             * We can't just check for a match of every column since multiple rows could each match on a single value
                             */
                            var pot_full_matches = new List<Object[]>();
                            List<object[]> first_col_matches;
                            if (row_dicts[0].TryGetValue(cur_row[0], out first_col_matches))
                            {
                                bool b_had_full_match = false;
                                foreach (var pot_match in first_col_matches)
                                {
                                    bool b_is_full_match = true;
                                    for (int j = 1; j < pot_match.Length; j++)
                                    {
                                        if (!pot_match[j].Equals(cur_row[j]))
                                        {
                                            b_is_full_match = false;
                                            break;
                                        }
                                    }

                                    //If full match, remove match from every column dict
                                    if (b_is_full_match)
                                    {
                                        b_had_full_match = true;
                                        for (int j = 0; j < row_dicts.Count; j++)
                                        {
                                            row_dicts[j][cur_row[j]].Remove(pot_match);
                                        }
                                        break;
                                    }
                                }

                                if (!b_had_full_match)
                                {
                                    dest_rows.Add(cloned_row);
                                }
                            } else {
                                dest_rows.Add(cloned_row);
                            }
                        }
                    }
                    conn.Close();
                }
            } finally {
                conn.Close();
            }

            write_line("Removed:");
            foreach (var row_pair in row_dicts[0])
            {
                foreach (var row in row_pair.Value)
                {
                    string s = "";
                    string sep = "";
                    foreach (var col in row)
                    {
                        s += $"{sep}{col.ToString()}";
                        sep = " | ";
                    }
                    write_line($"  {s}");
                }
            }

            write_line("Added:");
            foreach (var row in dest_rows)
            {
                string s = "";
                string sep = "";
                foreach (var col in row)
                {
                    s += $"{sep}{col.ToString()}";
                    sep = " | ";
                }
                write_line($"  {s}");
            }
        }
    }
}