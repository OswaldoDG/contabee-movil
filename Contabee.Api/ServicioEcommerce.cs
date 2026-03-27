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
            var res = await servicioEcommerce.FullAsync(true,TipoPrecio.Publico);
            if (res != null)
            {
                r.Payload = res.ToList();
            }
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get Catalogo Productos");
        }
        return r;
    }

    public async Task<bool> VerificarCompraIAP(Guid cuentaFiscalId, string dispositivoId, string productoTiendaId, string transaccionId, PasarelarPago pasarela)
    {
        try
        {
            var comprobante = new DtoComprobanteCompra
            {
                CuentaFiscalId = cuentaFiscalId.ToString(),
                DispositivoId = dispositivoId,
                PasarelarPago = pasarela,
                PasarelaId = transaccionId,
                ProductoTiendaId = productoTiendaId,
            };
            await servicioEcommerce.VerificarAsync(cuentaFiscalId, comprobante);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

