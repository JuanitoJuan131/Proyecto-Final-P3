using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class WidgetRepositorio : RepositorioOracleBase, IRepositorio<Widget>
    {
        public int Insertar(Widget entidad)
        {
            const string sql = @"INSERT INTO widgets (tipo_widget, descripcion, posicion_x, posicion_y, id_dashboard, id_tag, parametros_json)
                                 VALUES (:tipo_widget, :descripcion, :posicion_x, :posicion_y, :id_dashboard, :id_tag, :parametros_json)
                                 RETURNING id_widget INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdWidget = LeerIdSalida(id);
                return entidad.IdWidget;
            }
        }

        public void Actualizar(Widget entidad)
        {
            Ejecutar(@"UPDATE widgets
                      SET tipo_widget = :tipo_widget, descripcion = :descripcion, posicion_x = :posicion_x,
                          posicion_y = :posicion_y, id_dashboard = :id_dashboard, id_tag = :id_tag,
                          parametros_json = :parametros_json
                      WHERE id_widget = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdWidget);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM widgets WHERE id_widget = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Widget ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM widgets WHERE id_widget = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Widget> ObtenerTodos()
        {
            return Consultar("SELECT * FROM widgets ORDER BY id_widget", Mapear);
        }

        public List<Widget> ObtenerPorDashboard(int idDashboard)
        {
            return Consultar("SELECT * FROM widgets WHERE id_dashboard = :id_dashboard ORDER BY id_widget", Mapear,
                comando => AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, idDashboard));
        }

        public void ReemplazarPorDashboard(int idDashboard, IEnumerable<Widget> widgets)
        {
            try
            {
                ReemplazarPorDashboard(idDashboard, widgets, true);
            }
            catch (OracleException ex)
            {
                if (!IsParametrosJsonMissing(ex))
                {
                    throw;
                }

                ReemplazarPorDashboard(idDashboard, widgets, false);
            }
        }

        private void ReemplazarPorDashboard(int idDashboard, IEnumerable<Widget> widgets, bool incluirParametrosJson)
        {
            using (var conexion = CrearConexion())
            {
                conexion.Open();
                using (var transaccion = conexion.BeginTransaction())
                {
                    try
                    {
                        using (var borrar = CrearComando(conexion, "DELETE FROM widgets WHERE id_dashboard = :id_dashboard"))
                        {
                            borrar.Transaction = transaccion;
                            AgregarParametro(borrar, "id_dashboard", OracleDbType.Int32, idDashboard);
                            borrar.ExecuteNonQuery();
                        }

                        foreach (var widget in widgets)
                        {
                            widget.IdDashboard = idDashboard;
                            var sql = incluirParametrosJson
                                ? @"INSERT INTO widgets
                                    (tipo_widget, descripcion, posicion_x, posicion_y, id_dashboard, id_tag, parametros_json)
                                    VALUES (:tipo_widget, :descripcion, :posicion_x, :posicion_y, :id_dashboard, :id_tag, :parametros_json)
                                    RETURNING id_widget INTO :id"
                                : @"INSERT INTO widgets
                                    (tipo_widget, descripcion, posicion_x, posicion_y, id_dashboard, id_tag)
                                    VALUES (:tipo_widget, :descripcion, :posicion_x, :posicion_y, :id_dashboard, :id_tag)
                                    RETURNING id_widget INTO :id";

                            using (var insertar = CrearComando(conexion, sql))
                            {
                                insertar.Transaction = transaccion;
                                Parametros(insertar, widget, incluirParametrosJson);
                                var id = AgregarParametroIdSalida(insertar, "id");
                                insertar.ExecuteNonQuery();
                                widget.IdWidget = LeerIdSalida(id);
                            }
                        }

                        transaccion.Commit();
                    }
                    catch
                    {
                        transaccion.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void Parametros(OracleCommand comando, Widget entidad)
        {
            Parametros(comando, entidad, true);
        }

        private static void Parametros(OracleCommand comando, Widget entidad, bool incluirParametrosJson)
        {
            AgregarParametro(comando, "tipo_widget", OracleDbType.Varchar2, entidad.TipoWidget.ToString());
            AgregarParametro(comando, "descripcion", OracleDbType.Varchar2, entidad.Descripcion);
            AgregarParametro(comando, "posicion_x", OracleDbType.Decimal, entidad.PosicionX);
            AgregarParametro(comando, "posicion_y", OracleDbType.Decimal, entidad.PosicionY);
            AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, entidad.IdDashboard);
            AgregarParametro(comando, "id_tag", OracleDbType.Int32, entidad.IdTag.HasValue ? (object)entidad.IdTag.Value : DBNull.Value);
            if (incluirParametrosJson)
            {
                AgregarParametro(comando, "parametros_json", OracleDbType.Clob, entidad.ParametrosJson);
            }
        }

        private static Widget Mapear(OracleDataReader lector)
        {
            TipoWidget tipo;
            Enum.TryParse(LeerTexto(lector, "tipo_widget"), out tipo);
            return new Widget
            {
                IdWidget = LeerEntero(lector, "id_widget"),
                TipoWidget = tipo,
                Descripcion = LeerTexto(lector, "descripcion"),
                PosicionX = LeerDecimal(lector, "posicion_x"),
                PosicionY = LeerDecimal(lector, "posicion_y"),
                IdDashboard = LeerEntero(lector, "id_dashboard"),
                IdTag = LeerEnteroNullable(lector, "id_tag"),
                ParametrosJson = HasColumn(lector, "parametros_json") ? LeerTexto(lector, "parametros_json") : null
            };
        }

        private static bool IsParametrosJsonMissing(OracleException ex)
        {
            return ex != null
                && ex.Message != null
                && ex.Message.IndexOf("PARAMETROS_JSON", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasColumn(OracleDataReader lector, string columnName)
        {
            for (var index = 0; index < lector.FieldCount; index++)
            {
                if (string.Equals(lector.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

