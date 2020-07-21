using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Sample
{
    public class ProxyEventListener : EventListener
    {
        public static ProxyEventListener Listener = new ProxyEventListener();

        // 'Well Known' GUID for the TPL event source
        private static Guid TPLSOURCEGUID = new Guid("2e5dba47-a3d2-4d16-8ee0-6671ffdcd7b5");

        public void CollectData()
        {
            EventSourceCreated += ProxyEventListener_EventSourceCreated;
            EventWritten += ProxyEventListener_EventWritten;
        }

        private void ProxyEventListener_EventSourceCreated(object sender, EventSourceCreatedEventArgs e)
        {
            if (e.EventSource.Guid == TPLSOURCEGUID)
            {
                // To get activity tracking, a specific keyword needs to be set before hand
                // This is done automatically by perfview, but needs to be manually done by other event listeners
                EnableEvents(e.EventSource, EventLevel.Informational, (EventKeywords)0x80);
            }
            else
            {
                switch (e.EventSource.Name)
                {
                    case "Microsoft-System-Net-Http":
                    case "Microsoft.AspNetCore.Hosting":
                    case "Microsoft-Extensions-Logging":
                    case "Microsoft-System-Net-NameResolution":
                        EnableEvents(e.EventSource, EventLevel.Informational);
                        break;

                    default:
                        Console.WriteLine($"new EventSource: {e.EventSource.Name}");
                        break;
                }
            }
        }

        private void ProxyEventListener_EventWritten(object sender, EventWrittenEventArgs e)
        {
            if (e.EventName != "Message" && e.EventName != "FormattedMessage")
            {
                ActivityLog.ProcessEvent(e);
            }
        }
    }

    internal class ActivityLog
    {
        private static ConcurrentDictionary<Guid, ActivityLog> activeTasks = new ConcurrentDictionary<Guid, ActivityLog>();
        public static List<ActivityLog> CompletedRootTasks = new List<ActivityLog>();

        public string Name;
        public DateTime startTime;
        public DateTime endTime;
        public TimeSpan duration;
        public List<string> messages = new List<string>();
        public ConcurrentBag<ActivityLog> children;
        public ActivityLog parent = null;

        public string toJSON()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"\t\"Name\": \"{Name}\", ");
            sb.AppendLine($"\t\"Duration\": {duration.Ticks}, ");
            sb.AppendLine($"\t\"Messages\": [\n{string.Join(",\n", messages)}\n], ");
            sb.AppendLine("\t\"Children\": [");
            if (children != null)
            {
                var first = true;
                foreach (var child in children)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        sb.Append(",");
                    }
                    sb.Append("\t\t" + child.toJSON());
                }
            }
            sb.AppendLine("]}");
            return sb.ToString();
        }

        public static void ProcessEvent(EventWrittenEventArgs e)
        {
            ActivityLog al;
            if (!activeTasks.TryGetValue(e.ActivityId, out al))
            {
                al = new ActivityLog();
                activeTasks.TryAdd(e.ActivityId, al);
                if (e.RelatedActivityId != Guid.Empty)
                {
                    ActivityLog parent;
                    if (activeTasks.TryGetValue(e.RelatedActivityId, out parent))
                    {
                        parent.AddChild(al);
                    }
                    else
                    {
                        Console.WriteLine($"{e.RelatedActivityId} cannot be found in list");
                    }
                }
            }
            al.ProcessEventDetails(e);
        }

        private void ProcessEventDetails(EventWrittenEventArgs e)
        {
            if (e.EventName.EndsWith("Start"))
            {
                startTime = e.TimeStamp;
                Name = e.EventName;
            }
            else if (e.EventName.EndsWith("Stop"))
            {
                endTime = e.TimeStamp;
                duration = endTime - startTime;
                ActivityLog entry;
                activeTasks.TryRemove(e.ActivityId, out entry);
                if (parent == null)
                {
                    Console.WriteLine(this.toJSON());
                }
            }
            messages.Add(e.ToJson());

        }

        private void AddChild(ActivityLog child)
        {
            if (children == null)
            {
                children = new ConcurrentBag<ActivityLog>();
            }
            children.Add(child);
            child.parent = this;
        }
    }

    internal static class EventExtensions
    {
        public static string ToJson(this EventWrittenEventArgs eventWritten)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"\t\"Source\": \"{eventWritten.EventSource.Name}\", ");
            sb.AppendLine($"\t\"Event\": \"{eventWritten.EventName}\", ");
            sb.AppendLine($"\t\"Message\": \"{JsonEncodedText.Encode(eventWritten.Message ?? "")}\", ");
            sb.AppendLine($"\t\"Level\": \"{eventWritten.Level}\", ");
            sb.AppendLine($"\t\"Ticks\": {eventWritten.TimeStamp.Ticks}, ");
            if (eventWritten.ActivityId != Guid.Empty) { sb.AppendLine($"\t\"ActivityId\": \"{eventWritten.ActivityId}\", "); }
            if (eventWritten.RelatedActivityId != Guid.Empty) { sb.AppendLine($"\t\"ParentId\": \"{eventWritten.RelatedActivityId}\", "); }

            sb.AppendLine("\t\"Payload\": {");

            for (var i = 0; i < eventWritten.PayloadNames.Count; i++)
            {
                if (i > 0) { sb.AppendLine(","); }
                sb.AppendLine($"\t\t\"{eventWritten.PayloadNames[i]}\": \"{JsonEncodedText.Encode(eventWritten.Payload[i].ToString() ?? "")}\"");
            }
            sb.AppendLine("\t}");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
