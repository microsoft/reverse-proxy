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
        private readonly T _value;

        internal Result(bool isSuccess, T value)
        {
            this.IsSuccess = isSuccess;
            this._value = value;
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

                return this._value;
            }
        }
    }

    /// <summary>
    /// Represents a result as either
    /// successful (with a corresponding <see cref="Value"/> of type <typeparamref name="TSuccess"/>)
    /// or failure (with a corresponding <see cref="Error"/> of type <typeparamref name="TError"/>).
    /// </summary>
    /// <typeparam name="TSuccess">Successful result type.</typeparam>
    /// <typeparam name="TError">Failure result type.</typeparam>
    internal readonly struct Result<TSuccess, TError>
    {
        private readonly TSuccess _value;
        private readonly TError _error;

        private Result(bool isSuccess, TSuccess value, TError error)
        {
            this.IsSuccess = isSuccess;
            this._value = value;
            this._error = error;
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

                return this._value;
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

                return this._error;
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
