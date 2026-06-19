namespace ContaBeeMovil.Helpers;

/// <summary>
/// Tipos de crédito que el dashboard puede resaltar con animación
/// tras una compra exitosa.
/// </summary>
public enum TipoCreditoResaltar
{
    Captura,
    Autoservicio,
    Colaboracion
}

/// <summary>
/// Crédito que aumentó tras una compra: su tipo y cuántos se ganaron
/// (para animar el conteo desde el valor anterior).
/// </summary>
public readonly record struct CreditoGanado(TipoCreditoResaltar Tipo, int Cantidad);
