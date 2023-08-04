using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RetroDRY
{
    /// <summary>
    /// A FileResult for use with public MVC API methods, which allows the caller to inject
    /// async content. From:
    /// https://blog.stephencleary.com/2016/11/streaming-zip-on-aspnet-core.html
    /// </summary>
    /// <remarks>
    /// The callback implementation must be careful to not do any synchronous operations. For example:
    ///     await using var wri = new StreamWriter(outputStream); //use "await using" so that the StreamWriter calls FlushAsync
    ///     await wri.WriteLineAsync($"Line of text");
    /// </remarks>
    public class FileCallbackResult : FileResult
    {
        readonly private Func<Stream, ActionContext, Task> _callback;

        /// <summary>
        /// Construct
        /// </summary>
        /// <param name="contentType"></param>
        /// <param name="callback">function to write contents</param>
        public FileCallbackResult(System.Net.Http.Headers.MediaTypeHeaderValue contentType, Func<Stream, ActionContext, Task> callback)
            : base(contentType.ToString())
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        /// <summary>
        /// </summary>
        /// <param name="context"></param>
        public override Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var executor = new FileCallbackResultExecutor(context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>());
            return executor.ExecuteAsync(context, this);
        }

        private sealed class FileCallbackResultExecutor : FileResultExecutorBase
        {
            public FileCallbackResultExecutor(ILoggerFactory loggerFactory)
                : base(CreateLogger<FileCallbackResultExecutor>(loggerFactory))
            {
            }

            public Task ExecuteAsync(ActionContext context, FileCallbackResult result)
            {
                SetHeadersAndLog(context, result, null, false);
                return result._callback(context.HttpContext.Response.Body, context);
            }
        }
    }
}
