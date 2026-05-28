namespace GUI
{
    public class DashboardStartupOptions
    {
        public DashboardStartupOptions()
        {
            ProjectName = "Linea de jugos naturales";
            DashboardName = "Monitoreo de motores";
            MotorId = "MOTOR_01";
            MotorName = "Lavadora de frutas";
            TemplateIndex = 0;
        }

        public string ProjectName { get; set; }
        public string DashboardName { get; set; }
        public string MotorId { get; set; }
        public string MotorName { get; set; }
        public int TemplateIndex { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserRole { get; set; }
        public int ProjectId { get; set; }
        public int DashboardId { get; set; }
        public string LayoutJson { get; set; }
        public bool LoadFromLayout { get; set; }
    }

    public class MotorDashboardOption
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string DisplayName
        {
            get { return Id + " - " + Name; }
        }
    }
}
