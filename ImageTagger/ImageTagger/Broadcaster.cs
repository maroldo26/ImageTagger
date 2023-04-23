using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageTagger
{
    public class Broadcaster
    {
        public const string PreviewEvent = "preview";

        private static readonly Dictionary<string, Action<object>> _subscribers = new Dictionary<string, Action<object>>();

        public static void Subscribe(string channel, Action<object> action)
        {
            if(_subscribers.TryGetValue(channel, out Action<object>? existingEvent))
            {
                existingEvent += action;
            }
            else
            {
                _subscribers.Add(channel, action);
            }
        }

        public static void Unsubscribe(string channel, Action<object> action)
        {
            if (_subscribers.TryGetValue(channel, out Action<object>? existingEvent))
            {
                existingEvent -= action;
            }
        }

        public static void RaiseEvent(string channel, object paramenter)
        {
            if (_subscribers.TryGetValue(channel, out Action<object>? existingEvent))
            {
                existingEvent(paramenter);
            }
        }
    }
}
