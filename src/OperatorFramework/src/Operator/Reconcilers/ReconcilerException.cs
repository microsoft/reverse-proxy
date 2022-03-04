// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using System;
using System.Runtime.Serialization;

namespace Microsoft.Kubernetes.Operator.Reconcilers;

[Serializable]
public class ReconcilerException : Exception
{
    public ReconcilerException()
    {
    }

    public ReconcilerException(string message) : base(message)
    {
    }

    public ReconcilerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ReconcilerException(V1Status status)
        : base((status ?? throw new ArgumentNullException(nameof(status))).Message)
    {
        Status = status;
    }

    public ReconcilerException(V1Status status, Exception innerException)
        : base((status ?? throw new ArgumentNullException(nameof(status))).Message, innerException)
    {
        Status = status;
    }

    protected ReconcilerException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    public V1Status Status { get; }
}
