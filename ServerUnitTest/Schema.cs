﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RetroDRY;

namespace UnitTest
{
#pragma warning disable CS0649

    [SingleMainRow]
    class Ogre : Persiston
    {
        [PrimaryKey(true)]
        public int OgreId;
        public string? Name;
        public decimal? Money;

        [ComputedColumn]
        public string? FormattedMoney;

        public List<PaymentMethodRow> PaymentMethod = new();

        public override void Recompute(Daton? daton)
        {
            FormattedMoney = $"{Money} gold pieces";
        }

        [ParentKey("OgreId")]
        public class PaymentMethodRow : Row
        {
            [PrimaryKey(true)]
            public int PaymentMethodId;
            public string? Method;
            public string? Notes;

            [ComputedColumn]
            public string? AngryMethod;

            public override void Recompute(Daton? daton)
            {
                AngryMethod = $"Arrg, I'll pay with {Method}";
            }
        }
    }

    [SingleMainRow, DatabaseNumber(6)]
    class Customer : Persiston
    {
        [PrimaryKey(true)]
        public int CustomerId;

        [Required, StringLength(200, MinimumLength = 1)]
        public string? Company;

        //[ForeignKey(typeof(Employee))]
        //public int SalesRepId;

        [LeftJoin("SalesRepId", "LastName"), Prompt("Sales rep.")]
        public string? SalesRepLastName;

        [StringLength(4000), WireType(Constants.TYPE_NSTRING)]
        public string? Notes;

        [Range(-2, +2, ErrorMessage = "VALUERANGE")]
        public decimal Money;

        public override Task Validate(IUser? user, Action<string> fail)
        {
            if (Company != null && Company.StartsWith("THE")) fail("THE");
            return Task.CompletedTask;
        }
    }

    class ExtCustomer : Persiston
    {
        public List<ExtRow> Ext = new();

        public class ExtRow : Row
        {
            [PrimaryKey(true)]
            public string? Info;

            [RetroHide]
            public int NothingToSeeHere;
        }
    }

    [SingleMainRow]
    class Employee : Persiston
    {
        [PrimaryKey(true)]
        public int EmpId;

        [StringLength(50, MinimumLength = 3), Prompt("DEFAULTLANG")]
        public string? FirstName;

        public string? LastName;

        public int? SupervisorId;

        [LeftJoin("SupervisorId", "LastName"), Prompt("Supervisor")]
        public string? SupervisorLastName;
    }

    class EmployeeList : Viewon
    {
        public List<TopRow> Employee = new();

        [InheritFrom("Employee")]
        public class TopRow : Row
        {
            public int EmpId;

            [SortColumn(false), InheritFrom("Employee.FirstName")]
            public string? FirstName;

            [SortColumn(true)]
            public string? LastName;

            public int SupervisorId;

            [LeftJoin("SupervisorId", "LastName"), Prompt("Supervisor")]
            public string? SupervisorLastName;
        }

        [Criteria]
        public abstract class Criteria
        {
            [Prompt("Last name starts with"), StringLength(5)]
            public string? LastName;
        }

        public override Task ValidateCriteria(IUser? user, ViewonKey key, Action<string> fail)
        {
            Assert.IsNotNull(key.Criteria);
            var companyCri = key.Criteria.FirstOrDefault(c => c.Name == "LastName"); //assume this exists for the tests
            if (companyCri?.PackedValue.StartsWith("THE") == true) fail("THE");
            return Task.CompletedTask;
        }
    }
}

