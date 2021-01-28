using System;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestFuncTransform : RequestTransform
    {
        private readonly Func<RequestTransformContext, Task> _func;

        public RequestFuncTransform(Func<RequestTransformContext, Task> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        public override Task ApplyAsync(RequestTransformContext context)
        {
            return _func(context);
        }
    }
}
