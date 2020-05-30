using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SampleServer.Tests
{
    public static class TestUtils
    {
        /// <summary>
        /// Execute parameterless nonquery SQL statement
        /// </summary>
        public static int ExecuteSql(string sql)
        {
            using var db = new Npgsql.NpgsqlConnection(Globals.ConnectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Execute scalar query
        /// </summary>
        public static object QueryScalar(string sql)
        {
            using var db = new Npgsql.NpgsqlConnection(Globals.ConnectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteScalar();
        }

        public static int CountRecords(string tableName)
        {
            return Convert.ToInt32(QueryScalar($"select count(*) from {tableName}"));
        }

        public static int LockCount()
        {
            int count = Convert.ToInt32(QueryScalar("select count(*) from RetroLock where LockedBy is not null"));
            return count;
        }
    }
}
