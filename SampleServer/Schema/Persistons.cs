using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using RetroDRY;

namespace SampleServer.Schema
{
    /// <summary>
    /// Lookup table of phone types (like "cell", "work")
    /// </summary>
    [Prompt("Phone types")]
    public class PhoneTypeLookup : Persiston
    {
        public List<PhoneTypeRow> PhoneType = new();

        [Prompt("Type")]
        public class PhoneTypeRow : Row
        {
            [PrimaryKey(true)]
            [Prompt("ID")]
            public short? PhoneTypeId;

            [StringLength(20), MainColumn, SortColumn]
            [Prompt("Phone type")]
            public string? TypeOfPhone;
        }
    }

    /// <summary>
    /// Lookup table of sale statuses (like Confirmed, Shipped etc)
    /// </summary>
    [Prompt("Sale statuses")]
    public class SaleStatusLookup : Persiston
    {
        public List<SaleStatusRow> SaleStatus = new();

        [Prompt("Status")]
        public class SaleStatusRow : Row
        {
            [PrimaryKey(true)]
            [Prompt("ID")]
            public short? StatusId;

            [StringLength(20, MinimumLength = 1), MainColumn, SortColumn]
            [Prompt("Sale status")]
            public string? Name;

            [StringLength(20), WireType(Constants.TYPE_NSTRING)]
            public string? Note;
        }
    }

    /// <summary>
    /// One employee with their contacts
    /// </summary>
    [SingleMainRow]
    public class Employee : Persiston
    {
        [PrimaryKey(true)]
        [Prompt("ID")]
        public int? EmployeeId;

        [StringLength(50, MinimumLength = 1), Prompt("First name")]
        public string? FirstName;

        [StringLength(50, MinimumLength = 1), Prompt("Last name"), MainColumn]
        public string? LastName;

        [ForeignKey(typeof(Employee))]
        [Prompt("Supervisor ID")]
        [SelectBehavior(typeof(EmployeeList))]
        public int? SupervisorId;

        [LeftJoin("SupervisorId", "LastName"), Prompt("Supervisor")]
        public string? SupervisorLastName;

        [Prompt("Hired on")]
        public DateTime HireDate;

        [Prompt("Employee is toxic to work environment")]
        public bool IsToxic;

        [Range(0, 10), Prompt("Neatness of desk (0-10)")]
        public int NeatDeskRating;

        public List<EContactRow> EContact = new();

        [SqlTableName("EmployeeContact"), ParentKey("EmployeeId")]
        [Prompt("Employee contact")]
        public class EContactRow : Row
        {
            [PrimaryKey(true)]
            [Prompt("ID")]
            public int? ContactId;

            [ForeignKey(typeof(PhoneTypeLookup)), Prompt("Phone type")]
            public short PhoneType;

            [StringLength(50), Prompt("Phone number")]
            public string? Phone;
        }
    }

    /// <summary>
    /// One customer
    /// </summary>
    [SingleMainRow]
    public class Customer : Persiston
    {
        [PrimaryKey(true)]
        [Prompt("ID")]
        public int? CustomerId;

        [StringLength(200, MinimumLength = 1)]
        public string? Company;

        [ForeignKey(typeof(Employee))]
        [SelectBehavior(typeof(EmployeeList))]
        [Prompt("Sales rep ID")]
        public int SalesRepId;

        [LeftJoin("SalesRepId", "LastName"), Prompt("Sales rep")]
        public string? SalesRepLastName;

        [StringLength(4000), WireType(Constants.TYPE_NSTRING)]
        public string? Notes;

        public override Task Validate(IUser? user, Action<string> fail)
        {
            //silly rule to demonstrate custom validation:
            if (Company != null && Company.StartsWith("The", StringComparison.InvariantCultureIgnoreCase))
                fail("Companies cannot start with 'the' ");
            //note that you can use language-dependent strings here by checking user.LangCode

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// One item that the company sells which can have multiple variants
    /// </summary>
    [SingleMainRow]
    public class Item : Persiston
    {
        [PrimaryKey(true)]
        [Prompt("ID")]
        public int? ItemId;

        [Prompt("I-code"), RegularExpression("^[A-Z]{2}-[0-9]{4}$"), MainColumn]
        public string? ItemCode;

        [StringLength(200, MinimumLength = 3)]
        public string? Description;

        public double? Weight;

        public decimal? Price;

        public List<ItemVariantRow> ItemVariant = new();

        [ParentKey("ItemId")]
        [Prompt("Variant")]
        public class ItemVariantRow : Row
        {
            [PrimaryKey(true)]
            [Prompt("Var-ID")]
            public int? ItemVariantId;

            [StringLength(20, MinimumLength = 1), Prompt("Sub-code"), MainColumn, SortColumn]
            public string? VariantCode;

            [StringLength(200, MinimumLength = 3)]
            public string? Description;
        }
    }

    /// <summary>
    /// One sale including its line items and notes
    /// </summary>
    [SingleMainRow]
    public class Sale : Persiston
    {
        [PrimaryKey(true), MainColumn]
        [Prompt("ID")]
        public int? SaleId;

        [ForeignKey(typeof(Customer))]
        [SelectBehavior(typeof(CustomerList))]
        [Prompt("Customer ID")]
        public int CustomerId;

        [Prompt("Sale date"), WireType(Constants.TYPE_DATETIME)]
        public DateTime SaleDate;

        [Prompt("Shipped on"), WireType(Constants.TYPE_DATETIME)]
        public DateTime? ShippedDate;

        [ForeignKey(typeof(SaleStatusLookup)), Prompt("Status")]
        public short Status;

        public List<SaleItemRow> SaleItem = new();

        [ParentKey("SaleId")]
        [Prompt("Item sold")]
        public class SaleItemRow : Row
        {
            [PrimaryKey(true), SortColumn]
            [Prompt("ID")]
            public int? SaleItemId;

            [ForeignKey(typeof(Item))]
            [Prompt("Item-ID")]
            [SelectBehavior(typeof(ItemList), UseDropdown = true)] //UseDropdown here is ok because the company has only a few items
            public int ItemId;

            [LeftJoin("ItemId", "Description")]
            public string? ItemDescription;

            [Range(1, 999)]
            public int Quantity;

            [Prompt("Var-ID")]
            [SelectBehavior(typeof(ItemVariantList), AutoCriterionName ="ItemId", AutoCriterionValueColumnName = "ItemId", ViewonValueColumnName = "ItemVariantId", UseDropdown = true)]
            public int? ItemVariantId;

            [Prompt("Ext Price")]
            public decimal ExtendedPrice;

            public List<SaleItemNoteRow> SaleItemNote = new();

            [ParentKey("SaleItemId")]
            [Prompt("Sale note")]
            public class SaleItemNoteRow : Row
            {
                [PrimaryKey(true)]
                [Prompt("ID")]
                public int? SaleItemNoteId;

                [StringLength(4000), WireType(Constants.TYPE_NSTRING)]
                public string? Note;
            }
        }
    }
}
