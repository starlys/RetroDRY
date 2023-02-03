using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using RetroDRY;

namespace SampleServer.Schema
{
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
