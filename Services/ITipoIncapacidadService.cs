using GestorArchivos_RRHH.Models;

namespace GestorArchivos_RRHH.Services
{
    public interface ITipoIncapacidadService
    {
        List<TipoIncapacidad> ObtenerTipos();
        TipoIncapacidad? ObtenerPorValor(string valor);
        bool Agregar(TipoIncapacidad tipo, out string? error);
        bool Eliminar(string valor, out string? error);
    }
}