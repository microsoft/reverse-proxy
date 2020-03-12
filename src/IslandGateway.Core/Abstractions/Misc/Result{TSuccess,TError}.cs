// <copyright file="Result{TSuccess,TError}.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Represents a result as either
    /// successful (with a corresponding <see cref="Value"/> of type <typeparamref name="TSuccess"/>)
    /// or failure (with a corresponding <see cref="Error"/> of type <typeparamref name="TError"/>).
    /// </summary>
    /// <typeparam name="TSuccess">Successful result type.</typeparam>
    /// <typeparam name="TError">Failure result type.</typeparam>
    internal readonly struct Result<TSuccess, TError>
    {
        private readonly TSuccess value;
        private readonly TError error;

        private Result(bool isSuccess, TSuccess value, TError error)
        {
            this.IsSuccess = isSuccess;
            this.value = value;
            this.error = error;
        }

        public bool IsSuccess { get; }

        public TSuccess Value
        {
            get
            {
                if (!this.IsSuccess)
                {
                    throw new Exception($"Cannot get {nameof(this.Value)} of a failure result.");
                }

                return this.value;
            }
        }

        public TError Error
        {
            get
            {
                if (this.IsSuccess)
                {
                    throw new Exception($"Cannot get {nameof(this.Error)} of a successful result.");
                }

                return this.error;
            }
        }

        public static Result<TSuccess, TError> Success(TSuccess value)
        {
            return new Result<TSuccess, TError>(true, value, default);
        }

        public static Result<TSuccess, TError> Failure(TError error)
        {
            return new Result<TSuccess, TError>(false, default, error);
        }
    }
}
