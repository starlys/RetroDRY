using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using RetroDRY;

namespace SampleServer.Schema
{
    /// <summary>
    /// Lookup table of phone types (like "cell", "work")
    /// </summary>
    public class PhoneTypeLookup : Persiston
    {
        [Prompt("Type")]
        public List<PhoneTypeRow> PhoneType;
        
        public class PhoneTypeRow : Row
        {
            [Key]
            public short PhoneTypeId;

            [StringLength(20), Required, MainColumn, SortColumn]
            public string TypeOfPhone;
        }
    }

    /// <summary>
    /// Lookup table of sale statuses (like Confirmed, Shipped etc)
    /// </summary>
    public class SaleStatusLookup : Persiston
    {
        public List<SaleStatusRow> SaleStatus;
        
        public class SaleStatusRow : Row
        {
            [Key]
            public short StatusId;

            [Required, StringLength(20, MinimumLength = 1), MainColumn, SortColumn]
            public string Name;

            [StringLength(20), WireType(Constants.TYPE_NSTRING)]
            public string Note;
        }
    }

    /// <summary>
    /// One employee with their contacts
    /// </summary>
    [SingleMainRow]
    public class Employee : Persiston
    {
        [Key]
        public int EmployeeId;

        [Required, StringLength(50, MinimumLength = 1), Prompt("First name")]
        public string FirstName;

        [Required, StringLength(50, MinimumLength = 1), Prompt("Last name"), MainColumn]
        public string LastName;

        [ForeignKey(typeof(Employee))]
        public int? SupervisorId;

        [LeftJoin("SupervisorId", "LastName"), Prompt("Supervisor")]
        public string SupervisorLastName;

        [Required, Prompt("Hired on")]
        public DateTime HireDate;

        public List<EContactRow> EContact;

        [SqlTableName("EmployeeContact"), ParentKey("EmployeeId")]
        public class EContactRow : Row
        {
            [Key]
            public int ContactId;

            [ForeignKey(typeof(PhoneTypeLookup)), Prompt("Phone type")]
            public short PhoneType;

            [StringLength(50), Prompt("Phone number")]
            public string Phone;
        }
    }

    /// <summary>
    /// One customer
    /// </summary>
    [SingleMainRow]
    public class Customer : Persiston
    {
        [Key]
        public int CustomerId;

        [Required, StringLength(200, MinimumLength = 1)]
        public string Company;

        [ForeignKey(typeof(Employee))]
        public int SalesRepId;

        [LeftJoin("SalesRepId", "LastName"), Prompt("Sales rep.")]
        public string SalesRepLastName;

        [StringLength(4000), WireType(Constants.TYPE_NSTRING)]
        public string Notes;
    }

    /// <summary>
    /// One item that the company sells which can have multiple variants
    /// </summary>
    [SingleMainRow]
    public class Item : Persiston
    {
        [Key]
        public int ItemId;

        [Required, Prompt("I-code"), RegularExpression("^[A-Z]{2}-[0-9]{4}$"), MainColumn]
        public string ItemCode;

        [Required, StringLength(200, MinimumLength = 10)]
        public string Description;

        public List<ItemVariantRow> ItemVariant;

        [ParentKey("ItemId")]
        public class ItemVariantRow : Row
        {
            [Key]
            public int ItemVariantId;

            [Required, StringLength(20, MinimumLength = 1), Prompt("Sub-code"), MainColumn, SortColumn]
            public string VariantCode;

            [Required, StringLength(200, MinimumLength = 10)]
            public string Description;
        }
    }

    /// <summary>
    /// One sale including its line items and notes
    /// </summary>
    [SingleMainRow]
    public class Sale : Persiston
    {
        [Key, MainColumn]
        public int SaleId;

        [Required, ForeignKey(typeof(Customer))]
        public int CustomerId;

        [Required, Prompt("Sale date"), WireType(Constants.TYPE_DATETIME)]
        public DateTime SaleDate;

        [Prompt("Shipped on"), WireType(Constants.TYPE_DATETIME)]
        public DateTime? ShippedDate;

        [Required, ForeignKey(typeof(SaleStatusLookup)), Prompt("Status")]
        public short Status;

        public List<SaleItemRow> SaleItem;

        [ParentKey("SaleItemId")]
        public class SaleItemRow : Row
        {
            [Key, SortColumn] 
            public int SaleItemId;

            [Required, ForeignKey(typeof(Item))]
            public int ItemId;

            [Required, Range(1, 999)]
            public int Quantity; 

            public int ItemVariantId;

            public List<SaleItemNoteRow> SaleItemNote;

            [ParentKey("SaleItemId")]
            public class SaleItemNoteRow : Row
            {
                [Key]
                public int SaleItemNoteId;

                [StringLength(4000), WireType(Constants.TYPE_NSTRING)]
                public string Note;
            }
        }
    }
}
