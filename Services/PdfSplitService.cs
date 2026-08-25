using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace GestorArchivos_RRHH.Services
{
   
    /// Divide pdf en 1 y 3 docs soporta dos tipos de documentos: Contratos y Finiquitos
    
    public class PdfSplitService
    {
        //Cuenta el número total de páginas de un archivo PDF
        // so: Validar que el PDF tenga páginas antes de dividirlo

        public int ObtenerCantidadPaginas(string rutaPdfOriginal)
        {
            //validate Url

            if (string.IsNullOrWhiteSpace(rutaPdfOriginal))
            {
                throw new ArgumentException("La ruta del archivo PDF es obligatoria.");
            }

            //validate file

            if (!File.Exists(rutaPdfOriginal))
            {
                throw new FileNotFoundException("No se encontró el archivo PDF seleccionado.", rutaPdfOriginal);
            }

            //Pdf sharp
            // Import page of other documents

            using PdfDocument documentoOriginal = PdfReader.Open(rutaPdfOriginal, PdfDocumentOpenMode.Import);

            //count page total

            int totalPaginas = documentoOriginal.PageCount;

            //validate pdf

            if (totalPaginas == 0)
            {
                throw new InvalidOperationException("El archivo PDF no contiene páginas.");
            }

            return totalPaginas;
        }


        // Elimina caracteres que Windows no permite en nombres de archivos (\/:*?"<>|)

        private string LimpiarNombreArchivo(string nombre)
        {
            char[] caracteresInvalidos = Path.GetInvalidFileNameChars();

            foreach (char caracter in caracteresInvalidos)
            {
                nombre = nombre.Replace(caracter.ToString(), "");
            }
            return nombre.Trim();
        }
        // Section CONTRATOS
        public (int cantidadGenerada, List<string> nombresArchivos) DividirPdfConNombres(
            string rutaPdfOriginal,           // Url
            string carpetaDestino,            // file of save
            int paginasPorDocumento,          // one page with code
            List<string> codigos              // codes exel
        )
        {
            // Check that the PDF path isn’t empty
            if (string.IsNullOrWhiteSpace(rutaPdfOriginal))
            {
                throw new ArgumentException("La ruta del archivo PDF es obligatoria.");
            }

            //Validate that the PDF file exists
            if (!File.Exists(rutaPdfOriginal))
            {
                throw new FileNotFoundException("No se encontró el archivo PDF seleccionado.", rutaPdfOriginal);
            }

            // Validate that the destination folder isn’t empty
            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                throw new ArgumentException("La carpeta de destino es obligatoria.");
            }

            // create the destination folder if it doesn’t exist

            Directory.CreateDirectory(carpetaDestino);

            if (paginasPorDocumento <= 0)
            {
                throw new ArgumentException("La cantidad de páginas por documento debe ser mayor que cero.");
            }

            if (codigos == null || codigos.Count == 0)
            {
                throw new InvalidOperationException("No se encontraron códigos de empleados.");
            }

            using PdfDocument documentoOriginal = PdfReader.Open(rutaPdfOriginal, PdfDocumentOpenMode.Import);
            int totalPaginas = documentoOriginal.PageCount;
            //validate that the PDF isn’t empty

            if (totalPaginas == 0)
            {
                throw new InvalidOperationException("El archivo PDF no contiene páginas.");
            }

            if (totalPaginas % paginasPorDocumento != 0)
            {
                int paginasSobrantes = totalPaginas % paginasPorDocumento;

                throw new InvalidOperationException(
                    $"El PDF contiene {totalPaginas} páginas. " +
                    $"Los documentos deben contener grupos exactos de " +
                    $"{paginasPorDocumento} página(s). " +
                    $"Quedan {paginasSobrantes} página(s) sin completar."
                );
            }

            int cantidadDocumentos = totalPaginas / paginasPorDocumento;

            if (codigos.Count != cantidadDocumentos)
            {
                throw new InvalidOperationException(
                    $"El PDF generará {cantidadDocumentos} documentos, " +
                    $"pero el Excel contiene {codigos.Count} códigos. " +
                    $"La cantidad de códigos debe coincidir con la cantidad de documentos a generar."
                );
            }

            // date new

            string fechaActual = DateTime.Now.ToString("dd-MM-yyyy");

            //name of the files generated
            List<string> nombresArchivos = new List<string>();

            int numeroDocumento = 1;

            for (int paginaInicial = 0; paginaInicial < totalPaginas; paginaInicial += paginasPorDocumento)
            {
                // create new PDF document
                using PdfDocument nuevoDocumento = new PdfDocument();

                //add pages to the new document

                for (int pagina = 0; pagina < paginasPorDocumento; pagina++)
                {
                    nuevoDocumento.AddPage(documentoOriginal.Pages[paginaInicial + pagina]);
                }

                //get codes for the employee

                string codigo = codigos[numeroDocumento - 1].Trim();

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    throw new InvalidOperationException(
                        $"El código correspondiente al documento {numeroDocumento} está vacío."
                    );
                }

                codigo = LimpiarNombreArchivo(codigo);

                //code identity unique

                string identificador = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
                //create new name for the file generated

                string nombreArchivo = $"{codigo}- Employeement Agreement-{fechaActual}-{identificador}.pdf";

                //construct the complete path for the new file

                string rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);

                while (File.Exists(rutaArchivoSalida))
                {
                    identificador = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
                    nombreArchivo = $"{codigo}- Employeement Agreement-{fechaActual}-{identificador}.pdf";
                    rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);
                }

                nombresArchivos.Add(nombreArchivo);

                //save the new PDF file

                nuevoDocumento.Save(rutaArchivoSalida);

                numeroDocumento++;
            }

            //return the results: number of documents generated and the list of file names

            return (numeroDocumento - 1, nombresArchivos);
        }


        //secction Finiquitos

        public (int cantidadGenerada, List<string> nombresArchivos) DividirPdfFiniquitos(
            string rutaPdfOriginal,           // Url of pdf original
            string carpetaDestino,            // carpeta donde guardar los finiquitos
            int paginasPorDocumento,          // páginas por cada finiquito (3)
            List<string> codigos              // codes of exel
        )
        {

            //validate that the PDF path isn’t empty
            if (string.IsNullOrWhiteSpace(rutaPdfOriginal))
            {
                throw new ArgumentException("La ruta del archivo PDF es obligatoria.");
            }

            //validate that the PDF file exists
            if (!File.Exists(rutaPdfOriginal))
            {
                throw new FileNotFoundException("No se encontró el archivo PDF seleccionado.", rutaPdfOriginal);
            }

            //validate that the destination folder isn’t empty
            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                throw new ArgumentException("La carpeta de destino es obligatoria.");
            }

            //create the destination folder if it doesn’t exist
            Directory.CreateDirectory(carpetaDestino);

            //validate that the number of pages per document is greater than zero

            if (paginasPorDocumento <= 0)
            {
                throw new ArgumentException("La cantidad de páginas por documento debe ser mayor que cero.");
            }
            //validate that the list of codes isn’t null or empty

            if (codigos == null || codigos.Count == 0)
            {
                throw new InvalidOperationException("No se encontraron códigos de empleados.");
            }

            using PdfDocument documentoOriginal = PdfReader.Open(rutaPdfOriginal, PdfDocumentOpenMode.Import);
            //get total number of pages in the original PDF

            int totalPaginas = documentoOriginal.PageCount;

            //validate that the PDF isn’t empty

            if (totalPaginas == 0)
            {
                throw new InvalidOperationException("El archivo PDF no contiene páginas.");
            }

            //Validate that the total number of pages is divisible by the number of pages per document

            if (totalPaginas % paginasPorDocumento != 0)
            {
                int paginasSobrantes = totalPaginas % paginasPorDocumento;

                throw new InvalidOperationException(
                    $"El PDF contiene {totalPaginas} páginas. " +
                    $"Los finiquitos deben contener grupos exactos de " +
                    $"{paginasPorDocumento} página(s). " +
                    $"Quedan {paginasSobrantes} página(s) sin completar."
                );
            }

            // 3 pages per document → 9 pages / 3 pages per document = 3 documents

            int cantidadDocumentos = totalPaginas / paginasPorDocumento;

            //validate that the number of codes matches the number of documents to generate
            if (codigos.Count != cantidadDocumentos)
            {
                throw new InvalidOperationException(
                    $"El PDF generará {cantidadDocumentos} finiquitos, " +
                    $"pero el Excel contiene {codigos.Count} códigos. " +
                    $"La cantidad de códigos debe coincidir con la cantidad de finiquitos a generar."
                );
            }

            //validate date 

            int añoActual = DateTime.Now.Year;
            string fechaFiniquito = $"30-12-{añoActual}";

            //list to store the names of the generated files

            List<string> nombresArchivos = new List<string>();

            int numeroDocumento = 1;

            //Divide the pdf

            for (int paginaInicial = 0; paginaInicial < totalPaginas; paginaInicial += paginasPorDocumento)
            {
                //create a new PDF document for each finiquito

                using PdfDocument nuevoDocumento = new PdfDocument();

                //add the pages for the current finiquito

                for (int pagina = 0; pagina < paginasPorDocumento; pagina++)
                {
                    nuevoDocumento.AddPage(documentoOriginal.Pages[paginaInicial + pagina]);
                }

                //codes for employes
                string codigo = codigos[numeroDocumento - 1].Trim();

                //validate that the code isn’t empty
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    throw new InvalidOperationException(
                        $"El código correspondiente al finiquito {numeroDocumento} está vacío."
                    );
                }
                codigo = LimpiarNombreArchivo(codigo);

                // generate code unique that document

                string identificador = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();

                string nombreArchivo = $"{codigo}-Involuntary Termination Letter-{fechaFiniquito}-{identificador}.pdf";
                //Url complete for the new file

                string rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);
                //protected for overwrite the file if it already exists
                while (File.Exists(rutaArchivoSalida))
                {
                    identificador = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
                    nombreArchivo = $"{codigo}-Involuntary Termination Letter-{fechaFiniquito}-{identificador}.pdf";
                    rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);
                }

                //save pdf 

                nombresArchivos.Add(nombreArchivo);
                nuevoDocumento.Save(rutaArchivoSalida);
                numeroDocumento++;
            }

            //return the results

            return (numeroDocumento - 1, nombresArchivos);
        }


        // Método legacy para finiquitos (sin códigos)
        public int DividirPdf(
            string rutaPdfOriginal,
            string carpetaDestino,
            int paginasPorDocumento,
            string prefijoArchivo
        )
        {
            if (string.IsNullOrWhiteSpace(rutaPdfOriginal))
            {
                throw new ArgumentException("La ruta del archivo PDF es obligatoria.");
            }

            if (!File.Exists(rutaPdfOriginal))
            {
                throw new FileNotFoundException("No se encontró el archivo PDF seleccionado.", rutaPdfOriginal);
            }

            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                throw new ArgumentException("La carpeta de destino es obligatoria.");
            }

            Directory.CreateDirectory(carpetaDestino);

            if (paginasPorDocumento <= 0)
            {
                throw new ArgumentException("La cantidad de páginas por documento debe ser mayor que cero.");
            }

            if (string.IsNullOrWhiteSpace(prefijoArchivo))
            {
                throw new ArgumentException("El prefijo del archivo es obligatorio.");
            }

            using PdfDocument documentoOriginal = PdfReader.Open(rutaPdfOriginal, PdfDocumentOpenMode.Import);

            int totalPaginas = documentoOriginal.PageCount;

            if (totalPaginas == 0)
            {
                throw new InvalidOperationException("El archivo PDF no contiene páginas.");
            }

            if (totalPaginas % paginasPorDocumento != 0)
            {
                int paginasSobrantes = totalPaginas % paginasPorDocumento;

                throw new InvalidOperationException(
                    $"El PDF contiene {totalPaginas} páginas. " +
                    $"Los documentos deben contener grupos exactos de " +
                    $"{paginasPorDocumento} página(s). " +
                    $"Quedan {paginasSobrantes} página(s) sin completar."
                );
            }

            // Divide

            int numeroDocumento = 1;

            for (int paginaInicial = 0; paginaInicial < totalPaginas; paginaInicial += paginasPorDocumento)
            {
                using PdfDocument nuevoDocumento = new PdfDocument();

                for (int pagina = 0; pagina < paginasPorDocumento; pagina++)
                {
                    nuevoDocumento.AddPage(documentoOriginal.Pages[paginaInicial + pagina]);
                }
                //Generate name 
                string nombreArchivo = $"{prefijoArchivo}_{numeroDocumento:D4}.pdf";
                string rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);

                if (File.Exists(rutaArchivoSalida))
                {
                    int consecutivo = 2;
                    string nombreBase = $"{prefijoArchivo}_{numeroDocumento:D4}";

                    do
                    {
                        nombreArchivo = $"{nombreBase}_{consecutivo}.pdf";
                        rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);
                        consecutivo++;
                    } while (File.Exists(rutaArchivoSalida));
                }

                //Pdf save

                nuevoDocumento.Save(rutaArchivoSalida);

                numeroDocumento++;
            }
            //retur cantidad of documents generated

            return numeroDocumento - 1;
        }


        // Divide PDF - INCAPACIDADES

        public (int cantidadGenerada, List<string> nombresArchivos) DividirPdfIncapacidades(
            string rutaPdfOriginal,
            string carpetaDestino,
            int paginasPorDocumento,
            List<string> codigos
        )
        {
            // validate PDF

            if (string.IsNullOrWhiteSpace(rutaPdfOriginal))
            {
                throw new ArgumentException("La ruta del archivo PDF es obligatoria.");
            }

            if (!File.Exists(rutaPdfOriginal))
            {
                throw new FileNotFoundException("No se encontró el archivo PDF seleccionado.", rutaPdfOriginal);
            }

            // Validate destination folder

            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                throw new ArgumentException("La carpeta de destino es obligatoria.");
            }

            Directory.CreateDirectory(carpetaDestino);

            // Validate page for document

            if (paginasPorDocumento <= 0)
            {
                throw new ArgumentException("La cantidad de páginas por documento debe ser mayor que cero.");
            }

            // Validate codes list

            if (codigos == null || codigos.Count == 0)
            {
                throw new InvalidOperationException("No se encontraron códigos de empleados.");
            }

            // Open the original pdf 

            using PdfDocument documentoOriginal = PdfReader.Open(rutaPdfOriginal, PdfDocumentOpenMode.Import);

            int totalPaginas = documentoOriginal.PageCount;

            // Validate pdf empty

            if (totalPaginas == 0)
            {
                throw new InvalidOperationException("El archivo PDF no contiene páginas.");
            }

            // Validate complete groups

            if (totalPaginas % paginasPorDocumento != 0)
            {
                int paginasSobrantes = totalPaginas % paginasPorDocumento;

                throw new InvalidOperationException(
                    $"El PDF contiene {totalPaginas} páginas. " +
                    $"Las incapacidades deben contener grupos exactos de " +
                    $"{paginasPorDocumento} página(s). " +
                    $"Quedan {paginasSobrantes} página(s) sin completar."
                );
            }

            // calculate the number of documents to generate

            int cantidadDocumentos = totalPaginas / paginasPorDocumento;

            // Validate number of codes 

            if (codigos.Count != cantidadDocumentos)
            {
                throw new InvalidOperationException(
                    $"El PDF generará {cantidadDocumentos} incapacidades, " +
                    $"pero el Excel contiene {codigos.Count} códigos. " +
                    $"La cantidad de códigos debe coincidir con la cantidad de incapacidades a generar."
                );
            }

            // Date for the incapacity documents

            string fechaActual = DateTime.Now.ToString("dd-MM-yyyy");

            // list names of the generated files

            List<string> nombresArchivos = new List<string>();

            // Divide pdf

            int numeroDocumento = 1;

            for (int paginaInicial = 0; paginaInicial < totalPaginas; paginaInicial += paginasPorDocumento)
            {
                // Create new document

                using PdfDocument nuevoDocumento = new PdfDocument();

                // add pages 

                for (int pagina = 0; pagina < paginasPorDocumento; pagina++)
                {
                    nuevoDocumento.AddPage(documentoOriginal.Pages[paginaInicial + pagina]);
                }

                string codigo = codigos[numeroDocumento - 1].Trim();

                // Validate codes 

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    throw new InvalidOperationException(
                        $"El código correspondiente a la incapacidad {numeroDocumento} está vacío."
                    );
                }

                // clear 

                codigo = LimpiarNombreArchivo(codigo);

                // generate unique identifier

                string identificador = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();

                string nombreArchivo = $"{codigo}-Incapacidad-{fechaActual}-{identificador}.pdf";

                // Url finish

                string rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);

                // protectes against overwriting existing files

                while (File.Exists(rutaArchivoSalida))
                {
                    identificador = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
                    nombreArchivo = $"{codigo}-Incapacidad-{fechaActual}-{identificador}.pdf";
                    rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);
                }

                // save the name of the generated file

                nombresArchivos.Add(nombreArchivo);

                // Save the new pdf 

                nuevoDocumento.Save(rutaArchivoSalida);

                numeroDocumento++;
            }

            // Return the results

            return (numeroDocumento - 1, nombresArchivos);
        }
    }
}