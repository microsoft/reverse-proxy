// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.ReverseProxy.Utilities.Tests
{
    public class ReferenceEqualityComparerTests
    {
        [Fact]
        public void Equals_SameObject_ReturnsTrue()
        {
            var obj = new object();

            var equals = ReferenceEqualityComparer<object>.Default.Equals(obj, obj);

            Assert.True(equals);
        }

        [Fact]
        public void Equals_Nulls_ReturnsTrue()
        {
            var equals = ReferenceEqualityComparer<object>.Default.Equals(null, null);

            Assert.True(equals);
        }

        [Fact]
        public void Equals_LeftNull_ReturnsFalse()
        {
            var item = new object();

            var equals = ReferenceEqualityComparer<object>.Default.Equals(null, item);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_RightNull_ReturnsFalse()
        {
            var item = new object();

            var equals = ReferenceEqualityComparer<object>.Default.Equals(item, null);

            Assert.False(equals);
        }

        [Fact]
        public void GetHashCode_Objects_Works()
        {
            var items = Enumerable.Range(0, 100).Select(i => new object()).ToList();

            var codes1 = items.Select(item => ReferenceEqualityComparer<object>.Default.GetHashCode(item)).ToList();
            var codes2 = items.Select(item => ReferenceEqualityComparer<object>.Default.GetHashCode(item)).ToList();

            Assert.Equal(codes2, codes1);

            // Producing the same hash code for lots of different objects
            // is technically possible, but extremely unlikely.
            Assert.Contains(codes1, code => code != codes1[0]);
        }

        [Fact]
        public void GetHashCode_Null_Works()
        {
            var code = ReferenceEqualityComparer<object>.Default.GetHashCode(null);

            Assert.Equal(0, code);
        }

        [Fact]
        public void EntToEnd_WithoutCustomComparer()
        {
            var dict = new HashSet<EverythingEquals>();
            var item1 = new EverythingEquals();
            var item2 = new EverythingEquals();

            dict.Add(item1);
            var added = dict.Add(item2);

            Assert.False(added, $"since {nameof(EverythingEquals)} implements IEquatable<>, that implementation is used by default");
        }

        [Fact]
        public void EntToEnd_WithCustomComparer()
        {
            var dict = new HashSet<EverythingEquals>(ReferenceEqualityComparer<EverythingEquals>.Default);
            var item1 = new EverythingEquals();
            var item2 = new EverythingEquals();

            dict.Add(item1);
            var added = dict.Add(item2);

            Assert.True(added);
        }

        private class EverythingEquals : IEquatable<EverythingEquals>
        {
            public bool Equals(EverythingEquals other)
            {
                // All instances of this class are considered equal to anything else.
                return true;
            }

            public override int GetHashCode()
            {
                // All instances of this class are considered equal,
                // hence they all produce the same hash code.
                return 0;
            }
        }
    }
}
