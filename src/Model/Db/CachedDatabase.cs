using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlServerCe;
using System.Data.Common;

namespace SdfComparator.Model.Db {
    //FIXME: Expose a full IDictionary interface rather than IEnumerable?
    public class CachedDatabase : IEnumerable<CachedTable> {
        public string Filepath { get; private set; }
        public DbConnection Connection { get; private set; }
        private Dictionary<string, CachedTable> tables;

        public CachedTable this[string table_name] => tables[table_name];

        public CachedDatabase(string filepath) {
            Filepath = filepath;
            Connection = new SqlCeConnection($"Data Source = {Filepath};");
            tables = new Dictionary<string, CachedTable>();

            cache_tables();
        }

        private void cache_tables() {
            try {
                tables.Clear();
                Connection.Open();

                DbCommand cmd = Connection.CreateCommand();
                cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'TABLE';";
                var rdr = cmd.ExecuteReader();
                while (rdr.Read()) {
                    var name = (string)rdr.GetValue(0);
                    tables.Add(name, new CachedTable(this, name));
                }
            } finally {
                Connection.Close();
            }
        }

        public bool TryGetValue(string table_name, out CachedTable output) {
            return tables.TryGetValue(table_name, out output);
        }

        public bool ContainsKey(string table_name) {
            return tables.ContainsKey(table_name);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<CachedTable> GetEnumerator() {
            return tables.Values.GetEnumerator();
        }
    }
}
