using GestorArchivos_RRHH.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GestorArchivos_RRHH.Controllers
{
    public class ContratoController : Controller
    {

        // Settings


        private readonly IConfiguration _configuration;

        public ContratoController(IConfiguration configuration)
        {
            _configuration = configuration;
        }



        // get CONTRATO


        public IActionResult Index()
        {
            // Write result of  process 


            if (TempData["ArchivosGenerados"] != null)
            {
                string json = TempData["ArchivosGenerados"]!.ToString()!;

                List<string>? archivosGenerados = JsonSerializer.Deserialize<List<string>>(json);

                ViewBag.ArchivosGenerados = archivosGenerados;
                ViewBag.MensajeExito = TempData["MensajeExito"]?.ToString();
                ViewBag.CarpetaContratos = TempData["CarpetaContratos"]?.ToString();
            }


            // see error if exist


            ViewBag.Error = TempData["Error"]?.ToString();

            // read the destination folder from cookie or appsettings
            string carpetaDestino = Request.Cookies["CarpetaDestino"];

            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                // Si no hay cookie, usar la de appsettings
                carpetaDestino = _configuration["RutasArchivos:Contratos"];
                if (!string.IsNullOrWhiteSpace(carpetaDestino))
                {
                    // save cookie 
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



        // Procesing CONTRATOS


        [HttpPost]
        public async Task<IActionResult> Procesar(
            IFormFile pdfContrato,
            IFormFile archivoExcel,
            string carpetaDestino = null)  {
            // Validate PDF


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


            // Validate EXCEL


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

            try
            {
                Directory.CreateDirectory(carpetaFinal);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se puede acceder a la carpeta: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }

            // Guardar la carpeta en una cookie para que persista
            CookieOptions options = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(365),
                HttpOnly = true,
                IsEssential = true
            };
           
            // File TEMPORAL


            string carpetaTemporal = Path.Combine(Path.GetTempPath(), "GestorArchivosRRHH", "Temporal");
            Directory.CreateDirectory(carpetaTemporal);


            // genetare name temporal unic for PDF file


            string rutaPdfOriginal = Path.Combine(carpetaTemporal, $"{Guid.NewGuid()}.pdf");

            try
            {

                // Save PDF complet temporarily


                using (FileStream stream = new FileStream(rutaPdfOriginal, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await pdfContrato.CopyToAsync(stream);
                }


                // create services


                PdfSplitService pdfSplitService = new PdfSplitService();
                ExcelCodeService excelCodeService = new ExcelCodeService();


                // Count pages in PDF


                int cantidadPaginas = pdfSplitService.ObtenerCantidadPaginas(rutaPdfOriginal);


                // Exel codes read from file


                List<string> codigos = excelCodeService.LeerCodigos(archivoExcel);


                // validate if exist codes in EXCEL file


                if (codigos.Count == 0)
                {
                    TempData["Error"] = "El archivo Excel no contiene códigos.";
                    return RedirectToAction(nameof(Index));
                }


                // Validate if exist


                if (codigos.Count != cantidadPaginas)
                {
                    TempData["Error"] =
                        $"El PDF contiene {cantidadPaginas} páginas, " +
                        $"pero el Excel contiene {codigos.Count} códigos. " +
                        $"La cantidad de códigos debe coincidir con " +
                        $"la cantidad de páginas del PDF.";
                    return RedirectToAction(nameof(Index));
                }


                // divide pdf with names from EXCEL file

                var resultado = pdfSplitService.DividirPdfConNombres(
                    rutaPdfOriginal,
                    carpetaFinal, 
                    paginasPorDocumento: 1,
                    codigos: codigos
                );

                int cantidadGenerada = resultado.cantidadGenerada;
                List<string> archivosGenerados = resultado.nombresArchivos;


                // Verific all files exist in the destination folder. If any file is missing, it will be re moved from the list. 


                archivosGenerados = archivosGenerados
                    .Where(nombre => System.IO.File.Exists(Path.Combine(carpetaFinal, nombre))) // ========================================= CAMBIO (INICIO) =========================================
                    .ToList();


                // Save result 


                TempData["ArchivosGenerados"] = JsonSerializer.Serialize(archivosGenerados);
                TempData["MensajeExito"] = $" Proceso completado. Se generaron {cantidadGenerada} contratos.";
                TempData["CarpetaContratos"] = carpetaFinal; // ========================================= CAMBIO (INICIO) =========================================


                //Open folder 

                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", carpetaFinal); // ========================================= CAMBIO (INICIO) =========================================
                }
                catch (Exception ex)
                {
                    
                    Console.WriteLine($"No se pudo abrir la carpeta: {ex.Message}");
                }


                // redirect a index with result


                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {

                // See Errors


                TempData["Error"] = $"Ocurrió un error al procesar los contratos: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
            finally
            {

                // delete original PDF file

                if (System.IO.File.Exists(rutaPdfOriginal))
                {
                    try
                    {
                        System.IO.File.Delete(rutaPdfOriginal);
                    }
                    catch
                    {
                        // No interrumpir el proceso
                    }
                }
            }
        }



        // Dowload CONTRATO 
        [HttpGet]
        public IActionResult Descargar(string nombreArchivo)
        {

            // Validar name


            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return NotFound();
            }

            // Url no valid 


            nombreArchivo = Path.GetFileName(nombreArchivo);




            string? carpetaContratos = _configuration["RutasArchivos:Contratos"];

            if (string.IsNullOrWhiteSpace(carpetaContratos))
            {
                return BadRequest("No está configurada la ruta de destino de contratos.");
            }


            // CONSTRUIR RUTA DEL ARCHIVO


            string rutaArchivo = Path.Combine(carpetaContratos, nombreArchivo);


            // validate if existe file


            if (!System.IO.File.Exists(rutaArchivo))
            {
                return NotFound($"No se encontró el archivo: {nombreArchivo}");
            }


            // dowload result file 



            return PhysicalFile(
                rutaArchivo,
                "application/pdf",
                nombreArchivo
            );
        }
    }
}