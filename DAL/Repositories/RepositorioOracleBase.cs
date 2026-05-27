using DAL.Data;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;

namespace DAL.Repositories
{
    public abstract class RepositorioOracleBase
    {
        protected OracleConnection CrearConexion()
        {
            return OracleConexion.CrearConexion();
        }

        protected static OracleCommand CrearComando(OracleConnection conexion, string sql)
        {
            return new OracleCommand(sql, conexion) { BindByName = true };
        }

        protected static void AgregarParametro(OracleCommand comando, string nombre, OracleDbType tipo, object valor)
        {
            comando.Parameters.Add(nombre, tipo).Value = valor ?? DBNull.Value;
        }

        protected static void AgregarParametroBooleano(OracleCommand comando, string nombre, bool valor)
        {
            comando.Parameters.Add(nombre, OracleDbType.Int32).Value = valor ? 1 : 0;
        }

        protected static OracleParameter AgregarParametroIdSalida(OracleCommand comando, string nombre)
        {
            var parametro = comando.Parameters.Add(nombre, OracleDbType.Int32);
            parametro.Direction = ParameterDirection.Output;
            return parametro;
        }

        protected static int LeerIdSalida(OracleParameter parametro)
        {
            if (parametro.Value is OracleDecimal decimalOracle)
            {
                return decimalOracle.ToInt32();
            }

            return Convert.ToInt32(parametro.Value);
        }

        protected static int LeerEntero(OracleDataReader lector, string columna)
        {
            return Convert.ToInt32(lector[columna]);
        }

        protected static int? LeerEnteroNullable(OracleDataReader lector, string columna)
        {
            return lector[columna] == DBNull.Value ? (int?)null : Convert.ToInt32(lector[columna]);
        }

        protected static double LeerDecimal(OracleDataReader lector, string columna)
        {
            return Convert.ToDouble(lector[columna]);
        }

        protected static double? LeerDecimalNullable(OracleDataReader lector, string columna)
        {
            return lector[columna] == DBNull.Value ? (double?)null : Convert.ToDouble(lector[columna]);
        }

        protected static string LeerTexto(OracleDataReader lector, string columna)
        {
            return lector[columna] == DBNull.Value ? null : Convert.ToString(lector[columna]);
        }

        protected static bool LeerBooleano(OracleDataReader lector, string columna)
        {
            return Convert.ToInt32(lector[columna]) == 1;
        }

        protected static DateTime? LeerFechaNullable(OracleDataReader lector, string columna)
        {
            return lector[columna] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(lector[columna]);
        }

        protected List<T> Consultar<T>(string sql, Func<OracleDataReader, T> mapear, Action<OracleCommand> configurar = null)
        {
            var resultado = new List<T>();
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                configurar?.Invoke(comando);
                conexion.Open();
                using (var lector = comando.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        resultado.Add(mapear(lector));
                    }
                }
            }

            return resultado;
        }

        protected T ConsultarUno<T>(string sql, Func<OracleDataReader, T> mapear, Action<OracleCommand> configurar)
        {
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                configurar(comando);
                conexion.Open();
                using (var lector = comando.ExecuteReader())
                {
                    return lector.Read() ? mapear(lector) : default(T);
                }
            }
        }

        protected void Ejecutar(string sql, Action<OracleCommand> configurar)
        {
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                configurar(comando);
                conexion.Open();
                comando.ExecuteNonQuery();
            }
        }
    }
}
