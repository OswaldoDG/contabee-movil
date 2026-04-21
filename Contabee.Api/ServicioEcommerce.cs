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

    public async Task<RespuestaPayload<RespuestaCuponValido>> ValidarCupon(string codigo)
    {
        RespuestaPayload<RespuestaCuponValido> r = new();
        try
        {
            var res = await servicioEcommerce.ValidarAsync(codigo, TipoCuentaCupon.UsuarioApp, null);
            r.Payload = res;
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioEcommerce-ValidarCupon");
        }
        return r;
    }

    public async Task<bool> VerificarCompraIAP(Guid cuentaFiscalId, DtoComprobanteCompra comprobante)
    {
        try
        {
            await servicioEcommerce.VerificarAsync(cuentaFiscalId, comprobante);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CompletarCompraIAP(Guid cuentaFiscalId, DtoComprobanteCompra comprobante)
    {
        try
        {
            await servicioEcommerce.CompletarAsync(cuentaFiscalId, comprobante);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
