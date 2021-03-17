using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlServerCe;

namespace SDF_comparator {
    class CachedTable {
        public CachedDatabase ParentDb { get; private set; }
        public string Name { get; private set; }
        public Dictionary<string, CachedColumn> columns;


        public CachedTable(CachedDatabase db, string table_name) {
            ParentDb = db;
            Name = table_name;

            columns = new Dictionary<string, CachedColumn>();
            cache_columns();
        }

        private void cache_columns() {
            var conn = ParentDb.Connection;
            columns.Clear();

            SqlCeCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, ORDINAL_POSITION, CHARACTER_MAXIMUM_LENGTH "
                + $"FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{Name}';";
            var rdr = cmd.ExecuteReader();
            while (rdr.Read()) {
                string name = (string)rdr.GetValue(0);
                string data_type = (string)rdr.GetValue(1);
                int idx = (int)rdr.GetValue(2);
                int max_len = rdr.GetValue(3) as int? ?? -1;
                var col = new CachedColumn(name, data_type, idx, max_len);
                columns.Add(name, col);
            }
        }
    }
}
