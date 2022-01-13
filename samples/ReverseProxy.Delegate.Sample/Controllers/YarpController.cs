using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Yarp.ReverseProxy.Configuration;
using Yarp.Sample;

namespace Yarp.Sample.Controllers
{
    [Route("/yarp")]
    [ApiController]
    public class YarpController : Controller
    {
        private readonly IProxyConfigProvider _proxyConfigProvider;
        private readonly IReproConfig _reproConfig ;
        public YarpController(IProxyConfigProvider proxyConfigProvider, IReproConfig reproConfig)
        {
            _proxyConfigProvider = proxyConfigProvider;
            _reproConfig = reproConfig;
        }

        [HttpGet("reload")]
        public ActionResult Reload()
        {
            ((InMemoryConfigProvider)_proxyConfigProvider).Update(_reproConfig.GetRoutes(), _reproConfig.GetClusters());
            return Ok("ok");
        }

    }
}
