// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.Controller.Hosting.Fakes
{
    public class TestLatches
    {
        public TestLatch RunEnter { get; } = new TestLatch();
        public TestLatch RunResult { get; } = new TestLatch();
        public TestLatch RunExit { get; } = new TestLatch();
    }
}
