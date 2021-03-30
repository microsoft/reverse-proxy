// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Yarp.ReverseProxy.Utilities
{
    //Copied from https://github.com/dotnet/runtime/blob/1ee59da9f6104c611b137c9d14add04becefdf14/src/libraries/Common/src/System/Text/ValueStringBuilder.cs
    internal ref partial struct ValueStringBuilder
    {
        private char[] _arrayToReturnToPool;
        private int _pos;

        public int Length
        {
            get => _pos;
            set {
                Debug.Assert(value >= 0);
                Debug.Assert(value <= RawChars.Length);
                _pos = value;
            }
        }

        public override string ToString()
        {
            var s = RawChars.Slice(0, _pos).ToString();
            Dispose();
            return s;
        }

        /// <summary>Returns the underlying storage of the builder.</summary>
        public Span<char> RawChars { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char c)
        {
            var pos = _pos;
            if ((uint)pos < (uint)RawChars.Length)
            {
                RawChars[pos] = c;
                _pos = pos + 1;
            }
            else
            {
                GrowAndAppend(c);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string s)
        {
            if (s == null)
            {
                return;
            }

            var pos = _pos;
            if (pos > RawChars.Length - s.Length)
            {
                Grow(s.Length);
            }

            s.AsSpan().CopyTo(RawChars.Slice(pos));
            _pos += s.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(int i)
        {
            var pos = _pos;
            if (i.TryFormat(RawChars.Slice(pos), out var charsWritten, default, null))
            {
                _pos = pos + charsWritten;
            }
            else
            {
                Append(i.ToString(CultureInfo.InvariantCulture));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowAndAppend(char c)
        {
            Grow(1);
            Append(c);
        }

        /// <summary>
        /// Resize the internal buffer either by doubling current buffer size or
        /// by adding <paramref name="additionalCapacityBeyondPos"/> to
        /// <see cref="_pos"/> whichever is greater.
        /// </summary>
        /// <param name="additionalCapacityBeyondPos">
        /// Number of chars requested beyond current position.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int additionalCapacityBeyondPos)
        {
            Debug.Assert(additionalCapacityBeyondPos > 0);
            Debug.Assert(_pos > RawChars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

            // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative
            var poolArray = ArrayPool<char>.Shared.Rent((int)Math.Max((uint)(_pos + additionalCapacityBeyondPos), (uint)RawChars.Length * 2));

            RawChars.Slice(0, _pos).CopyTo(poolArray);

            var toReturn = _arrayToReturnToPool;
            RawChars = _arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var toReturn = _arrayToReturnToPool;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }
    }
}
