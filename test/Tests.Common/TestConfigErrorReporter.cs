// <copyright file="TestConfigErrorReporter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using IslandGateway.Core.Abstractions;

namespace Tests.Common
{
    public class TestConfigErrorReporter : IConfigErrorReporter
    {
        public List<TestConfigError> Errors { get; } = new List<TestConfigError>();

        public void ReportError(string code, string elementId, string message)
        {
            this.Errors.Add(new TestConfigError { ErrorCode = code, ElementId = elementId, Message = message });
        }
    }
}
