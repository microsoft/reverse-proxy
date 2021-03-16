// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using IngressController.Dispatching;

namespace IngressController.Controllers
{
    /// <summary>
    /// DispatchController provides API used by callers to begin streaming
    /// information being sent out through the <see cref="IDispatcher"/> muxer.
    /// </summary>
    [Route("api/dispatch")]
    [ApiController]
    public class DispatchController : ControllerBase
    {
        private readonly IDispatcher _dispatcher;

        public DispatchController(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        [HttpGet("/api/dispatch")]
        public async Task<IActionResult> WatchAsync()
        {
            return new DispatchActionResult(_dispatcher, HttpContext.RequestAborted);
        }
    }
}
