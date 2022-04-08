// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.Kubernetes.OperatorFramework.Hosting.Fakes;

public class TestLatches
{
    public TestLatch RunEnter { get; } = new TestLatch();
    public TestLatch RunResult { get; } = new TestLatch();
    public TestLatch RunExit { get; } = new TestLatch();
}
