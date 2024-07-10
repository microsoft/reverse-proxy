using System;

using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.Utilities;

internal interface ILazyServiceResolver<T>
{
    T? GetService();
}

internal abstract class LazyServiceResolver<T>
    : ILazyServiceResolver<T>
    where T : notnull
{
    protected readonly IServiceProvider ServiceProvider;
    private T? _value;
    private bool _resolved;

    public LazyServiceResolver(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public T? GetService()
    {
        if (_resolved)
        {
            return _value;
        }
        else
        {
            _resolved = true;
            return _value = Resolve();
        }
    }

    protected virtual T? Resolve()
    {
        return ServiceProvider.GetService<T>();
    }
}
