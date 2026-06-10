using System.Windows.Input;

namespace ContaBeeMovil.Views;

public partial class BotonCargando : ContentView
{
    public static readonly BindableProperty TextoProperty =
        BindableProperty.Create(nameof(Texto), typeof(string), typeof(BotonCargando), string.Empty,
            propertyChanged: (b, _, _) => ((BotonCargando)b).ActualizarTexto());

    public static readonly BindableProperty EstaCargandoProperty =
        BindableProperty.Create(nameof(EstaCargando), typeof(bool), typeof(BotonCargando), false,
            propertyChanged: (b, _, n) => ((BotonCargando)b).ActualizarCargando((bool)n));

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(BotonCargando));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(BotonCargando));

    public static readonly BindableProperty RadioEsquinaProperty =
        BindableProperty.Create(nameof(RadioEsquina), typeof(int), typeof(BotonCargando), 12,
            propertyChanged: (b, _, n) => ((BotonCargando)b).AplicarRadio((int)n));

    public static readonly BindableProperty AlturaProperty =
        BindableProperty.Create(nameof(Altura), typeof(double), typeof(BotonCargando), 48.0,
            propertyChanged: (b, _, n) => ((BotonCargando)b).AplicarAltura((double)n));

    public string Texto
    {
        get => (string)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    public bool EstaCargando
    {
        get => (bool)GetValue(EstaCargandoProperty);
        set => SetValue(EstaCargandoProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public int RadioEsquina
    {
        get => (int)GetValue(RadioEsquinaProperty);
        set => SetValue(RadioEsquinaProperty, value);
    }

    public double Altura
    {
        get => (double)GetValue(AlturaProperty);
        set => SetValue(AlturaProperty, value);
    }

    public event EventHandler? Clicked;

    public BotonCargando()
    {
        InitializeComponent();
        ActualizarTexto();
        AplicarRadio(RadioEsquina);
        AplicarAltura(Altura);
    }

    private void ActualizarTexto()
    {
        if (BotonInterno is null) return;
        if (!EstaCargando)
            BotonInterno.Text = Texto;
    }

    private void AplicarRadio(int radio)
    {
        if (BotonInterno is not null)
            BotonInterno.CornerRadius = radio;
    }

    private void AplicarAltura(double altura)
    {
        if (BotonInterno is not null)
            BotonInterno.HeightRequest = altura;
    }

    private void ActualizarCargando(bool cargando)
    {
        if (BotonInterno is null || Spinner is null) return;

        BotonInterno.Text = cargando ? string.Empty : Texto;
        Spinner.IsVisible = cargando;
        Spinner.IsRunning = cargando;
    }

    private void BotonInterno_Clicked(object? sender, EventArgs e)
    {
        if (EstaCargando) return;

        Clicked?.Invoke(this, EventArgs.Empty);

        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    /// <summary>
    /// Ejecuta <paramref name="accion"/> mostrando el spinner mientras corre y
    /// restaurando el texto al terminar, ocurra éxito o excepción.
    /// </summary>
    public async Task EjecutarAsync(Func<Task> accion)
    {
        if (EstaCargando) return;

        EstaCargando = true;
        try
        {
            await accion();
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
