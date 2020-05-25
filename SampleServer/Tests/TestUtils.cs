using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SampleServer.Tests
{
    public static class TestUtils
    {
        /// <summary>
        /// Execute parameterless SQL statement
        /// </summary>
        public static int ExecuteSql(string sql)
        {
            using var db = new Npgsql.NpgsqlConnection(Globals.ConnectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }
    }
}
