
using Contabee.Api.Ecommerce;

namespace Contabee.Api.abstractions;

public interface IServicioEcommerce
{
    Task<RespuestaPayload<List<DtoCategoriasProducto>>> GetCatalogoProductos();
    Task<bool> VerificarCompraIAP(Guid cuentaFiscalId, string dispositivoId, string productoTiendaId, string verificationData, string compraId, DtoProducto producto, PasarelarPago pasarela);
    Task<RespuestaPayload<RespuestaCuponValido>> ValidarCupon(string codigo);
}
