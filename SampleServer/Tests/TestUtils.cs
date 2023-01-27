using System;
using System.Collections.Generic;
using System.Linq;
using RetroDRY;

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
        /// Execute scalar query; return DBNull if no result
        /// </summary>
        public static object QueryScalar(string sql)
        {
            using var db = new Npgsql.NpgsqlConnection(Globals.ConnectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            return cmd.ExecuteScalar() ?? DBNull.Value;
        }

        /// <summary>
        /// Load a list of values from one column
        /// </summary>
        public static IEnumerable<T?> LoadList<T>(string sql)
        {
            var ret = new List<T?>();
            using var db = new Npgsql.NpgsqlConnection(Globals.ConnectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                object? value = rdr[0];
                if (value is DBNull) value = null;
                ret.Add((T?)Convert.ChangeType(value, typeof(T?)));
            }
            return ret;
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

        public static IEnumerable<Diagnostics.Report> AllDiagnostics => Globals.TestingRetroverse
            .Where(r => r.Diagnostics != null).Select(r => r.Diagnostics!.GetStatus());
    }
}
