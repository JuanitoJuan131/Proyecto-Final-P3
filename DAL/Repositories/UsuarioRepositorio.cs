using ENTITY.Models;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;

namespace DAL.Repositories
{
    public class UsuarioRepositorio : RepositorioOracleBase, IRepositorio<Usuario>
    {
        public int Insertar(Usuario entidad)
        {
            const string sql = @"INSERT INTO usuarios (nombre, email, password, rol)
                                 VALUES (:nombre, :email, :password, :rol)
                                 RETURNING id_usuario INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
                AgregarParametro(comando, "email", OracleDbType.Varchar2, entidad.Email);
                AgregarParametro(comando, "password", OracleDbType.Varchar2, entidad.Password);
                AgregarParametro(comando, "rol", OracleDbType.Varchar2, entidad.Rol);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdUsuario = LeerIdSalida(id);
                return entidad.IdUsuario;
            }
        }

        public void Actualizar(Usuario entidad)
        {
            Ejecutar(@"UPDATE usuarios
                      SET nombre = :nombre, email = :email, password = :password, rol = :rol
                      WHERE id_usuario = :id", comando =>
            {
                AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
                AgregarParametro(comando, "email", OracleDbType.Varchar2, entidad.Email);
                AgregarParametro(comando, "password", OracleDbType.Varchar2, entidad.Password);
                AgregarParametro(comando, "rol", OracleDbType.Varchar2, entidad.Rol);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdUsuario);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM usuarios WHERE id_usuario = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Usuario ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM usuarios WHERE id_usuario = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Usuario ObtenerPorEmail(string email)
        {
            return ConsultarUno("SELECT * FROM usuarios WHERE email = :email", Mapear, comando => AgregarParametro(comando, "email", OracleDbType.Varchar2, email));
        }

        public List<Usuario> ObtenerTodos()
        {
            return Consultar("SELECT * FROM usuarios ORDER BY id_usuario", Mapear);
        }

        private static Usuario Mapear(OracleDataReader lector)
        {
            return new Usuario
            {
                IdUsuario = LeerEntero(lector, "id_usuario"),
                Nombre = LeerTexto(lector, "nombre"),
                Email = LeerTexto(lector, "email"),
                Password = LeerTexto(lector, "password"),
                Rol = LeerTexto(lector, "rol")
            };
        }
    }
}

