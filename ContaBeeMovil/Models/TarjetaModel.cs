using System.Windows.Input;

namespace ContaBeeMovil.Models;

public class TarjetaModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Alias { get; set; } = string.Empty;
    public string UltimosDigitos { get; set; } = string.Empty;
    public string NumeroMascarado => $".... .... .... {UltimosDigitos}";
    public string DisplayLabel => string.IsNullOrEmpty(UltimosDigitos)
        ? Alias
        : $"{Alias} ({UltimosDigitos})";

    public ICommand? DeleteCommand { get; set; }
    public double SwipeItemWidth { get; set; }
}
