using GestorArchivos_RRHH.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GestorArchivos_RRHH.Controllers
{
    public class IncapacidadController : Controller
    {
        // ============================================================
        // Configuración
        // ============================================================

        private readonly IConfiguration _configuration;
        private readonly PdfSplitService _pdfSplitService;

        // Regla de negocio: 1 página del PDF = 1 incapacidad
        private const int PAGINAS_POR_INCAPACIDAD = 1;

        public IncapacidadController(IConfiguration configuration)
        {
            _configuration = configuration;
            _pdfSplitService = new PdfSplitService();
        }


        // ============================================================
        // GET: INCAPACIDAD/INDEX
        // ============================================================

        public IActionResult Index()
        {
            if (TempData["ArchivosGenerados"] != null)
            {
                string json = TempData["ArchivosGenerados"]!.ToString()!;
                List<string>? archivosGenerados = JsonSerializer.Deserialize<List<string>>(json);

                ViewBag.ArchivosGenerados = archivosGenerados;
                ViewBag.MensajeExito = TempData["MensajeExito"]?.ToString();
                ViewBag.CarpetaIncapacidades = TempData["CarpetaIncapacidades"]?.ToString();
            }
            else
            {
                ViewBag.ArchivosGenerados = null;
                ViewBag.MensajeExito = null;
                ViewBag.CarpetaIncapacidades = null;
            }

            ViewBag.Error = TempData["Error"]?.ToString();

            string? carpetaDestino = Request.Cookies["CarpetaDestinoIncapacidades"];

            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                carpetaDestino = _configuration["RutasArchivos:Incapacidades"];

                if (!string.IsNullOrWhiteSpace(carpetaDestino))
                {
                    CookieOptions options = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(365),
                        HttpOnly = true,
                        IsEssential = true
                    };
                    Response.Cookies.Append("CarpetaDestinoIncapacidades", carpetaDestino, options);
                }
            }

            ViewBag.CarpetaDestino = carpetaDestino;

            return View();
        }


        // ============================================================
        // POST: INCAPACIDAD/PROCESAR
        // Divide el PDF y genera una incapacidad por cada código
        // ============================================================

        [HttpPost]
        public async Task<IActionResult> Procesar(
            IFormFile pdfIncapacidad,
            IFormFile archivoExcel,
            string? carpetaDestino = null)
        {
            // ---------- Validar PDF ----------
            if (pdfIncapacidad == null || pdfIncapacidad.Length == 0)
            {
                TempData["Error"] = "Debes seleccionar un archivo PDF.";
                return RedirectToAction(nameof(Index));
            }

            bool esPdf = pdfIncapacidad.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                || pdfIncapacidad.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

            if (!esPdf)
            {
                TempData["Error"] = "El archivo seleccionado debe ser un PDF.";
                return RedirectToAction(nameof(Index));
            }

            // ---------- Validar Excel ----------
            if (archivoExcel == null || archivoExcel.Length == 0)
            {
                TempData["Error"] = "Debes seleccionar un archivo Excel con los códigos.";
                return RedirectToAction(nameof(Index));
            }

            string extensionExcel = Path.GetExtension(archivoExcel.FileName).ToLowerInvariant();

            if (extensionExcel != ".xlsx")
            {
                TempData["Error"] = "El archivo de códigos debe ser un Excel .xlsx.";
                return RedirectToAction(nameof(Index));
            }

            // ---------- Resolver carpeta destino ----------
            string? carpetaFinal = carpetaDestino;

            if (string.IsNullOrWhiteSpace(carpetaFinal))
                carpetaFinal = _configuration["RutasArchivos:Incapacidades"];

            if (string.IsNullOrWhiteSpace(carpetaFinal))
            {
                TempData["Error"] = "No se encontró configurada la ruta de incapacidades en appsettings.json y no se especificó una carpeta.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                Directory.CreateDirectory(carpetaFinal);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se puede acceder a la carpeta: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }

            // ---------- Guardar preferencias ----------
            CookieOptions options = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(365),
                HttpOnly = true,
                IsEssential = true
            };
            Response.Cookies.Append("CarpetaDestinoIncapacidades", carpetaFinal, options);

            string historial = Request.Cookies["HistorialCarpetasIncapacidades"] ?? "";
            var carpetas = historial.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (!carpetas.Contains(carpetaFinal))
            {
                carpetas.Add(carpetaFinal);
                string nuevoHistorial = string.Join("|", carpetas);

                CookieOptions historialOptions = new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(365),
                    HttpOnly = true,
                    IsEssential = true
                };
                Response.Cookies.Append("HistorialCarpetasIncapacidades", nuevoHistorial, historialOptions);
            }

            // ---------- Carpeta temporal ----------
            string carpetaTemporal = Path.Combine(Path.GetTempPath(), "GestorArchivosRRHH", "Temporal");
            Directory.CreateDirectory(carpetaTemporal);

            string rutaPdfOriginal = Path.Combine(carpetaTemporal, $"{Guid.NewGuid()}.pdf");

            try
            {
                // Guardar PDF temporal
                using (FileStream stream = new FileStream(rutaPdfOriginal, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await pdfIncapacidad.CopyToAsync(stream);
                }

                // Leer códigos y contar páginas
                ExcelCodeService excelCodeService = new ExcelCodeService();
                int cantidadPaginas = _pdfSplitService.ObtenerCantidadPaginas(rutaPdfOriginal);
                List<string> codigos = excelCodeService.LeerCodigos(archivoExcel);

                if (codigos.Count == 0)
                {
                    TempData["Error"] = "El archivo Excel no contiene códigos.";
                    return RedirectToAction(nameof(Index));
                }

                // ---------- Validar páginas completas ----------
                if (cantidadPaginas % PAGINAS_POR_INCAPACIDAD != 0)
                {
                    int paginasSobrantes = cantidadPaginas % PAGINAS_POR_INCAPACIDAD;

                    TempData["Error"] =
                        $"El PDF contiene {cantidadPaginas} páginas. " +
                        $"Cada incapacidad debe tener {PAGINAS_POR_INCAPACIDAD} páginas. " +
                        $"Quedan {paginasSobrantes} página(s) sin completar.";
                    return RedirectToAction(nameof(Index));
                }

                int cantidadIncapacidades = cantidadPaginas / PAGINAS_POR_INCAPACIDAD;

                // ---------- Validar códigos vs incapacidades ----------
                if (codigos.Count != cantidadIncapacidades)
                {
                    TempData["Error"] =
                        $"El PDF generará {cantidadIncapacidades} incapacidad(es) " +
                        $"(con {PAGINAS_POR_INCAPACIDAD} página(s) cada una), " +
                        $"pero el Excel contiene {codigos.Count} código(s). " +
                        $"La cantidad de códigos debe coincidir con la cantidad de incapacidades.";
                    return RedirectToAction(nameof(Index));
                }

                // ---------- Dividir el PDF ----------
                var resultado = _pdfSplitService.DividirPdfIncapacidades(
                    rutaPdfOriginal,
                    carpetaFinal,
                    paginasPorDocumento: PAGINAS_POR_INCAPACIDAD,
                    codigos: codigos
                );

                int cantidadGenerada = resultado.cantidadGenerada;
                List<string> archivosGenerados = resultado.nombresArchivos
                    .Where(nombre => System.IO.File.Exists(Path.Combine(carpetaFinal, nombre)))
                    .ToList();

                // ---------- Guardar resultado ----------
                TempData["ArchivosGenerados"] = JsonSerializer.Serialize(archivosGenerados);
                TempData["MensajeExito"] = $"Proceso completado. Se generaron {cantidadGenerada} incapacidades.";
                TempData["CarpetaIncapacidades"] = carpetaFinal;

                // Abrir carpeta automáticamente (solo Windows)
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", carpetaFinal);
                }
                catch
                {
                    // Si falla, no interrumpe
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ocurrió un error al procesar las incapacidades: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
            finally
            {
                // Borrar PDF temporal
                if (System.IO.File.Exists(rutaPdfOriginal))
                {
                    try
                    {
                        System.IO.File.Delete(rutaPdfOriginal);
                    }
                    catch
                    {
                        // No interrumpe
                    }
                }
            }
        }


        // ============================================================
        // GET: INCAPACIDAD/DESCARGAR
        // Descarga un PDF por nombre
        // ============================================================

        [HttpGet]
        public IActionResult Descargar(string nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return NotFound();
            }

            // Protección contra path traversal
            nombreArchivo = Path.GetFileName(nombreArchivo);

            // 1) Cookie → 2) appsettings
            string? carpetaIncapacidades = Request.Cookies["CarpetaDestinoIncapacidades"];

            if (string.IsNullOrWhiteSpace(carpetaIncapacidades))
            {
                carpetaIncapacidades = _configuration["RutasArchivos:Incapacidades"];
            }

            if (string.IsNullOrWhiteSpace(carpetaIncapacidades))
            {
                return BadRequest("No está configurada la ruta de destino de incapacidades.");
            }

            string rutaArchivo = Path.Combine(carpetaIncapacidades, nombreArchivo);

            if (!System.IO.File.Exists(rutaArchivo))
            {
                return NotFound($"No se encontró el archivo: {nombreArchivo}");
            }

            return PhysicalFile(
                rutaArchivo,
                "application/pdf",
                nombreArchivo
            );
        }
    }
}