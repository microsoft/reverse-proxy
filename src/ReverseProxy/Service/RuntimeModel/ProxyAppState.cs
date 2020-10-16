// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    internal class ProxyAppState : IProxyAppState, IProxyAppStateSetter
    {
        private readonly TaskCompletionSource<bool> _isFullyInitialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsFullyInitialized => _isFullyInitialized.Task.IsCompleted;

        public Task WaitForFullInitialization()
        {
            return _isFullyInitialized.Task;
        }

        void IProxyAppStateSetter.SetFullyInitialized()
        {
            _isFullyInitialized.TrySetResult(true);
        }
    }
}
