using Contabee.Api.abstractions;
using Contabee.Api.Ecommerce;

namespace Contabee.Api;

public class ServicioEcommerce(HttpClient httpClient) : IServicioEcommerce
{
    private readonly ServicioEcommerceClient servicioEcommerce = new(httpClient.BaseAddress!.ToString(), httpClient);

    public async Task<RespuestaPayload<List<DtoCategoriasProducto>>> GetCatalogoProductos()
    {
        RespuestaPayload<List<DtoCategoriasProducto>> r = new();
        try
        {
            var res = await servicioEcommerce.FullAsync(true, TipoPrecio.Publico);
            if (res != null)
                r.Payload = res.ToList();
            r.Ok = true;
        }
        catch (Exception ex)
        {
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
        System.Diagnostics.Debug.WriteLine($"[Ecommerce] VerificarCompraIAP → cfid={cuentaFiscalId} producto={comprobante.ProductoTiendaId} pasarela={comprobante.PasarelarPago} pasarelaId={comprobante.PasarelaId}");
        try
        {
            await servicioEcommerce.VerificarAsync(cuentaFiscalId, comprobante);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] VerificarCompraIAP ← OK");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] VerificarCompraIAP ← ERROR {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CompletarCompraIAP(Guid cuentaFiscalId, DtoComprobanteCompra comprobante)
    {
        System.Diagnostics.Debug.WriteLine($"[Ecommerce] CompletarCompraIAP → cfid={cuentaFiscalId} producto={comprobante.ProductoTiendaId} pasarela={comprobante.PasarelarPago} pasarelaId={comprobante.PasarelaId}");
        try
        {
            await servicioEcommerce.CompletarAsync(cuentaFiscalId, comprobante);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] CompletarCompraIAP ← OK");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] CompletarCompraIAP ← ERROR {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task<List<CuponUsuario>> CuponesUsuario()
    {
        System.Diagnostics.Debug.WriteLine($"[Ecommerce] CuponesUsuario →");
        try
        {
            var res = await servicioEcommerce.CuponesAsync();
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] CuponesUsuario ← OK");
            return res.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] CuponesUsuario ← ERROR {ex.GetType().Name}: {ex.Message}");
            return new List<CuponUsuario>();
        }
    }

    public async Task<CuponUsuario> AplicarCupon(string codigo, ActivacionCuponDto activacionCupon)
    {
        System.Diagnostics.Debug.WriteLine($"[Ecommerce] AplicarCupon → codigo={codigo} cuentaId={activacionCupon.UsuarioId} cfid={activacionCupon.CuentaFiscalId}");
        try
        {
            var res = await servicioEcommerce.AplicarAsync(codigo, activacionCupon);
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] AplicarCupon ← OK");
            return res;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ecommerce] AplicarCupon ← ERROR {ex.GetType().Name}: {ex.Message}");
            return new CuponUsuario();
        }
    }
}
