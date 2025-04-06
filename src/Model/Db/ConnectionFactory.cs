using System;
using System.Data.Common;
using System.Data.SqlServerCe;

namespace SdfComparator.Model.Db {
    class ConnectionFactory {
        /*
         * The whole SqlServerCe namespace was deprecated before .NET Core
         * see https://learn.microsoft.com/en-us/previous-versions/sql/compact/sql-server-compact-4.0/ec4st0e3(v=vs.100) 
         */
        public static DbConnection ConnectionFromFile(string filepath) {
            return new SqlCeConnection($"Data Source = {filepath};");
        }
    }
}
