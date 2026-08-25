using GestorArchivos_RRHH.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GestorArchivos_RRHH.Controllers
{
    public class IncapacidadController : Controller
    {
        // Setting 

        private readonly IConfiguration _configuration;
        private readonly PdfSplitService _pdfSplitService;

        public IncapacidadController(IConfiguration configuration)
        {
            _configuration = configuration;
            _pdfSplitService = new PdfSplitService();
        }

        // get INCAPACIDAD/INDEX

        public IActionResult Index()
        {
            // Recieve the list of generated files from TempData and pass it to the view
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

            // Mesage for errors
            ViewBag.Error = TempData["Error"]?.ToString();

            
            string carpetaDestino = Request.Cookies["CarpetaDestinoIncapacidades"];

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

        // Post procesing incapacidades

        [HttpPost]
        public async Task<IActionResult> Procesar(
            IFormFile pdfIncapacidad,
            IFormFile archivoExcel,
            string carpetaDestino = null) 
        {
            // Validate PDF

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

            // Validate  EXCEL

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

         
            string? carpetaFinal = carpetaDestino;

            if (string.IsNullOrWhiteSpace(carpetaFinal))
            {
                carpetaFinal = _configuration["RutasArchivos:Incapacidades"];
            }

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
           
            // tempora folder 

            string carpetaTemporal = Path.Combine(Path.GetTempPath(), "GestorArchivosRRHH", "Temporal");
            Directory.CreateDirectory(carpetaTemporal);

            string rutaPdfOriginal = Path.Combine(carpetaTemporal, $"{Guid.NewGuid()}.pdf");

            try
            {
                // save temporal folder 
                using (FileStream stream = new FileStream(rutaPdfOriginal, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await pdfIncapacidad.CopyToAsync(stream);
                }

                // Read codes 

                ExcelCodeService excelCodeService = new ExcelCodeService();
                int cantidadPaginas = _pdfSplitService.ObtenerCantidadPaginas(rutaPdfOriginal);
                List<string> codigos = excelCodeService.LeerCodigos(archivoExcel);

                if (codigos.Count == 0)
                {
                    TempData["Error"] = "El archivo Excel no contiene códigos.";
                    return RedirectToAction(nameof(Index));
                }

                // Validate that the number of codes 

                int paginasPorDocumento = 1;

                if (codigos.Count != cantidadPaginas)
                {
                    TempData["Error"] =
                        $"El PDF contiene {cantidadPaginas} páginas, " +
                        $"pero el Excel contiene {codigos.Count} códigos. " +
                        $"La cantidad de códigos debe coincidir con la cantidad de páginas del PDF.";
                    return RedirectToAction(nameof(Index));
                }

                // Divide the PDF into individual pages and save them with the corresponding codes
                var resultado = _pdfSplitService.DividirPdfIncapacidades(
                    rutaPdfOriginal,
                    carpetaFinal, 
                    paginasPorDocumento: 1,
                    codigos: codigos
                );

                int cantidadGenerada = resultado.cantidadGenerada;
                List<string> archivosGenerados = resultado.nombresArchivos;

                // Verificar que los archivos existen
                archivosGenerados = archivosGenerados
                    .Where(nombre => System.IO.File.Exists(Path.Combine(carpetaFinal, nombre))) // ========================================= CAMBIO (INICIO) =========================================
                    .ToList();

                // save result 

                TempData["ArchivosGenerados"] = JsonSerializer.Serialize(archivosGenerados);
                TempData["MensajeExito"] = $" Proceso completado. Se generaron {cantidadGenerada} incapacidades.";
                TempData["CarpetaIncapacidades"] = carpetaFinal; // ========================================= CAMBIO (INICIO) =========================================

                // Open folder automatically
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", carpetaFinal); // ========================================= CAMBIO (INICIO) =========================================
                }
                catch
                {
                    //if failed 
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
                // Delete pdf temporal
                if (System.IO.File.Exists(rutaPdfOriginal))
                {
                    try
                    {
                        System.IO.File.Delete(rutaPdfOriginal);
                    }
                    catch
                    {
                        //Not interrupted 
                    }
                }
            }
        }

        // Get pdf incapacidad by name

        [HttpGet]
        public IActionResult Descargar(string nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return NotFound();
            }

            nombreArchivo = Path.GetFileName(nombreArchivo);

            string? carpetaIncapacidades = _configuration["RutasArchivos:Incapacidades"];

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