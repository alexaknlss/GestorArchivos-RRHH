using GestorArchivos_RRHH.Services;
using Microsoft.AspNetCore.Mvc;

namespace GestorArchivos_RRHH.Controllers
{
    public class IncapacidadController : Controller
    {
        // GET: Incapacidad
        public IActionResult Index()
        {
            return View();
        }


        // =========================================
        // PROCESAR INCAPACIDADES
        // =========================================

        [HttpPost]
        public async Task<IActionResult> Procesar(
            IFormFile pdfIncapacidad
        )
        {
            if (pdfIncapacidad == null ||
                pdfIncapacidad.Length == 0)
            {
                ViewBag.Error =
                    "Debes seleccionar un archivo PDF.";

                return View("Index");
            }


            bool esPdf =
                pdfIncapacidad.ContentType.Equals(
                    "application/pdf",
                    StringComparison.OrdinalIgnoreCase
                )
                ||
                pdfIncapacidad.FileName.EndsWith(
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase
                );


            if (!esPdf)
            {
                ViewBag.Error =
                    "El archivo seleccionado debe ser un PDF.";

                return View("Index");
            }


            // Carpeta temporal para el PDF original
            string carpetaTemporal =
                Path.Combine(
                    Path.GetTempPath(),
                    "GestorArchivosRRHH",
                    "Temporal"
                );


            Directory.CreateDirectory(
                carpetaTemporal
            );


            string rutaPdfOriginal =
                Path.Combine(
                    carpetaTemporal,
                    $"{Guid.NewGuid()}.pdf"
                );


            /*
             * Las incapacidades individuales
             * se generan temporalmente aquí.
             */
            string carpetaIncapacidades =
                Path.Combine(
                    Path.GetTempPath(),
                    "GestorArchivosRRHH",
                    "Incapacidades"
                );


            Directory.CreateDirectory(
                carpetaIncapacidades
            );


            try
            {
                // Guardar temporalmente el PDF subido
                using (
                    FileStream stream =
                        new FileStream(
                            rutaPdfOriginal,
                            FileMode.Create
                        )
                )
                {
                    await pdfIncapacidad.CopyToAsync(
                        stream
                    );
                }


                /*
                 * INCAPACIDADES:
                 *
                 * 1 página = 1 PDF individual
                 */
                PdfSplitService pdfSplitService =
                    new PdfSplitService();


                int cantidadGenerada =
                    pdfSplitService.DividirPdf(
                        rutaPdfOriginal,
                        carpetaIncapacidades,
                        paginasPorDocumento: 1,
                        prefijoArchivo: "Incapacidad"
                    );


                // Obtener nombres de los archivos generados
                List<string> archivosGenerados =
                    Directory
                        .GetFiles(
                            carpetaIncapacidades,
                            "Incapacidad_*.pdf"
                        )
                        .Select(
                            ruta =>
                                Path.GetFileName(ruta)
                        )
                        .OrderBy(
                            nombre => nombre
                        )
                        .ToList();


                ViewBag.ArchivosGenerados =
                    archivosGenerados;


                ViewBag.MensajeExito =
                    $"Proceso completado correctamente. " +
                    $"Se generaron {cantidadGenerada} incapacidades.";


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
                // Eliminar únicamente el PDF grande temporal
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


        // =========================================
        // DESCARGAR INCAPACIDAD
        // =========================================

        [HttpGet]
        public IActionResult Descargar(
            string nombreArchivo
        )
        {
            if (
                string.IsNullOrWhiteSpace(
                    nombreArchivo
                )
            )
            {
                return NotFound();
            }


            // Protección contra rutas externas
            nombreArchivo =
                Path.GetFileName(
                    nombreArchivo
                );


            string carpetaIncapacidades =
                Path.Combine(
                    Path.GetTempPath(),
                    "GestorArchivosRRHH",
                    "Incapacidades"
                );


            string rutaArchivo =
                Path.Combine(
                    carpetaIncapacidades,
                    nombreArchivo
                );


            if (
                !System.IO.File.Exists(
                    rutaArchivo
                )
            )
            {
                return NotFound();
            }


            byte[] contenido =
                System.IO.File.ReadAllBytes(
                    rutaArchivo
                );


            /*
             * Después de enviar el PDF al navegador,
             * eliminar la copia temporal.
             */
            Response.OnCompleted(
                () =>
                {
                    try
                    {
                        if (
                            System.IO.File.Exists(
                                rutaArchivo
                            )
                        )
                        {
                            System.IO.File.Delete(
                                rutaArchivo
                            );
                        }
                    }
                    catch
                    {
                        // No interrumpir la descarga
                        // si falla la limpieza temporal.
                    }


                    return Task.CompletedTask;
                }
            );


            return File(
                contenido,
                "application/pdf",
                nombreArchivo
            );
        }
    }
}