// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ReverseProxy.Core.Service.RuntimeModel.Transforms
{
    public class PathStringTransform : RequestParametersTransform
    {
        private PathString Value { get; set; }

        public override void Run(RequestParametersTransformContext context)
        {
            throw new NotImplementedException();
        }
    }
}
