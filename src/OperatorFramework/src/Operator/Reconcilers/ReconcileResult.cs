// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Operator
{
    public struct ReconcileResult : IEquatable<ReconcileResult>
    {
        public bool Requeue { get; set; }
        public TimeSpan RequeueAfter { get; set; }
        public Exception Error { get; set; }

        public override bool Equals(object obj)
        {
            return obj is ReconcileResult result && Equals(result);
        }

        public bool Equals(ReconcileResult other)
        {
            return Requeue == other.Requeue &&
                   RequeueAfter.Equals(other.RequeueAfter) &&
                   EqualityComparer<Exception>.Default.Equals(Error, other.Error);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Requeue, RequeueAfter, Error);
        }

        public static bool operator ==(ReconcileResult left, ReconcileResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ReconcileResult left, ReconcileResult right)
        {
            return !(left == right);
        }
    }
}
