// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Resources;
using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Kubernetes.Utils
{
    public static class TestYaml
    {
        private static readonly IResourceSerializers _serializers = new ResourceSerializers();

        public static T LoadFromEmbeddedStream<T>()
        {
            StackTrace stackTrace = new StackTrace();
            var frame = 1;
            var method = stackTrace.GetFrame(frame).GetMethod();
            var reflectedType = method.ReflectedType;
            while (true)
            {
                var assemblyName = reflectedType.Assembly.GetName().Name;

                if (assemblyName == "System.Private.CoreLib")
                {
                    frame += 1;
                    method = stackTrace.GetFrame(frame).GetMethod();
                    reflectedType = method.ReflectedType;
                }
                else
                {
                    break;
                }
            }

            var methodName = method.Name;
            if (methodName == "MoveNext" && reflectedType.Name.StartsWith("<", StringComparison.Ordinal))
            {
                methodName = reflectedType.Name[1..reflectedType.Name.IndexOf('>', StringComparison.Ordinal)];
                reflectedType = reflectedType.DeclaringType;
            }

            var manifestStream = reflectedType.Assembly.GetManifestResourceStream(reflectedType, $"{reflectedType.Name}.{methodName}.yaml");
            if (manifestStream == null)
            {
                throw new FileNotFoundException($"Could not find embedded stream {reflectedType.FullName}.{methodName}.yaml");
            }

            using var reader = new StreamReader(manifestStream);
            return _serializers.DeserializeYaml<T>(reader.ReadToEnd());
        }
    }
}
