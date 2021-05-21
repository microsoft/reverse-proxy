// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Abstractions
{
    // Mirrors CookieBuilder and CookieOptions
    // https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Abstractions/src/CookieBuilder.cs
    /// <summary>
    /// Config for session affinity cookies.
    /// </summary>
    public sealed record SessionAffinityCookieConfig
    {
        /// <summary>
        /// The cookie path.
        /// </summary>
        public string? Path { get; init; }

        /// <summary>
        /// The domain to associate the cookie with.
        /// </summary>
        public string? Domain { get; init; }

        /// <summary>
        /// Indicates whether a cookie is accessible by client-side script.
        /// </summary>
        /// <remarks>Defaults to <see cref="true"/>.</remarks>
        public bool? HttpOnly { get; init; }

        /// <summary>
        /// The policy that will be used to determine <see cref="CookieOptions.Secure"/>.
        /// </summary>
        /// <remarks>Defaults to <see cref="CookieSecurePolicy.None"/>.</remarks>
        public CookieSecurePolicy? SecurePolicy { get; init; }

        /// <summary>
        /// The SameSite attribute of the cookie.
        /// </summary>
        /// <remarks>Defaults to <see cref="SameSiteMode.Unspecified"/>.</remarks>
        public SameSiteMode? SameSite { get; init; }

        /// <summary>
        /// Gets or sets the lifespan of a cookie.
        /// </summary>
        public TimeSpan? Expiration { get; init; }

        /// <summary>
        /// Gets or sets the max-age for the cookie.
        /// </summary>
        public TimeSpan? MaxAge { get; init; }

        /// <summary>
        /// Indicates if this cookie is essential for the application to function correctly. If true then
        /// consent policy checks may be bypassed.
        /// </summary>
        /// <remarks>Defaults to <see cref="false"/>.</remarks>
        public bool? IsEssential { get; init; }

        public bool Equals(SessionAffinityCookieConfig? other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(Path, other.Path, StringComparison.Ordinal)
                && string.Equals(Domain, other.Domain, StringComparison.OrdinalIgnoreCase)
                && HttpOnly == other.HttpOnly
                && SecurePolicy == other.SecurePolicy
                && SameSite == other.SameSite
                && Expiration == other.Expiration
                && MaxAge == other.MaxAge
                && IsEssential == other.IsEssential;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Path?.GetHashCode(StringComparison.Ordinal),
                Domain?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                HttpOnly,
                SecurePolicy,
                SameSite,
                Expiration,
                MaxAge,
                IsEssential);
        }
    }
}
