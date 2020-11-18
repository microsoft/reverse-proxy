// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class ProxyHttpRequestOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new ProxyHttpRequestOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            var options = new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version11,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
#endif
            };

            var clone = options.DeepClone();

            Assert.NotSame(options, clone);
            Assert.Equal(options.RequestTimeout, clone.RequestTimeout);
            Assert.Equal(options.Version, clone.Version);
#if NET
            Assert.Equal(options.VersionPolicy, clone.VersionPolicy);
#endif
        }


        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version11,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
#endif
            };

            var options2 = new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version11,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
#endif
            };

            var equals = ProxyHttpRequestOptions.Equals(options1, options2);

            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            var options1 = new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version11,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
#endif
            };

            var options2 = new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version20,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
#endif
            };

            var options3 = new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version11,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
#endif
            };

            var equals1 = ProxyHttpRequestOptions.Equals(options1, options2);
            var equals2 = ProxyHttpRequestOptions.Equals(options1, options3);

            Assert.False(equals1);
#if NET
            Assert.False(equals2);
#else
            Assert.True(equals2);
#endif
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            var options2 = new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version11,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
#endif
            };

            var equals = ProxyHttpRequestOptions.Equals(null, options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            var options1 = new ProxyHttpRequestOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(60),
                Version = HttpVersion.Version11,
#if NET
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
#endif
            };

            var equals = ProxyHttpRequestOptions.Equals(options1, null);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            var equals = ProxyHttpClientOptions.Equals(null, null);

            Assert.True(equals);
        }
    }
}
