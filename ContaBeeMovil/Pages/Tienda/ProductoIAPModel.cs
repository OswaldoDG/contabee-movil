namespace ContaBeeMovil.Pages.Tienda;

public class ProductoIAPModel
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Unidades { get; set; } = string.Empty;
    public string PrecioTexto { get; set; } = string.Empty;
    public double PrecioValor { get; set; }
    public bool DisponibleEnTienda { get; set; }
}
