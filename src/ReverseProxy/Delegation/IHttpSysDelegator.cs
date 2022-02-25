#if NET6_0_OR_GREATER
namespace Yarp.ReverseProxy.Delegation;

public interface IHttpSysDelegator
{
    /// <summary>
    /// Disposes the handle to the given queue if it exists and tries to recreate it.
    /// </summary>
    /// <param name="queueName">The name of the queue to reset.</param>
    /// <param name="urlPrefix">The url prefix of the queue to reset.</param>
    void ResetQueue(string queueName, string urlPrefix);
}
#endif
