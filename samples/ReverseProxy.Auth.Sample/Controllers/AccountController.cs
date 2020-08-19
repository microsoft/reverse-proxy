// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.ReverseProxy.Auth.Sample.Controllers
{
    [AllowAnonymous]
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login(string returnUrl)
        {
            ViewData["returnUrl"] = returnUrl;

            return View();
        }

        [HttpPost]
        public IActionResult Login(string name, string claim, string returnUrl)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, name),
                new Claim("CustomClaim", claim)
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            return SignIn(principal, new AuthenticationProperties()
            {
                RedirectUri = returnUrl
                // SignIn is the only one that requires a scheme: https://github.com/dotnet/aspnetcore/issues/23325
            }, CookieAuthenticationDefaults.AuthenticationScheme);
        }

        [HttpPost]
        public IActionResult Logout()
        {
            return SignOut(new AuthenticationProperties()
            {
                RedirectUri = "/Account/LoggedOut",
            });
        }

        [HttpGet]
        public IActionResult LoggedOut()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
