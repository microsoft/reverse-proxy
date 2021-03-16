// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Kubernetes.ResourceKinds;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Kubernetes.Resources
{
    public class ResourcePatcher : IResourcePatcher
    {
        private readonly IEqualityComparer<JToken> _tokenEqualityComparer = new JTokenEqualityComparer();

        public JsonPatchDocument CreateJsonPatch(CreatePatchParameters parameters)
        {
            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var context = new Context(parameters);

            var patch = AccumulatePatch(new JsonPatchDocument(), context);

            return patch;
        }

        private JsonPatchDocument AccumulatePatch(JsonPatchDocument patch, Context context)
        {
            return context.Element.MergeStrategy switch
            {
                ElementMergeStrategy.Unknown => MergeApplyAny(patch, context),
                ElementMergeStrategy.ReplacePrimative => ReplacePrimative(patch, context),

                ElementMergeStrategy.MergeObject => MergeObject(patch, context),
                ElementMergeStrategy.MergeMap => MergeMap(patch, context),

                ElementMergeStrategy.MergeListOfPrimative => MergeListOfPrimative(patch, context),
                ElementMergeStrategy.ReplaceListOfPrimative => ReplaceListOfPrimative(patch, context),
                ElementMergeStrategy.MergeListOfObject => MergeListOfObject(patch, context),
                ElementMergeStrategy.ReplaceListOfObject => ReplaceListOfObject(patch, context),

                _ => throw new Exception("Unhandled merge strategy"),
            };
        }

        private JsonPatchDocument MergeApplyAny(JsonPatchDocument patch, Context context)
        {
            if (context.ApplyToken is JObject)
            {
                return MergeObject(patch, context);
            }
            else if (context.ApplyToken is JArray)
            {
                return ReplaceListOfObjectOrPrimative(patch, context);
            }
            else if (context.ApplyToken is JValue)
            {
                return ReplacePrimative(patch, context);
            }
            else if (context.ApplyToken == null && context.LastAppliedToken != null)
            {
                return patch.Remove(context.Path);
            }
            else
            {
                throw NewFormatException(context);
            }
        }

        private static FormatException NewFormatException(Context context)
        {
            return new FormatException($"{context.Kind.Kind}.{context.Kind.ApiVersion} {context.Path} type {context.ApplyToken?.Type} is incorrect for {context.Element?.MergeStrategy}");
        }

        private JsonPatchDocument ReplacePrimative(JsonPatchDocument patch, Context context)
        {
            if (context.ApplyToken is JValue apply)
            {
                if (context.LiveToken is JValue live &&
                    _tokenEqualityComparer.Equals(apply, live))
                {
                    // live value is correct
                }
                else
                {
                    // live value is different, or live is not a primative value
                    patch = patch.Replace(context.Path, apply);
                }
            }
            else
            {
                throw NewFormatException(context);
            }

            return patch;
        }

        private JsonPatchDocument MergeObject(JsonPatchDocument patch, Context context)
        {
            var apply = (JObject)context.ApplyToken;
            var lastApplied = context.LastAppliedToken as JObject;
            var live = context.LiveToken as JObject;

            if (live == null)
            {
                return patch.Replace(context.Path, apply);
            }

            foreach (var applyProperty in apply.Properties())
            {
                var name = applyProperty.Name;
                var path = $"{context.Path}/{EscapePath(name)}";

                var liveProperty = live.Property(name, StringComparison.Ordinal);

                if (liveProperty == null)
                {
                    patch = patch.Add(path, applyProperty.Value);
                }
                else
                {
                    var lastAppliedProperty = lastApplied?.Property(name, StringComparison.Ordinal);

                    var nested = context.Push(
                        path,
                        context.Element.GetPropertyElementType(name),
                        applyProperty.Value,
                        lastAppliedProperty?.Value,
                        liveProperty.Value);

                    patch = AccumulatePatch(patch, nested);
                }
            }

            foreach (var liveProperty in live.Properties())
            {
                var name = liveProperty.Name;
                var applyProperty = apply.Property(name, StringComparison.Ordinal);

                if (applyProperty == null)
                {
                    var lastAppliedProperty = lastApplied?.Property(name, StringComparison.Ordinal);
                    if (lastAppliedProperty != null)
                    {
                        var path = $"{context.Path}/{EscapePath(name)}";
                        patch = patch.Remove(path);
                    }
                }
            }

            return patch;
        }

        private JsonPatchDocument MergeMap(JsonPatchDocument patch, Context context)
        {
            var apply = (JObject)context.ApplyToken;
            var lastApplied = context.LastAppliedToken as JObject;
            var live = context.LiveToken as JObject;

            if (live == null)
            {
                return patch.Replace(context.Path, apply);
            }

            var collectionElement = context.Element.GetCollectionElementType();

            foreach (var applyProperty in apply.Properties())
            {
                var key = applyProperty.Name;
                var path = $"{context.Path}/{EscapePath(key)}";

                var liveProperty = live.Property(key, StringComparison.Ordinal);

                if (liveProperty == null)
                {
                    patch = patch.Add(path, applyProperty.Value);
                }
                else
                {
                    var lastAppliedProperty = lastApplied?.Property(key, StringComparison.Ordinal);

                    var propertyContext = context.Push(
                        path,
                        collectionElement,
                        applyProperty.Value,
                        lastAppliedProperty?.Value,
                        liveProperty.Value);

                    patch = AccumulatePatch(patch, propertyContext);
                }
            }

            foreach (var liveProperty in live.Properties())
            {
                var name = liveProperty.Name;
                var applyProperty = apply.Property(name, StringComparison.Ordinal);

                if (applyProperty == null)
                {
                    var lastAppliedProperty = lastApplied?.Property(name, StringComparison.Ordinal);
                    if (lastAppliedProperty != null)
                    {
                        var path = $"{context.Path}/{EscapePath(name)}";
                        patch = patch.Remove(path);
                    }
                }
            }

            return patch;
        }

        private JsonPatchDocument MergeListOfPrimative(JsonPatchDocument patch, Context context)
        {
            if (!(context.ApplyToken is JArray applyArray))
            {
                throw NewFormatException(context);
            }

            if (!(context.LiveToken is JArray liveArray))
            {
                // live is not an array, so replace it
                return patch.Replace(context.Path, applyArray);
            }

            List<JToken> lastAppliedList;
            if (context.LastAppliedToken is JArray lastAppliedArray)
            {
                lastAppliedList = lastAppliedArray.ToList();
            }
            else
            {
                lastAppliedList = new List<JToken>();
            }

            var applyEnumerator = applyArray.GetEnumerator();
            var applyIndex = 0;
            var applyAvailable = applyIndex < applyArray.Count;
            var applyValue = applyAvailable ? applyArray[applyIndex] : null;

            var liveIndex = 0;
            foreach (var liveValue in liveArray)
            {
                // match live value to remaining last applied values
                var lastAppliedIndex = lastAppliedList.FindIndex(lastAppliedValue => _tokenEqualityComparer.Equals(lastAppliedValue, liveValue));
                var wasLastApplied = lastAppliedIndex != -1;
                if (wasLastApplied)
                {
                    // remove from last applied list to preserve the number of live values that are accounted for
                    lastAppliedList.RemoveAt(lastAppliedIndex);
                }

                if (applyAvailable && _tokenEqualityComparer.Equals(applyValue, liveValue))
                {
                    // next live value matches next apply value in order, take no action and advance
                    liveIndex++;
                    applyIndex++;
                    applyAvailable = applyIndex < applyArray.Count;
                    applyValue = applyAvailable ? applyArray[applyIndex] : null;
                }
                else if (wasLastApplied)
                {
                    // next live value matches last applied, but is either removed or does not match next apply value
                    patch = patch.Remove($"{context.Path}/{liveIndex}");
                }
                else
                {
                    // next live value is not controlled by last applied, so take no action and advance live
                    liveIndex++;
                }
            }

            var path = $"{context.Path}/-";
            while (applyAvailable)
            {
                // remaining apply values are appended
                patch = patch.Add(path, applyValue);

                applyIndex++;
                applyAvailable = applyIndex < applyArray.Count;
                applyValue = applyAvailable ? applyArray[applyIndex] : null;
            }

            return patch;
        }

        private JsonPatchDocument ReplaceListOfPrimative(JsonPatchDocument patch, Context context)
        {
            return ReplaceListOfObjectOrPrimative(patch, context);
        }

        private JsonPatchDocument MergeListOfObject(JsonPatchDocument patch, Context context)
        {
            if (!(context.ApplyToken is JArray apply))
            {
                throw NewFormatException(context);
            }

            if (!(context.LiveToken is JArray live))
            {
                // live is not an array, so replace it
                return patch.Replace(context.Path, apply);
            }

            var applyItems = apply.Select((item, index) => (name: item[context.Element.MergeKey]?.Value<string>(), index, item)).ToArray();
            var liveItems = live.Select((item, index) => (name: item[context.Element.MergeKey]?.Value<string>(), index, item)).ToArray();
            var lastAppliedItems = context.LastAppliedToken?.Select((item, index) => (name: item[context.Element.MergeKey]?.Value<string>(), index, item))?.ToArray() ?? Array.Empty<(string name, int index, JToken item)>();

            var element = context.Element.GetCollectionElementType();

            foreach (var (name, _, applyToken) in applyItems)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("Merge key is required on object");
                }

                var (_, index, liveToken) = liveItems.SingleOrDefault(item => item.name == name);
                var (_, _, lastAppliedToken) = lastAppliedItems.SingleOrDefault(item => item.name == name);

                if (liveToken != null)
                {
                    var itemContext = context.Push(
                        path: $"{context.Path}/{index}",
                        element: element,
                        apply: applyToken,
                        lastApplied: lastAppliedToken,
                        live: liveToken);

                    patch = AccumulatePatch(patch, itemContext);
                }
                else
                {
                    patch = patch.Add($"{context.Path}/-", applyToken);
                }
            }

            foreach (var (name, _, lastApplyToken) in lastAppliedItems)
            {
                var (_, index, liveToken) = liveItems.SingleOrDefault(item => item.name == name);
                var (_, _, applyToken) = applyItems.SingleOrDefault(item => item.name == name);

                if (applyToken == null && liveToken != null)
                {
                    patch = patch.Remove($"{context.Path}/{index}");
                }
            }

            return patch;
        }

        private JsonPatchDocument ReplaceListOfObject(JsonPatchDocument patch, Context context)
        {
            return ReplaceListOfObjectOrPrimative(patch, context);
        }

        private JsonPatchDocument ReplaceListOfObjectOrPrimative(JsonPatchDocument patch, Context context)
        {
            if (context.ApplyToken is JArray apply)
            {
                if (context.LiveToken is JArray live &&
                    _tokenEqualityComparer.Equals(apply, live))
                {
                    // live is correct
                }
                else
                {
                    // live array has any differences, or live is not an array
                    patch = patch.Replace(context.Path, context.ApplyToken);
                }
            }
            else
            {
                throw NewFormatException(context);
            }

            return patch;
        }

        private static string EscapePath(string name)
        {
            return name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        }

        private struct Context
        {
            public Context(CreatePatchParameters context)
            {
                Path = string.Empty;
                Kind = context.ResourceKind ?? DefaultResourceKind.Unknown;
                Element = context.ResourceKind?.Schema ?? DefaultResourceKindElement.Unknown;
                ApplyToken = (JToken)context.ApplyResource;
                LastAppliedToken = (JToken)context.LastAppliedResource;
                LiveToken = (JToken)context.LiveResource;
            }

            public Context(string path, IResourceKind kind, IResourceKindElement element, JToken apply, JToken lastApplied, JToken live) : this()
            {
                Path = path;
                Kind = kind;
                Element = element;
                ApplyToken = apply;
                LastAppliedToken = lastApplied;
                LiveToken = live;
            }

            public string Path { get; }

            public IResourceKind Kind { get; }

            public IResourceKindElement Element { get; }

            public JToken ApplyToken { get; }

            public JToken LastAppliedToken { get; }

            public JToken LiveToken { get; }

            public Context Push(string path, IResourceKindElement element, JToken apply, JToken lastApplied, JToken live)
            {
                return new Context(path, Kind, element, apply, lastApplied, live);
            }
        }
    }
}
