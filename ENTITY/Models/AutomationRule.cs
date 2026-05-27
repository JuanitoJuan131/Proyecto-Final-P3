using System.Collections.Generic;

namespace ENTITY.Models
{
    public class AutomationRule
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
        public int ProjectId { get; set; }
        public List<RuleCondition> Conditions { get; } = new List<RuleCondition>();
        public List<RuleAction> Actions { get; } = new List<RuleAction>();
    }

    public class RuleCondition
    {
        public string Tag { get; set; }
        public string Operator { get; set; }
        public double Threshold { get; set; }
    }

    public class RuleAction
    {
        public string ActionType { get; set; }
        public string ParametersJson { get; set; }
    }
}
