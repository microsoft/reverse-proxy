// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    internal class ProxyAppState : IProxyAppState
    {
        private readonly TaskCompletionSource<bool> _isFullyInitialized = new TaskCompletionSource<bool>();

        public bool IsFullyInitialized => _isFullyInitialized.Task.IsCompleted;

        public Task WaitForFullInitialization()
        {
            return _isFullyInitialized.Task;
        }

        void IProxyAppState.SetFullyInitialized()
        {
            _isFullyInitialized.TrySetResult(true);
        }
    }
}
