using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Reference https://github.com/cdanek/KaimiraWeightedList/blob/main/WeightedList.cs
/// </summary>
public sealed class WeightedList<T> : IEnumerable<T>
{

    public int TotalWeight => _totalWeight;
    public int MinWeight => _minWeight;
    public int MaxWeight => _maxWeight;
    public T this[int index] => _list[index];
    public int Count => _list.Count;
    private readonly List<T> _list = [];
    private readonly List<int> _weights = [];
    private readonly List<int> _probabilities = [];
    private readonly List<int> _alias = [];
    private readonly Random _rand = new();
    private int _totalWeight;
    private bool _areAllProbabilitiesIdentical = false;
    private int _minWeight;
    private int _maxWeight;

    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

    public void Add(T item, int weight)
    {
        _list.Add(item);
        _weights.Add(weight <= 0 ? 1 : weight);
        Recalculate();
    }

    public void Clear()
    {
        _list.Clear();
        _weights.Clear();
        Recalculate();
    }

    public T Next()
    {
        if (Count == 0)
        {
            return default;
        }

        var nextInt = _rand.Next(Count);
        if (_areAllProbabilitiesIdentical)
        {
            return _list[nextInt];
        }

        var nextProbability = _rand.Next(_totalWeight);
        return nextProbability < _probabilities[nextInt] ? _list[nextInt] : _list[_alias[nextInt]];
    }

    public void Contains(T item) => _list.Contains(item);
    public int IndexOf(T item) => _list.IndexOf(item);

    public void Insert(int index, T item, int weight)
    {
        _list.Insert(index, item);
        _weights.Insert(index, weight <= 0 ? 1 : weight);
        Recalculate();
    }

    public void Remove(T item)
    {
        var index = IndexOf(item);
        RemoveAt(index);
        Recalculate();
    }

    public void RemoveAt(int index)
    {
        _list.RemoveAt(index);
        _weights.RemoveAt(index);
        Recalculate();
    }


    private void Recalculate()
    {
        _totalWeight = 0;
        _areAllProbabilitiesIdentical = false;
        _minWeight = 0;
        _maxWeight = 0;
        var isFirst = true;

        _alias.Clear(); // STEP 1
        _probabilities.Clear(); // STEP 1

        var scaledProbabilityNumerator = new List<int>(Count);
        var small = new List<int>(Count); // STEP 2
        var large = new List<int>(Count); // STEP 2
        foreach (var weight in _weights)
        {
            if (isFirst)
            {
                _minWeight = _maxWeight = weight;
                isFirst = false;
            }

            _minWeight = weight < _minWeight ? weight : _minWeight;
            _maxWeight = _maxWeight < weight ? weight : _maxWeight;
            _totalWeight += weight;
            scaledProbabilityNumerator.Add(weight * Count); // STEP 3
            _alias.Add(0);
            _probabilities.Add(0);
        }

        // Degenerate case, all probabilities are equal.
        if (_minWeight == _maxWeight)
        {
            _areAllProbabilitiesIdentical = true;
            return;
        }

        // STEP 4
        for (var i = 0; i < Count; i++)
        {
            if (scaledProbabilityNumerator[i] < _totalWeight)
            {
                small.Add(i);
            }
            else
            {
                large.Add(i);
            }
        }

        // STEP 5
        while (small.Count > 0 && large.Count > 0)
        {
            var l = small[^1]; // 5.1
            small.RemoveAt(small.Count - 1);
            var g = large[^1]; // 5.2
            large.RemoveAt(large.Count - 1);
            _probabilities[l] = scaledProbabilityNumerator[l]; // 5.3
            _alias[l] = g; // 5.4
            var tmp = scaledProbabilityNumerator[g] + scaledProbabilityNumerator[l] -
                      _totalWeight; // 5.5, even though using ints for this algorithm is stable
            scaledProbabilityNumerator[g] = tmp;
            if (tmp < _totalWeight)
            {
                small.Add(g); // 5.6 the large is now in the small pile
            }
            else
            {
                large.Add(g); // 5.7 add the large back to the large pile
            }
        }

        // STEP 6
        while (large.Count > 0)
        {
            var g = large[^1]; // 6.1
            large.RemoveAt(large.Count - 1);
            _probabilities[g] = _totalWeight; //6.1
        }

        // STEP 7 - Can't happen for this implementation but left in source to match Keith Schwarz's algorithm
#pragma warning disable S125 // Sections of code should not be commented out
        //while (small.Count > 0)
        //{
        //    int l = small[^1]; // 7.1
        //    small.RemoveAt(small.Count - 1);
        //    _probabilities[l] = _totalWeight;
        //}
#pragma warning restore S125 // Sections of code should not be commented out
    }
}
