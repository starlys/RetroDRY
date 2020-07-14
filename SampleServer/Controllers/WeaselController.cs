using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using RetroDRY;

namespace SampleServer.Controllers
{
    /// <summary>
    /// Sample controller where you might put in endpoints to handle datons in compatible format
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class WeaselController : ControllerBase
    {
        /// <summary>
        /// Sample login endpoint
        /// </summary>
        [HttpPost("login")]
        public object Login([FromBody]LoginRequest req)
        {
            //in a real app, validate user against database
            var user = UserCache.Users.FirstOrDefault(u => u.Id == req.Id);

            //check password, register with RetroDRY
            string message = "";
            string sessionKey = "";
            if (user != null)
            {
                if (user.Password == req.Password)
                    sessionKey = Globals.Retroverse.CreateSession(user);
                else user = null;
            }

            return new
            {
                Success = user != null,
                Message = message,
                SessionKey = sessionKey
            };
        }

        /// <summary>
        /// Get any daton in compatible format by key
        /// </summary>
        [HttpGet("any/{datonKey}")]
        public async Task<object> Get(string datonKey)
        {
            //authenticate; for demo purposes we will assume the user, but for a real app you would check the authentication 
            //header and look up the user in a cache or databse
            var user = UserCache.Buffy_The_Admin;

            //get the daton, with permissions enforced (so it may be missing some rows and columns depending on the user)
            var key = DatonKey.Parse(datonKey);
            var loadResult = await Globals.Retroverse.GetDaton(key, user);
            if (loadResult.Daton == null) return null; //could check loadResult.Errors here too

            //return as json
            string json = Retrovert.ToWire(Globals.Retroverse.DataDictionary, loadResult.Daton, true);
            return Content(json, "application/json");
        }

        /// <summary>
        /// Save a persiston provided in diff format
        /// </summary>
        [HttpPost("any")]
        public async Task<object> Save([FromBody]JObject diffJson)
        {
            //WARNING: This sample implementation bypasses locking, versioning and subscriptions. If you use it side by side with
            //the RetroDRY implementation, changes saved here won't be noticed by RetroDRY clients.

            //authenticate; for demo purposes we will assume the user, but for a real app you would check the authentication 
            //header and look up the user in a cache or databse
            var user = UserCache.Buffy_The_Admin;

            //parse the changes provided by the caller
            var diff = Retrovert.FromDiff(Globals.Retroverse.DataDictionary, diffJson);

            //save
            MultiSaver.Result[] saveresult;
            using (var saver = new MultiSaver(Globals.Retroverse, user, new[] { diff }))
            {
                await saver.Save();
                saveresult = saver.GetResults();
            }

            return new
            {
                Key = saveresult[0].NewKey.ToString(),
                saveresult[0].Errors
            };
        }
    }

    public class LoginRequest
    {
        public string Id { get; set; }
        public string Password { get; set; }
    }
}
