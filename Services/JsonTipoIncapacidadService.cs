using System.Text.Json;
using GestorArchivos_RRHH.Models;

namespace GestorArchivos_RRHH.Services
{
    public class JsonTipoIncapacidadService : ITipoIncapacidadService
    {
        private readonly string _rutaArchivo;
        private readonly object _lock = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Valores por defecto que se crean la primera vez que arranca la app
        private static readonly List<TipoIncapacidad> _defaults = new()
        {
            new() { Valor = "Severance pay advancement",        Texto = "Severance pay advancement" },
            new() { Valor = "Medical leave (Restricted)", Texto = "Medical leave (Restricted)" },
            new() { Valor = "Maternity Leave",    Texto = "Maternity Leave" },
            new() { Valor = "Leave & time off",  Texto = "Leave & time off" },
            new() { Valor = "Good Behaviour Certificate",Texto = "Good Behaviour Certificate" },
            new() { Valor = "Job Application",   Texto = "Job Application" },
            new() { Valor = "Personal ID", Texto = "Personal ID" },
            new() { Valor = "Internal and EHS rules", Texto = "Internal and EHS rules" },
            new() { Valor = "Code of conduct certificate", Texto = "Code of conduct certificate" },
            new() { Valor = "Confidentiality agreement", Texto = "Confidentiality agreement" },
            new() { Valor = "Warning letter", Texto = "Warning letter" },
            new() { Valor = "Involuntary termination letter", Texto = "Involuntary termination letter" },
            new() { Valor = "Voluntary termination letter", Texto = "Voluntary termination letter" },
            new() { Valor = "Promotion letter", Texto = "Promotion letter" },
            new() { Valor = "Residence Permit", Texto = "Residence Permit" }
        };

        public JsonTipoIncapacidadService(IWebHostEnvironment env)
        {
            // ruta de el json 
            _rutaArchivo = Path.Combine(env.ContentRootPath, "tipos-incapacidad.json");

            // si no existe se crea
            if (!File.Exists(_rutaArchivo))
                Guardar(_defaults);
        }

        // leemos

        public List<TipoIncapacidad> ObtenerTipos()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_rutaArchivo))
                        return new List<TipoIncapacidad>(_defaults);

                    string json = File.ReadAllText(_rutaArchivo);

                    return JsonSerializer.Deserialize<List<TipoIncapacidad>>(json, _jsonOptions)
                           ?? new List<TipoIncapacidad>();
                }
                catch
                {
                    // si el json falla no daña nada
                    return new List<TipoIncapacidad>(_defaults);
                }
            }
        }

        public TipoIncapacidad? ObtenerPorValor(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;

            return ObtenerTipos()
                .FirstOrDefault(t => t.Valor.Equals(valor, StringComparison.OrdinalIgnoreCase));
        }

        // constructos para agregar 

        public bool Agregar(TipoIncapacidad tipo, out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(tipo.Valor) || string.IsNullOrWhiteSpace(tipo.Texto))
            {
                error = "El valor y el texto son obligatorios.";
                return false;
            }

            if (tipo.Valor.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "El valor no puede contener caracteres inválidos (\\ / : * ? \" < > |).";
                return false;
            }

            lock (_lock)
            {
                var lista = ObtenerTipos();

                if (lista.Any(t => t.Valor.Equals(tipo.Valor, StringComparison.OrdinalIgnoreCase)))
                {
                    error = $"Ya existe un tipo con el valor '{tipo.Valor}'.";
                    return false;
                }

                lista.Add(tipo);
                Guardar(lista);   // guarda en el disco
                return true;
            }
        }

        // eliminamos 
        public bool Eliminar(string valor, out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(valor))
            {
                error = "Debes indicar el valor a eliminar.";
                return false;
            }

            lock (_lock)
            {
                var lista = ObtenerTipos();

                var item = lista.FirstOrDefault(t =>
                    t.Valor.Equals(valor, StringComparison.OrdinalIgnoreCase));

                if (item == null)
                {
                    error = $"No se encontró el tipo '{valor}'.";
                    return false;
                }

                // ayuda a que siempre haya al menos un tipo de incapacidad
                if (lista.Count <= 1)
                {
                    error = "Debe existir al menos un tipo de incapacidad.";
                    return false;
                }

                lista.Remove(item);
                Guardar(lista);   //guardamos
                return true;
            }
        }

        // guardar

        private void Guardar(List<TipoIncapacidad> lista)
        {
            string json = JsonSerializer.Serialize(lista, _jsonOptions);

            // Respaldo del anterior antes de sobrescribir
            if (File.Exists(_rutaArchivo))
            {
                try
                {
                    File.Copy(_rutaArchivo, _rutaArchivo + ".bak", overwrite: true);
                }
                catch
                {
                   
                }
            }

            // escritura temporal
            string temp = _rutaArchivo + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _rutaArchivo, overwrite: true);
        }
    }
}