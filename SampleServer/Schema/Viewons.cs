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
            [Key, ForeignKey(typeof(Employee)), SortColumn(false)]
            public int EmployeeId;

            [SortColumn(false)]
            public string FirstName;

            [SortColumn(true)]
            public string LastName;

            [ForeignKey(typeof(Employee))]
            public int SupervisorId;

            [LeftJoin("SupervisorId", "LastName"), Prompt("Supervisor")]
            public string SupervisorLastName;
        }

        [Criteria]
        public abstract class Criteria
        {
            [Prompt("Last name starts with")]
            public string LastName;
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
            [Key, ForeignKey(typeof(Customer)), SortColumn(false)]
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
            [Key, ForeignKey(typeof(Item))]
            public int ItemId;

            [SortColumn(true)]
            public string ItemCode;

            [SortColumn(false)]
            public string Description;
        }

        [Criteria]
        public abstract class Criteria
        {
            [Prompt("I-code starts with")]
            public string ItemCode;

            [Prompt("Item description")] 
            public string Description;
        }
    }

    /// <summary>
    /// List of sales
    /// </summary>
    public class SaleList : Viewon
    {
        public List<TopRow> Sale;

        [InheritFrom("Sale")]
        public class TopRow : Row
        {
            [Key, ForeignKey(typeof(Sale))]
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
            [LookupBehavior(typeof(CustomerList))]
            public int? CustomerId;

            public DateTime? SaleDate;

            [ForeignKey(typeof(SaleStatusLookup))]
            public short? Status;
        }
    }
}
