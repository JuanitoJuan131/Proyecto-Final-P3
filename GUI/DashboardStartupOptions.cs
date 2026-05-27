namespace GUI
{
    public class DashboardStartupOptions
    {
        public DashboardStartupOptions()
        {
            ProjectName = "Linea de jugos naturales";
            DashboardName = "Monitoreo de motores";
            TemplateIndex = 0;
        }

        public string ProjectName { get; set; }
        public string DashboardName { get; set; }
        public int TemplateIndex { get; set; }
    }
}
