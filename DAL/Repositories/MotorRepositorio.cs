using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class MotorRepositorio : RepositorioOracleBase, IRepositorio<Motor>
    {
        public int Insertar(Motor entidad)
        {
            const string sql = @"INSERT INTO motores (nombre, modelo, fabricante, estado, fecha_instalacion, id_dispositivo)
                                 VALUES (:nombre, :modelo, :fabricante, :estado, :fecha_instalacion, :id_dispositivo)
                                 RETURNING id_motor INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdMotor = LeerIdSalida(id);
                return entidad.IdMotor;
            }
        }

        public void Actualizar(Motor entidad)
        {
            Ejecutar(@"UPDATE motores
                      SET nombre = :nombre, modelo = :modelo, fabricante = :fabricante, estado = :estado,
                          fecha_instalacion = :fecha_instalacion, id_dispositivo = :id_dispositivo
                      WHERE id_motor = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdMotor);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM motores WHERE id_motor = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Motor ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM motores WHERE id_motor = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Motor> ObtenerTodos()
        {
            return Consultar("SELECT * FROM motores ORDER BY id_motor", Mapear);
        }

        public List<Motor> ObtenerPorDispositivo(int idDispositivo)
        {
            return Consultar("SELECT * FROM motores WHERE id_dispositivo = :id_dispositivo ORDER BY id_motor", Mapear,
                comando => AgregarParametro(comando, "id_dispositivo", OracleDbType.Int32, idDispositivo));
        }

        private static void Parametros(OracleCommand comando, Motor entidad)
        {
            AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
            AgregarParametro(comando, "modelo", OracleDbType.Varchar2, entidad.Modelo);
            AgregarParametro(comando, "fabricante", OracleDbType.Varchar2, entidad.Fabricante);
            AgregarParametro(comando, "estado", OracleDbType.Varchar2, entidad.Estado);
            AgregarParametro(comando, "fecha_instalacion", OracleDbType.Date, entidad.FechaInstalacion.HasValue ? (object)entidad.FechaInstalacion.Value : DBNull.Value);
            AgregarParametro(comando, "id_dispositivo", OracleDbType.Int32, entidad.IdDispositivo);
        }

        private static Motor Mapear(OracleDataReader lector)
        {
            return new Motor
            {
                IdMotor = LeerEntero(lector, "id_motor"),
                Nombre = LeerTexto(lector, "nombre"),
                Modelo = LeerTexto(lector, "modelo"),
                Fabricante = LeerTexto(lector, "fabricante"),
                Estado = LeerTexto(lector, "estado"),
                FechaInstalacion = LeerFechaNullable(lector, "fecha_instalacion"),
                IdDispositivo = LeerEntero(lector, "id_dispositivo")
            };
        }
    }
}

