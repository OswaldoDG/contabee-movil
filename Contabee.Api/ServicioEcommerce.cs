using Contabee.Api.abstractions;
using Contabee.Api.Ecommerce;
using Contabee.Api.Logging;

namespace Contabee.Api;

public class ServicioEcommerce(HttpClient httpClient, IAppLogger logger) : IServicioEcommerce
{
    private readonly ServicioEcommerceClient servicioEcommerce = new(httpClient.BaseAddress!.ToString(), httpClient);
    private readonly IAppLogger _logger = logger;

    public async Task<RespuestaPayload<List<DtoCategoriasProducto>>> GetCatalogoProductos()
    {
        RespuestaPayload<List<DtoCategoriasProducto>> r = new();
        try
        {
            _logger.Info("ServicioEcommerce.GetCatalogoProductos", "Inicio de consulta de catálogo de productos.");
            var res = await servicioEcommerce.FullAsync(true, TipoPrecio.Publico);
            if (res != null)
                r.Payload = res.ToList();
            r.Ok = true;
            _logger.Info("ServicioEcommerce.GetCatalogoProductosExitoso", "Consulta de catálogo de productos completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioEcommerce.GetCatalogoProductosException", "Excepción no controlada al consultar catálogo de productos.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get Catalogo Productos");
        }
        return r;
    }

    //public async Task<RespuestaPayload<RespuestaCuponValido>> ValidarCupon(string codigo)
    //{
    //    RespuestaPayload<RespuestaCuponValido> r = new();
    //    try
    //    {
    //        var res = await servicioEcommerce.ValidarAsync(codigo, TipoCuentaCupon.UsuarioApp, null);
    //        r.Payload = res;
    //        r.Ok = true;
    //    }
    //    catch (Exception ex)
    //    {
    //        r.Error = ex.ErrorGenerico("ServicioEcommerce-ValidarCupon");
    //    }
    //    return r;
    //}

    public async Task<bool> VerificarCompraIAP(Guid cuentaFiscalId, DtoComprobanteCompra comprobante)
    {
        _logger.Info("ServicioEcommerce.VerificarCompraIAP", "Inicio de verificación de compra IAP.");
        System.Diagnostics.Debug.WriteLine($"[Ecommerce] VerificarCompraIAP → cfid={cuentaFiscalId} producto={comprobante.ProductoTiendaId} pasarela={comprobante.PasarelarPago} pasarelaId={comprobante.PasarelaId}");
        try
        {
            await servicioEcommerce.VerificarAsync(cuentaFiscalId, comprobante);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] VerificarCompraIAP ← OK");
            _logger.Info("ServicioEcommerce.VerificarCompraIAPExitoso", "Verificación de compra IAP completada.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioEcommerce.VerificarCompraIAPException", "Excepción no controlada al verificar compra IAP.", ex);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] VerificarCompraIAP ← ERROR {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CompletarCompraIAP(Guid cuentaFiscalId, DtoComprobanteCompra comprobante)
    {
        _logger.Info("ServicioEcommerce.CompletarCompraIAP", "Inicio de completado de compra IAP.");
        System.Diagnostics.Debug.WriteLine($"[Ecommerce] CompletarCompraIAP → cfid={cuentaFiscalId} producto={comprobante.ProductoTiendaId} pasarela={comprobante.PasarelarPago} pasarelaId={comprobante.PasarelaId}");
        try
        {
            await servicioEcommerce.CompletarAsync(cuentaFiscalId, comprobante);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] CompletarCompraIAP ← OK");
            _logger.Info("ServicioEcommerce.CompletarCompraIAPExitoso", "Completado de compra IAP finalizado correctamente.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioEcommerce.CompletarCompraIAPException", "Excepción no controlada al completar compra IAP.", ex);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] CompletarCompraIAP ← ERROR {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task<List<CuponUsuario>> CuponesUsuario()
    {
        _logger.Info("ServicioEcommerce.CuponesUsuario", "Inicio de consulta de cupones de usuario.");
        System.Diagnostics.Debug.WriteLine($"[Ecommerce] CuponesUsuario →");
        try
        {
            var res = await servicioEcommerce.CuponesAsync();
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] CuponesUsuario ← OK");
            _logger.Info("ServicioEcommerce.CuponesUsuarioExitoso", "Consulta de cupones de usuario completada.");
            return res.ToList();
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioEcommerce.CuponesUsuarioException", "Excepción no controlada al consultar cupones de usuario.", ex);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] CuponesUsuario ← ERROR {ex.GetType().Name}: {ex.Message}");
            return new List<CuponUsuario>();
        }
    }

    public async Task<CuponUsuario> AplicarCupon(string codigo, ActivacionCuponDto activacionCupon)
    {
        _logger.Info("ServicioEcommerce.AplicarCupon", "Inicio de aplicación de cupón.");
        System.Diagnostics.Debug.WriteLine($"[Ecommerce] AplicarCupon → codigo={codigo} cuentaId={activacionCupon.UsuarioId} cfid={activacionCupon.CuentaFiscalId}");
        try
        {
            var res = await servicioEcommerce.AplicarAsync(codigo, activacionCupon);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] AplicarCupon ← OK");
            _logger.Info("ServicioEcommerce.AplicarCuponExitoso", "Aplicación de cupón completada.");
            return res;
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioEcommerce.AplicarCuponException", "Excepción no controlada al aplicar cupón.", ex);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] AplicarCupon ← ERROR {ex.GetType().Name}: {ex.Message}");
            return new CuponUsuario();
        }
    }
}
