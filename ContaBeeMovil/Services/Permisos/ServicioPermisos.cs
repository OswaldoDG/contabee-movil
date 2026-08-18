using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Services.Permisos;

/// <summary>
/// Punto único para pedir permisos del sistema.
///
/// Regla: NUNCA se cachea la negación. Cada vez que el usuario toca la acción se
/// vuelve a consultar y a pedir el permiso, porque el SO sí vuelve a mostrar su
/// diálogo mientras la negación no sea permanente. Cuando ya es permanente
/// (Android: "No volver a preguntar" / 2ª negación; iOS: cualquier negación) el
/// diálogo del SO ya no aparece nunca más, así que se ofrece abrir los ajustes de
/// la app — que es el único camino para reactivarlo.
/// </summary>
public class ServicioPermisos : IServicioPermisos
{
    private readonly IServicioAlerta _alerta;
    private readonly IServicioLogs _logs;

    public ServicioPermisos(IServicioAlerta alerta, IServicioLogs logs)
    {
        _alerta = alerta;
        _logs = logs;
    }

    public Task<bool> AsegurarCamaraAsync(string motivo) =>
        AsegurarAsync<Permissions.Camera>("cámara", motivo);

    private async Task<bool> AsegurarAsync<TPermiso>(string nombre, string motivo)
        where TPermiso : Permissions.BasePermission, new()
    {
        try
        {
            var status = await MainThread.InvokeOnMainThreadAsync(
                (Func<Task<PermissionStatus>>)(() => Permissions.CheckStatusAsync<TPermiso>()));
            if (status == PermissionStatus.Granted)
                return true;

            // Reintentos: mientras el SO acepte volver a mostrar su diálogo, se le da
            // al usuario la oportunidad de corregir una negación accidental.
            while (true)
            {
                status = await MainThread.InvokeOnMainThreadAsync(
                    (Func<Task<PermissionStatus>>)(() => Permissions.RequestAsync<TPermiso>()));
                if (status == PermissionStatus.Granted)
                    return true;

                // ShouldShowRationale == true  → negó, pero el SO SÍ volverá a preguntar.
                // ShouldShowRationale == false → negación permanente (o iOS): solo ajustes.
                var puedeVolverAPreguntar = await MainThread.InvokeOnMainThreadAsync(
                    (Func<bool>)(() => Permissions.ShouldShowRationale<TPermiso>()));

                _logs.Log($"[ServicioPermisos] {nombre} → {status} | puedeVolverAPreguntar={puedeVolverAPreguntar}");

                if (!puedeVolverAPreguntar)
                {
                    await OfrecerAjustesAsync(nombre, motivo);
                    return false;
                }

                var reintentar = await _alerta.MostrarAsync(
                    $"Permiso de {nombre}",
                    $"ContaBee necesita la {nombre} {motivo}. Sin este permiso no podemos continuar.",
                    verBotonCancelar: true,
                    verBotonConfirmar: true,
                    cancelarText: "Ahora no",
                    confirmarText: "Permitir");

                if (!reintentar)
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logs.Log($"[ServicioPermisos] {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private async Task OfrecerAjustesAsync(string nombre, string motivo)
    {
        var abrir = await _alerta.MostrarAsync(
            $"Permiso de {nombre} desactivado",
            $"ContaBee necesita la {nombre} {motivo}, pero el permiso está bloqueado y el sistema ya no lo vuelve a preguntar. " +
            $"Actívalo en los ajustes de la app y vuelve a intentarlo.",
            verBotonCancelar: true,
            verBotonConfirmar: true,
            cancelarText: "Ahora no",
            confirmarText: "Abrir ajustes");

        if (!abrir)
            return;

        try
        {
            AppInfo.Current.ShowSettingsUI();
        }
        catch (Exception ex)
        {
            _logs.Log($"[ServicioPermisos] no se pudieron abrir ajustes: {ex.Message}");
        }
    }
}
