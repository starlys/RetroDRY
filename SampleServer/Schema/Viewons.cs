using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using RetroDRY;

namespace SampleServer.Schema
{
    /// <summary>
    /// List of employees
    /// </summary>
    public class EmployeeList : Viewon
    {
        public List<TopRow> Employee;

        [InheritFrom("Employee")]
        public class TopRow : Row
        {
            [PrimaryKey(true), ForeignKey(typeof(Employee)), SortColumn(false)]
            public int EmployeeId;

            [SortColumn(false)]
            public string FirstName;

            [SortColumn(true)]
            public string LastName;

            [ForeignKey(typeof(Employee))]
            public int SupervisorId;

            [LeftJoin("SupervisorId", "LastName"), Prompt("Supervisor")]
            public string SupervisorLastName;

            public bool IsToxic;
        }

        [Criteria]
        public abstract class Criteria
        {
            [Prompt("Last name starts with")]
            public string LastName;

            [Prompt("Toxic human?")]
            public bool IsToxic;
        }
    }

    /// <summary>
    /// List of customers
    /// </summary>
    public class CustomerList : Viewon
    {
        [Prompt("Customer List")]
        public List<TopRow> Customer; 

        [InheritFrom("Customer")]
        public class TopRow : Row
        {
            [PrimaryKey(true), ForeignKey(typeof(Customer)), SortColumn(false)]
            public int CustomerId;

            [SortColumn(true)]
            public string Company;

            [ForeignKey(typeof(Employee))]
            public int SalesRepId;

            [LeftJoin("SalesRepId", "LastName"), Prompt("Sales rep.")]
            public string SalesRepLastName;
        }

        [Criteria]
        public abstract class Criteria
        {
            [Prompt("Company starts with")]
            public string Company;

            [InheritFrom("Customer.SalesRepId")]
            public int SalesRepId;

            [InheritFrom("Customer.SalesRepLastName")]
            public string SalesRepLastName;
        }
    }

    /// <summary>
    /// List of items
    /// </summary>
    public class ItemList : Viewon
    {
        public List<TopRow> Item;

        [InheritFrom("Item")]
        public class TopRow : Row
        {
            [PrimaryKey(true), ForeignKey(typeof(Item))]
            public int ItemId;

            [SortColumn(true)]
            public string ItemCode;

            [SortColumn(false)]
            public string Description;

            public double? Weight;

            public decimal Price;
        }

        [Criteria]
        public abstract class Criteria
        {
            [Prompt("I-code starts with")]
            public string ItemCode;

            [Prompt("Item description")] 
            public string Description;

            public double? Weight;
        }
    }

    /// <summary>
    /// List of item variants (used only as source of dropdown select for Sale.ItemVariantId)
    /// </summary>
    public class ItemVariantList : Viewon
    {
        public List<TopRow> ItemVariant;

        public class TopRow : Row
        {
            [PrimaryKey(true)]
            public int ItemVariantId;

            public int ItemId;

            [MainColumn, SortColumn(true)]
            public string VariantCode;

            [VisibleInDropdown]
            public string Description;
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
        public List<TopRow> Sale;

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
