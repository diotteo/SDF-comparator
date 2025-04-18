﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;

namespace SdfComparator.Model.Db {
    //FIXME: Expose a full IDictionary interface rather than IEnumerable?
    public class CachedDatabase : IEnumerable<CachedTable> {
        public string Filepath { get; private set; }
        public DbConnection Connection { get; private set; }
        private readonly Dictionary<string, CachedTable> tables;
        private bool _is_cached;

        public CachedTable this[string table_name] => tables[table_name];

        public CachedDatabase(string filepath) {
            Filepath = filepath;
            Connection = ConnectionFactory.ConnectionFromFile(Filepath);
            tables = new Dictionary<string, CachedTable>();

            _is_cached = false;
        }

        public void CacheTables() {
            try {
                tables.Clear();
                Connection.Open();

                DbCommand cmd = Connection.CreateCommand();
                cmd.CommandText =
                        "SELECT "
                        + "TABLE_NAME "
                        + "FROM INFORMATION_SCHEMA.TABLES "
                        + "WHERE "
                        + "TABLE_TYPE = 'TABLE';";
                var rdr = cmd.ExecuteReader();
                while (rdr.Read()) {
                    var name = (string)rdr.GetValue(0);
                    var ctable = new CachedTable(this, name);
                    ctable.CacheColumns();
                    tables.Add(name, ctable);
                }
                _is_cached = true;
            } finally {
                Connection.Close();
            }
        }

        private void CacheIfMissing() {
            if (!_is_cached) {
                CacheTables();
            }
        }

        public bool TryGetValue(string table_name, out CachedTable output) {
            CacheIfMissing();
            return tables.TryGetValue(table_name, out output);
        }

        public bool ContainsKey(string table_name) {
            CacheIfMissing();
            return tables.ContainsKey(table_name);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<CachedTable> GetEnumerator() {
            CacheIfMissing();
            return tables.Values.GetEnumerator();
        }
    }
}
