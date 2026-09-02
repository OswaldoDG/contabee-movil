using System.Globalization;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Dev;

#if IOS && DEBUG
using StoreKit2;
using UIKit;
#endif

namespace ContaBeeMovil.Services.IAP;

public sealed class ServicioReembolsoApple : IServicioReembolsoApple
{
    private const string ClaveUltimaTransaccion = "iap.apple.ultima_transaccion_id";

    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IServicioLogs _logs;

    public ServicioReembolsoApple(
        IServicioAlmacenamiento almacenamiento,
        IServicioLogs logs)
    {
        _almacenamiento = almacenamiento;
        _logs = logs;
    }

    public string? ObtenerUltimaTransaccionId()
    {
        var valor = _almacenamiento.LeerPreferencia(ClaveUltimaTransaccion);
        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    public bool RegistrarTransaccion(string? transaccionId)
    {
#if IOS && DEBUG
        if (!TryParseTransaccionId(transaccionId, out var id))
            return false;

        _almacenamiento.GuardarPreferencia(
            ClaveUltimaTransaccion,
            id.ToString(CultureInfo.InvariantCulture));
        return true;
#else
        return false;
#endif
    }

    public async Task<ResultadoReembolsoApple> SolicitarAsync(
        string? transaccionId,
        CancellationToken cancellationToken = default)
    {
#if IOS && DEBUG
        if (!OperatingSystem.IsIOSVersionAtLeast(15))
        {
            return new ResultadoReembolsoApple(
                EstadoSolicitudReembolsoApple.NoDisponible,
                "La solicitud de reembolso requiere iOS 15 o posterior.");
        }

        transaccionId = string.IsNullOrWhiteSpace(transaccionId)
            ? ObtenerUltimaTransaccionId()
            : transaccionId.Trim();

        if (!TryParseTransaccionId(transaccionId, out var id))
        {
            return new ResultadoReembolsoApple(
                EstadoSolicitudReembolsoApple.TransaccionInvalida,
                "Realiza una compra Sandbox o captura un Transaction ID numérico válido.");
        }

        var escena = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault(x => x.ActivationState == UISceneActivationState.ForegroundActive);

        if (escena is null)
        {
            return new ResultadoReembolsoApple(
                EstadoSolicitudReembolsoApple.NoDisponible,
                "No se encontró una ventana activa para mostrar la hoja de Apple.");
        }

        try
        {
            RegistrarTransaccion(id.ToString(CultureInfo.InvariantCulture));
            _logs.Log($"IAP Apple: abriendo solicitud de reembolso — transacción={id}");

#pragma warning disable CA1416 // El binding omite iOS en su atributo; Apple expone esta API desde iOS 15.
            var resultado = await Transaction.BeginRefundRequestAsync(
                id,
                escena,
                cancellationToken);
#pragma warning restore CA1416

            if (resultado == Transaction.RefundRequestStatus.Success)
            {
                _logs.Log($"IAP Apple: solicitud de reembolso enviada — transacción={id}");
                return new ResultadoReembolsoApple(EstadoSolicitudReembolsoApple.Enviada);
            }

            _logs.Log($"IAP Apple: solicitud de reembolso cancelada — transacción={id}");
            return new ResultadoReembolsoApple(EstadoSolicitudReembolsoApple.Cancelada);
        }
        catch (OperationCanceledException)
        {
            return new ResultadoReembolsoApple(EstadoSolicitudReembolsoApple.Cancelada);
        }
        catch (Exception ex)
        {
            _logs.Log($"IAP Apple: solicitud de reembolso falló — {ex.GetType().Name}: {ex.Message}");
            return new ResultadoReembolsoApple(
                EstadoSolicitudReembolsoApple.Error,
                ex.Message);
        }
#else
        await Task.CompletedTask;
        return new ResultadoReembolsoApple(
            EstadoSolicitudReembolsoApple.NoDisponible,
            "Esta función solo está disponible en iOS.");
#endif
    }

    private static bool TryParseTransaccionId(string? valor, out ulong id) =>
        ulong.TryParse(
            valor?.Trim(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out id) && id > 0;
}
