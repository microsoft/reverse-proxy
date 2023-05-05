// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Utilities;

internal static class Observability
{
    public static readonly ActivitySource YarpActivitySource = new ActivitySource("Yarp.ReverseProxy");

    public static Activity? GetYarpActivity(this HttpContext context)
    {
        return context.Features[typeof(YarpActivity)] as Activity;
    }

    public static void SetYarpActivity(this HttpContext context, Activity? activity)
    {
        if (activity != null)
        {
            context.Features[typeof(YarpActivity)] = activity;
        }
    }

    public static void AddError(this Activity activity, string message, string description)
    {
        if (activity != null) {
            var tagsCollection = new ActivityTagsCollection
            {
                { "error", message },
                { "description", description }
            };

            activity.AddEvent(new ActivityEvent("Error", default, tagsCollection));
        }
    }

    private class YarpActivity
    {
    }
}
