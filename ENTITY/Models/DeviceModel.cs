using System.Collections.Generic;

namespace ENTITY.Models
{
    public class DeviceModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DeviceType { get; set; }
        public string Protocol { get; set; }
        public bool IsSimulated { get; set; }
        public string Esp32ClientId { get; set; }
        public int ProjectId { get; set; }
        public List<TagData> Tags { get; } = new List<TagData>();
    }
}
