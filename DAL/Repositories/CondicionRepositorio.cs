using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class CondicionRepositorio : RepositorioOracleBase, IRepositorio<Condicion>
    {
        public int Insertar(Condicion entidad)
        {
            const string sql = @"INSERT INTO condiciones (operador, valor_umbral, id_regla, id_tag)
                                 VALUES (:operador, :valor_umbral, :id_regla, :id_tag)
                                 RETURNING id_condicion INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdCondicion = LeerIdSalida(id);
                return entidad.IdCondicion;
            }
        }

        public void Actualizar(Condicion entidad)
        {
            Ejecutar(@"UPDATE condiciones
                      SET operador = :operador, valor_umbral = :valor_umbral, id_regla = :id_regla, id_tag = :id_tag
                      WHERE id_condicion = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdCondicion);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM condiciones WHERE id_condicion = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Condicion ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM condiciones WHERE id_condicion = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Condicion> ObtenerTodos()
        {
            return Consultar("SELECT * FROM condiciones ORDER BY id_condicion", Mapear);
        }

        public List<Condicion> ObtenerPorRegla(int idRegla)
        {
            return Consultar("SELECT * FROM condiciones WHERE id_regla = :id_regla ORDER BY id_condicion", Mapear,
                comando => AgregarParametro(comando, "id_regla", OracleDbType.Int32, idRegla));
        }

        public void EliminarPorRegla(int idRegla)
        {
            Ejecutar("DELETE FROM condiciones WHERE id_regla = :id_regla",
                comando => AgregarParametro(comando, "id_regla", OracleDbType.Int32, idRegla));
        }

        private static void Parametros(OracleCommand comando, Condicion entidad)
        {
            AgregarParametro(comando, "operador", OracleDbType.Varchar2, entidad.Operador);
            AgregarParametro(comando, "valor_umbral", OracleDbType.Decimal, entidad.ValorUmbral);
            AgregarParametro(comando, "id_regla", OracleDbType.Int32, entidad.IdRegla);
            AgregarParametro(comando, "id_tag", OracleDbType.Int32, entidad.IdTag);
        }

        private static Condicion Mapear(OracleDataReader lector)
        {
            return new Condicion
            {
                IdCondicion = LeerEntero(lector, "id_condicion"),
                Operador = LeerTexto(lector, "operador"),
                ValorUmbral = LeerDecimal(lector, "valor_umbral"),
                IdRegla = LeerEntero(lector, "id_regla"),
                IdTag = LeerEntero(lector, "id_tag")
            };
        }
    }
}

