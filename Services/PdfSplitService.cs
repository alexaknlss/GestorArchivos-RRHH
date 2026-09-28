using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Globalization;

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

            // date new (format: yyyyMMdd → 20260910)

            string fechaActual = DateTime.Now.ToString("yyyyMMdd");

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

                //build the base name for the file (without counter)
                //rename the name of contrato file here 
                string baseNombre = $"{codigo}- Employment Agreement-{fechaActual}";

                //create new name for the file generated
                string nombreArchivo = $"{baseNombre}.pdf";

                //construct the complete path for the new file
                string rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);

                // protect against overwriting existing files
                int contador = 1;
                while (File.Exists(rutaArchivoSalida))
                {
                    nombreArchivo = $"{baseNombre}-{contador}.pdf";
                    rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);
                    contador++;
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

            //validate date (always December 30 of current year: yyyy1230)

            int añoActual = DateTime.Now.Year;
            string fechaFiniquito = $"{añoActual}1230";

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

                //build the base name for the file (without counter)
                //rename the finiquito file here
                string baseNombre = $"{codigo}-Termination letter-{fechaFiniquito}";

                //create new name for the file generated
                string nombreArchivo = $"{baseNombre}.pdf";

                //Url complete for the new file
                string rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);

                //protect against overwriting existing files
                int contador = 1;
                while (File.Exists(rutaArchivoSalida))
                {
                    nombreArchivo = $"{baseNombre}-{contador}.pdf";
                    rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);
                    contador++;
                }

                //save pdf 

                nombresArchivos.Add(nombreArchivo);
                nuevoDocumento.Save(rutaArchivoSalida);
                numeroDocumento++;
            }

            //return the results

            return (numeroDocumento - 1, nombresArchivos);
        }








        public (int cantidadGenerada, List<string> nombresArchivos) DividirPdfIncapacidades(
     string rutaPdfOriginal,
     string carpetaDestino,
     List<string> codigos,
     int paginasPorDocumento = 1)
        {
            // Validación del parámetro
            if (paginasPorDocumento <= 0)
                throw new ArgumentException("El número de páginas por documento debe ser mayor a cero.",
                    nameof(paginasPorDocumento));

            // Validar PDF
            if (string.IsNullOrWhiteSpace(rutaPdfOriginal))
                throw new ArgumentException("La ruta del archivo PDF es obligatoria.");

            if (!File.Exists(rutaPdfOriginal))
                throw new FileNotFoundException("No se encontró el archivo PDF seleccionado.", rutaPdfOriginal);

            // Validar carpeta destino
            if (string.IsNullOrWhiteSpace(carpetaDestino))
                throw new ArgumentException("La carpeta de destino es obligatoria.");

            Directory.CreateDirectory(carpetaDestino);

            // Validar códigos
            if (codigos == null || codigos.Count == 0)
                throw new InvalidOperationException("No se encontraron códigos de empleados.");

            // Abrir PDF original
            using PdfDocument documentoOriginal = PdfReader.Open(rutaPdfOriginal, PdfDocumentOpenMode.Import);

            int totalPaginas = documentoOriginal.PageCount;

            if (totalPaginas == 0)
                throw new InvalidOperationException("El archivo PDF no contiene páginas.");

            // Validar grupos completos
            if (totalPaginas % paginasPorDocumento != 0)
            {
                int paginasSobrantes = totalPaginas % paginasPorDocumento;
                throw new InvalidOperationException(
                    $"El PDF contiene {totalPaginas} páginas. " +
                    $"Cada incapacidad debe tener {paginasPorDocumento} página(s). " +
                    $"Quedan {paginasSobrantes} página(s) sin completar."
                );
            }

            int cantidadDocumentos = totalPaginas / paginasPorDocumento;

            // Validar cantidad de códigos
            if (codigos.Count != cantidadDocumentos)
            {
                throw new InvalidOperationException(
                    $"El PDF generará {cantidadDocumentos} incapacidad(es) " +
                    $"(con {paginasPorDocumento} página(s) cada una). " +
                    $"Pero el Excel contiene {codigos.Count} código(s). " +
                    $"La cantidad de códigos debe coincidir con la cantidad de incapacidades."
                );
            }

            // renombrado fecha
            string fechaActual = DateTime.Now.ToString("yyyyMMdd");
            List<string> nombresArchivos = new List<string>();

            int numeroDocumento = 1;

            for (int paginaInicial = 0;
                 paginaInicial < totalPaginas;
                 paginaInicial += paginasPorDocumento)
            {
                using PdfDocument nuevoDocumento = new PdfDocument();

                for (int pagina = 0; pagina < paginasPorDocumento; pagina++)
                {
                    nuevoDocumento.AddPage(documentoOriginal.Pages[paginaInicial + pagina]);
                }

                string codigo = codigos[numeroDocumento - 1].Trim();

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    throw new InvalidOperationException(
                        $"El código correspondiente a la incapacidad {numeroDocumento} está vacío."
                    );
                }

                codigo = LimpiarNombreArchivo(codigo);

                // nombre sin codigo unico
                string nombreArchivo = $"{codigo}-Medical Leave-{fechaActual}.pdf";
                string rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);

               
                int contador = 1;
                while (File.Exists(rutaArchivoSalida))
                {
                    nombreArchivo = $"{codigo}-Medical Leave-{fechaActual} ({contador}).pdf";
                    rutaArchivoSalida = Path.Combine(carpetaDestino, nombreArchivo);
                    contador++;
                }

                nombresArchivos.Add(nombreArchivo);

                nuevoDocumento.Save(rutaArchivoSalida);

                numeroDocumento++;
            }

            return (numeroDocumento - 1, nombresArchivos);
        }

    }
}