using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class DashboardRepositorio : RepositorioOracleBase, IRepositorio<Dashboard>
    {
        public int Insertar(Dashboard entidad)
        {
            const string sql = @"INSERT INTO dashboards (nombre, layout_json, fecha_creacion, id_proyecto)
                                 VALUES (:nombre, :layout_json, NVL(:fecha_creacion, SYSDATE), :id_proyecto)
                                 RETURNING id_dashboard INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
                AgregarParametro(comando, "layout_json", OracleDbType.Clob, entidad.LayoutJson);
                AgregarParametro(comando, "fecha_creacion", OracleDbType.Date, entidad.FechaCreacion.HasValue ? (object)entidad.FechaCreacion.Value : DBNull.Value);
                AgregarParametro(comando, "id_proyecto", OracleDbType.Int32, entidad.IdProyecto);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdDashboard = LeerIdSalida(id);
                return entidad.IdDashboard;
            }
        }

        public void Actualizar(Dashboard entidad)
        {
            Ejecutar(@"UPDATE dashboards
                      SET nombre = :nombre, layout_json = :layout_json, fecha_creacion = :fecha_creacion, id_proyecto = :id_proyecto
                      WHERE id_dashboard = :id", comando =>
            {
                AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
                AgregarParametro(comando, "layout_json", OracleDbType.Clob, entidad.LayoutJson);
                AgregarParametro(comando, "fecha_creacion", OracleDbType.Date, entidad.FechaCreacion.HasValue ? (object)entidad.FechaCreacion.Value : DBNull.Value);
                AgregarParametro(comando, "id_proyecto", OracleDbType.Int32, entidad.IdProyecto);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdDashboard);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM dashboards WHERE id_dashboard = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Dashboard ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM dashboards WHERE id_dashboard = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Dashboard> ObtenerTodos()
        {
            return Consultar("SELECT * FROM dashboards ORDER BY id_dashboard", Mapear);
        }

        public List<Dashboard> ObtenerPorProyecto(int idProyecto)
        {
            return Consultar("SELECT * FROM dashboards WHERE id_proyecto = :id_proyecto ORDER BY id_dashboard", Mapear,
                comando => AgregarParametro(comando, "id_proyecto", OracleDbType.Int32, idProyecto));
        }

        private static Dashboard Mapear(OracleDataReader lector)
        {
            return new Dashboard
            {
                IdDashboard = LeerEntero(lector, "id_dashboard"),
                Nombre = LeerTexto(lector, "nombre"),
                LayoutJson = LeerTexto(lector, "layout_json"),
                FechaCreacion = LeerFechaNullable(lector, "fecha_creacion"),
                IdProyecto = LeerEntero(lector, "id_proyecto")
            };
        }
    }
}

