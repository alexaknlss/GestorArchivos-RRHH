using ClosedXML.Excel;

namespace GestorArchivos_RRHH.Services
{
    public class ExcelCodeService
    {
        public List<string> LeerCodigos(IFormFile archivoExcel)
        {
            List<string> codigos = new List<string>();

            if (archivoExcel == null || archivoExcel.Length == 0)
            {
                throw new Exception(
                    "El archivo Excel está vacío o no fue seleccionado."
                );
            }

            using (Stream stream = archivoExcel.OpenReadStream())
            using (XLWorkbook workbook = new XLWorkbook(stream))
            {
                IXLWorksheet hoja = workbook.Worksheets.First();

                int ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 0;

                if (ultimaFila == 0)
                {
                    throw new Exception(
                        "El archivo Excel no contiene datos."
                    );
                }

                for (int fila = 2; fila <= ultimaFila; fila++)
                {
                    string codigo =
                        hoja.Cell(fila, 1)
                            .GetString()
                            .Trim();

                    if (!string.IsNullOrWhiteSpace(codigo))
                    {
                        codigos.Add(codigo);
                    }
                }
            }

            if (codigos.Count == 0)
            {
                throw new Exception(
                    "No se encontraron códigos válidos en el archivo Excel."
                );
            }

            return codigos;
        }
    }
}