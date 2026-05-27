using System;

namespace ENTITY.Models
{
    public class TagData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public int DeviceId { get; set; }
        public double CurrentValue { get; set; }
        public DateTime LastRead { get; set; }
    }
}
