using DAL.Repositories;
using ENTITY.Models;
using System;

namespace BLL.Security
{
    public class AutenticacionService
    {
        private readonly UsuarioRepositorio _usuarioRepositorio = new UsuarioRepositorio();

        public ResultadoAutenticacion Autenticar(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return ResultadoAutenticacion.Fallido("Ingrese email y contrasena.");
            }

            try
            {
                var usuario = _usuarioRepositorio.ObtenerPorEmail(email.Trim());
                if (usuario == null || !string.Equals(usuario.Password, password, StringComparison.Ordinal))
                {
                    return ResultadoAutenticacion.Fallido("Credenciales invalidas o usuario no registrado.");
                }

                return ResultadoAutenticacion.Exitoso(usuario);
            }
            catch (Exception ex)
            {
                return ResultadoAutenticacion.Fallido("No se pudo validar el usuario en Oracle: " + ex.Message);
            }
        }
    }

    public class ResultadoAutenticacion
    {
        public bool Autenticado { get; private set; }
        public string Mensaje { get; private set; }
        public Usuario Usuario { get; private set; }

        public static ResultadoAutenticacion Exitoso(Usuario usuario)
        {
            return new ResultadoAutenticacion { Autenticado = true, Usuario = usuario };
        }

        public static ResultadoAutenticacion Fallido(string mensaje)
        {
            return new ResultadoAutenticacion { Autenticado = false, Mensaje = mensaje };
        }
    }
}
