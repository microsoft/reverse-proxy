// <copyright file="Result{T}.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Represents the result as either successful with a corresponding value of type <typeparamref name="T"/>
    /// or as a failure with no associated extra information.
    /// Error reporting should be done through alternative mechanisms.
    /// </summary>
    /// <typeparam name="T">Successful result type.</typeparam>
    internal readonly struct Result<T>
    {
        private readonly T value;

        internal Result(bool isSuccess, T value)
        {
            this.IsSuccess = isSuccess;
            this.value = value;
        }

        public bool IsSuccess { get; }

        public T Value
        {
            get
            {
                if (!this.IsSuccess)
                {
                    throw new Exception($"Cannot get {nameof(this.Value)} of a failed result.");
                }

                return this.value;
            }
        }
    }

    internal static class Result
    {
        public static Result<TSuccess> Success<TSuccess>(TSuccess value)
        {
            return new Result<TSuccess>(true, value);
        }

        public static Result<TSuccess> Failure<TSuccess>()
        {
            return new Result<TSuccess>(false, default);
        }
    }
}
