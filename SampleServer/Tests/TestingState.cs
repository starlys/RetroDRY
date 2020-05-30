using RetroDRY;
using System;
using System.Threading.Tasks;

namespace SampleServer.Tests
{
    /// <summary>
    /// Global state info for the currently running integration tests
    /// </summary>
    public static class TestingState
    {
        public static TestStep[] Steps = new[]
        {
            // ****************************************************************************
            // ***************************** QUICK TEST STEPS *****************************
            // ****************************************************************************

            new TestStep
            {
                //client - no action
                //server - clears out database and clears existing sessions
                StepCode = "a10-10",
                NextStepCode = "a10-20",
                Validate = () =>
                {
                    TestUtils.ExecuteSql("truncate RetroLock,SaleItemNote,SaleItem,Sale,ItemVariant,Item,Customer,EmployeeContact,Employee,SaleStatus,PhoneType restart identity cascade");
                    Startup.InitializeRetroDRY();
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - attempts start session with unknown user, confirm bad response
                //server - confirm no sessions active
                StepCode = "a10-20",
                NextStepCode = "a10-30",
                Validate = () =>
                {
                    if (Globals.Retroverse.Diagnostics.GetStatus().NumSessions != 0) throw new Exception("Expected no sessions");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - starts session with good user, confirm data dictionary is complete
                //server - confirm one session active
                StepCode = "a10-30",
                NextStepCode = "a20-10",
                Validate = () =>
                {
                    if (Globals.Retroverse.Diagnostics.GetStatus().NumSessions != 1) throw new Exception("Expected 1 session");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - gets customers viewon and confirms it is empty
                //server - no action
                StepCode = "a20-10",
                NextStepCode = "a30-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - loads and saves PhoneType with new rows in it
                //server - confirm database contains phone types and there are no locks
                StepCode = "a30-10",
                NextStepCode = "a30-20",
                Validate = async () =>
                {
                    await Task.Delay(200);
                    if (TestUtils.LockCount() != 0) throw new Exception("Expected no locks");
                    if (TestUtils.CountRecords("PhoneType") != 2) throw new Exception("Expected 2 phone types");
                    //return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - creates new Employee, then attempt to create invalid Customer 
                //server - confirm database contains employee and there are no locks
                StepCode = "a30-20",
                NextStepCode = "a30-30",
                Validate = () =>
                {
                    if (TestUtils.LockCount() != 0) throw new Exception("Expected no locks");
                    if (TestUtils.CountRecords("EmployeeContact") != 1) throw new Exception("Expected 1 emp contact");
                    if (TestUtils.CountRecords("Customer") != 0) throw new Exception("Expected no customers");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - creates new valid Customer 
                //server - confirm database contains customer and there are no locks
                StepCode = "a30-30",
                NextStepCode = "a40-10",
                Validate = () =>
                {
                    if (TestUtils.LockCount() != 0) throw new Exception("Expected no locks");
                    if (TestUtils.CountRecords("Customer") != 1) throw new Exception("Expected 1 customer");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - gets customers viewon and confirms it has one row
                //server - no action 
                StepCode = "a40-10",
                NextStepCode = "a50-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - lock customer
                //server - confirm lock is active in lock table
                StepCode = "a50-10",
                NextStepCode = "a60-10",
                Validate = () =>
                {
                    if (TestUtils.LockCount() != 1) throw new Exception("Expected one lock");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - unlock customer without making changes
                //server - confirm unlock
                StepCode = "a60-10",
                NextStepCode = "a70-10",
                Validate = () =>
                {
                    if (TestUtils.LockCount() != 0) throw new Exception("Expected no locks");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - lock employee then save change to main and child rows, and unlock
                //server - confirm changes were saved and no locks
                StepCode = "a70-10",
                NextStepCode = "a70-20",
                Validate = () =>
                {
                    if (TestUtils.LockCount() != 0) throw new Exception("Expected no locks");
                    if (TestUtils.CountRecords("EmployeeContact") != 2) throw new Exception("Expected two emp contacts");
                    if (TestUtils.QueryScalar("select LastName from Employee where EmployeeId=1").ToString() != "Smurf") 
                        throw new Exception("Last name didn't change to Smurf");
                    if (TestUtils.QueryScalar("select Phone from EmployeeContact where EmployeeId=1 and Phone like '5%'").ToString() != "505 555 1234")
                        throw new Exception("Phone didn't change to 505");
                    if (TestUtils.QueryScalar("select Phone from EmployeeContact where EmployeeId=1 and Phone like 's%'").ToString() != "sammy@smurfland.com")
                        throw new Exception("Email didn't change to sammy@smurfland.com");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - log in with Nate_The_Noter (who can only view and update customer notes); get customerlist and confirm
                //only notes are visible; get specific customer and confirm the same; attempt to save changes to an invalid field;
                //then attempt to save changes to only the valid field (also tests passive undo by re-getting customer from client cache)
                //server - no action
                StepCode = "a70-20",
                NextStepCode = "DONE",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },


            // ****************************************************************************
            // ***************************** SLOW TEST STEPS ******************************
            // ****************************************************************************

            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - 
                StepCode = "b",
                NextStepCode = "",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
        };
    }
}
