using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class HistorialEventoRepositorio : RepositorioOracleBase, IRepositorio<HistorialEvento>
    {
        public int Insertar(HistorialEvento entidad)
        {
            const string sql = @"INSERT INTO historial_eventos (descripcion, valor_detectado, fecha_evento, id_regla, id_motor)
                                 VALUES (:descripcion, :valor_detectado, NVL(:fecha_evento, CURRENT_TIMESTAMP), :id_regla, :id_motor)
                                 RETURNING id_evento INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdEvento = LeerIdSalida(id);
                return entidad.IdEvento;
            }
        }

        public void Actualizar(HistorialEvento entidad)
        {
            Ejecutar(@"UPDATE historial_eventos
                      SET descripcion = :descripcion, valor_detectado = :valor_detectado, fecha_evento = :fecha_evento,
                          id_regla = :id_regla, id_motor = :id_motor
                      WHERE id_evento = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdEvento);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM historial_eventos WHERE id_evento = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public HistorialEvento ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM historial_eventos WHERE id_evento = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<HistorialEvento> ObtenerTodos()
        {
            return Consultar("SELECT * FROM historial_eventos ORDER BY fecha_evento DESC", Mapear);
        }

        public List<HistorialEvento> ObtenerPorMotor(int idMotor)
        {
            return Consultar("SELECT * FROM historial_eventos WHERE id_motor = :id_motor ORDER BY fecha_evento DESC", Mapear,
                comando => AgregarParametro(comando, "id_motor", OracleDbType.Int32, idMotor));
        }

        private static void Parametros(OracleCommand comando, HistorialEvento entidad)
        {
            AgregarParametro(comando, "descripcion", OracleDbType.Varchar2, entidad.Descripcion);
            AgregarParametro(comando, "valor_detectado", OracleDbType.Decimal, entidad.ValorDetectado.HasValue ? (object)entidad.ValorDetectado.Value : DBNull.Value);
            AgregarParametro(comando, "fecha_evento", OracleDbType.TimeStamp, entidad.FechaEvento.HasValue ? (object)entidad.FechaEvento.Value : DBNull.Value);
            AgregarParametro(comando, "id_regla", OracleDbType.Int32, entidad.IdRegla.HasValue ? (object)entidad.IdRegla.Value : DBNull.Value);
            AgregarParametro(comando, "id_motor", OracleDbType.Int32, entidad.IdMotor.HasValue ? (object)entidad.IdMotor.Value : DBNull.Value);
        }

        private static HistorialEvento Mapear(OracleDataReader lector)
        {
            return new HistorialEvento
            {
                IdEvento = LeerEntero(lector, "id_evento"),
                Descripcion = LeerTexto(lector, "descripcion"),
                ValorDetectado = LeerDecimalNullable(lector, "valor_detectado"),
                FechaEvento = LeerFechaNullable(lector, "fecha_evento"),
                IdRegla = LeerEnteroNullable(lector, "id_regla"),
                IdMotor = LeerEnteroNullable(lector, "id_motor")
            };
        }
    }
}

