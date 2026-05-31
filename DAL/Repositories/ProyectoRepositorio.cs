using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class ProyectoRepositorio : RepositorioOracleBase, IRepositorio<Proyecto>
    {
        public int Insertar(Proyecto entidad)
        {
            const string sql = @"INSERT INTO proyectos (nombre, fecha_creacion, id_usuario)
                                 VALUES (:nombre, NVL(:fecha_creacion, SYSDATE), :id_usuario)
                                 RETURNING id_proyecto INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
                AgregarParametro(comando, "fecha_creacion", OracleDbType.Date, entidad.FechaCreacion == default(DateTime) ? (object)DBNull.Value : entidad.FechaCreacion);
                AgregarParametro(comando, "id_usuario", OracleDbType.Int32, entidad.IdUsuario);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdProyecto = LeerIdSalida(id);
                return entidad.IdProyecto;
            }
        }

        public void Actualizar(Proyecto entidad)
        {
            Ejecutar(@"UPDATE proyectos
                      SET nombre = :nombre, fecha_creacion = :fecha_creacion, id_usuario = :id_usuario
                      WHERE id_proyecto = :id", comando =>
            {
                AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
                AgregarParametro(comando, "fecha_creacion", OracleDbType.Date, entidad.FechaCreacion);
                AgregarParametro(comando, "id_usuario", OracleDbType.Int32, entidad.IdUsuario);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdProyecto);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM proyectos WHERE id_proyecto = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Proyecto ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM proyectos WHERE id_proyecto = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Proyecto> ObtenerTodos()
        {
            return Consultar("SELECT * FROM proyectos ORDER BY id_proyecto", Mapear);
        }

        public List<Proyecto> ObtenerPorUsuario(int idUsuario)
        {
            return Consultar("SELECT * FROM proyectos WHERE id_usuario = :id_usuario ORDER BY id_proyecto", Mapear,
                comando => AgregarParametro(comando, "id_usuario", OracleDbType.Int32, idUsuario));
        }

        private static Proyecto Mapear(OracleDataReader lector)
        {
            return new Proyecto
            {
                IdProyecto = LeerEntero(lector, "id_proyecto"),
                Nombre = LeerTexto(lector, "nombre"),
                FechaCreacion = Convert.ToDateTime(lector["fecha_creacion"]),
                IdUsuario = LeerEntero(lector, "id_usuario")
            };
        }
    }
}

