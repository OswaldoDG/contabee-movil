namespace ContaBeeMovil.Services.Permisos;

public interface IServicioPermisos
{
    /// <summary>
    /// Garantiza el permiso de cámara. Se debe llamar en CADA intento del usuario:
    /// si negó antes, vuelve a pedirlo; si lo negó de forma permanente, ofrece abrir
    /// los ajustes del sistema.
    /// </summary>
    /// <param name="motivo">Cierre de la frase "ContaBee necesita la cámara …",
    /// p. ej. "para tomar la foto de tu ticket".</param>
    /// <returns>true si el permiso quedó concedido.</returns>
    Task<bool> AsegurarCamaraAsync(string motivo);
}
