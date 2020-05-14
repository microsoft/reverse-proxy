// ------------------------------------------------------------------------------
// <copyright file="ExceptionExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Extensions for the <see cref="Exception"/> class.
    /// </summary>
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Determines whether the provided <paramref name="exception"/> should be considered fatal. An exception
        /// is considered to be fatal if it, or any of the inner exceptions are one of the following type:
        ///
        /// - System.OutOfMemoryException
        /// - System.InsufficientMemoryException
        /// - System.ThreadAbortException
        /// - System.AccessViolationException
        /// - System.SEHException
        /// - System.StackOverflowException
        /// - System.TypeInitializationException
        /// - Microsoft.PowerApps.CoreFramework.MonitoredException marked as Fatal.
        /// </summary>
        public static bool IsFatal(this Exception exception)
        {
            Contracts.CheckValue(exception, nameof(exception));

            while (exception != null)
            {
                if ((exception is OutOfMemoryException && !(exception is InsufficientMemoryException)) ||
                    (exception is ThreadAbortException) ||
                    (exception is AccessViolationException) ||
                    (exception is SEHException) ||
                    (exception is StackOverflowException) ||
                    (exception is TypeInitializationException))
                {
                    return true;
                }

                // These exceptions aren't fatal in themselves, but the CLR uses them
                // to wrap other exceptions, so we want to look deeper
                if (exception is TypeInitializationException || // TODO: May be considered fatal in itself, as cctor didn't run
                    exception is TargetInvocationException)
                {
                    exception = exception.InnerException;
                }
                else if (exception is AggregateException aex)
                {
                    // AggregateException can contain other AggregateExceptions in its InnerExceptions list so we
                    // flatten it first. That will essentially create a list of exceptions from the AggregateException's
                    // InnerExceptions property in such a way that any exception other than AggregateException is put
                    // into this list. If there is an AggregateException then exceptions from its InnerExceptions list are
                    // put into this new list etc. Then a new instance of AggregateException with this flattened list is returned.
                    //
                    // AggregateException InnerExceptions list is immutable after creation and the walk happens only for
                    // the InnerExceptions property of AggregateException and not InnerException of the specific exceptions.
                    // This means that the only way to have a circular referencing here is through reflection and forward-
                    // reference assignment which would be insane. In such case we would also run into stack overflow
                    // when tracing out the exception since AggregateException's ToString does not have any protection there.
                    //
                    // On that note that's another reason why we want to flatten here as opposed to just let recursion do its magic
                    // since in an unlikely case there is a circle we'll get OutOfMemory here instead of StackOverflow which is
                    // a lesser of the two evils.
                    var faex = aex.Flatten();
                    var iexs = faex.InnerExceptions;
                    if (iexs != null)
                    {
                        foreach (var iex in iexs)
                        {
                            if (iex.IsFatal())
                            {
                                return true;
                            }
                        }
                    }

                    exception = exception.InnerException;
                }
                else
                {
                    break;
                }
            }

            return false;
        }
    }
}
