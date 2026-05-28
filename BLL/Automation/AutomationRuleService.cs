using DAL.Repositories;
using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace BLL.Automation
{
    public class AutomationRuleService
    {
        private static readonly TimeSpan EventCooldown = TimeSpan.FromSeconds(30);
        private readonly ReglaRepositorio _reglaRepositorio = new ReglaRepositorio();
        private readonly CondicionRepositorio _condicionRepositorio = new CondicionRepositorio();
        private readonly AccionRepositorio _accionRepositorio = new AccionRepositorio();
        private readonly TagDatoRepositorio _tagRepositorio = new TagDatoRepositorio();
        private readonly HistorialEventoRepositorio _eventoRepositorio = new HistorialEventoRepositorio();
        private readonly Dictionary<int, DateTime> _lastEventByRule = new Dictionary<int, DateTime>();

        public List<TagDato> ObtenerTags()
        {
            return _tagRepositorio.ObtenerTodos();
        }

        public List<Regla> ObtenerReglasDashboard(int idDashboard)
        {
            return _reglaRepositorio.ObtenerPorDashboard(idDashboard);
        }

        public List<AutomationRuleSummary> ObtenerResumenReglasDashboard(int idDashboard)
        {
            return _reglaRepositorio.ObtenerPorDashboard(idDashboard)
                .Select(rule =>
                {
                    var condition = _condicionRepositorio.ObtenerPorRegla(rule.IdRegla).FirstOrDefault();
                    var action = _accionRepositorio.ObtenerPorRegla(rule.IdRegla).FirstOrDefault();
                    var tag = condition == null ? null : _tagRepositorio.ObtenerPorId(condition.IdTag);
                    var severity = ResolveSeverity(action == null ? Enumerable.Empty<Accion>() : new[] { action });

                    return new AutomationRuleSummary
                    {
                        IdRegla = rule.IdRegla,
                        Nombre = rule.Nombre,
                        Descripcion = rule.Descripcion,
                        Activa = rule.Activa,
                        IdTag = condition == null ? 0 : condition.IdTag,
                        Sensor = tag == null ? "Sensor no disponible" : tag.Nombre,
                        Unidad = tag == null ? null : tag.Unidad,
                        Operador = condition == null ? string.Empty : condition.Operador,
                        ValorUmbral = condition == null ? 0 : condition.ValorUmbral,
                        Severidad = severity,
                        Color = ColorParaSeveridad(severity)
                    };
                })
                .ToList();
        }

        public int CrearRegla(AutomationRuleDefinition definition)
        {
            ValidarDefinicion(definition);
            var regla = new Regla
            {
                Nombre = definition.Nombre.Trim(),
                Descripcion = SafeTrim(definition.Descripcion),
                Activa = definition.Activa,
                IdDashboard = definition.IdDashboard
            };

            _reglaRepositorio.Insertar(regla);
            _condicionRepositorio.Insertar(new Condicion
            {
                IdRegla = regla.IdRegla,
                IdTag = definition.IdTag,
                Operador = definition.Operador,
                ValorUmbral = definition.ValorUmbral
            });

            _accionRepositorio.Insertar(new Accion
            {
                IdRegla = regla.IdRegla,
                TipoAccion = definition.TipoAccion,
                ParametrosJson = SerializarAccion(definition.Severidad)
            });

            return regla.IdRegla;
        }

        public void ActualizarRegla(int idRegla, AutomationRuleDefinition definition)
        {
            ValidarDefinicion(definition);
            var regla = _reglaRepositorio.ObtenerPorId(idRegla);
            if (regla == null)
            {
                throw new InvalidOperationException("La regla seleccionada ya no existe.");
            }

            regla.Nombre = definition.Nombre.Trim();
            regla.Descripcion = SafeTrim(definition.Descripcion);
            regla.Activa = definition.Activa;
            regla.IdDashboard = definition.IdDashboard;
            _reglaRepositorio.Actualizar(regla);

            var condicion = _condicionRepositorio.ObtenerPorRegla(idRegla).FirstOrDefault();
            if (condicion == null)
            {
                condicion = new Condicion { IdRegla = idRegla };
                SetConditionValues(condicion, definition);
                _condicionRepositorio.Insertar(condicion);
            }
            else
            {
                SetConditionValues(condicion, definition);
                _condicionRepositorio.Actualizar(condicion);
            }

            var accion = _accionRepositorio.ObtenerPorRegla(idRegla).FirstOrDefault();
            if (accion == null)
            {
                _accionRepositorio.Insertar(new Accion
                {
                    IdRegla = idRegla,
                    TipoAccion = definition.TipoAccion,
                    ParametrosJson = SerializarAccion(definition.Severidad)
                });
            }
            else
            {
                accion.TipoAccion = definition.TipoAccion;
                accion.ParametrosJson = SerializarAccion(definition.Severidad);
                _accionRepositorio.Actualizar(accion);
            }
        }

        public void EliminarRegla(int idRegla)
        {
            _accionRepositorio.EliminarPorRegla(idRegla);
            _condicionRepositorio.EliminarPorRegla(idRegla);
            _reglaRepositorio.Eliminar(idRegla);
        }

        public List<AutomationTriggerResult> Evaluar(int idDashboard, string liveTag, double value)
        {
            var results = new List<AutomationTriggerResult>();
            if (idDashboard <= 0 || string.IsNullOrWhiteSpace(liveTag))
            {
                return results;
            }

            foreach (var regla in _reglaRepositorio.ObtenerPorDashboard(idDashboard).Where(r => r.Activa))
            {
                foreach (var condicion in _condicionRepositorio.ObtenerPorRegla(regla.IdRegla))
                {
                    var tag = _tagRepositorio.ObtenerPorId(condicion.IdTag);
                    if (tag == null || !TagMatches(liveTag, tag.Nombre))
                    {
                        continue;
                    }

                    if (!ConditionMatches(value, condicion.Operador, condicion.ValorUmbral))
                    {
                        continue;
                    }

                    var actions = _accionRepositorio.ObtenerPorRegla(regla.IdRegla);
                    var severity = ResolveSeverity(actions);
                    var description = regla.Nombre + ": " + liveTag + " = "
                        + value.ToString("0.0", CultureInfo.InvariantCulture)
                        + " " + condicion.Operador + " "
                        + condicion.ValorUmbral.ToString("0.0", CultureInfo.InvariantCulture)
                        + " [" + severity + "]";

                    if (CanPersistEvent(regla.IdRegla))
                    {
                        _eventoRepositorio.Insertar(new HistorialEvento
                        {
                            Descripcion = description,
                            ValorDetectado = value,
                            FechaEvento = DateTime.Now,
                            IdRegla = regla.IdRegla,
                            IdMotor = null
                        });
                    }

                    results.Add(new AutomationTriggerResult
                    {
                        RuleId = regla.IdRegla,
                        RuleName = regla.Nombre,
                        Description = description,
                        Tag = liveTag,
                        Value = value,
                        Severity = severity,
                        Color = ColorParaSeveridad(severity)
                    });
                }
            }

            return results;
        }

        private bool CanPersistEvent(int idRegla)
        {
            DateTime last;
            if (_lastEventByRule.TryGetValue(idRegla, out last) && DateTime.Now - last < EventCooldown)
            {
                return false;
            }

            _lastEventByRule[idRegla] = DateTime.Now;
            return true;
        }

        private static bool TagMatches(string liveTag, string dbTagName)
        {
            return string.Equals(liveTag, dbTagName, StringComparison.OrdinalIgnoreCase)
                || liveTag.EndsWith("." + dbTagName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ConditionMatches(double value, string operador, double threshold)
        {
            switch ((operador ?? string.Empty).Trim())
            {
                case ">": return value > threshold;
                case "<": return value < threshold;
                case ">=": return value >= threshold;
                case "<=": return value <= threshold;
                case "=":
                case "==": return Math.Abs(value - threshold) < 0.0001;
                case "!=": return Math.Abs(value - threshold) >= 0.0001;
                default: return false;
            }
        }

        private static string ResolveSeverity(IEnumerable<Accion> actions)
        {
            var joined = string.Join(";", actions.Select(a => a.ParametrosJson ?? string.Empty)).ToLowerInvariant();
            if (joined.Contains("critica") || joined.Contains("critical")) return "Critica";
            if (joined.Contains("advertencia") || joined.Contains("warning")) return "Advertencia";
            return "Normal";
        }

        private static void ValidarDefinicion(AutomationRuleDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            if (definition.IdDashboard <= 0)
            {
                throw new InvalidOperationException("Guarda el dashboard antes de crear reglas.");
            }

            if (string.IsNullOrWhiteSpace(definition.Nombre))
            {
                throw new InvalidOperationException("Escribe un nombre para la regla.");
            }

            if (definition.IdTag <= 0)
            {
                throw new InvalidOperationException("Selecciona un sensor valido.");
            }

            if (string.IsNullOrWhiteSpace(definition.Operador))
            {
                throw new InvalidOperationException("Selecciona un operador.");
            }
        }

        private static void SetConditionValues(Condicion condicion, AutomationRuleDefinition definition)
        {
            condicion.IdTag = definition.IdTag;
            condicion.Operador = definition.Operador;
            condicion.ValorUmbral = definition.ValorUmbral;
        }

        private static string SerializarAccion(string severity)
        {
            return new JavaScriptSerializer().Serialize(new
            {
                severity = string.IsNullOrWhiteSpace(severity) ? "Normal" : severity,
                color = ColorParaSeveridad(severity)
            });
        }

        private static string SafeTrim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string ColorParaSeveridad(string severity)
        {
            if (string.Equals(severity, "Critica", StringComparison.OrdinalIgnoreCase)) return "#EF4444";
            if (string.Equals(severity, "Advertencia", StringComparison.OrdinalIgnoreCase)) return "#F59E0B";
            return "#22C55E";
        }
    }

    public class AutomationRuleDefinition
    {
        public int IdDashboard { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public bool Activa { get; set; }
        public int IdTag { get; set; }
        public string Operador { get; set; }
        public double ValorUmbral { get; set; }
        public string TipoAccion { get; set; }
        public string Severidad { get; set; }
    }

    public class AutomationRuleSummary
    {
        public int IdRegla { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public bool Activa { get; set; }
        public int IdTag { get; set; }
        public string Sensor { get; set; }
        public string Unidad { get; set; }
        public string Operador { get; set; }
        public double ValorUmbral { get; set; }
        public string Severidad { get; set; }
        public string Color { get; set; }
    }

    public class AutomationTriggerResult
    {
        public int RuleId { get; set; }
        public string RuleName { get; set; }
        public string Description { get; set; }
        public string Tag { get; set; }
        public double Value { get; set; }
        public string Severity { get; set; }
        public string Color { get; set; }
    }
}
