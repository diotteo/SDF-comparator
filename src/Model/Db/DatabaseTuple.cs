using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlServerCe;
using System.Text;

namespace SdfComparator.Model.Db {
    public class DatabaseTuple {
        public CachedDatabase Orig { get; private set; }
        public CachedDatabase Dest { get; private set; }

        private bool _is_matched;
        //Matches
        private List<TableTuple> _matchedTables;
        public List<TableTuple> MatchedTables {
            get {
                MatchIfMissing();
                return _matchedTables;
            }
            private set {
                _matchedTables = value;
            }
        }

        //Diffs (either is null)
        private List<TableTuple> _unmatchedTables;
        public List<TableTuple> UnmatchedTables {
            get {
                MatchIfMissing();
                return _unmatchedTables;
            }
            private set {
                _unmatchedTables = value;
            }
        }

        private void Init(CachedDatabase orig, CachedDatabase dest) {
            Orig = orig;
            Dest = dest;
            MatchedTables = new List<TableTuple>();
            UnmatchedTables = new List<TableTuple>();
            _is_matched = false;
        }

        public DatabaseTuple(CachedDatabase orig, CachedDatabase dest) {
            Init(orig, dest);
        }

        public DatabaseTuple(string src_filepath, string dst_filepath) {
            var orig = new CachedDatabase(src_filepath);
            var dest = new CachedDatabase(dst_filepath);
            Init(orig, dest);
        }

        private void MatchIfMissing() {
            if (!_is_matched) {
                MatchTables();
            }
        }

        public List<TableTuple> MatchTables() {
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
                    _unmatchedTables.Add(new TableTuple(this, orig_name, dest_name));
                    break;
                case 2:
                    _matchedTables.Add(new TableTuple(this, name, name));
                    break;
                default:
                    throw new Exception($"{name} has {count} occurences");
                }
            }

            _is_matched = true;
            return _matchedTables;
        }
    }
}
