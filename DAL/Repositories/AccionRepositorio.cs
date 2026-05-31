using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class AccionRepositorio : RepositorioOracleBase, IRepositorio<Accion>
    {
        public int Insertar(Accion entidad)
        {
            const string sql = @"INSERT INTO acciones (tipo_accion, parametros_json, id_regla)
                                 VALUES (:tipo_accion, :parametros_json, :id_regla)
                                 RETURNING id_accion INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdAccion = LeerIdSalida(id);
                return entidad.IdAccion;
            }
        }

        public void Actualizar(Accion entidad)
        {
            Ejecutar(@"UPDATE acciones
                      SET tipo_accion = :tipo_accion, parametros_json = :parametros_json, id_regla = :id_regla
                      WHERE id_accion = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdAccion);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM acciones WHERE id_accion = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Accion ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM acciones WHERE id_accion = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Accion> ObtenerTodos()
        {
            return Consultar("SELECT * FROM acciones ORDER BY id_accion", Mapear);
        }

        public List<Accion> ObtenerPorRegla(int idRegla)
        {
            return Consultar("SELECT * FROM acciones WHERE id_regla = :id_regla ORDER BY id_accion", Mapear,
                comando => AgregarParametro(comando, "id_regla", OracleDbType.Int32, idRegla));
        }

        public void EliminarPorRegla(int idRegla)
        {
            Ejecutar("DELETE FROM acciones WHERE id_regla = :id_regla",
                comando => AgregarParametro(comando, "id_regla", OracleDbType.Int32, idRegla));
        }

        private static void Parametros(OracleCommand comando, Accion entidad)
        {
            AgregarParametro(comando, "tipo_accion", OracleDbType.Varchar2, entidad.TipoAccion);
            AgregarParametro(comando, "parametros_json", OracleDbType.Clob, entidad.ParametrosJson);
            AgregarParametro(comando, "id_regla", OracleDbType.Int32, entidad.IdRegla);
        }

        private static Accion Mapear(OracleDataReader lector)
        {
            return new Accion
            {
                IdAccion = LeerEntero(lector, "id_accion"),
                TipoAccion = LeerTexto(lector, "tipo_accion"),
                ParametrosJson = LeerTexto(lector, "parametros_json"),
                IdRegla = LeerEntero(lector, "id_regla")
            };
        }
    }
}

