using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Weighting;
public class CompoundedDestinationWeight : IDestinationWeight
{
    private double[]? _weights;
    private int[]? _keys;

    //bug: do we need to lock to return this?
    public double RelativeWeight { get; private set; } = 1.0;

    internal void SetWeightInternal(int hashCode, double weight)
    {
        lock (this)
        {
            if (_keys is null)
            {
                _keys = new int[1];
                _weights = new double[1];

                _keys[0] = hashCode;
                _weights[0] = weight;
            }
            else if (!_keys.Contains(hashCode))
            {
                Array.Resize(ref _keys, _keys.Length + 1);
                Array.Resize(ref _weights, _weights.Length + 1);
                _keys[_keys.Length - 1] = hashCode;
                _weights[_weights.Length - 1] = weight;
            }
            else
            {
                var index = Array.IndexOf(_keys, hashCode);
                _weights[index] = weight;
            }

            var totalWeight = 1.0;
            foreach (var w in _weights) { totalWeight *= w; }

            RelativeWeight = totalWeight;
        }
    }

    internal double? GetWeightInternal(int hashCode)
    {
        if (_keys is not null && _keys.Contains(hashCode))
        {
            var index = Array.IndexOf(_keys, hashCode);
            return _weights[index];
        }
        return null;
    }

    public void SetWeight(object key, double weight)
    {
        var k = key as string;
        if (k is not null)
        {
            var hash = k.GetHashCode(StringComparison.OrdinalIgnoreCase);
            SetWeightInternal(hash, weight);
        }
        else
        {
            SetWeightInternal(key.GetHashCode(), weight);
        }
    }

    public double? GetWeight(object key)
    {
        var k = key as string;
        if (k is not null)
        {
            var hash = k.GetHashCode(StringComparison.OrdinalIgnoreCase);
            return GetWeightInternal(hash);
        }
        else
        {
            return GetWeightInternal(key.GetHashCode());
        }
    }
}
