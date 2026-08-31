using System.Globalization;
using ContaBeeMovil.Models;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Services.Dev;

/// <summary>
/// Centraliza la activación y caducidad del modo desarrollador.
/// La configuración vigente se guarda en Preferences (SharedPreferences en Android).
/// </summary>
public sealed class ServicioModoDeveloper : IServicioModoDeveloper
{
    private const int DiasVigencia = 30;
    private const string ClaveActivo = "ModoDeveloper_Activo";
    private const string ClaveFechaActivacion = "ModoDeveloper_FechaActivacionUtc";
    private const string ClaveMigracionCompletada = "ModoDeveloper_MigracionSecureStorageCompletada";
    private const string ClaveAnteriorSecureStorage = "ModoDeveloper";

    private readonly IServicioAlmacenamiento _almacenamiento;

    public ServicioModoDeveloper(IServicioAlmacenamiento almacenamiento)
    {
        _almacenamiento = almacenamiento;
    }

    public void Activar()
    {
        GuardarActivacion(DateTimeOffset.UtcNow);
        _almacenamiento.GuardarPreferencia(ClaveMigracionCompletada, true);
        AppState.Instance.EsDev = true;
    }

    public async Task<bool> ValidarVigenciaAsync()
    {
        try
        {
            await MigrarActivacionAnteriorAsync();

            var activo = _almacenamiento.LeerPreferenciaBool(ClaveActivo);
            var fechaTexto = _almacenamiento.LeerPreferencia(ClaveFechaActivacion);
            var ahora = DateTimeOffset.UtcNow;

            var vigente = activo &&
                DateTimeOffset.TryParse(
                    fechaTexto,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var fechaActivacion) &&
                fechaActivacion <= ahora &&
                ahora <= fechaActivacion.AddDays(DiasVigencia);

            if (!vigente)
                _almacenamiento.GuardarPreferencia(ClaveActivo, false);

            AppState.Instance.EsDev = vigente;
            return vigente;
        }
        catch
        {
            // Una preferencia corrupta o un fallo al migrar no debe impedir el arranque.
            AppState.Instance.EsDev = false;
            return false;
        }
    }

    private async Task MigrarActivacionAnteriorAsync()
    {
        if (_almacenamiento.ContienePreferencia(ClaveMigracionCompletada))
            return;

        var activacionAnterior = await _almacenamiento
            .LeerSeguroAsync<ModoDeveloperDto>(ClaveAnteriorSecureStorage);

        // La activación pudo ocurrir mientras se leía SecureStorage. En ese caso,
        // se conserva la fecha nueva que ya fue escrita en Preferences.
        if (!_almacenamiento.ContienePreferencia(ClaveFechaActivacion) &&
            activacionAnterior is { EsDev: true } &&
            DateTimeOffset.TryParse(
                activacionAnterior.FechaActivacion,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var fechaAnterior))
        {
            GuardarActivacion(fechaAnterior.ToUniversalTime());
        }

        _almacenamiento.GuardarPreferencia(ClaveMigracionCompletada, true);
    }

    private void GuardarActivacion(DateTimeOffset fechaActivacion)
    {
        _almacenamiento.GuardarPreferencia(ClaveActivo, true);
        _almacenamiento.GuardarPreferencia(
            ClaveFechaActivacion,
            fechaActivacion.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }
}
