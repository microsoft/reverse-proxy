// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Queues.Composition
{
    /// <summary>
    /// Class WorkQueueBase is a delegating base class for <see cref="IWorkQueue{TItem}" /> interface. These classes are
    /// ported from go, which favors composition over inheritance, so the pattern is followed.
    /// Implements the <see cref="IWorkQueue{TItem}" />.
    /// </summary>
    /// <typeparam name="TItem">The type of the t item.</typeparam>
    /// <seealso cref="IWorkQueue{TItem}" />
    public abstract class WorkQueueBase<TItem> : IWorkQueue<TItem>
    {
        private readonly IWorkQueue<TItem> _base;
        private bool _disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkQueueBase{TItem}" /> class.
        /// </summary>
        /// <param name="workQueue">The work queue.</param>
        public WorkQueueBase(IWorkQueue<TItem> workQueue) => _base = workQueue;

        /// <inheritdoc/>
        public virtual void Add(TItem item) => _base.Add(item);

        /// <inheritdoc/>
        public virtual void Done(TItem item) => _base.Done(item);

        /// <inheritdoc/>
        public virtual Task<(TItem item, bool shutdown)> GetAsync(CancellationToken cancellationToken) => _base.GetAsync(cancellationToken);

        /// <inheritdoc/>
        public virtual int Len() => _base.Len();

        /// <inheritdoc/>
        public virtual void ShutDown() => _base.ShutDown();

        /// <inheritdoc/>
        public virtual bool ShuttingDown() => _base.ShuttingDown();

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _base.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }
    }
}
