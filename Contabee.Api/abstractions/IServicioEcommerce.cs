using Contabee.Api.Ecommerce;

namespace Contabee.Api.abstractions;

public interface IServicioEcommerce
{
    Task<RespuestaPayload<List<DtoCategoriasProducto>>> GetCatalogoProductos();
    Task<bool> VerificarCompraIAP(Guid cuentaFiscalId, DtoComprobanteCompra comprobante);
    Task<bool> CompletarCompraIAP(Guid cuentaFiscalId, DtoComprobanteCompra comprobante);
    Task<CuponUsuario> ValidarCupon(string codigo);
    Task<List<CuponUsuario>> CuponesUsuario(Guid? cuentaFiscalId = null);
    Task<CuponUsuario> AplicarCupon(string codigo, ActivacionCuponDto activacionCupon);
}
