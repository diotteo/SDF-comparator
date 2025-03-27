using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlServerCe;
using System.Text;

namespace SdfComparator {
    class DatabaseTuple {
        public CachedDatabase Orig { get; private set; }
        public CachedDatabase Dest { get; private set; }

        //Matches
        public List<TableTuple> MatchedTables { get; private set; }

        //Diffs (either is null)
        public List<TableTuple> UnmatchedTables { get; private set; }


        public DatabaseTuple(CachedDatabase orig, CachedDatabase dest) {
            Orig = orig;
            Dest = dest;
            MatchedTables = new List<TableTuple>();
            UnmatchedTables = new List<TableTuple>();

            if (orig != null && dest != null) {
                //TODO: lazy eval
                match_tables();
            }
        }

        private void match_tables() {
            /* FIXME: we should do something a bit more sophisticated:
             * start by matching table names, then ensure data types and such haven’t changed
             * ... then try another matching strategy?
             */

            var table_counts = new Dictionary<string, int>();
            //FIXME: Pretty sure I want duplicates here, use Concat() instead of Union()?
            foreach (var table in Orig.Union(Dest)) {
                var name = table.Name;
                if (!table_counts.ContainsKey(name)) {
                    table_counts.Add(name, 1);
                } else {
                    table_counts[name]++;
                }
            }

            foreach (var kp in table_counts) {
                var name = kp.Key;
                var count = kp.Value;
                switch (count) {
                case 1:
                    string orig_name = null;
                    string dest_name = null;
                    if (Orig.ContainsKey(name)) {
                        orig_name = name;
                    } else {
                        dest_name = name;
                    }
                    UnmatchedTables.Add(new TableTuple(this, orig_name, dest_name));
                    break;
                case 2:
                    MatchedTables.Add(new TableTuple(this, name, name));
                    break;
                default:
                    throw new Exception($"{name} has {count} occurences");
                }
            }
        }
    }
}
