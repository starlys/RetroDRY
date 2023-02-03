using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using RetroDRY;

namespace SampleServer.Schema
{
    /// <summary>
    /// List of employees
    /// </summary>
    public class EmployeeList : Viewon
    {
        public List<TopRow> Employee = new();

        [InheritFrom("Employee")]
        public class TopRow : Row
        {
            [PrimaryKey(true), ForeignKey(typeof(Employee)), SortColumn(false)]
            public int EmployeeId;

            [SortColumn(false)]
            public string? FirstName;

            [SortColumn, SqlTableName("Employee")]
            public string? LastName;

            [ForeignKey(typeof(Employee))]
            public int SupervisorId;

            [LeftJoin("SupervisorId", "LastName"), Prompt("Supervisor")]
            public string? SupervisorLastName;

            public bool IsToxic;

            public int NeatDeskRating;
        }

        [Criteria, InheritFrom("Employee")]
        public abstract class Criteria
        {
            [InheritFrom("Employee.LastName"), Prompt("Last name starts with")]
            public string? LastName;

            [Prompt("Toxic human?")]
            public bool IsToxic;

            public int NeatDeskRating;
        }
    }

    /// <summary>
    /// List of customers
    /// </summary>
    public class CustomerList : Viewon
    {
        [Prompt("Customer List")]
        public List<TopRow> Customer = new(); 

        [InheritFrom("Customer")]
        public class TopRow : Row
        {
            [PrimaryKey(true), ForeignKey(typeof(Customer)), SortColumn(false)]
            public int CustomerId;

            [SortColumn(true)]
            public string? Company;

            [ForeignKey(typeof(Employee))]
            public int SalesRepId;

            [LeftJoin("SalesRepId", "LastName"), Prompt("Sales rep.")]
            public string? SalesRepLastName;
        }

        [Criteria]
        public abstract class Criteria
        {
            [Prompt("Company starts with")]
            public string? Company;

            [InheritFrom("Customer.SalesRepId"), ForeignKey(typeof(Customer))]
            public int SalesRepId;
        }

        public override Task ValidateCriteria(IUser? user, ViewonKey key, Action<string> fail)
        {
            if (key.Criteria == null)
                fail("missing criteria");
            else
            {
                var companyCri = key.Criteria.FirstOrDefault(c => c.Name == "Company");
                if (companyCri != null && companyCri.PackedValue.StartsWith("The", StringComparison.InvariantCultureIgnoreCase))
                    fail("Cannot search for companies starting with 'the'");
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// List of items
    /// </summary>
    public class ItemList : Viewon
    {
        public List<TopRow> Item = new();

        [InheritFrom("Item")]
        public class TopRow : Row
        {
            [PrimaryKey(true), ForeignKey(typeof(Item))]
            public int ItemId;

            [SortColumn(true)]
            public string? ItemCode;

            [SortColumn(false)]
            public string? Description;

            public double? Weight;

            public decimal Price;
        }

        [Criteria]
        public abstract class Criteria
        {
            [Prompt("I-code starts with")]
            public string? ItemCode;

            [Prompt("Item description"), RegularExpression("^[^0-9]*$", ErrorMessage = "May not search on digits")] 
            public string? Description;

            public double? Weight;
        }
    }

    /// <summary>
    /// List of item variants (used only as source of dropdown select for Sale.ItemVariantId)
    /// </summary>
    public class ItemVariantList : Viewon
    {
        public List<TopRow> ItemVariant = new();

        public class TopRow : Row
        {
            [PrimaryKey(true)]
            public int ItemVariantId;

            public int ItemId;

            [MainColumn, SortColumn(true)]
            public string? VariantCode;

            [VisibleInDropdown]
            public string? Description;
        }

        [Criteria]
        public abstract class Criteria
        {
            public int? ItemId;
        }
    }

    /// <summary>
    /// List of sales
    /// </summary>
    public class SaleList : Viewon
    {
        public List<TopRow> Sale = new();

        [InheritFrom("Sale", IncludeCustom = true)]
        public class TopRow : Row
        {
            [PrimaryKey(true), ForeignKey(typeof(Sale))]
            public int SaleId;

            [ForeignKey(typeof(Customer))]
            public int CustomerId;

            [SortColumn, WireType(Constants.TYPE_DATETIME)]
            public DateTime SaleDate;

            [ForeignKey(typeof(SaleStatusLookup)), SortColumn(false)]
            public short Status;
        }

        [Criteria]
        public abstract class Criteria
        {
            [InheritFrom("Sale.CustomerId")]
            [ForeignKey(typeof(Customer))]
            [SelectBehavior(typeof(CustomerList))]
            public int? CustomerId;

            public DateTime? SaleDate;

            [ForeignKey(typeof(SaleStatusLookup))]
            public short? Status;
        }
    }

    /// <summary>
    /// List of sales joined with customers (demonstrates inner join)
    /// </summary>
    public class SaleCustomerList : Viewon
    {
        public List<TopRow> Sale = new();

        [SqlFrom("Sale inner join Customer on Sale.CustomerId=Customer.CustomerId")]
        public class TopRow : Row
        {
            [PrimaryKey(true), ForeignKey(typeof(Sale))]
            public int SaleId;

            public string? Company;

            [SortColumn, WireType(Constants.TYPE_DATETIME)]
            public DateTime SaleDate;

            [ForeignKey(typeof(SaleStatusLookup)), SortColumn(false)]
            public short Status;

            [SqlTableName("Customer"), Prompt("Cust notes")]
            public string? Notes;

            [ComputedColumn]
            public int RowInfo1; //# chars in Company name

            [ComputedColumn]
            public int RowInfo2; //running total of RowInfo1

            public override void Recompute(Daton? daton)
            {
                RowInfo1 = (Company ?? "").Length;
            }
        }

        [Criteria]
        public abstract class Criteria
        {
            public DateTime? SaleDate;
        }

        /// <summary>
        /// Demonstrates how row recompute is done for the whole daton, then RecomputeAll can use the
        /// values computed for each row
        /// </summary>
        /// <param name="datondef"></param>
        public override void RecomputeAll(DatonDef datondef)
        {
            int running = 0;
            foreach (var row in Sale)
            {
                running += row.RowInfo1;
                row.RowInfo2 = running;
            }
        }
    }

    /// <summary>
    /// List of BigTable
    /// </summary>
    public class BigTableList : Viewon
    {
        public List<TopRow> BigTable = new();

        public class TopRow : Row
        {
            [SortColumn(true), PrimaryKey(true)]
            public string? Name;
        }

        [Criteria]
        public abstract class Criteria
        {
            public string? Name;
        }
    }
}
