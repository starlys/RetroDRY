using RetroDRY;
using SampleServer.Schema;
using System;
using System.Linq;
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

            /*
             * Slow tests require the server to be in IntegrationTestMode
             * Configuration for slow integration tests:
             * There are 24 client instances (all in the same browser) and 3 servers (in the same process) - 8 clients per server.
             * Clients 0,3,6... go to server 0; 1,4,7... go to server 1; etc
             * Clients 0,1 are looking at January orders; 2,3 are looking at February, etc.
             */

            new TestStep
            {
                //client - no action
                //server - clears out database and clears existing sessions; goes to integration test mode
                StepCode = "b10-10",
                NextStepCode = "b20-10",
                Validate = () =>
                {
                    TestUtils.ExecuteSql("truncate RetroLock,SaleItemNote,SaleItem,Sale,ItemVariant,Item,Customer,EmployeeContact,Employee,SaleStatus,PhoneType restart identity cascade");
                    Startup.InitializeRetroDRY(integrationTestMode: true);
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - one client only creates SaleStatus and Item records
                //server - clear out sessions
                StepCode = "b20-10",
                NextStepCode = "b20-20",
                Validate = () =>
                {
                    Startup.InitializeRetroDRY(integrationTestMode: true);
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - create all client sessions; in parallel each creates one customer, and 40 sales for that customer, on a random day in 2020
                //server - check cache is empty and 960 sales exist
                StepCode = "b20-20",
                NextStepCode = "b30-10",
                Validate = () =>
                {
                    if (TestUtils.CountRecords("Sale") != 960) throw new Exception("Wrong number of sales created");
                    if (TestUtils.AllDiagnostics.Sum(d => d.NumCachedDatons) > 7) throw new Exception("Expected customers and order to not be in cache");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - query for all sales in their month
                //server - confirm 12 (ideally), not 24, queries were sent to database
                StepCode = "b30-10",
                NextStepCode = "b30-20",
                Validate = () =>
                {
                    //the following won't work with the test design because any pair of clients for a given month are going to two servers
                    //int numLoads = TestUtils.AllDiagnostics.Sum(d => d.LoadCount);
                    //if (numLoads < 12 || numLoads > 22) throw new Exception("Expected to load less than 24 viewons");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - subscribe to all the sales that were queried, then attempt to lock 
                //(in parallel, so only one of the two clients for that month will be successful for each sale); then confirm
                //that a total of 960 locks were successful in total
                //server - confirm 960 server locks exist, and 960*2 subscriptions exist in total
                StepCode = "b30-20",
                NextStepCode = "b30-30",
                Validate = () =>
                {
                    if (TestUtils.LockCount() != 960) throw new Exception("Expected all sales to be locked");
                    int numSubs = TestUtils.AllDiagnostics.Sum(d => d.SubscriptionCount);
                    if (numSubs != 960 * 2) throw new Exception($"Expected all sales to be subscribed by 2 clients each; actual is {numSubs}");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - save a change to shipped date on all locked sales and keep lock
                //server - confirm same number of locks/subscriptions as previous step
                StepCode = "b30-30",
                NextStepCode = "b40-05",
                Validate = () =>
                {
                    int lockCount = TestUtils.LockCount();
                    if (lockCount != 960) throw new Exception($"Expected all sales to be locked; actual is {lockCount}");
                    int numSubs = TestUtils.AllDiagnostics.Sum(d => d.SubscriptionCount);
                    if (numSubs != 960 * 2) throw new Exception($"Expected all sales to be subscribed by 2 clients each; actual is {numSubs}");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - clients 22,23 (December sales) quit cleanly
                //server - confirm no december sales are locked, and there are 22 clients total
                StepCode = "b40-05",
                NextStepCode = "b40-10",
                Validate = () =>
                {
                    int numClients = TestUtils.AllDiagnostics.Sum(d => d.NumSessions);
                    if (numClients != 22) throw new Exception("Sessions timed out unexpectedly");
                    var saleIds = TestUtils.LoadList<int>("select SaleId from Sale where SaleDate>'2020-12-1'");
                    foreach (int saleId in saleIds) 
                    {
                        object lockedBy = TestUtils.QueryScalar($"select LockedBy from RetroLock where DatonKey='Sale|={saleId}'");
                        if (!(lockedBy is DBNull)) throw new Exception($"A december sale is still locked: {saleId}/{lockedBy}");
                    }
                    int lockCount = TestUtils.LockCount();
                    int expectedLockCount = 960 - saleIds.Count();
                    if (lockCount != expectedLockCount) throw new Exception($"Expected Deceber sales to be unlocked; actual is {lockCount}; expected is {expectedLockCount}");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - unlock all, then wait for subscriptions to propogtate, then confirm all sales that are not locked received update with new shipped date
                //server - confirm no clients timed out (should still be long polling)
                StepCode = "b40-10",
                NextStepCode = "b40-20",
                Validate = () =>
                {
                    int numClients = TestUtils.AllDiagnostics.Sum(d => d.NumSessions);
                    if (numClients != 22) throw new Exception("Sessions timed out unexpectedly");
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - clients 20,21 (November) abort without saying bye
                //server - wait for client timeouts, then confirm 20 clients total, and that there are no viewons in any cache
                StepCode = "b40-20",
                NextStepCode = "b40-30",
                Validate = async () =>
                {
                    await Task.Delay(15000);
                    foreach (var r in Globals.TestingRetroverse) 
                        r.DiagnosticCleanup();
                    var diags = TestUtils.AllDiagnostics;
                    int numClients = diags.Sum(d => d.NumSessions);
                    if (numClients != 20) throw new Exception($"Sessions timed out unexpectedly; there are {numClients} sessions");
                    int cachedViewons = diags.Sum(d => d.NumCachedViewons);
                    if (cachedViewons > 0) throw new Exception("Expected no viewons to still be in cache");
                }
            },
            new TestStep
            {
                //client - clients all unsubscribe from everything; confirm cache empty
                //server - wait and clean server cache, then confirm cache empty
                StepCode = "b40-30",
                NextStepCode = "b40-80",
                Validate = async () =>
                {
                    await Task.Delay(15000);
                    foreach (var r in Globals.TestingRetroverse)
                        r.DiagnosticCleanup();
                    var diags = TestUtils.AllDiagnostics;
                    int cacheCount = diags.Sum(d => d.NumCachedDatons);
                    if (cacheCount > 0) throw new Exception("Expected cache to be empty");
                }
            },
            new TestStep
            {
                //client - quit nicely
                //server - confirm no sessions, reset server back to normal mode
                StepCode = "b40-80",
                NextStepCode = "DONE",
                Validate = () =>
                {
                    var diags = TestUtils.AllDiagnostics;
                    int numClients = diags.Sum(d => d.NumSessions);
                    if (numClients != 0) throw new Exception("Expected all sessions to be ended");
                    Startup.InitializeRetroDRY();
                    return Task.CompletedTask;
                }
            }
        };
    }
}
