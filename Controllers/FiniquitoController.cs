using GestorArchivos_RRHH.Services;
using Microsoft.AspNetCore.Mvc;

namespace GestorArchivos_RRHH.Controllers
{
    public class FiniquitoController : Controller
    {
        private readonly PdfSplitService _pdfSplitService;

        public FiniquitoController()
        {
            _pdfSplitService = new PdfSplitService();
        }

        // GET: Finiquito
        public IActionResult Index()
        {
            return View();
        }

        // POST: Finiquito/Procesar
        [HttpPost]
        public async Task<IActionResult> Procesar(IFormFile pdfFiniquito)
        {
            if (pdfFiniquito == null || pdfFiniquito.Length == 0)
            {
                TempData["Error"] =
                    "Debes seleccionar un archivo PDF.";

                return RedirectToAction(nameof(Index));
            }

            bool esPdf =
                pdfFiniquito.ContentType.Equals(
                    "application/pdf",
                    StringComparison.OrdinalIgnoreCase
                )
                ||
                pdfFiniquito.FileName.EndsWith(
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase
                );

            if (!esPdf)
            {
                TempData["Error"] =
                    "El archivo seleccionado debe ser un PDF.";

                return RedirectToAction(nameof(Index));
            }

            string carpetaTemporal =
                Path.Combine(
                    Path.GetTempPath(),
                    "GestorArchivosRRHH",
                    "Temporal"
                );

            Directory.CreateDirectory(
                carpetaTemporal
            );

            string nombreTemporal =
                $"{Guid.NewGuid()}.pdf";

            string rutaPdfOriginal =
                Path.Combine(
                    carpetaTemporal,
                    nombreTemporal
                );

            string carpetaDestino =
                Path.Combine(
                    Path.GetTempPath(),
                    "GestorArchivosRRHH",
                    "Finiquitos"
                );

            Directory.CreateDirectory(
                carpetaDestino
            );

            try
            {
                using (
                    FileStream stream =
                        new FileStream(
                            rutaPdfOriginal,
                            FileMode.Create
                        )
                )
                {
                    await pdfFiniquito.CopyToAsync(
                        stream
                    );
                }

                /*
                 * FINIQUITOS:
                 * 3 páginas = 1 PDF
                 */
                int cantidadGenerada =
                    _pdfSplitService.DividirPdf(
                        rutaPdfOriginal,
                        carpetaDestino,
                        paginasPorDocumento: 3,
                        prefijoArchivo: "Finiquito"
                    );

                /*
                 * Buscar todos los PDFs generados
                 * y preparar la lista para la vista.
                 */
                List<string> archivosGenerados =
                    Directory
                        .GetFiles(
                            carpetaDestino,
                            "Finiquito_*.pdf"
                        )
                        .Select(
                            ruta => Path.GetFileName(ruta)
                        )
                        .OrderBy(
                            nombre => nombre
                        )
                        .ToList();

                ViewBag.ArchivosGenerados =
                    archivosGenerados;

                ViewBag.CantidadGenerada =
                    cantidadGenerada;

                ViewBag.MensajeExito =
                    $"Proceso completado correctamente. " +
                    $"Se generaron {cantidadGenerada} finiquitos.";

                return View("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error =
                    ex.Message;

                return View("Index");
            }
            finally
            {
                if (
                    System.IO.File.Exists(
                        rutaPdfOriginal
                    )
                )
                {
                    System.IO.File.Delete(
                        rutaPdfOriginal
                    );
                }
            }
        }

        // GET: Finiquito/Descargar?nombreArchivo=Finiquito_0001.pdf
        [HttpGet]
        public IActionResult Descargar(
            string nombreArchivo
        )
        {
            if (string.IsNullOrWhiteSpace(
                nombreArchivo
            ))
            {
                return NotFound();
            }

            /*
             * Evita que alguien intente mandar
             * rutas completas o ../ desde la URL.
             */
            nombreArchivo =
                Path.GetFileName(
                    nombreArchivo
                );

            string carpetaDestino =
                Path.Combine(
                    Path.GetTempPath(),
                    "GestorArchivosRRHH",
                    "Finiquitos"
                );

            string rutaArchivo =
                Path.Combine(
                    carpetaDestino,
                    nombreArchivo
                );

            if (!System.IO.File.Exists(
                rutaArchivo
            ))
            {
                return NotFound();
            }

            byte[] contenido =
                System.IO.File.ReadAllBytes(
                    rutaArchivo
                );

            return File(
                contenido,
                "application/pdf",
                nombreArchivo
            );
        }
    }
}