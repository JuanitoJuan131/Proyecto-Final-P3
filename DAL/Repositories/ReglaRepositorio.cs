using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class ReglaRepositorio : RepositorioOracleBase, IRepositorio<Regla>
    {
        public int Insertar(Regla entidad)
        {
            const string sql = @"INSERT INTO reglas (nombre, descripcion, activa, id_dashboard)
                                 VALUES (:nombre, :descripcion, :activa, :id_dashboard)
                                 RETURNING id_regla INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdRegla = LeerIdSalida(id);
                return entidad.IdRegla;
            }
        }

        public void Actualizar(Regla entidad)
        {
            Ejecutar(@"UPDATE reglas
                      SET nombre = :nombre, descripcion = :descripcion, activa = :activa, id_dashboard = :id_dashboard
                      WHERE id_regla = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdRegla);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM reglas WHERE id_regla = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Regla ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM reglas WHERE id_regla = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Regla> ObtenerTodos()
        {
            return Consultar("SELECT * FROM reglas ORDER BY id_regla", Mapear);
        }

        public List<Regla> ObtenerPorDashboard(int idDashboard)
        {
            return Consultar("SELECT * FROM reglas WHERE id_dashboard = :id_dashboard ORDER BY id_regla", Mapear,
                comando => AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, idDashboard));
        }

        private static void Parametros(OracleCommand comando, Regla entidad)
        {
            AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
            AgregarParametro(comando, "descripcion", OracleDbType.Varchar2, entidad.Descripcion);
            AgregarParametroBooleano(comando, "activa", entidad.Activa);
            AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, entidad.IdDashboard);
        }

        private static Regla Mapear(OracleDataReader lector)
        {
            return new Regla
            {
                IdRegla = LeerEntero(lector, "id_regla"),
                Nombre = LeerTexto(lector, "nombre"),
                Descripcion = LeerTexto(lector, "descripcion"),
                Activa = LeerBooleano(lector, "activa"),
                IdDashboard = LeerEntero(lector, "id_dashboard")
            };
        }
    }
}

