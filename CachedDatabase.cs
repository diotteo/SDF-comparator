using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;

namespace SDF_comparator {
    class CachedDatabase : IEnumerable {
        public string Filepath { get; private set; }
        private SqlCeConnection conn;
        public Dictionary<string, CachedTable> tables;
        public CachedDatabase(string filepath) {
            Filepath = filepath;
            conn = new SqlCeConnection($"Data Source = {Filepath};");
            tables = new Dictionary<string, CachedTable>();

            cache_tables();
        }
        public SqlCeConnection GetConnection() {
            return conn;
        }
        private void cache_tables() {
            try {
                tables.Clear();
                conn.Open();

                SqlCeCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'TABLE';";
                var rdr = cmd.ExecuteReader();
                while (rdr.Read()) {
                    var name = (string)rdr.GetValue(0);
                    tables.Add(name, new CachedTable(this, name));
                }
            } finally {
                conn.Close();
            }
        }
        public bool TryGetValue(string value, out CachedTable output) {
            return tables.TryGetValue(value, out output);
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        public IEnumerator GetEnumerator() {
            return tables.GetEnumerator();
        }
    }
}
