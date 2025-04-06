using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlServerCe;
using System.Data.Common;


namespace SdfComparator.Model.Db {
    public class CachedTable : IDictionary<string, CachedColumn> {
        public CachedDatabase ParentDb { get; private set; }
        public string Name { get; private set; }

        private readonly Dictionary<string, CachedColumn> columns;

        private bool _is_cached;

        public CachedTable(CachedDatabase db, string table_name) {
            _is_cached = false;
            ParentDb = db;
            Name = table_name;

            columns = new Dictionary<string, CachedColumn>();
        }

        private void CacheIfMissing() {
            if (!_is_cached) {
                CacheColumns();
            }
        }

        public void CacheColumns() {
            var conn = ParentDb.Connection;
            var b_close = conn.State == 0;
            try {
                if (b_close) {
                    conn.Open();
                }
                columns.Clear();

                DbCommand cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT "
                    + "COLUMN_NAME, "
                    + "DATA_TYPE, "
                    + "ORDINAL_POSITION, "
                    + "CHARACTER_MAXIMUM_LENGTH "
                    + "FROM INFORMATION_SCHEMA.COLUMNS "
                    + "WHERE "
                    + $"TABLE_NAME = '{Name}';";
                var rdr = cmd.ExecuteReader();
                while (rdr.Read()) {
                    string name = (string)rdr.GetValue(0);
                    string data_type = (string)rdr.GetValue(1);
                    int idx = (int)rdr.GetValue(2);
                    int max_len = rdr.GetValue(3) as int? ?? -1;
                    var col = new CachedColumn(name, data_type, idx, max_len);
                    columns.Add(name, col);
                }
            } finally {
                if (b_close) {
                    conn.Close();
                }
            }
            _is_cached = true;
        }
        ICollection<string> IDictionary<string, CachedColumn>.Keys {
            get {
                CacheIfMissing();
                return columns.Keys;
            }
        }

        ICollection<CachedColumn> IDictionary<string, CachedColumn>.Values {
            get {
                CacheIfMissing();
                return columns.Values;
            }
        }

        int ICollection<KeyValuePair<string, CachedColumn>>.Count {
            get {
                CacheIfMissing();
                return columns.Count;
            }
        }

        bool ICollection<KeyValuePair<string, CachedColumn>>.IsReadOnly {
            get {
                CacheIfMissing();
                return ((ICollection<KeyValuePair<string, CachedColumn>>)columns).IsReadOnly;
            }
        }

        CachedColumn IDictionary<string, CachedColumn>.this[string key] {
            get {
                CacheIfMissing();
                return columns[key];
            }
            set => throw new NotImplementedException();
        }

        bool IDictionary<string, CachedColumn>.ContainsKey(string key) {
            CacheIfMissing();
            return columns.ContainsKey(key);
        }

        bool IDictionary<string, CachedColumn>.TryGetValue(string key, out CachedColumn value) {
            CacheIfMissing();
            return columns.TryGetValue(key, out value);
        }

        bool ICollection<KeyValuePair<string, CachedColumn>>.Contains(KeyValuePair<string, CachedColumn> item) {
            CacheIfMissing();
            return columns.Contains(item);
        }

        void ICollection<KeyValuePair<string, CachedColumn>>.CopyTo(KeyValuePair<string, CachedColumn>[] array, int arrayIndex) {
            CacheIfMissing();
            ((ICollection<KeyValuePair<string, CachedColumn>>)columns).CopyTo(array, arrayIndex);
        }

        IEnumerator<KeyValuePair<string, CachedColumn>> IEnumerable<KeyValuePair<string, CachedColumn>>.GetEnumerator() {
            CacheIfMissing();
            return columns.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return ((IEnumerable<KeyValuePair<string, CachedColumn>>)this).GetEnumerator();
        }

        void IDictionary<string, CachedColumn>.Add(string key, CachedColumn value) {
            throw new NotImplementedException();
        }

        bool IDictionary<string, CachedColumn>.Remove(string key) {
            throw new NotImplementedException();
        }

        void ICollection<KeyValuePair<string, CachedColumn>>.Add(KeyValuePair<string, CachedColumn> item) {
            throw new NotImplementedException();
        }

        void ICollection<KeyValuePair<string, CachedColumn>>.Clear() {
            throw new NotImplementedException();
        }

        bool ICollection<KeyValuePair<string, CachedColumn>>.Remove(KeyValuePair<string, CachedColumn> item) {
            throw new NotImplementedException();
        }
    }
}
