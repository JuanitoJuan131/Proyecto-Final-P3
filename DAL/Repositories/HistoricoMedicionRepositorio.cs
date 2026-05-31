using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class HistoricoMedicionRepositorio : RepositorioOracleBase, IRepositorio<HistoricoMedicion>
    {
        public int Insertar(HistoricoMedicion entidad)
        {
            const string sql = @"INSERT INTO historico_mediciones (valor, fecha_medicion, id_motor, id_tag)
                                 VALUES (:valor, NVL(:fecha_medicion, CURRENT_TIMESTAMP), :id_motor, :id_tag)
                                 RETURNING id_medicion INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                AgregarParametro(comando, "valor", OracleDbType.Decimal, entidad.Valor);
                AgregarParametro(comando, "fecha_medicion", OracleDbType.TimeStamp, entidad.FechaMedicion.HasValue ? (object)entidad.FechaMedicion.Value : DBNull.Value);
                AgregarParametro(comando, "id_motor", OracleDbType.Int32, entidad.IdMotor);
                AgregarParametro(comando, "id_tag", OracleDbType.Int32, entidad.IdTag);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdMedicion = LeerIdSalida(id);
                return entidad.IdMedicion;
            }
        }

        public void Actualizar(HistoricoMedicion entidad)
        {
            Ejecutar(@"UPDATE historico_mediciones
                      SET valor = :valor, fecha_medicion = :fecha_medicion, id_motor = :id_motor, id_tag = :id_tag
                      WHERE id_medicion = :id", comando =>
            {
                AgregarParametro(comando, "valor", OracleDbType.Decimal, entidad.Valor);
                AgregarParametro(comando, "fecha_medicion", OracleDbType.TimeStamp, entidad.FechaMedicion.HasValue ? (object)entidad.FechaMedicion.Value : DBNull.Value);
                AgregarParametro(comando, "id_motor", OracleDbType.Int32, entidad.IdMotor);
                AgregarParametro(comando, "id_tag", OracleDbType.Int32, entidad.IdTag);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdMedicion);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM historico_mediciones WHERE id_medicion = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public HistoricoMedicion ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM historico_mediciones WHERE id_medicion = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<HistoricoMedicion> ObtenerTodos()
        {
            return Consultar("SELECT * FROM historico_mediciones ORDER BY fecha_medicion DESC", Mapear);
        }

        public List<HistoricoMedicion> ObtenerPorMotor(int idMotor)
        {
            return Consultar("SELECT * FROM historico_mediciones WHERE id_motor = :id_motor ORDER BY fecha_medicion DESC", Mapear,
                comando => AgregarParametro(comando, "id_motor", OracleDbType.Int32, idMotor));
        }

        private static HistoricoMedicion Mapear(OracleDataReader lector)
        {
            return new HistoricoMedicion
            {
                IdMedicion = LeerEntero(lector, "id_medicion"),
                Valor = LeerDecimal(lector, "valor"),
                FechaMedicion = LeerFechaNullable(lector, "fecha_medicion"),
                IdMotor = LeerEntero(lector, "id_motor"),
                IdTag = LeerEntero(lector, "id_tag")
            };
        }
    }
}

