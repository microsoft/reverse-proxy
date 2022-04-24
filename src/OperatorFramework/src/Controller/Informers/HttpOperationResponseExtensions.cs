// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Exceptions;
using Microsoft.Rest;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Informers
{
    internal static class HttpOperationResponseExtensions
    {
        public static Watcher<T> CustomWatch<T, L>(
            this Task<HttpOperationResponse<L>> responseTask,
            Action<WatchEventType, T> onEvent,
            Action<Exception> onError = null,
            Action onClosed = null)
        {
            return new Watcher<T>(MakeStreamReaderCreator<T, L>(responseTask), onEvent, onError, onClosed);
        }

        private static Func<Task<TextReader>> MakeStreamReaderCreator<T, L>(Task<HttpOperationResponse<L>> responseTask)
        {
            return async () =>
            {
                var response = await responseTask.ConfigureAwait(false);

                if (!(response.Response.Content is Microsoft.Kubernetes.Client.LineSeparatedHttpContent content))
                {
                    throw new KubernetesClientException("not a watchable request or failed response");
                }

                return content.StreamReader;
            };
        }
    }
}
