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
            return Globals.Retroverse.HandleHttpMain(req);
        }

        [HttpPost("long")]
        public Task<LongResponse> Long(LongRequest req)
        {
            return Globals.Retroverse.HandleHttpLong(req);
        }

    }
}