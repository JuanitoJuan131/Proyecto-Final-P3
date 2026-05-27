using System;
using System.Collections.Generic;

namespace BLL.Simulation
{
    public class TelemetryDispatcher
    {
        public event Action<string, object> TagUpdated;

        public void Dispatch(Dictionary<string, object> tags)
        {
            foreach (var tag in tags)
            {
                TagUpdated?.Invoke(tag.Key, tag.Value);
            }
        }
    }
}
