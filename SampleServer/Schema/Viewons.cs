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
            public int EmployeeId;

            [SortColumn(false)]
            public string FirstName;

            [SortColumn(true)]
            public string LastName;

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
        public List<TopRow> Customer; 

        [InheritFrom("Customer")]
        public class TopRow : Row
        {
            [Key]
            public int CustomerId;

            [SortColumn]
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
            public int ItemId;

            [SortColumn]
            public string ItemCode;

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
}
