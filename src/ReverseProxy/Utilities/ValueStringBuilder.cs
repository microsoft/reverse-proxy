using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Yarp.ReverseProxy.Utilities
{
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
            if (s.Length == 1 && (uint)pos < (uint)RawChars.Length) // very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
            {
                RawChars[pos] = s[0];
                _pos = pos + 1;
            }
            else
            {
                AppendSlow(s);
            }
        }

        private void AppendSlow(string s)
        {
            var pos = _pos;
            if (pos > RawChars.Length - s.Length)
            {
                Grow(s.Length);
            }

            s.AsSpan().CopyTo(RawChars.Slice(pos));
            _pos += s.Length;
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
