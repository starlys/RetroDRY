using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SampleServer.Tests;

namespace SampleServer.Controllers
{
    /// <summary>
    /// Integration test endpoints
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet("newsession")]
        public object GetSession()
        {
            bool isLocal = Request.Host.Host == "localhost" || Request.Host.Host == "127.0.0.1";
            if (!isLocal) throw new Exception("Must run on localhost");
            var user = UserCache.Buffy_The_Admin;
            string sessionKey = Globals.Retroverse.CreateSession(user);
            return new
            {
                sessionKey
            };
        }

        /// <summary>
        /// Get next action to do on client, or if argument is 'start', begin test suite.
        /// </summary>
        [HttpGet("nextaction/{completedStepCode}")]
        public async Task<object> GetNextAction(string completedStepCode)
        {
            TestStep step;
            if (completedStepCode == "start")
                step = GetStep(TestingState.FirstStepCode);
            else
                step = GetStep(completedStepCode);
            await step.Validate();
            return new
            {
                step.NextStepCode
            };
        }

        private TestStep GetStep(string code) => TestingState.Steps.First(s => s.StepCode == code);
    }
}