using Microsoft.AspNetCore.Mvc;

namespace GestorArchivos_RRHH.Controllers
{
    public class IncapacidadRevisionController : Controller
    {
        private readonly IConfiguration _configuration;

        public IncapacidadRevisionController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // get history of folders (INICIO)
        private List<string> ObtenerHistorialCarpetas()
        {
            string historial = Request.Cookies["HistorialCarpetasIncapacidades"] ?? "";
            return historial.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private void GuardarHistorialCarpetas(List<string> carpetas)
        {
            string historial = string.Join("|", carpetas.Distinct());
            CookieOptions options = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(365),
                HttpOnly = true,
                IsEssential = true
            };
            Response.Cookies.Append("HistorialCarpetasIncapacidades", historial, options);
        }

        private void AgregarCarpetaAlHistorial(string carpeta)
        {
            if (string.IsNullOrWhiteSpace(carpeta)) return;
            var carpetas = ObtenerHistorialCarpetas();
            if (!carpetas.Contains(carpeta))
            {
                carpetas.Add(carpeta);
                GuardarHistorialCarpetas(carpetas);
            }
        }
        // get history of folders (FIN)
        public IActionResult Index(string nombreArchivo = null, string carpetaOrigen = null)
        {
            // get history of folders
            var carpetas = ObtenerHistorialCarpetas();

            if (carpetas.Count == 0)
            {
                string carpetaActual = Request.Cookies["CarpetaDestinoIncapacidades"];
                if (string.IsNullOrWhiteSpace(carpetaActual))
                {
                    carpetaActual = _configuration["RutasArchivos:Incapacidades"];
                }
                if (!string.IsNullOrWhiteSpace(carpetaActual) && Directory.Exists(carpetaActual))
                {
                    AgregarCarpetaAlHistorial(carpetaActual);
                    carpetas = ObtenerHistorialCarpetas();
                }
                else
                {
                    ViewBag.Error = "No se encontró ninguna carpeta de incapacidades. Genera incapacidades primero.";
                    ViewBag.TodosLosArchivos = new List<(string Nombre, string Carpeta)>();
                    return View();
                }
            }
            // date format for display
            var todosLosArchivos = new List<(string Nombre, string Carpeta)>();
            foreach (var carpeta in carpetas)
            {
                if (Directory.Exists(carpeta))
                {
                    var archivos = Directory.GetFiles(carpeta, "*.pdf")
                        .Select(Path.GetFileName)
                        .Where(n => !string.IsNullOrEmpty(n));
                    foreach (var archivo in archivos)
                    {
                        todosLosArchivos.Add((archivo, carpeta));
                    }
                }
            }

            // order for name 
            todosLosArchivos = todosLosArchivos.OrderBy(a => a.Nombre).ToList();

            // if selected file is not in the list, reset selection
            if (!string.IsNullOrEmpty(nombreArchivo))
            {
                var seleccionado = todosLosArchivos.FirstOrDefault(a => a.Nombre == nombreArchivo);
                if (seleccionado != default)
                {
                    carpetaOrigen = seleccionado.Carpeta;
                }
                else
                {
                    nombreArchivo = null;
                    carpetaOrigen = null;
                }
            }

            // if not selected file, select the first one in the list
            if (string.IsNullOrEmpty(nombreArchivo) && todosLosArchivos.Any())
            {
                var primero = todosLosArchivos.First();
                nombreArchivo = primero.Nombre;
                carpetaOrigen = primero.Carpeta;
            }

            ViewBag.NombreActual = nombreArchivo;
            ViewBag.NuevoNombre = nombreArchivo != null ? Path.GetFileNameWithoutExtension(nombreArchivo) : "";
            ViewBag.CarpetaOrigen = carpetaOrigen;
            ViewBag.TodosLosArchivos = todosLosArchivos;
            ViewBag.MensajeExito = TempData["MensajeExito"]?.ToString();
            ViewBag.Error = TempData["Error"]?.ToString();


            // intent to get the destination folder from cookie
            string carpetaDestino = Request.Cookies["CarpetaDestinoRevision"];

            // if not found in cookie, try to get from the previous destination folder cookie
            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                carpetaDestino = Request.Cookies["CarpetaDestinoIncapacidades"];
            }

            // if not found in previus cookie, try to get from configuration
            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                carpetaDestino = _configuration["RutasArchivos:IncapacidadesDefinitivas"];
            }

            ViewBag.CarpetaDestino = carpetaDestino;
            // asing the destination folder to the cookie for future use

            return View();
        }

        // Get incapacidad revision ver pdf

        [HttpGet]
        public IActionResult VerPdf(string nombreArchivo, string carpetaOrigen)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo) || string.IsNullOrWhiteSpace(carpetaOrigen))
            {
                return NotFound();
            }

            if (!Directory.Exists(carpetaOrigen))
            {
                return NotFound();
            }

            string rutaArchivo = Path.Combine(carpetaOrigen, Path.GetFileName(nombreArchivo));

            if (!System.IO.File.Exists(rutaArchivo))
            {
                return NotFound();
            }

            var fileBytes = System.IO.File.ReadAllBytes(rutaArchivo);
            Response.Headers.Add("Content-Disposition", $"inline; filename=\"{Path.GetFileName(nombreArchivo)}\"");
            return File(fileBytes, "application/pdf");
        }

        [HttpPost]
        public IActionResult Guardar(string nombreOriginal, string nuevoNombre, string carpetaOrigen, string carpetaDestino)
        {
            if (string.IsNullOrWhiteSpace(nombreOriginal))
            {
                TempData["Error"] = "No se especificó el archivo original.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(nuevoNombre))
            {
                TempData["Error"] = "Debes ingresar un nombre para el archivo.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(carpetaOrigen))
            {
                TempData["Error"] = "No se especificó la carpeta de origen.";
                return RedirectToAction(nameof(Index));
            }

            char[] caracteresInvalidos = Path.GetInvalidFileNameChars();
            if (nuevoNombre.IndexOfAny(caracteresInvalidos) >= 0)
            {
                TempData["Error"] = "El nombre contiene caracteres no permitidos.";
                return RedirectToAction(nameof(Index));
            }

            // Save and persist the destination folder
            string carpetaFinal = carpetaDestino;
            if (string.IsNullOrWhiteSpace(carpetaFinal))
            {
                carpetaFinal = _configuration["RutasArchivos:IncapacidadesDefinitivas"];
            }

            if (string.IsNullOrWhiteSpace(carpetaFinal))
            {
                TempData["Error"] = "No se encontró configurada la carpeta definitiva.";
                return RedirectToAction(nameof(Index));
            }

            // save folder in cookie for future use
            CookieOptions options = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(365),
                HttpOnly = true,
                IsEssential = true
            };
            Response.Cookies.Append("CarpetaDestinoRevision", carpetaFinal, options);

            // Create the destination folder if it doesn't exist
            Directory.CreateDirectory(carpetaFinal);

            // url 
            string rutaOrigen = Path.Combine(carpetaOrigen, Path.GetFileName(nombreOriginal));

            if (!System.IO.File.Exists(rutaOrigen))
            {
                TempData["Error"] = $"El archivo {nombreOriginal} ya no existe en la carpeta de origen.";
                return RedirectToAction(nameof(Index));
            }

            // Construct the destination file path, ensuring no overwrite
            string nombreBase = nuevoNombre;
            string nombreArchivoDestino = $"{nombreBase}.pdf";
            string rutaDestino = Path.Combine(carpetaFinal, nombreArchivoDestino);

            int contador = 1;
            while (System.IO.File.Exists(rutaDestino))
            {
                nombreArchivoDestino = $"{nombreBase} ({contador}).pdf";
                rutaDestino = Path.Combine(carpetaFinal, nombreArchivoDestino);
                contador++;
            }

            try
            {
                // Copy the file to the destination
                System.IO.File.Copy(rutaOrigen, rutaDestino);

                // delete original file with retry logic
                bool eliminado = false;
                for (int i = 0; i < 5; i++) // Restart 5 times
                {
                    try
                    {
                        System.IO.File.Delete(rutaOrigen);
                        eliminado = true;
                        break;
                    }
                    catch
                    {
                        if (i == 4) 
                            throw; 
                        System.Threading.Thread.Sleep(500); 
                    }
                }

                // Verfiy if the file was deleted, if not, check if it still exists
                if (!eliminado && !System.IO.File.Exists(rutaOrigen))
                {
                    eliminado = true;
                }

                if (!eliminado)
                {
                    TempData["Error"] = "El archivo se copió correctamente, pero no se pudo eliminar el original. Cierra el visor PDF y prueba de nuevo.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["MensajeExito"] = $" Archivo renombrado y movido: {nombreArchivoDestino}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al procesar el archivo: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

       

        [HttpPost]
        public IActionResult Eliminar(string nombreArchivo, string carpetaOrigen)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                TempData["Error"] = "No se especificó ningún archivo.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(carpetaOrigen))
            {
                TempData["Error"] = "No se especificó la carpeta de origen.";
                return RedirectToAction(nameof(Index));
            }

            if (!Directory.Exists(carpetaOrigen))
            {
                TempData["Error"] = "La carpeta de origen no existe.";
                return RedirectToAction(nameof(Index));
            }

            string rutaArchivo = Path.Combine(carpetaOrigen, Path.GetFileName(nombreArchivo));

            if (!System.IO.File.Exists(rutaArchivo))
            {
                TempData["Error"] = $"El archivo {nombreArchivo} no existe.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                System.IO.File.Delete(rutaArchivo);
                TempData["MensajeExito"] = $" Archivo {nombreArchivo} eliminado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar el archivo: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        

        [HttpPost]
        public IActionResult LimpiarHistorial()
        {
            CookieOptions options = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(-1),
                HttpOnly = true,
                IsEssential = true
            };
            Response.Cookies.Append("HistorialCarpetasIncapacidades", "", options);
            TempData["MensajeExito"] = "Historial de carpetas limpiado.";
            return RedirectToAction(nameof(Index));
        }
    }
}