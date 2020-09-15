// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Utilities
{
    public class CaseInsensitiveEqualHelperTests
    {
        [Fact]
        public void Equals_Same_Instance_Returns_True()
        {
            // Arrange
            var list1 = new string[] { "item1", "item2" };

            // Act
            var equals = CaseInsensitiveEqualHelper.Equals(list1, list1);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_Empty_List_Returns_True()
        {
            // Arrange
            var list1 = new string[] { };

            var list2 = new string[] { };

            // Act
            var equals = CaseInsensitiveEqualHelper.Equals(list1, list2);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_List_Same_Value_Returns_True()
        {
            // Arrange
            var list1 = new string[] { "item1", "item2" };

            var list2 = new string[] { "item1", "item2" };

            // Act
            var equals = CaseInsensitiveEqualHelper.Equals(list1, list2);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_List_Different_Value_Returns_False()
        {
            // Arrange
            var list1 = new string[] { "item1", "item2" };

            var list2 = new string[] { "item3", "item4" };

            // Act
            var equals = CaseInsensitiveEqualHelper.Equals(list1, list2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_List_Null_Returns_False()
        {
            // Arrange
            var list2 = new string[] { "item1", "item2" };

            // Act
            var equals = CaseInsensitiveEqualHelper.Equals(null, list2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_List_Null_Returns_False()
        {
            // Arrange
            var list1 = new string[] { "item1", "item2" };

            // Act
            var equals = CaseInsensitiveEqualHelper.Equals(list1, null);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Null_List_Returns_True()
        {
            // Arrange

            // Act
            var equals = CaseInsensitiveEqualHelper.Equals(list1: null, list2: null);

            // Assert
            Assert.True(equals);
        }
    }
}
