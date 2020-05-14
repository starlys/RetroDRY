using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using RetroDRY;

namespace SampleServer.Schema
{
    public class CustomerSql : RetroSql
    {
        protected override SqlSelectBuilder.Where MainTableWhereClause(TableDef tabledef, PersistonKey key)
        {
            return base.MainTableWhereClause(tabledef, key);
        }
        protected override SqlSelectBuilder.Where MainTableWhereClause(TableDef tabledef, ViewonKey key)
        {
            return base.MainTableWhereClause(tabledef, key);
        }
    }
}
