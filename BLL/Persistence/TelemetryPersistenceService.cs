using DAL.Repositories;
using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BLL.Persistence
{
    public class TelemetryPersistenceService
    {
        private const string UsuarioSimulacionEmail = "admin@visualiot.com";
        private const string ProyectoSimulacion = "Proyecto VisualIoT";
        private const string DashboardSimulacion = "Dashboard principal";
        private static readonly TimeSpan IntervaloHistorico = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan IntervaloReintentoConexion = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan IntervaloAvisoPersistencia = TimeSpan.FromMinutes(1);

        private readonly object _sync = new object();
        private readonly UsuarioRepositorio _usuarioRepositorio = new UsuarioRepositorio();
        private readonly ProyectoRepositorio _proyectoRepositorio = new ProyectoRepositorio();
        private readonly DashboardRepositorio _dashboardRepositorio = new DashboardRepositorio();
        private readonly DispositivoRepositorio _dispositivoRepositorio = new DispositivoRepositorio();
        private readonly MotorRepositorio _motorRepositorio = new MotorRepositorio();
        private readonly TagDatoRepositorio _tagRepositorio = new TagDatoRepositorio();
        private readonly HistoricoMedicionRepositorio _historicoRepositorio = new HistoricoMedicionRepositorio();
        private readonly Dictionary<string, MotorBinding> _bindings = new Dictionary<string, MotorBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _ultimaPersistenciaPorTag = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private bool _inicializado;
        private DateTime _ultimoErrorConexion = DateTime.MinValue;
        private DateTime _ultimoAvisoPersistencia = DateTime.MinValue;
        private string _ultimoMensajePersistencia;
        private int _idDashboard;

        public event Action<string> PersistenceWarning;

        public void PersistirLectura(string tag, object value)
        {
            var partes = SepararTag(tag);
            if (partes == null)
            {
                return;
            }

            double valor;
            if (!TryConvertirNumero(value, out valor))
            {
                return;
            }

            var ahora = DateTime.Now;
            lock (_sync)
            {
                if (!_inicializado && DateTime.Now - _ultimoErrorConexion < IntervaloReintentoConexion)
                {
                    return;
                }

                DateTime ultima;
                if (_ultimaPersistenciaPorTag.TryGetValue(tag, out ultima) && ahora - ultima < IntervaloHistorico)
                {
                    return;
                }

                try
                {
                    InicializarSiHaceFalta();
                    var binding = ObtenerBinding(partes.MotorId);
                    var tagDato = ObtenerTag(binding.IdDispositivo, partes.Metrica, valor);

                    _tagRepositorio.ActualizarValorActual(tagDato.IdTag, valor);
                    _historicoRepositorio.Insertar(new HistoricoMedicion
                    {
                        Valor = valor,
                        FechaMedicion = ahora,
                        IdMotor = binding.IdMotor,
                        IdTag = tagDato.IdTag
                    });

                    _ultimaPersistenciaPorTag[tag] = ahora;
                }
                catch (Exception ex)
                {
                    _ultimoErrorConexion = DateTime.Now;
                    NotificarAdvertenciaPersistencia("No se pudo persistir telemetria en Oracle: " + ex.Message);
                }
            }
        }

        private void NotificarAdvertenciaPersistencia(string mensaje)
        {
            var ahora = DateTime.Now;
            if (string.Equals(_ultimoMensajePersistencia, mensaje, StringComparison.OrdinalIgnoreCase)
                && ahora - _ultimoAvisoPersistencia < IntervaloAvisoPersistencia)
            {
                return;
            }

            _ultimoMensajePersistencia = mensaje;
            _ultimoAvisoPersistencia = ahora;
            PersistenceWarning?.Invoke(mensaje);
        }

        private void InicializarSiHaceFalta()
        {
            if (_inicializado)
            {
                return;
            }

            var usuario = _usuarioRepositorio.ObtenerPorEmail(UsuarioSimulacionEmail)
                ?? _usuarioRepositorio.ObtenerTodos().FirstOrDefault()
                ?? CrearUsuarioSimulacion();

            var proyecto = _proyectoRepositorio.ObtenerPorUsuario(usuario.IdUsuario)
                .FirstOrDefault(p => string.Equals(p.Nombre, ProyectoSimulacion, StringComparison.OrdinalIgnoreCase))
                ?? CrearProyectoSimulacion(usuario.IdUsuario);

            var dashboard = _dashboardRepositorio.ObtenerPorProyecto(proyecto.IdProyecto)
                .FirstOrDefault(d => string.Equals(d.Nombre, DashboardSimulacion, StringComparison.OrdinalIgnoreCase))
                ?? CrearDashboardSimulacion(proyecto.IdProyecto);

            _idDashboard = dashboard.IdDashboard;
            _inicializado = true;
        }

        private Usuario CrearUsuarioSimulacion()
        {
            var usuario = new Usuario
            {
                Nombre = "Administrador VisualIoT",
                Email = UsuarioSimulacionEmail,
                Password = "admin123",
                Rol = "admin"
            };

            _usuarioRepositorio.Insertar(usuario);
            return usuario;
        }

        private Proyecto CrearProyectoSimulacion(int idUsuario)
        {
            var proyecto = new Proyecto
            {
                Nombre = ProyectoSimulacion,
                FechaCreacion = DateTime.Now,
                IdUsuario = idUsuario
            };

            _proyectoRepositorio.Insertar(proyecto);
            return proyecto;
        }

        private Dashboard CrearDashboardSimulacion(int idProyecto)
        {
            var dashboard = new Dashboard
            {
                Nombre = DashboardSimulacion,
                LayoutJson = "{\"columnas\":12,\"filas\":8,\"tamanoCelda\":88}",
                FechaCreacion = DateTime.Now,
                IdProyecto = idProyecto
            };

            _dashboardRepositorio.Insertar(dashboard);
            return dashboard;
        }

        private MotorBinding ObtenerBinding(string motorId)
        {
            MotorBinding binding;
            if (_bindings.TryGetValue(motorId, out binding))
            {
                return binding;
            }

            var dispositivo = _dispositivoRepositorio.ObtenerPorDashboard(_idDashboard)
                .FirstOrDefault(d => string.Equals(d.Nombre, "Simulador " + motorId, StringComparison.OrdinalIgnoreCase))
                ?? CrearDispositivo(motorId);

            var motor = _motorRepositorio.ObtenerPorDispositivo(dispositivo.IdDispositivo)
                .FirstOrDefault(m => string.Equals(m.Nombre, motorId, StringComparison.OrdinalIgnoreCase))
                ?? CrearMotor(motorId, dispositivo.IdDispositivo);

            binding = new MotorBinding
            {
                IdDispositivo = dispositivo.IdDispositivo,
                IdMotor = motor.IdMotor,
                Tags = _tagRepositorio.ObtenerPorDispositivo(dispositivo.IdDispositivo)
                    .ToDictionary(t => t.Nombre, StringComparer.OrdinalIgnoreCase)
            };
            _bindings[motorId] = binding;
            return binding;
        }

        private Dispositivo CrearDispositivo(string motorId)
        {
            var dispositivo = new Dispositivo
            {
                Nombre = "Simulador " + motorId,
                Tipo = "Motor industrial",
                Protocolo = "Simulacion",
                Simulado = true,
                IdDashboard = _idDashboard
            };

            _dispositivoRepositorio.Insertar(dispositivo);
            return dispositivo;
        }

        private Motor CrearMotor(string motorId, int idDispositivo)
        {
            var motor = new Motor
            {
                Nombre = motorId,
                Modelo = "Simulado",
                Fabricante = "VisualIoT",
                Estado = "activo",
                FechaInstalacion = DateTime.Now,
                IdDispositivo = idDispositivo
            };

            _motorRepositorio.Insertar(motor);
            return motor;
        }

        private TagDato ObtenerTag(int idDispositivo, string metrica, double valor)
        {
            var binding = _bindings.Values.First(b => b.IdDispositivo == idDispositivo);
            TagDato tagDato;
            if (binding.Tags.TryGetValue(metrica, out tagDato))
            {
                return tagDato;
            }

            tagDato = new TagDato
            {
                Nombre = metrica,
                Unidad = UnidadPara(metrica),
                ValorActual = valor,
                IdDispositivo = idDispositivo
            };

            _tagRepositorio.Insertar(tagDato);
            binding.Tags[metrica] = tagDato;
            return tagDato;
        }

        private static TagParts SepararTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            var separador = tag.IndexOf('.');
            if (separador <= 0 || separador == tag.Length - 1)
            {
                return null;
            }

            return new TagParts
            {
                MotorId = tag.Substring(0, separador),
                Metrica = tag.Substring(separador + 1)
            };
        }

        private static bool TryConvertirNumero(object value, out double numero)
        {
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out numero);
        }

        private static string UnidadPara(string metrica)
        {
            if (string.Equals(metrica, "RPM", StringComparison.OrdinalIgnoreCase)) return "RPM";
            if (string.Equals(metrica, "Temperatura", StringComparison.OrdinalIgnoreCase)) return "C";
            if (string.Equals(metrica, "Presion", StringComparison.OrdinalIgnoreCase)) return "psi";
            if (string.Equals(metrica, "Vibracion", StringComparison.OrdinalIgnoreCase)) return "mm/s";
            if (string.Equals(metrica, "Voltaje", StringComparison.OrdinalIgnoreCase)) return "V";
            if (string.Equals(metrica, "Corriente", StringComparison.OrdinalIgnoreCase)) return "A";
            if (string.Equals(metrica, "Torque", StringComparison.OrdinalIgnoreCase)) return "Nm";
            if (string.Equals(metrica, "Nivel", StringComparison.OrdinalIgnoreCase)) return "%";
            if (string.Equals(metrica, "Eficiencia", StringComparison.OrdinalIgnoreCase)) return "%";
            return string.Empty;
        }

        private class MotorBinding
        {
            public int IdDispositivo { get; set; }
            public int IdMotor { get; set; }
            public Dictionary<string, TagDato> Tags { get; set; }
        }

        private class TagParts
        {
            public string MotorId { get; set; }
            public string Metrica { get; set; }
        }
    }
}
