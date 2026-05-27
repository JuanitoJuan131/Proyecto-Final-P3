using Oracle.ManagedDataAccess.Client;
using System.Configuration;

namespace DAL.Data
{
    public static class OracleConexion
    {
        public const string NombreCadena = "VisualLotOracle";

        public static OracleConnection CrearConexion()
        {
            return new OracleConnection(ObtenerCadenaConexion());
        }

        public static string ObtenerCadenaConexion()
        {
            var settings = ConfigurationManager.ConnectionStrings[NombreCadena];
            if (settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                return settings.ConnectionString;
            }

            return "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=orclpdb)));User Id=visual;Password=visual123;";
        }
    }
}
