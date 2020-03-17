// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace IslandGateway.Signals
{
    /// <summary>
    /// Extension methods for <see cref="IReadableSignal{T}"/>.
    /// </summary>
    public static class SignalExtensions
    {
        /// <summary>
        /// Projects the <paramref name="source"/> signal into a new signal
        /// using the <paramref name="selector"/> function.
        /// </summary>
        public static IReadableSignal<TResult> Select<TSource, TResult>(
            this IReadableSignal<TSource> source,
            Func<TSource, TResult> selector)
        {
            CheckValue(source, nameof(source));
            CheckValue(selector, nameof(selector));

            var result = new Signal<TResult>(source.Context);
            Update();
            return result;

            void Update()
            {
                var snapshot = source.GetSnapshot();
                result.Value = selector(snapshot.Value);
                snapshot.OnChange(Update);
            }
        }

        /// <summary>
        /// Flattens a nested signal into a new signal.
        /// </summary>
        public static IReadableSignal<T> Flatten<T>(this IReadableSignal<IReadableSignal<T>> source)
        {
            CheckValue(source, nameof(source));

            var result = new Signal<T>(source.Context);
            ISignalSnapshot<IReadableSignal<T>> outerSnapshot;
            IDisposable innerSubscription = null;

            OnOuterChanged();
            return result;

            void OnOuterChanged()
            {
                // If we were subscribed to a different signal, we don't care about it anymore since the outer changed.
                innerSubscription?.Dispose();

                outerSnapshot = source.GetSnapshot();
                if (outerSnapshot.Value is null)
                {
                    result.Value = default;
                    innerSubscription = null;
                }
                else
                {
                    if (!ReferenceEquals(outerSnapshot.Value.Context, source.Context))
                    {
                        throw new InvalidOperationException($"Mismatched {nameof(SignalContext)}'s. Cannot mix signals created from different factories.");
                    }

                    var innerSnapshot = outerSnapshot.Value.GetSnapshot();
                    result.Value = innerSnapshot.Value;
                    innerSubscription = innerSnapshot.OnChange(OnInnerChanged);
                }
                outerSnapshot.OnChange(OnOuterChanged);
            }

            void OnInnerChanged()
            {
                // No need to unsubscribe, the old subscription will never fire again and GC will clean things up eventually
                var innerSnapshot = outerSnapshot.Value.GetSnapshot();
                result.Value = source.Value.Value;
                innerSubscription = innerSnapshot.OnChange(OnInnerChanged);
            }
        }

        /// <summary>
        /// Projects the <paramref name="source"/> signal into a new signal
        /// using the <paramref name="selector"/> function and flattens the result into a new signal.
        /// </summary>
        public static IReadableSignal<TResult> SelectMany<TSource, TResult>(
            this IReadableSignal<TSource> source,
            Func<TSource, IReadableSignal<TResult>> selector)
        {
            CheckValue(source, nameof(source));
            CheckValue(selector, nameof(selector));

            return source.Select(selector).Flatten();
        }

        /// <summary>
        /// Projects the <paramref name="source"/> signal into a new signal
        /// that reacts to all changes but drops the original value.
        /// </summary>
        public static IReadableSignal<Unit> DropValue<T>(this IReadableSignal<T> source)
        {
            CheckValue(source, nameof(source));

            return source.Select(value => Unit.Instance);
        }

        /// <summary>
        /// Creates a signal that reacts to changes to each item
        /// in the <paramref name="source"/> list without materializing any projections.
        /// If <paramref name="source"/> is empty, null is returned.
        /// </summary>
        public static IReadableSignal<T> AnyChange<T>(this IEnumerable<IReadableSignal<T>> source)
        {
            CheckValue(source, nameof(source));

            Signal<T> result = null;
            foreach (var item in source)
            {
                if (result == null)
                {
                    result = new Signal<T>(item.Context, item.Value);
                }
                else
                {
                    if (!ReferenceEquals(result.Context, item.Context))
                    {
                        throw new InvalidOperationException($"Mismatched {nameof(SignalContext)}'s. Cannot mix signals created from different factories.");
                    }
                }

                var snapshot = item.GetSnapshot();
                snapshot.OnChange(Update);

                void Update()
                {
                    var snapshot = item.GetSnapshot();
                    result.Value = snapshot.Value;
                    snapshot.OnChange(Update);
                }
            }

            return result;
        }

        private static void CheckValue<T>(T value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
