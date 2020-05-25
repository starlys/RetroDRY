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
        public static string FirstStepCode = "10-10";

        public static TestStep[] Steps = new[]
        {
            new TestStep
            {
                //client - no action
                //server - clears out database
                StepCode = "10-10",
                NextStepCode = "10-20",
                Validate = () =>
                {
                    TestUtils.ExecuteSql("delete from Customer"); //todo etc all tables
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - attempts start session with unknown user, confirm bad response
                //server - confirm no sessions active
                StepCode = "10-20",
                NextStepCode = "10-30",
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
                StepCode = "10-30",
                NextStepCode = "20-10",
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
                StepCode = "20-10",
                NextStepCode = "30-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //TODO LEFT OFF HERE
                //client - creates new customer and saves
                //server - confirm database contains customer and there are no locks
                StepCode = "30-10",
                NextStepCode = "40-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - gets customers viewon and confirms it has one row
                //server - no action 
                StepCode = "40-10",
                NextStepCode = "50-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - lock customer
                //server - confirm lock is active in lock table
                StepCode = "50-10",
                NextStepCode = "60-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - unlock customer without making changes
                //server - confirm unlock
                StepCode = "60-10",
                NextStepCode = "70-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - lock customer then save change to main and child rows, and unlock
                //server - confirm changes were saved
                StepCode = "70-10",
                NextStepCode = "80-10",
                Validate = () =>
                {
                    //todo maybe find way to inspect incoming diff to ensure expected contents
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - confirm 
                StepCode = "30-10",
                NextStepCode = "40-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - confirm 
                StepCode = "30-10",
                NextStepCode = "40-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - confirm 
                StepCode = "30-10",
                NextStepCode = "40-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - confirm 
                StepCode = "30-10",
                NextStepCode = "40-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - confirm 
                StepCode = "30-10",
                NextStepCode = "40-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - confirm 
                StepCode = "30-10",
                NextStepCode = "40-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
            new TestStep
            {
                //client - 
                //server - confirm 
                StepCode = "30-10",
                NextStepCode = "40-10",
                Validate = () =>
                {
                    return Task.CompletedTask;
                }
            },
        };
    }
}
