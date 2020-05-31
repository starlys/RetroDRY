using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RetroDRY;

namespace SampleServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RetroController : ControllerBase
    {
        [HttpPost("main")]
        public Task<MainResponse> Main([FromBody]MainRequest req)
        {
            return GetRetroverse().HandleHttpMain(req);
        }

        [HttpPost("long")]
        public Task<LongResponse> Long(LongRequest req)
        {
            return GetRetroverse().HandleHttpLong(req);
        }

        /// <summary>
        /// In a real app, this would just refer to a single global Retroverse instance. 
        /// For integration testing, we support 3 independent psuedo-servers in the same process.
        /// </summary>
        private Retroverse GetRetroverse()
        {
            if (Request.Host.Port == 5003) return Globals.TestingRetroverse[2];
            if (Request.Host.Port == 5002) return Globals.TestingRetroverse[1];
            return Globals.Retroverse; //port 5001
        }
    }
}