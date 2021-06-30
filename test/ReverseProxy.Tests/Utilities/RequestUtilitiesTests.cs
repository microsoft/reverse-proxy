// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Utilities
{
    public class RequestUtilitiesTests
    {
        [Theory]
        [InlineData("http://localhost", "", "", "http://localhost/")]
        [InlineData("http://localhost/", "", "", "http://localhost/")]
        [InlineData("http://localhost", "/", "", "http://localhost/")]
        [InlineData("http://localhost/", "/", "", "http://localhost/")]
        [InlineData("http://localhost", "", "?query", "http://localhost/?query")]
        [InlineData("http://localhost", "/path", "?query", "http://localhost/path?query")]
        [InlineData("http://localhost", "/path/", "?query", "http://localhost/path/?query")]
        [InlineData("http://localhost/", "/path", "?query", "http://localhost/path?query")]
        [InlineData("http://localhost/base", "", "", "http://localhost/base")]
        [InlineData("http://localhost/base", "", "?query", "http://localhost/base?query")]
        [InlineData("http://localhost/base", "/path", "?query", "http://localhost/base/path?query")]
        [InlineData("http://localhost/base/", "/path", "?query", "http://localhost/base/path?query")]
        [InlineData("http://localhost/base/", "/path/", "?query", "http://localhost/base/path/?query")]
        public void MakeDestinationAddress(string destinationPrefix, string path, string query, string expected)
        {
            var uri = RequestUtilities.MakeDestinationAddress(destinationPrefix, new PathString(path), new QueryString(query));
            Assert.Equal(expected, uri.AbsoluteUri);
        }
    }
}
