// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Routing.Template;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    internal class QueryStringParameterTransform : RequestParametersTransform
    {
        private readonly QueryStringTransformMode _mode;
        private readonly string _key;
        private readonly RouteTemplate _template;
        private readonly TemplateBinderFactory _binderFactory;

        public QueryStringParameterTransform(QueryStringTransformMode mode, string key, string value, TemplateBinderFactory binderFactory)
        {
            _mode = mode;
            _key = key;
            _binderFactory = binderFactory ?? throw new ArgumentNullException(nameof(binderFactory));
            _template = TemplateParser.Parse(value);
        }

        public override void Apply(RequestParametersTransformContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var routeValues = context.HttpContext.Request.RouteValues;
            var binder = _binderFactory.Create(_template, defaults: routeValues);
            var value = binder.BindValues(acceptedValues: routeValues);

            switch (_mode)
            {
                case QueryStringTransformMode.Append:
                    context.Query = context.Query.Add(_key, value);
                    break;
                default:
                    throw new NotImplementedException(_mode.ToString());
            }
        }
    }

    public enum QueryStringTransformMode
    {
        Append,
    }
}
