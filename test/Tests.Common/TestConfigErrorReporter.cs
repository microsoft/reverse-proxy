// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.Abstractions;

namespace Tests.Common
{
    public class TestConfigErrorReporter : IConfigErrorReporter
    {
        public List<TestConfigError> Errors { get; } = new List<TestConfigError>();

        public void ReportError(string code, string elementId, string message)
        {
            Errors.Add(new TestConfigError { ErrorCode = code, ElementId = elementId, Message = message });
        }

        public void ReportError(string code, string itemId, string message, Exception ex)
        {
            Errors.Add(new TestConfigError { ErrorCode = code, ElementId = itemId, Message = message, Exception = ex });
        }
    }
}
