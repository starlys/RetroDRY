using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using RetroDRY;

namespace SampleServer.Schema
{
    /// <summary>
    /// This override demonstrates setting computed fields in or after loading.
    /// Note that Computed2 is set properly when loading a page for display, and Computed3 is set properly when exporting.
    /// </summary>
    public class CustomerListSql : RetroSql
    {
        public override async Task<LoadResult?> Load(IDbConnection db, DataDictionary dbdef, IUser? user, DatonKey key, int pageSize)
        {
            //technique 2 of 3 for setting computed field in a viewon. For other techniques, see CustomerList class.
            //After loading the whole daton, then loop through and set
            //any other fields you need. Load() is not called for export, however.
            int rowNo = 0;
            var loadResult = await base.Load(db, dbdef, user, key, pageSize);
            if (loadResult == null) return null;
            foreach (var row in ((CustomerList)loadResult.Daton!).Customer)
            {
                row.Computed2 = ++rowNo;
            }
            return loadResult;
        }

        protected override async IAsyncEnumerable<Row> LoadForExportImpl(IDbConnection db, DataDictionary dbdef, DatonDef datondef, 
            TableDef tabledef, ColDef sortField, SqlSelectBuilder.Where whereClause, int pageSize)
        {
            //this block shows how to get all distinct SalesRepId from customer that will be exported in advance of
            //issuing the actual query to export. You could use a technique like this to store some related data in memory,
            //and then use that data when customizing each row to export
            var sql = new SqlSelectBuilder(SqlFlavor!, "Customer", "SalesRepId", new List<string> { "distinct SalesRepId" });
            if (whereClause != null) sql.WhereClause = whereClause;
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql.ToString();
            whereClause?.ExportParameters(cmd);
            List<int> usedSalesRepIds = new(); //note that we are not using this list for anything in this example.
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read()) usedSalesRepIds.Add(reader.GetInt32(0));
            }

            //technique 3 of 3 for setting computed field in a viewon. For other techniques, see CustomerList class.
            //As it loads each for for export, set the fields you need.
            int rowNo = 1000;
            var ret = base.LoadForExportImpl(db, dbdef, datondef, tabledef, sortField, whereClause!, pageSize);
            await foreach (var row in ret)
            {
                ((CustomerList.TopRow)row).Computed3 = ++rowNo;
                yield return row;
            }
        }
    }

    /// <summary>
    /// This override demonstrates 2 ways to modify saved values
    /// </summary>
    public class CustomerSql : RetroSql
    {
        public override async Task Save(IDbConnection db, IUser user, Persiston? pristineDaton, Persiston modifiedDaton, PersistonDiff diff)
        {
            await base.Save(db, user, pristineDaton, modifiedDaton, diff);
            using var cmd = db.CreateCommand();

            //change the notes value after RetroDRY saving
            var modifiedCustomer = (Customer)modifiedDaton;
            cmd.CommandText = "update Customer set Notes = Notes || '!' where CustomerId=" + modifiedCustomer.CustomerId;
            cmd.ExecuteNonQuery();
        }

        protected override void PopulateWriterColumns(SqlWriteBuilder builder, RowChangingData cdata, bool includePrimaryKey)
        {
            base.PopulateWriterColumns(builder, cdata, includePrimaryKey);

            //change the Notes value during RetroDRY saving
            if (cdata.ModifiedRow is Customer cust)
                builder.ChangeOrAddNonKey("Notes", Constants.TYPE_STRING, ">" + cust.Notes);
        }

        public override void CustomizeWhereClause(SqlSelectBuilder.Where where, string clause, params object[] _params)
        {
            if (clause.StartsWith("CustomerId=")) Debug.WriteLine(clause);
            base.CustomizeWhereClause(where, clause, _params);
        }
    }
}
