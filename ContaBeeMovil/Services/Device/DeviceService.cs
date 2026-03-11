using banditoth.MAUI.DeviceId;
using banditoth.MAUI.DeviceId.Interfaces;
using ContaBeeMovil.Services.Almacenamiento;


namespace ContaBeeMovil.Services.Device;

public class DeviceService
{
    private const string DEVICE_ID_KEY = "IdDispositivo";
    private const string DEFAULT_DEVICE_ID = "cbeeid-00000000-0000-0000-0000-000000000000";
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IDeviceIdProvider _deviceIdProvider;

    public DeviceService(IServicioAlmacenamiento almacenamiento, IDeviceIdProvider deviceIdProvider)
    {
        _almacenamiento = almacenamiento;
        _deviceIdProvider = deviceIdProvider;
    }

    public async Task CheckDeviceIdAsync()
    {
        try
        {
            string? deviceId = await _almacenamiento.LeerSeguroAsync(DEVICE_ID_KEY);

            if (string.IsNullOrEmpty(deviceId) || deviceId == DEFAULT_DEVICE_ID)
            {
                deviceId = GenerateDeviceId();
                await _almacenamiento.GuardarSeguroAsync(DEVICE_ID_KEY, deviceId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeviceService] Error en CheckDeviceIdAsync: {ex.Message}");
        }
    }

    public async Task<string?> GetDeviceIdAsync()
    {
        return await _almacenamiento.LeerSeguroAsync(DEVICE_ID_KEY);
    }

    private string GenerateDeviceId()
    {
        var idTemp = _deviceIdProvider.GetDeviceId();
        return !string.IsNullOrEmpty(idTemp)
            ? $"cbeeid-{idTemp}"
            : $"cbeeid-{Guid.NewGuid()}";
    }

#if ANDROID
    private string? GetAndroidId()
    {
        try
        {
            var context = Android.App.Application.Context;
            return Android.Provider.Settings.Secure.GetString(
                context.ContentResolver,
                Android.Provider.Settings.Secure.AndroidId
            );
        }
        catch
        {
            return null;
        }
    }
#endif
}
