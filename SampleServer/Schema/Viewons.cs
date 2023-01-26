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

            [SortColumn(true)]
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

        public override Task ValidateCriteria(IUser user, ViewonKey key, Action<string> fail)
        {
            var companyCri = key.Criteria.FirstOrDefault(c => c.Name == "Company");
            if (companyCri != null && companyCri.PackedValue.StartsWith("The", StringComparison.InvariantCultureIgnoreCase))
                fail("Cannot search for companies starting with 'the'");
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
}
