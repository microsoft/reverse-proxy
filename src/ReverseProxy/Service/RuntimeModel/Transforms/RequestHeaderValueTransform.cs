// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderValueTransform : RequestHeaderTransform
    {
        private readonly string _value;
        private readonly bool _append;

        public RequestHeaderValueTransform(string value, bool append)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _append = append;
        }

        public override StringValues Apply(HttpContext context, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            if (_append)
            {
                return StringValues.Concat(values, _value);
            }

            // Set
            return _value;
        }
    }
}
