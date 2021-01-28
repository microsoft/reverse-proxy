// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.Config
{
    public static class TransformHelpers
    {
        public static void TryCheckTooManyParameters(Action<Exception> onError, IReadOnlyDictionary<string, string> rawTransform, int expected)
        {
            if (rawTransform.Count > expected)
            {
                onError(new InvalidOperationException("The transform contains more parameters than expected: " + string.Join(';', rawTransform.Keys)));
            }
        }

        public static void CheckTooManyParameters(IReadOnlyDictionary<string, string> rawTransform, int expected)
        {
            TryCheckTooManyParameters(ex => throw ex, rawTransform, expected);
        }
    }
}
