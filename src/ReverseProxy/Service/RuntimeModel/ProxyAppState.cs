// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    internal class ProxyAppState : IProxyAppState
    {
        private readonly TaskCompletionSource<bool> _isFullyInitialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitializationTask => _isFullyInitialized.Task;

        void IProxyAppState.SetFullyInitialized()
        {
            _isFullyInitialized.TrySetResult(true);
        }
    }
}
