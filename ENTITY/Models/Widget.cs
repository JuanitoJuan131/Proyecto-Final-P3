using Newtonsoft.Json;
using System.Collections.Generic;

namespace ENTITY.Models
{
    public enum TipoWidget
    {
        Medidor,
        Numerico,
        Tanque,
        Tendencia,
        Led,
        Motor,
        PanelAlarmas,
        BarraProgreso
    }

    public class Widget
    {
        public int IdWidget { get; set; }
        public TipoWidget TipoWidget { get; set; }
        public string Descripcion { get; set; }
        public double PosicionX { get; set; }
        public double PosicionY { get; set; }
        public int IdDashboard { get; set; }
        public int? IdTag { get; set; }
        public Dashboard Dashboard { get; set; }
        public TagDato Tag { get; set; }
        public string ParametrosJson { get; set; }

        public Dictionary<string, object> Config
        {
            get
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(ParametrosJson ?? "{}")
                    ?? new Dictionary<string, object>();
            }
        }
    }
}
