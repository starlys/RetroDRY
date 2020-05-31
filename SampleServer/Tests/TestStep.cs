using System;
using System.Threading.Tasks;

namespace SampleServer.Tests
{
    public class TestStep
    {
        /// <summary>
        /// Step identifier shared on client and server
        /// </summary>
        public string StepCode;

        /// <summary>
        /// Server action to take after client performs the step
        /// </summary>
        public Func<Task> Validate;

        /// <summary>
        /// Next client step to take after server validation
        /// </summary>
        public string NextStepCode;
    }
}
