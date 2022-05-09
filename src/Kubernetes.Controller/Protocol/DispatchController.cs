// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Yarp.Kubernetes.Controller.Dispatching;

namespace Yarp.Kubernetes.Controller.Controllers;

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
    public Task<IActionResult> WatchAsync()
    {
        return Task.FromResult<IActionResult>(new DispatchActionResult(_dispatcher));
    }
}
