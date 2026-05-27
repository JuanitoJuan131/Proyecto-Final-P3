using Newtonsoft.Json;
using System.Collections.Generic;

namespace ENTITY.Models
{
    public class DashboardModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ProjectId { get; set; }
        public string LayoutJson { get; set; }
        public List<WidgetModel> Widgets { get; } = new List<WidgetModel>();

        public LayoutConfig Layout
        {
            get { return JsonConvert.DeserializeObject<LayoutConfig>(LayoutJson ?? "{}") ?? new LayoutConfig(); }
        }
    }

    public class LayoutConfig
    {
        public int Columns { get; set; } = 12;
        public int Rows { get; set; } = 8;
        public int CellSize { get; set; } = 88;
    }
}
