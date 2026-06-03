using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class DispositivoRepositorio : RepositorioOracleBase, IRepositorio<Dispositivo>
    {
        public int Insertar(Dispositivo entidad)
        {
            const string sql = @"INSERT INTO dispositivos (nombre, tipo, protocolo, simulado, id_dashboard)
                                 VALUES (:nombre, :tipo, :protocolo, :simulado,:id_dashboard)
                                 RETURNING id_dispositivo INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdDispositivo = LeerIdSalida(id);
                return entidad.IdDispositivo;
            }
        }

        public void Actualizar(Dispositivo entidad)
        {
            Ejecutar(@"UPDATE dispositivos
                      SET nombre = :nombre, tipo = :tipo, protocolo = :protocolo, simulado = :simulado, id_dashboard = :id_dashboard
                      WHERE id_dispositivo = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdDispositivo);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM dispositivos WHERE id_dispositivo = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Dispositivo ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM dispositivos WHERE id_dispositivo = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Dispositivo> ObtenerTodos()
        {
            return Consultar("SELECT * FROM dispositivos ORDER BY id_dispositivo", Mapear);
        }

        public List<Dispositivo> ObtenerPorDashboard(int idDashboard)
        {
            return Consultar("SELECT * FROM dispositivos WHERE id_dashboard = :id_dashboard ORDER BY id_dispositivo", Mapear,
                comando => AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, idDashboard));
        }

        private static void Parametros(OracleCommand comando, Dispositivo entidad)
        {
            AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
            AgregarParametro(comando, "tipo", OracleDbType.Varchar2, entidad.Tipo);
            AgregarParametro(comando, "protocolo", OracleDbType.Varchar2, entidad.Protocolo);
            AgregarParametroBooleano(comando, "simulado", entidad.Simulado);
            AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, entidad.IdDashboard);
        }

        private static Dispositivo Mapear(OracleDataReader lector)
        {
            return new Dispositivo
            {
                IdDispositivo = LeerEntero(lector, "id_dispositivo"),
                Nombre = LeerTexto(lector, "nombre"),
                Tipo = LeerTexto(lector, "tipo"),
                Protocolo = LeerTexto(lector, "protocolo"),
                Simulado = LeerBooleano(lector, "simulado"),
                IdDashboard = LeerEntero(lector, "id_dashboard")
            };
        }
    }
}

