// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Kubernetes.Controller.Queues
{
    /// <summary>
    /// Class Heap is a list adapter structure that can add data into the
    /// collection by <see cref="Push" /> in a way that can be removed in sorted
    /// order by <see cref="Pop" />.
    /// Package heap provides heap operations for any type that implements
    /// heap.Interface. A heap is a tree with the property that each node is the
    /// minimum-valued node in its subtree.
    /// The minimum element in the tree is the root, at index 0.
    /// A heap is a common way to implement a priority queue. To build a priority
    /// queue, implement the Heap interface with the (negative) priority as the
    /// ordering for the Less method, so Push adds items while Pop removes the
    /// highest-priority item from the queue. The Examples include such an
    /// implementation; the file example_pq_test.go has the complete source.
    /// </summary>
    /// <typeparam name="T">The type of item on the heap.</typeparam>
    public class Heap<T>
    {
        private readonly IList<T> _list;
        private readonly IComparer<T> _comparer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Heap{T}" /> class.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <param name="comparer">The comparer.</param>
        public Heap(IList<T> list, IComparer<T> comparer)
        {
            _list = list;
            _comparer = comparer;
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The count.</value>
        public int Count => _list.Count;

        /// <summary>
        /// Push pushes the element x onto the heap.
        /// The complexity is O(log n) where n = h.Len().
        /// </summary>
        /// <param name="item">The item.</param>
        public void Push(T item)
        {
            _list.Add(item);
            Up(_list.Count - 1);
        }

        /// <summary>
        /// Pop removes and returns the minimum element (according to Less) from the heap.
        /// The complexity is O(log n) where n = h.Len().
        /// Pop is equivalent to Remove(h, 0).
        /// </summary>
        /// <returns>The minimum item.</returns>
        public T Pop()
        {
            var n = _list.Count - 1;
            Swap(0, n);
            Down(0, n);
            var item = _list[_list.Count - 1];
            _list.RemoveAt(_list.Count - 1);
            return item;
        }

        /// <summary>
        /// Returns the minimum element without removing from the collection.
        /// </summary>
        /// <returns>The minimum item.</returns>
        public T Peek()
        {
            return _list[0];
        }

        /// <summary>
        /// Returns the minimum element if available without removing from the collection.
        /// </summary>
        /// <param name="item">The minimum item.</param>
        /// <returns><c>true</c> if item is available, <c>false</c> otherwise.</returns>
        public bool TryPeek(out T item)
        {
            if (Count > 0)
            {
                item = Peek();
                return true;
            }

            item = default;
            return false;
        }

        private void Swap(int i, int j)
        {
            (_list[i], _list[j]) = (_list[j], _list[i]);
        }

        private bool Less(int j, int i)
        {
            return _comparer.Compare(_list[j], _list[i]) < 0;
        }

        private void Up(int j)
        {
            while (true)
            {
                int i = (j - 1) / 2; // parent

                if (i == j || !Less(j, i))
                {
                    break;
                }

                Swap(i, j);
                j = i;
            }
        }

        private bool Down(int i0, int n)
        {
            int i = i0;

            while (true)
            {
                int j1 = 2 * i + 1;

                if (j1 >= n || j1 < 0)
                {
                    // j1 < 0 after int overflow
                    break;
                }

                int j = j1; // left child

                int j2 = j1 + 1;
                if (j2 < n && Less(j2, j1))
                {
                    j = j2; // = 2*i + 2  // right child
                }

                if (!Less(j, i))
                {
                    break;
                }

                Swap(i, j);
                i = j;
            }

            return i > i0;
        }
    }
}
