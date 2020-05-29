// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Generates a new request path by plugging matched route parameters into the given pattern.
    /// </summary>
    public class PathRouteValueTransform : RequestParametersTransform
    {
        private string Pattern { get; set; }

        public PathRouteValueTransform(string pattern)
        {
            Pattern = pattern;
        }

        public override void Run(RequestParametersTransformContext context)
        {
            // throw new NotImplementedException();
        }
    }
}
