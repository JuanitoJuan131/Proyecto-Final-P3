using Newtonsoft.Json;
using System.Collections.Generic;

namespace ENTITY.Models
{
    public enum WidgetType
    {
        Gauge,
        Numeric,
        Tank,
        Trend,
        Led,
        Motor,
        AlarmPanel
    }

    public class WidgetModel
    {
        public int Id { get; set; }
        public WidgetType Type { get; set; }
        public string Title { get; set; }
        public string Tag { get; set; }
        public int Column { get; set; }
        public int Row { get; set; }
        public int Width { get; set; } = 2;
        public int Height { get; set; } = 2;
        public string ConfigJson { get; set; }

        public Dictionary<string, object> Config
        {
            get
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(ConfigJson ?? "{}")
                    ?? new Dictionary<string, object>();
            }
        }
    }
}
