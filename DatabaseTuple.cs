using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlServerCe;
using System.Text;

namespace SDF_comparator {
    class DatabaseTuple {
        public CachedDatabase Orig { get; private set; }
        public CachedDatabase Dest { get; private set; }
        public List<TableTuple> table_tuples;
        public List<TableTuple> diff_tables;
        public DatabaseTuple(CachedDatabase orig, CachedDatabase dest) {
            Orig = orig;
            Dest = dest;
            table_tuples = new List<TableTuple>();
            diff_tables = new List<TableTuple>();

            if (orig != null && dest != null) {
                match_tables();
            }
        }
        private void match_tables() {
            /* FIXME: we should do something a bit more sophisticated:
             * start by matching table names, then ensure data types and such haven’t changed
             * ... then try another matching strategy?
             */

            var table_counts = new Dictionary<string, int>();
            foreach (var table_keypair in Orig.tables.Union(Dest.tables)) {
                var name = table_keypair.Key;
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
                    if (Orig.tables.ContainsKey(name)) {
                        orig_name = name;
                    } else {
                        dest_name = name;
                    }
                    diff_tables.Add(new TableTuple(this, orig_name, dest_name));
                    break;
                case 2:
                    table_tuples.Add(new TableTuple(this, name, name));
                    break;
                default:
                    throw new Exception($"{name} has {count} occurences");
                }
            }
        }
    }
}
