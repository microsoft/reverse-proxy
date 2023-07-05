using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Weighting;
public class CompoundedDestinationWeight : IDestinationWeight
{
    private double[]? _weights;

    //bug: do we need to lock to return this?
    public double RelativeWeight { get; private set; } = 1.0;

    internal void SetWeight(int index, double weight)
    {
        lock(this)
        {
            _weights[index] = weight;
            if (_weights != null)
            {
                foreach (var w in _weights) { weight *= w; }
            }
            RelativeWeight = weight;
        }
    }

    internal void ExtendValues(int newSize)
    {
        lock (this)
        {
            int oldSize = 0;
            if (_weights != null)
            {
                oldSize = _weights.Length;
                Array.Resize(ref _weights, newSize);
            }
            else
            {
                _weights = new double[newSize];
            }
            for (var i = oldSize;  i < newSize; i++)
            {
                _weights[i] = 1.0;
            }
        }
    }

    public void SetWeight(object key, double weight)
    {
        throw new NotImplementedException();
    }
}
