using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Weighting;
public class CompoundedWeightingProvider : IProxyWeightingProvider
{
    private List<int> _keys = new();

    public void SetDestinationWeight(DestinationState destination, object identifier, double weight)
    {
        var key = identifier.GetHashCode();
        int index = 0;
        if (!_keys.Contains(key))
        {
            _keys.Add(key);
            index = _keys.Count;
             
          //  UpdateDestinationsWeights(key);
        }
        else
        {
            index=_keys.IndexOf(key);
        }

        var dw = destination.Weight as CompoundedDestinationWeight;
        dw?.SetWeight(index, weight);

    }

    private void UpdateDestinationsWeights(int key)
    {
        throw new NotImplementedException();
    }

    public void SetDestinationWeights(DestinationState destination, IConfigurationSection configuration)
    {
        throw new NotImplementedException();
    }

    public void UpdateDestinationState(DestinationState destination)
    {
        var newWeights = destination.Model.Config.Weights;
        if (newWeights != null)
        {
            foreach (var kvp in newWeights)
            {
                SetDestinationWeight(destination, kvp.Key.ToLower(), kvp.Value);
            }
        }
    }
}
