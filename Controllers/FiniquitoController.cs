using GestorArchivos_RRHH.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GestorArchivos_RRHH.Controllers
{
    public class FiniquitoController : Controller
    {

        // Settings


        private readonly IConfiguration _configuration;
        private readonly PdfSplitService _pdfSplitService;

        public FiniquitoController(IConfiguration configuration)
        {
            _configuration = configuration;
            _pdfSplitService = new PdfSplitService();
        }



        // get finiquitos index


        public IActionResult Index()
        {
            // Recibir resultados del procesamiento
            if (TempData["ArchivosGenerados"] != null)
            {
                string json = TempData["ArchivosGenerados"]!.ToString()!;
                List<string>? archivosGenerados = JsonSerializer.Deserialize<List<string>>(json);

                ViewBag.ArchivosGenerados = archivosGenerados;
                ViewBag.MensajeExito = TempData["MensajeExito"]?.ToString();
                ViewBag.CarpetaFiniquitos = TempData["CarpetaFiniquitos"]?.ToString();
            }
            else
            {
                ViewBag.ArchivosGenerados = null;
                ViewBag.MensajeExito = null;
                ViewBag.CarpetaFiniquitos = null;
            }

            // Mensajes de error
            ViewBag.Error = TempData["Error"]?.ToString();

           
            string carpetaDestino = Request.Cookies["CarpetaDestinoFiniquitos"];

            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                carpetaDestino = _configuration["RutasArchivos:Finiquitos"];
                if (!string.IsNullOrWhiteSpace(carpetaDestino))
                {
                    CookieOptions options = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(365),
                        HttpOnly = true,
                        IsEssential = true
                    };
                    Response.Cookies.Append("CarpetaDestinoFiniquitos", carpetaDestino, options);
                }
            }

            ViewBag.CarpetaDestino = carpetaDestino;
          
            return View();
        }


        // post finiquitos 

        [HttpPost]
        public async Task<IActionResult> Procesar(
            IFormFile pdfFiniquito,
            IFormFile archivoExcel,
            string carpetaDestino = null) 
        {
            // Validate PDF

            if (pdfFiniquito == null || pdfFiniquito.Length == 0)
            {
                TempData["Error"] = "Debes seleccionar un archivo PDF.";
                return RedirectToAction(nameof(Index));
            }

            bool esPdf = pdfFiniquito.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                || pdfFiniquito.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

            if (!esPdf)
            {
                TempData["Error"] = "El archivo seleccionado debe ser un PDF.";
                return RedirectToAction(nameof(Index));
            }

            // Validate Excel file

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
                carpetaFinal = _configuration["RutasArchivos:Finiquitos"];
            }

            if (string.IsNullOrWhiteSpace(carpetaFinal))
            {
                TempData["Error"] = "No se encontró configurada la ruta de finiquitos en appsettings.json y no se especificó una carpeta.";
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
            Response.Cookies.Append("CarpetaDestinoFiniquitos", carpetaFinal, options);
           
            // file temporal path

            string carpetaTemporal = Path.Combine(Path.GetTempPath(), "GestorArchivosRRHH", "Temporal");
            Directory.CreateDirectory(carpetaTemporal);

            string rutaPdfOriginal = Path.Combine(carpetaTemporal, $"{Guid.NewGuid()}.pdf");

            try
            {
                // Guardar PDF temporal
                using (FileStream stream = new FileStream(rutaPdfOriginal, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await pdfFiniquito.CopyToAsync(stream);
                }


                ExcelCodeService excelCodeService = new ExcelCodeService();
                int cantidadPaginas = _pdfSplitService.ObtenerCantidadPaginas(rutaPdfOriginal);
                List<string> codigos = excelCodeService.LeerCodigos(archivoExcel);

                if (codigos.Count == 0)
                {
                    TempData["Error"] = "El archivo Excel no contiene códigos.";
                    return RedirectToAction(nameof(Index));
                }


                int paginasPorDocumento = 3;
                int cantidadDocumentosEsperados = cantidadPaginas / paginasPorDocumento;

                if (cantidadPaginas % paginasPorDocumento != 0)
                {
                    TempData["Error"] =
                        $"El PDF contiene {cantidadPaginas} páginas. " +
                        $"Los finiquitos deben tener grupos exactos de {paginasPorDocumento} páginas. " +
                        $"Quedan {cantidadPaginas % paginasPorDocumento} página(s) sin completar.";
                    return RedirectToAction(nameof(Index));
                }

                if (codigos.Count != cantidadDocumentosEsperados)
                {
                    TempData["Error"] =
                        $"El PDF generará {cantidadDocumentosEsperados} finiquitos " +
                        $"({paginasPorDocumento} páginas cada uno), " +
                        $"pero el Excel contiene {codigos.Count} códigos.";
                    return RedirectToAction(nameof(Index));
                }



                var resultado = _pdfSplitService.DividirPdfFiniquitos(
                    rutaPdfOriginal,
                    carpetaFinal, 
                    paginasPorDocumento: 3,
                    codigos: codigos
                );

                int cantidadGenerada = resultado.cantidadGenerada;
                List<string> archivosGenerados = resultado.nombresArchivos;

                // Verificar que los archivos existen
                archivosGenerados = archivosGenerados
                    .Where(nombre => System.IO.File.Exists(Path.Combine(carpetaFinal, nombre))) // ========================================= CAMBIO (INICIO) =========================================
                    .ToList();

                //Save result

                TempData["ArchivosGenerados"] = JsonSerializer.Serialize(archivosGenerados);
                TempData["MensajeExito"] = $" Proceso completado. Se generaron {cantidadGenerada} finiquitos.";
                TempData["CarpetaFiniquitos"] = carpetaFinal; 
                // Abrir carpeta automáticamente
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", carpetaFinal); // ========================================= CAMBIO (INICIO) =========================================
                }
                catch
                {
                    // No importa si falla
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ocurrió un error al procesar los finiquitos: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
            finally
            {
                // Eliminar PDF temporal
                if (System.IO.File.Exists(rutaPdfOriginal))
                {
                    try
                    {
                        System.IO.File.Delete(rutaPdfOriginal);
                    }
                    catch
                    {
                       
                    }
                }
            }
        }


        // get finiquito download

        [HttpGet]
        public IActionResult Descargar(string nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return NotFound();
            }

            nombreArchivo = Path.GetFileName(nombreArchivo);

            string? carpetaFiniquitos = _configuration["RutasArchivos:Finiquitos"];

            if (string.IsNullOrWhiteSpace(carpetaFiniquitos))
            {
                return BadRequest("No está configurada la ruta de destino de finiquitos.");
            }

            string rutaArchivo = Path.Combine(carpetaFiniquitos, nombreArchivo);

            if (!System.IO.File.Exists(rutaArchivo))
            {
                return NotFound($"No se encontró el archivo: {nombreArchivo}");
            }

            // El archivo permanece en la carpeta después de la descarga
            return PhysicalFile(
                rutaArchivo,
                "application/pdf",
                nombreArchivo
            );
        }
    }
}