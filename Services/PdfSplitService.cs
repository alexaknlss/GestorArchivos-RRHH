using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace GestorArchivos_RRHH.Services
{
    public class PdfSplitService
    {
        public int DividirPdf(
            string rutaPdfOriginal,
            string carpetaDestino,
            int paginasPorDocumento,
            string prefijoArchivo)
        {
            // Validar que exista el PDF original
            if (string.IsNullOrWhiteSpace(rutaPdfOriginal))
            {
                throw new ArgumentException(
                    "La ruta del archivo PDF es obligatoria."
                );
            }

            if (!File.Exists(rutaPdfOriginal))
            {
                throw new FileNotFoundException(
                    "No se encontró el archivo PDF seleccionado.",
                    rutaPdfOriginal
                );
            }

            // Validar la carpeta donde se guardarán los PDFs
            if (string.IsNullOrWhiteSpace(carpetaDestino))
            {
                throw new ArgumentException(
                    "La carpeta de destino es obligatoria."
                );
            }

            // Crear la carpeta si todavía no existe
            Directory.CreateDirectory(carpetaDestino);

            // Validar páginas por documento
            if (paginasPorDocumento <= 0)
            {
                throw new ArgumentException(
                    "La cantidad de páginas por documento debe ser mayor que cero."
                );
            }

            if (string.IsNullOrWhiteSpace(prefijoArchivo))
            {
                prefijoArchivo = "Documento";
            }

            // Abrir el PDF original
            using PdfDocument documentoOriginal =
                PdfReader.Open(
                    rutaPdfOriginal,
                    PdfDocumentOpenMode.Import
                );

            int totalPaginas =
                documentoOriginal.PageCount;

            if (totalPaginas == 0)
            {
                throw new InvalidOperationException(
                    "El archivo PDF no contiene páginas."
                );
            }

            // Evitar documentos incompletos
            if (totalPaginas % paginasPorDocumento != 0)
            {
                int paginasSobrantes =
                    totalPaginas % paginasPorDocumento;

                throw new InvalidOperationException(
                    $"El PDF contiene {totalPaginas} páginas. " +
                    $"Los documentos deben contener grupos exactos de " +
                    $"{paginasPorDocumento} página(s). " +
                    $"Quedan {paginasSobrantes} página(s) sin completar."
                );
            }

            int numeroDocumento = 1;

            // Dividir el PDF
            for (
                int paginaInicial = 0;
                paginaInicial < totalPaginas;
                paginaInicial += paginasPorDocumento
            )
            {
                using PdfDocument nuevoDocumento =
                    new PdfDocument();

                // Agregar las páginas que corresponden
                for (
                    int pagina = 0;
                    pagina < paginasPorDocumento;
                    pagina++
                )
                {
                    nuevoDocumento.AddPage(
                        documentoOriginal.Pages[
                            paginaInicial + pagina
                        ]
                    );
                }

                // Ejemplo:
                // Contrato_0001.pdf
                // Contrato_0002.pdf
                //
                // o:
                // Finiquito_0001.pdf
                // Finiquito_0002.pdf

                string nombreArchivo =
                    $"{prefijoArchivo}_{numeroDocumento:D4}.pdf";

                string rutaArchivoSalida =
                    Path.Combine(
                        carpetaDestino,
                        nombreArchivo
                    );

                // Guardar directamente como PDF individual
                nuevoDocumento.Save(
                    rutaArchivoSalida
                );

                numeroDocumento++;
            }

            // Retornar cuántos documentos fueron creados
            return numeroDocumento - 1;
        }
    }
}