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
        [HttpGet("newsession/{serverNo},{userId}")]
        public object GetSession(int serverNo, string userId)
        {
            bool isLocal = Request.Host.Host == "localhost" || Request.Host.Host == "127.0.0.1";
            if (!isLocal) throw new Exception("Must run on localhost");
            var user = UserCache.Users.First(u => u.Id == userId);
            var retroverse = Globals.TestingRetroverse?[serverNo] ?? Globals.Retroverse;
            string sessionKey = retroverse.CreateSession(user);
            return new
            {
                sessionKey
            };
        }

        /// <summary>
        /// Get next action to do on client
        /// </summary>
        [HttpGet("nextaction/{completedStepCode}")]
        public async Task<object> GetNextAction(string completedStepCode)
        {
            TestStep step = GetStep(completedStepCode);
            try
            {
                if (step.Validate != null)
                    await step.Validate();
            }
            catch (Exception ex)
            {
                return new
                {
                    NextStepCode = "",
                    ValidateError = ex.Message
                };
            }
            return new
            {
                step.NextStepCode,
                ValidateError = ""
            };
        }

        static TestStep GetStep(string code) => TestingState.Steps.First(s => s.StepCode == code);
    }
}