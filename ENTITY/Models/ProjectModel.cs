using System;
using System.Collections.Generic;

namespace ENTITY.Models
{
    public class ProjectModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UserId { get; set; }
        public List<DeviceModel> Devices { get; } = new List<DeviceModel>();
        public List<DashboardModel> Dashboards { get; } = new List<DashboardModel>();
        public List<AutomationRule> Rules { get; } = new List<AutomationRule>();
    }
}
