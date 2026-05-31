using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class TagDatoRepositorio : RepositorioOracleBase, IRepositorio<TagDato>
    {
        public int Insertar(TagDato entidad)
        {
            const string sql = @"INSERT INTO tagdata (nombre, unidad, valor_actual, id_dispositivo)
                                 VALUES (:nombre, :unidad, :valor_actual, :id_dispositivo)
                                 RETURNING id_tag INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdTag = LeerIdSalida(id);
                return entidad.IdTag;
            }
        }

        public void Actualizar(TagDato entidad)
        {
            Ejecutar(@"UPDATE tagdata
                      SET nombre = :nombre, unidad = :unidad, valor_actual = :valor_actual, id_dispositivo = :id_dispositivo
                      WHERE id_tag = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdTag);
            });
        }

        public void ActualizarValorActual(int idTag, double valorActual)
        {
            Ejecutar("UPDATE tagdata SET valor_actual = :valor_actual WHERE id_tag = :id",
                comando =>
                {
                    AgregarParametro(comando, "valor_actual", OracleDbType.Decimal, valorActual);
                    AgregarParametro(comando, "id", OracleDbType.Int32, idTag);
                });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM tagdata WHERE id_tag = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public TagDato ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM tagdata WHERE id_tag = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<TagDato> ObtenerTodos()
        {
            return Consultar("SELECT * FROM tagdata ORDER BY id_tag", Mapear);
        }

        public List<TagDato> ObtenerPorDispositivo(int idDispositivo)
        {
            return Consultar("SELECT * FROM tagdata WHERE id_dispositivo = :id_dispositivo ORDER BY id_tag", Mapear,
                comando => AgregarParametro(comando, "id_dispositivo", OracleDbType.Int32, idDispositivo));
        }

        private static void Parametros(OracleCommand comando, TagDato entidad)
        {
            AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
            AgregarParametro(comando, "unidad", OracleDbType.Varchar2, entidad.Unidad);
            AgregarParametro(comando, "valor_actual", OracleDbType.Decimal, entidad.ValorActual);
            AgregarParametro(comando, "id_dispositivo", OracleDbType.Int32, entidad.IdDispositivo);
        }

        private static TagDato Mapear(OracleDataReader lector)
        {
            return new TagDato
            {
                IdTag = LeerEntero(lector, "id_tag"),
                Nombre = LeerTexto(lector, "nombre"),
                Unidad = LeerTexto(lector, "unidad"),
                ValorActual = LeerDecimalNullable(lector, "valor_actual") ?? 0,
                IdDispositivo = LeerEntero(lector, "id_dispositivo")
            };
        }
    }
}

