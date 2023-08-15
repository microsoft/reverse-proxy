using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Weighting;
internal static class WeightUtils
{
    // Gets a random destination from the available destinations based on the relative weights
    // Uses a binary search to find the destination

    internal static DestinationState getRandomWeightedDestination(IReadOnlyList<DestinationState> availableDestinations, IRandomFactory randomFactory)
    {
        var totalWeight = 0.0;

        // Store a weight point for each destination in an array which represents the sum of weights at that destination
        Span<double> weightPoints = stackalloc double[availableDestinations.Count];
        for (var i = 0; i < availableDestinations.Count; i++)
        {
            var w = availableDestinations[i].Weight?.RelativeWeight ?? 1.0;
            totalWeight += w;
            weightPoints[i] = totalWeight;
        }

        // Get a random value between 0 and the total weight
        var random = randomFactory.CreateRandomInstance();
        var randomWeight = random.NextDouble() * totalWeight;
        var targetIndex = findWeightedIndex(weightPoints, randomWeight);

        return availableDestinations[targetIndex];
    }

    internal static DestinationState getRandomWeightedDestinationWithSkip(IReadOnlyList<DestinationState> availableDestinations, DestinationState skipDestination, IRandomFactory randomFactory)
    {
        var totalWeight = 0.0;
        var i = 0;
        var skipIndex = 0;

        // Store a weight point for each destination in an array which represents the sum of weights at that destination
        Span<double> weightPoints = stackalloc double[availableDestinations.Count - 1];

        foreach (var destination in availableDestinations)
        {
            if (destination != skipDestination)
            {
                var w = destination.Weight?.RelativeWeight ?? 1.0;
                totalWeight += w;
                weightPoints[i] = totalWeight;
                i++;
            }
            else
            {
                skipIndex = i;
            }
        }

        // Get a random value between 0 and the total weight
        var random = randomFactory.CreateRandomInstance();
        var randomWeight = random.NextDouble() * totalWeight;
        var targetIndex = findWeightedIndex(weightPoints, randomWeight);

        if (targetIndex >= skipIndex)
        {
            targetIndex++;
        }

        return availableDestinations[targetIndex];
    }

    internal static int findWeightedIndex(Span<double> weights, double findValue)
    {
        var targetIndex = 0;
        var startIndex = 0;
        var endIndex = weights.Length - 1;
        while (true)
        {
            targetIndex = (startIndex + endIndex) / 2;
            var currentWeight = weights[targetIndex];
            var previousWeight = targetIndex > 0 ? weights[targetIndex - 1] : 0;
            if (findValue >= previousWeight && findValue < currentWeight)
            {
                // Found the destination
                break;
            }
            if (findValue < previousWeight)
            {
                // Search the left half
                endIndex = targetIndex - 1;
            }
            else
            {
                // Search the right half
                startIndex = targetIndex + 1;
            }
        }
        return targetIndex;
    }

}
