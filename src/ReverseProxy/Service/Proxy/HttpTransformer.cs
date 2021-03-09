// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    public class HttpTransformer
    {
        /// <summary>
        /// A default set of transforms that copies all request and response fields and headers, except for some
        /// protocol specific values.
        /// </summary>
        public static readonly HttpTransformer Default = new HttpTransformer();

        /// <summary>
        /// Used to create derived instances.
        /// </summary>
        protected HttpTransformer() { }

        /// <summary>
        /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage fields are
        /// initialized except RequestUri, which will be initialized after the callback if no value is provided.
        /// The string parameter represents the destination URI prefix that should be used when constructing the RequestUri.
        /// The headers are copied by the base implementation, excluding some protocol headers like HTTP/2 pseudo headers (":authority").
        /// </summary>
        /// <param name="httpContext">The incoming request.</param>
        /// <param name="proxyRequest">The outgoing proxy request.</param>
        /// <param name="destinationPrefix">The uri prefix for the selected destination server which can be used to create the RequestUri.</param>
        public virtual Task TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            foreach (var header in httpContext.Request.Headers)
            {
                var headerName = header.Key;
                var headerValue = header.Value;
                if (StringValues.IsNullOrEmpty(headerValue))
                {
                    continue;
                }

                // Filter out HTTP/2 pseudo headers like ":method" and ":path", those go into other fields.
                if (headerName.Length > 0 && headerName[0] == ':')
                {
                    continue;
                }

                RequestUtilities.AddHeader(proxyRequest, headerName, headerValue);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// A callback that is invoked when the proxied response is received. The status code and reason phrase will be copied
        /// to the HttpContext.Response before the callback is invoked, but may still be modified there. The headers will be
        /// copied to HttpContext.Response.Headers by the base implementation, excludes certain protocol headers like
        /// `Transfer-Encoding: chunked`.
        /// </summary>
        /// <param name="httpContext">The incoming request.</param>
        /// <param name="proxyResponse">The response from the destination.</param>
        public virtual Task TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            var responseHeaders = httpContext.Response.Headers;
            CopyResponseHeaders(httpContext, proxyResponse.Headers, responseHeaders);
            if (proxyResponse.Content != null)
            {
                CopyResponseHeaders(httpContext, proxyResponse.Content.Headers, responseHeaders);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// A callback that is invoked after the response body to modify trailers, if supported. The trailers will be
        /// copied to the HttpContext.Response by the base implementation.
        /// </summary>
        /// <param name="httpContext">The incoming request.</param>
        /// <param name="proxyResponse">The response from the destination.</param>
        public virtual Task TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
            // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
            var responseTrailersFeature = httpContext.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            if (outgoingTrailers != null && !outgoingTrailers.IsReadOnly)
            {
                // Note that trailers, if any, should already have been declared in Proxy's response
                // by virtue of us having proxied all response headers in step 6.
                CopyResponseHeaders(httpContext, proxyResponse.TrailingHeaders, outgoingTrailers);
            }

            return Task.CompletedTask;
        }


        private static void CopyResponseHeaders(HttpContext httpContext, HttpHeaders source, IHeaderDictionary destination)
        {
            Debug.Assert(source.GetType() == typeof(HttpResponseHeaders) || source.GetType() == typeof(HttpContentHeaders));

            var isHttp2OrGreater = ProtocolHelper.IsHttp2OrGreater(httpContext.Request.Protocol);

            if (UnsafeHeaderManipulation.IsSupported)
            {
                var headers = UnsafeHeaderManipulation.GetRawResponseHeaders(source);
                if (headers is null)
                {
                    return;
                }

                foreach (var header in headers)
                {
                    var headerName = header.Key.Name;
                    if (RequestUtilities.ShouldSkipResponseHeader(headerName, isHttp2OrGreater))
                    {
                        continue;
                    }

                    if (header.Value is string valueString)
                    {
                        if (!UnsafeHeaderManipulation.HeaderValueContainsInvalidNewLine(valueString))
                        {
                            destination.Append(headerName, valueString);
                        }
                    }
                    // Fallback to regular, validated APIs
                    else if (source.TryGetValues(headerName, out var values))
                    {
                        Debug.Assert(values is string[]);
                        destination.Append(headerName, values as string[] ?? values.ToArray());
                    }
                }
            }
            else
            {
                foreach (var header in source)
                {
                    var headerName = header.Key;
                    if (RequestUtilities.ShouldSkipResponseHeader(headerName, isHttp2OrGreater))
                    {
                        continue;
                    }

                    Debug.Assert(header.Value is string[]);
                    destination.Append(headerName, header.Value as string[] ?? header.Value.ToArray());
                }
            }
        }
    }

    public static class FeatureSwitches
    {
        internal static bool UnsafeHeaderManipulation = true;
        public static void DisableUnsafeHeaderManipulation() => UnsafeHeaderManipulation = false;
    }

    //internal static class UnsafeHeaderManipulation
    public static class UnsafeHeaderManipulation
    {
        public static readonly bool IsSupported = CheckIsSupported();

        internal static bool HeaderValueContainsInvalidNewLine(string value)
        {
            var index = value.IndexOf('\r');

            if (index == -1)
            {
                return false;
            }

            return ContainsInvalidNewLineSlow(value, index);

            static bool ContainsInvalidNewLineSlow(string value, int i)
            {
                // Search for newlines followed by non-whitespace: This is not allowed in any header (be it a known or
                // custom header). E.g. "value\r\nbadformat: header" is invalid. However "value\r\n goodformat: header"
                // is valid: newlines followed by whitespace are allowed in header values.
                for (; i < value.Length; i++)
                {
                    if (value[i] == '\r' && (uint)(i + 1) < (uint)value.Length && value[i + 1] == '\n')
                    {
                        i++;

                        if (i == value.Length)
                        {
                            return true; // We have a string terminating with \r\n. This is invalid.
                        }

                        var c = value[i];
                        if (c != ' ' && c != '\t')
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        public static Dictionary<HeaderDescriptor, object> GetRawResponseHeaders(HttpHeaders headers)
        {
            Debug.Assert(headers.GetType() == typeof(HttpResponseHeaders) || headers.GetType() == typeof(HttpContentHeaders));

            return Unsafe.As<RawHttpHeaders>(headers).Data;
        }

        private sealed class RawHttpHeaders
        {
#pragma warning disable CS0649 // Data is never assigned to, and will always have its default value null
            public readonly Dictionary<HeaderDescriptor, object> Data;
#pragma warning restore CS0649 // Data is never assigned to, and will always have its default value null
        }

        public readonly struct HeaderDescriptor : IEquatable<HeaderDescriptor>
        {
#pragma warning disable CS0649 // Foo is never assigned to, and will always have its default value null
#pragma warning disable IDE0032 // Use auto property
            private readonly string _headerName;
            private readonly object _knownHeader;
#pragma warning restore IDE0032 // Use auto property
#pragma warning restore CS0649 // Foo is never assigned to, and will always have its default value null

            public string Name => _headerName;

            public bool Equals(HeaderDescriptor other) =>
                _knownHeader == null ?
                    string.Equals(_headerName, other._headerName, StringComparison.OrdinalIgnoreCase) :
                    _knownHeader == other._knownHeader;

            public override int GetHashCode() => _knownHeader?.GetHashCode() ?? StringComparer.OrdinalIgnoreCase.GetHashCode(_headerName);
        }

        private static bool CheckIsSupported()
        {
            try
            {
                if (!FeatureSwitches.UnsafeHeaderManipulation)
                {
                    return false;
                }

                if (Environment.Version.Major < 5)
                {
                    return false;
                }

                var isSupported = CheckIsSupportedCore();

                if (!isSupported)
                {
                    Debug.Fail("Unsafe header manipulation should be supported");
                }

                return isSupported;
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
                return false;
            }

            static bool CheckIsSupportedCore()
            {
                if (Environment.Version.Major > 6)
                {
                    // We should have a different story for 6/7
                    // If someone is using a year old build of YARP and their perf drops by 5%, that is fine
                    return false;
                }

                // Dictionary<HeaderDescriptor, object> _headerStore;
                var headerStoreField = typeof(HttpHeaders).GetField("_headerStore", BindingFlags.Instance | BindingFlags.NonPublic);
                if (headerStoreField is null)
                {
                    return false;
                }

                var headerStore = headerStoreField.FieldType;

                if (!headerStore.IsGenericType)
                {
                    return false;
                }

                if (headerStore.GetGenericTypeDefinition() != typeof(Dictionary<,>))
                {
                    return false;
                }

                var storeGenerics = headerStore.GetGenericArguments();
                if (storeGenerics.Length != 2)
                {
                    return false;
                }

                if (storeGenerics[1] != typeof(object))
                {
                    return false;
                }

                // internal readonly struct HeaderDescriptor : IEquatable<HeaderDescriptor>
                // {
                //     private readonly string _headerName;
                //     private readonly KnownHeader? _knownHeader;
                // }
                var headerDescriptor = storeGenerics[0];
                if (headerDescriptor.Name != nameof(HeaderDescriptor) || !headerDescriptor.IsValueType)
                {
                    return false;
                }

                var headerDescriptorFields = headerDescriptor.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (headerDescriptorFields.Length != 2)
                {
                    return false;
                }

                var (headerName, knownHeader) = headerDescriptorFields[0].Name == "_headerName"
                    ? (headerDescriptorFields[0], headerDescriptorFields[1])
                    : (headerDescriptorFields[1], headerDescriptorFields[0]);

                if (headerName.Name != "_headerName" || headerName.FieldType != typeof(string))
                {
                    return false;
                }

                if (knownHeader.Name != "_knownHeader" || knownHeader.FieldType.IsValueType)
                {
                    return false;
                }

                if (GetFieldOffset(headerDescriptor, headerName) != 0)
                {
                    return false;
                }

                if (GetFieldOffset(headerDescriptor, knownHeader) != IntPtr.Size)
                {
                    return false;
                }

                if (GetFieldOffset(typeof(HttpResponseHeaders), headerStoreField) != 0)
                {
                    return false;
                }

                if (GetFieldOffset(typeof(HttpContentHeaders), headerStoreField) != 0)
                {
                    return false;
                }

                // At this point it should be safe to access the raw headers
                // As we're lying to the runtime, some Dictionary functionality is not available
                // Ensure that the APIs we are actually using work
                if (!TestUsageScenario())
                {
                    return false;
                }

                return true;
            }

            static bool TestUsageScenario()
            {
                const string TestHeaderName = "foo";
                const string TestHeaderValue = "bar";

                var dummyResponse = new HttpResponseMessage();
                dummyResponse.Headers.TryAddWithoutValidation(TestHeaderName, TestHeaderValue);

                var rawHeaders = GetRawResponseHeaders(dummyResponse.Headers);
                if (rawHeaders is null)
                {
                    return false;
                }

                var sawHeader = false;
                foreach (var entry in rawHeaders)
                {
                    if (entry.Key.Name == TestHeaderName)
                    {
                        if (sawHeader || !ReferenceEquals(entry.Value, TestHeaderValue))
                        {
                            return false;
                        }
                        sawHeader = true;
                    }
                }

                if (!sawHeader)
                {
                    return false;
                }

                return true;
            }
        }

        public static int GetFieldOffset(Type type, FieldInfo field)
        {
            // Functionally equivalent to:
            // var dummyObject = new TObj();
            // return (int)Unsafe.ByteOffset(ref dummyObject, ref Unsafe.As<FIELD_TYPE, TObj>(ref dummyObject.FieldName));

            var fieldOffsetFunction = GenerateFieldOffsetMethod(field);
            var dummyObject = type.IsValueType ? Activator.CreateInstance(type) : FormatterServices.GetUninitializedObject(type);
            var offset = fieldOffsetFunction(dummyObject);
            return offset - (type.IsValueType ? 0 : IntPtr.Size);

            static Func<object, int> GenerateFieldOffsetMethod(FieldInfo field)
            {
                var method = new DynamicMethod(
                    name: "GetFieldOffset",
                    returnType: typeof(int),
                    parameterTypes: new[] { typeof(object) },
                    m: typeof(UnsafeHeaderManipulation).Module,
                    skipVisibility: true);

                var il = method.GetILGenerator();

                // Load the address of the object's field
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, field);

                // Load the object
                il.Emit(OpCodes.Ldarg_0);

                // Substract (field - object)
                il.Emit(OpCodes.Sub);

                // Convert to int32
                il.Emit(OpCodes.Conv_I4);

                // Return
                il.Emit(OpCodes.Ret);

                return (Func<object, int>)method.CreateDelegate(typeof(Func<object, int>));
            }
        }
    }
}
