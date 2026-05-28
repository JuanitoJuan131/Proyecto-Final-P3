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

    public class DashboardRepositorio : RepositorioOracleBase, IRepositorio<Dashboard>
    {
        public int Insertar(Dashboard entidad)
        {
            const string sql = @"INSERT INTO dashboards (nombre, layout_json, fecha_creacion, id_proyecto)
                                 VALUES (:nombre, :layout_json, NVL(:fecha_creacion, SYSDATE), :id_proyecto)
                                 RETURNING id_dashboard INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
                AgregarParametro(comando, "layout_json", OracleDbType.Clob, entidad.LayoutJson);
                AgregarParametro(comando, "fecha_creacion", OracleDbType.Date, entidad.FechaCreacion.HasValue ? (object)entidad.FechaCreacion.Value : DBNull.Value);
                AgregarParametro(comando, "id_proyecto", OracleDbType.Int32, entidad.IdProyecto);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdDashboard = LeerIdSalida(id);
                return entidad.IdDashboard;
            }
        }

        public void Actualizar(Dashboard entidad)
        {
            Ejecutar(@"UPDATE dashboards
                      SET nombre = :nombre, layout_json = :layout_json, fecha_creacion = :fecha_creacion, id_proyecto = :id_proyecto
                      WHERE id_dashboard = :id", comando =>
            {
                AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
                AgregarParametro(comando, "layout_json", OracleDbType.Clob, entidad.LayoutJson);
                AgregarParametro(comando, "fecha_creacion", OracleDbType.Date, entidad.FechaCreacion.HasValue ? (object)entidad.FechaCreacion.Value : DBNull.Value);
                AgregarParametro(comando, "id_proyecto", OracleDbType.Int32, entidad.IdProyecto);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdDashboard);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM dashboards WHERE id_dashboard = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Dashboard ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM dashboards WHERE id_dashboard = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Dashboard> ObtenerTodos()
        {
            return Consultar("SELECT * FROM dashboards ORDER BY id_dashboard", Mapear);
        }

        public List<Dashboard> ObtenerPorProyecto(int idProyecto)
        {
            return Consultar("SELECT * FROM dashboards WHERE id_proyecto = :id_proyecto ORDER BY id_dashboard", Mapear,
                comando => AgregarParametro(comando, "id_proyecto", OracleDbType.Int32, idProyecto));
        }

        private static Dashboard Mapear(OracleDataReader lector)
        {
            return new Dashboard
            {
                IdDashboard = LeerEntero(lector, "id_dashboard"),
                Nombre = LeerTexto(lector, "nombre"),
                LayoutJson = LeerTexto(lector, "layout_json"),
                FechaCreacion = LeerFechaNullable(lector, "fecha_creacion"),
                IdProyecto = LeerEntero(lector, "id_proyecto")
            };
        }
    }

    public class DispositivoRepositorio : RepositorioOracleBase, IRepositorio<Dispositivo>
    {
        public int Insertar(Dispositivo entidad)
        {
            const string sql = @"INSERT INTO dispositivos (nombre, tipo, protocolo, simulado, id_azure_iot, id_dashboard)
                                 VALUES (:nombre, :tipo, :protocolo, :simulado, :id_azure_iot, :id_dashboard)
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
                      SET nombre = :nombre, tipo = :tipo, protocolo = :protocolo, simulado = :simulado, id_azure_iot = :id_azure_iot, id_dashboard = :id_dashboard
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
            AgregarParametro(comando, "id_azure_iot", OracleDbType.Varchar2, entidad.IdAzureIot);
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
                IdAzureIot = LeerTexto(lector, "id_azure_iot"),
                IdDashboard = LeerEntero(lector, "id_dashboard")
            };
        }
    }

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

    public class WidgetRepositorio : RepositorioOracleBase, IRepositorio<Widget>
    {
        public int Insertar(Widget entidad)
        {
            const string sql = @"INSERT INTO widgets (tipo_widget, descripcion, posicion_x, posicion_y, id_dashboard, id_tag, parametros_json)
                                 VALUES (:tipo_widget, :descripcion, :posicion_x, :posicion_y, :id_dashboard, :id_tag, :parametros_json)
                                 RETURNING id_widget INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdWidget = LeerIdSalida(id);
                return entidad.IdWidget;
            }
        }

        public void Actualizar(Widget entidad)
        {
            Ejecutar(@"UPDATE widgets
                      SET tipo_widget = :tipo_widget, descripcion = :descripcion, posicion_x = :posicion_x,
                          posicion_y = :posicion_y, id_dashboard = :id_dashboard, id_tag = :id_tag,
                          parametros_json = :parametros_json
                      WHERE id_widget = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdWidget);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM widgets WHERE id_widget = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Widget ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM widgets WHERE id_widget = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Widget> ObtenerTodos()
        {
            return Consultar("SELECT * FROM widgets ORDER BY id_widget", Mapear);
        }

        public List<Widget> ObtenerPorDashboard(int idDashboard)
        {
            return Consultar("SELECT * FROM widgets WHERE id_dashboard = :id_dashboard ORDER BY id_widget", Mapear,
                comando => AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, idDashboard));
        }

        public void ReemplazarPorDashboard(int idDashboard, IEnumerable<Widget> widgets)
        {
            try
            {
                ReemplazarPorDashboard(idDashboard, widgets, true);
            }
            catch (OracleException ex)
            {
                if (!IsParametrosJsonMissing(ex))
                {
                    throw;
                }

                ReemplazarPorDashboard(idDashboard, widgets, false);
            }
        }

        private void ReemplazarPorDashboard(int idDashboard, IEnumerable<Widget> widgets, bool incluirParametrosJson)
        {
            using (var conexion = CrearConexion())
            {
                conexion.Open();
                using (var transaccion = conexion.BeginTransaction())
                {
                    try
                    {
                        using (var borrar = CrearComando(conexion, "DELETE FROM widgets WHERE id_dashboard = :id_dashboard"))
                        {
                            borrar.Transaction = transaccion;
                            AgregarParametro(borrar, "id_dashboard", OracleDbType.Int32, idDashboard);
                            borrar.ExecuteNonQuery();
                        }

                        foreach (var widget in widgets)
                        {
                            widget.IdDashboard = idDashboard;
                            var sql = incluirParametrosJson
                                ? @"INSERT INTO widgets
                                    (tipo_widget, descripcion, posicion_x, posicion_y, id_dashboard, id_tag, parametros_json)
                                    VALUES (:tipo_widget, :descripcion, :posicion_x, :posicion_y, :id_dashboard, :id_tag, :parametros_json)
                                    RETURNING id_widget INTO :id"
                                : @"INSERT INTO widgets
                                    (tipo_widget, descripcion, posicion_x, posicion_y, id_dashboard, id_tag)
                                    VALUES (:tipo_widget, :descripcion, :posicion_x, :posicion_y, :id_dashboard, :id_tag)
                                    RETURNING id_widget INTO :id";

                            using (var insertar = CrearComando(conexion, sql))
                            {
                                insertar.Transaction = transaccion;
                                Parametros(insertar, widget, incluirParametrosJson);
                                var id = AgregarParametroIdSalida(insertar, "id");
                                insertar.ExecuteNonQuery();
                                widget.IdWidget = LeerIdSalida(id);
                            }
                        }

                        transaccion.Commit();
                    }
                    catch
                    {
                        transaccion.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void Parametros(OracleCommand comando, Widget entidad)
        {
            Parametros(comando, entidad, true);
        }

        private static void Parametros(OracleCommand comando, Widget entidad, bool incluirParametrosJson)
        {
            AgregarParametro(comando, "tipo_widget", OracleDbType.Varchar2, entidad.TipoWidget.ToString());
            AgregarParametro(comando, "descripcion", OracleDbType.Varchar2, entidad.Descripcion);
            AgregarParametro(comando, "posicion_x", OracleDbType.Decimal, entidad.PosicionX);
            AgregarParametro(comando, "posicion_y", OracleDbType.Decimal, entidad.PosicionY);
            AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, entidad.IdDashboard);
            AgregarParametro(comando, "id_tag", OracleDbType.Int32, entidad.IdTag.HasValue ? (object)entidad.IdTag.Value : DBNull.Value);
            if (incluirParametrosJson)
            {
                AgregarParametro(comando, "parametros_json", OracleDbType.Clob, entidad.ParametrosJson);
            }
        }

        private static Widget Mapear(OracleDataReader lector)
        {
            TipoWidget tipo;
            Enum.TryParse(LeerTexto(lector, "tipo_widget"), out tipo);
            return new Widget
            {
                IdWidget = LeerEntero(lector, "id_widget"),
                TipoWidget = tipo,
                Descripcion = LeerTexto(lector, "descripcion"),
                PosicionX = LeerDecimal(lector, "posicion_x"),
                PosicionY = LeerDecimal(lector, "posicion_y"),
                IdDashboard = LeerEntero(lector, "id_dashboard"),
                IdTag = LeerEnteroNullable(lector, "id_tag"),
                ParametrosJson = HasColumn(lector, "parametros_json") ? LeerTexto(lector, "parametros_json") : null
            };
        }

        private static bool IsParametrosJsonMissing(OracleException ex)
        {
            return ex != null
                && ex.Message != null
                && ex.Message.IndexOf("PARAMETROS_JSON", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasColumn(OracleDataReader lector, string columnName)
        {
            for (var index = 0; index < lector.FieldCount; index++)
            {
                if (string.Equals(lector.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class ReglaRepositorio : RepositorioOracleBase, IRepositorio<Regla>
    {
        public int Insertar(Regla entidad)
        {
            const string sql = @"INSERT INTO reglas (nombre, descripcion, activa, id_dashboard)
                                 VALUES (:nombre, :descripcion, :activa, :id_dashboard)
                                 RETURNING id_regla INTO :id";
            using (var conexion = CrearConexion())
            using (var comando = CrearComando(conexion, sql))
            {
                Parametros(comando, entidad);
                var id = AgregarParametroIdSalida(comando, "id");
                conexion.Open();
                comando.ExecuteNonQuery();
                entidad.IdRegla = LeerIdSalida(id);
                return entidad.IdRegla;
            }
        }

        public void Actualizar(Regla entidad)
        {
            Ejecutar(@"UPDATE reglas
                      SET nombre = :nombre, descripcion = :descripcion, activa = :activa, id_dashboard = :id_dashboard
                      WHERE id_regla = :id", comando =>
            {
                Parametros(comando, entidad);
                AgregarParametro(comando, "id", OracleDbType.Int32, entidad.IdRegla);
            });
        }

        public void Eliminar(int id)
        {
            Ejecutar("DELETE FROM reglas WHERE id_regla = :id", comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public Regla ObtenerPorId(int id)
        {
            return ConsultarUno("SELECT * FROM reglas WHERE id_regla = :id", Mapear, comando => AgregarParametro(comando, "id", OracleDbType.Int32, id));
        }

        public List<Regla> ObtenerTodos()
        {
            return Consultar("SELECT * FROM reglas ORDER BY id_regla", Mapear);
        }

        public List<Regla> ObtenerPorDashboard(int idDashboard)
        {
            return Consultar("SELECT * FROM reglas WHERE id_dashboard = :id_dashboard ORDER BY id_regla", Mapear,
                comando => AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, idDashboard));
        }

        private static void Parametros(OracleCommand comando, Regla entidad)
        {
            AgregarParametro(comando, "nombre", OracleDbType.Varchar2, entidad.Nombre);
            AgregarParametro(comando, "descripcion", OracleDbType.Varchar2, entidad.Descripcion);
            AgregarParametroBooleano(comando, "activa", entidad.Activa);
            AgregarParametro(comando, "id_dashboard", OracleDbType.Int32, entidad.IdDashboard);
        }

        private static Regla Mapear(OracleDataReader lector)
        {
            return new Regla
            {
                IdRegla = LeerEntero(lector, "id_regla"),
                Nombre = LeerTexto(lector, "nombre"),
                Descripcion = LeerTexto(lector, "descripcion"),
                Activa = LeerBooleano(lector, "activa"),
                IdDashboard = LeerEntero(lector, "id_dashboard")
            };
        }
    }

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
