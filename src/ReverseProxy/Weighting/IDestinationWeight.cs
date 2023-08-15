using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Weighting;
public interface IDestinationWeight
{
    double RelativeWeight { get; }
    void SetWeight(object key, double weight);
    double? GetWeight(object key);
}
