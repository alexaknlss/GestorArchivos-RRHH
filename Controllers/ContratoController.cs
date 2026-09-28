using GestorArchivos_RRHH.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GestorArchivos_RRHH.Controllers
{
    public class ContratoController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public ContratoController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        public IActionResult Index()
        {
            if (TempData["ArchivosGenerados"] != null)
            {
                string json = TempData["ArchivosGenerados"]!.ToString()!;
                List<string>? archivosGenerados = JsonSerializer.Deserialize<List<string>>(json);
                ViewBag.ArchivosGenerados = archivosGenerados;
                ViewBag.MensajeExito = TempData["MensajeExito"]?.ToString();
                ViewBag.CarpetaContratos = TempData["CarpetaContratos"]?.ToString();
            }

            ViewBag.Error = TempData["Error"]?.ToString();

            string carpetaDestino = Request.Cookies["CarpetaDestino"];
            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                carpetaDestino = _configuration["RutasArchivos:Contratos"];
                if (!string.IsNullOrWhiteSpace(carpetaDestino))
                {
                    CookieOptions options = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(365),
                        HttpOnly = true,
                        IsEssential = true
                    };
                    Response.Cookies.Append("CarpetaDestino", carpetaDestino, options);
                }
            }

            ViewBag.CarpetaDestino = carpetaDestino;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Procesar(
            IFormFile pdfContrato,
            IFormFile archivoExcel,
            string carpetaDestino = null)
        {
            if (pdfContrato == null || pdfContrato.Length == 0)
            {
                TempData["Error"] = "Debes seleccionar un archivo PDF.";
                return RedirectToAction(nameof(Index));
            }

            bool esPdf = pdfContrato.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                || pdfContrato.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

            if (!esPdf)
            {
                TempData["Error"] = "El archivo seleccionado debe ser un PDF.";
                return RedirectToAction(nameof(Index));
            }

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
                carpetaFinal = _configuration["RutasArchivos:Contratos"];
            }

            if (string.IsNullOrWhiteSpace(carpetaFinal))
            {
                TempData["Error"] = "No se encontró configurada la ruta de contratos en appsettings.json y no se especificó una carpeta.";
                return RedirectToAction(nameof(Index));
            }

            if (!Path.IsPathRooted(carpetaFinal))
            {
                carpetaFinal = Path.Combine(_env.ContentRootPath, carpetaFinal);
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
            Response.Cookies.Append("CarpetaDestino", carpetaFinal, options);

            string carpetaTemporal = Path.Combine(Path.GetTempPath(), "GestorArchivosRRHH", "Temporal");
            Directory.CreateDirectory(carpetaTemporal);
            string rutaPdfOriginal = Path.Combine(carpetaTemporal, $"{Guid.NewGuid()}.pdf");

            try
            {
                using (FileStream stream = new FileStream(rutaPdfOriginal, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await pdfContrato.CopyToAsync(stream);
                }

                PdfSplitService pdfSplitService = new PdfSplitService();
                ExcelCodeService excelCodeService = new ExcelCodeService();

                int cantidadPaginas = pdfSplitService.ObtenerCantidadPaginas(rutaPdfOriginal);
                List<string> codigos = excelCodeService.LeerCodigos(archivoExcel);

                if (codigos.Count == 0)
                {
                    TempData["Error"] = "El archivo Excel no contiene códigos.";
                    return RedirectToAction(nameof(Index));
                }

                if (codigos.Count != cantidadPaginas)
                {
                    TempData["Error"] =
                        $"El PDF contiene {cantidadPaginas} páginas, " +
                        $"pero el Excel contiene {codigos.Count} códigos. " +
                        $"La cantidad de códigos debe coincidir con la cantidad de páginas del PDF.";
                    return RedirectToAction(nameof(Index));
                }

                var resultado = pdfSplitService.DividirPdfConNombres(
                    rutaPdfOriginal,
                    carpetaFinal,
                    paginasPorDocumento: 1,
                    codigos: codigos
                );

                int cantidadGenerada = resultado.cantidadGenerada;
                List<string> archivosGenerados = resultado.nombresArchivos;

                archivosGenerados = archivosGenerados
                    .Where(nombre => System.IO.File.Exists(Path.Combine(carpetaFinal, nombre)))
                    .ToList();

                TempData["ArchivosGenerados"] = JsonSerializer.Serialize(archivosGenerados);
                TempData["MensajeExito"] = $"Proceso completado. Se generaron {cantidadGenerada} contratos.";
                TempData["CarpetaContratos"] = carpetaFinal;

                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", carpetaFinal);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"No se pudo abrir la carpeta: {ex.Message}");
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ocurrió un error al procesar los contratos: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
            finally
            {
                if (System.IO.File.Exists(rutaPdfOriginal))
                {
                    try
                    {
                        System.IO.File.Delete(rutaPdfOriginal);
                    }
                    catch { }
                }
            }
        }

        [HttpGet]
        public IActionResult Descargar(string nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return NotFound();
            }

            nombreArchivo = Path.GetFileName(nombreArchivo);

            string? carpetaContratos = _configuration["RutasArchivos:Contratos"];
            if (string.IsNullOrWhiteSpace(carpetaContratos))
            {
                return BadRequest("No está configurada la ruta de destino de contratos.");
            }

            if (!Path.IsPathRooted(carpetaContratos))
            {
                carpetaContratos = Path.Combine(_env.ContentRootPath, carpetaContratos);
            }

            string rutaArchivo = Path.Combine(carpetaContratos, nombreArchivo);

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