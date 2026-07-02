using System.Windows.Input;
using ContaBeeMovil.Services.Device;
using MauiIcons.Material;
using Newtonsoft.Json;
using Busqueda = Contabee.Api.Identidad.Busqueda;
using Filtro = Contabee.Api.Identidad.Filtro;
using Operador = Contabee.Api.Identidad.Operador;
using Paginado = Contabee.Api.Identidad.Paginado;

namespace ContaBeeMovil.Views;

public partial class FiltrosEquipoView : ContentView
{
    private static readonly List<string> _estados =
        ["Todos", "Activos", "Inactivos"];

    private static readonly List<string> _camposOrden =
        ["Nombre", "Registro"];

    private bool _expandido = true;
    private bool _ordenAscendente;
    private const string PrefsKeyFiltros = "FiltrosEquipo_UltimaConsulta";

    public static readonly BindableProperty BuscarCommandProperty =
        BindableProperty.Create(
            nameof(BuscarCommand),
            typeof(ICommand),
            typeof(FiltrosEquipoView));

    public ICommand? BuscarCommand
    {
        get => (ICommand?)GetValue(BuscarCommandProperty);
        set => SetValue(BuscarCommandProperty, value);
    }

    public FiltrosEquipoView()
    {
        InitializeComponent();
        InicializarSelectores();
    }

    private void InicializarSelectores()
    {
        SelectorEstado.Elementos = _estados;
        SelectorEstado.IndiceSeleccionado = 0;

        SelectorOrden.Elementos = _camposOrden;
        SelectorOrden.IndiceSeleccionado = 0;

        RestaurarEstado();
    }

    public Busqueda BusquedaActual
    {
        get
        {
            var filtros = new List<Filtro>();

            if (!string.IsNullOrWhiteSpace(EntryTexto.Text))
            {
                filtros.Add(new Filtro
                {
                    Propiedad = "NombreEmail",
                    Operador = Operador.Contiene,
                    Valores = [EntryTexto.Text.Trim()]
                });
            }

            // Estado: Activos -> "1", Inactivos -> "0", Todos -> sin filtro
            if (SelectorEstado.IndiceSeleccionado == 1)
                filtros.Add(new Filtro { Propiedad = "AsociacionActiva", Operador = Operador.Igual, Valores = ["1"] });
            else if (SelectorEstado.IndiceSeleccionado == 2)
                filtros.Add(new Filtro { Propiedad = "AsociacionActiva", Operador = Operador.Igual, Valores = ["0"] });

            // Puede capturar: marcado -> filtra a quienes pueden capturar; desmarcado -> sin filtro
            if (ChkCaptura.IsChecked)
                filtros.Add(new Filtro { Propiedad = "PuedeCapturar", Operador = Operador.Igual, Valores = ["1"] });

            return new Busqueda
            {
                Filtros = filtros,
                OrdernarDesc = !_ordenAscendente,
                OrdenarPropiedad = MapearCampoOrden(SelectorOrden.ElementoSeleccionado as string ?? "Nombre"),
                Paginado = new Paginado { Pagina = 0, TamanoPagina = Config.AppSettings.Consulta.TamanoPagina },
                Contar = true
            };
        }
    }

    private static string MapearCampoOrden(string campo) => campo switch
    {
        "Registro" => "FechaRegistro",
        _ => campo
    };

    private void OnBuscarTapped(object sender, TappedEventArgs e)
    {
        EjecutarBusqueda(BusquedaActual);
    }

    private void EjecutarBusqueda(Busqueda busqueda)
    {
        GuardarEstado();
        if (BuscarCommand?.CanExecute(busqueda) == true)
            BuscarCommand.Execute(busqueda);
    }

    private void OnToggleCaptura(object sender, TappedEventArgs e)
    {
        ChkCaptura.IsChecked = !ChkCaptura.IsChecked;
        ActualizarIndicadorFiltros();
        EjecutarBusqueda(BusquedaActual);
    }

    private void OnToggleOrden(object sender, TappedEventArgs e)
    {
        _ordenAscendente = !_ordenAscendente;
        IconOrden.Text = _ordenAscendente
            ? FluentUI.arrow_up_20_regular
            : FluentUI.arrow_down_20_regular;
        EjecutarBusqueda(BusquedaActual);
    }

    private async void OnToggleExpandir(object sender, TappedEventArgs e)
    {
        _expandido = !_expandido;

        IconExpandir.Text = _expandido
            ? FluentUI.chevron_up_20_regular
            : FluentUI.chevron_down_20_regular;

        if (_expandido)
        {
            PanelFiltros.IsVisible = true;
            BtnBuscar.IsVisible = true;
            await PanelFiltros.FadeToAsync(1, 200);
        }
        else
        {
            await PanelFiltros.FadeToAsync(0, 150);
            PanelFiltros.IsVisible = false;
            BtnBuscar.IsVisible = false;
        }
        ActualizarIndicadorFiltros();
    }

    private void OnLimpiarFiltrosYActualizar(object sender, TappedEventArgs e)
    {
        EntryTexto.Text = string.Empty;
        SelectorEstado.IndiceSeleccionado = 0;
        ChkCaptura.IsChecked = false;

        ActualizarIndicadorFiltros();
        EjecutarBusqueda(BusquedaActual);
    }

    private bool TieneFiltrosExtra()
    {
        return SelectorEstado.IndiceSeleccionado > 0
            || ChkCaptura.IsChecked
            || !string.IsNullOrWhiteSpace(EntryTexto.Text);
    }

    private void ActualizarIndicadorFiltros()
    {
        IconFiltro.TextColor = TieneFiltrosExtra()
            ? Converters.EstadoBadgeColorConverter.ResolveColor("Primary", Colors.Orange)
            : Converters.EstadoBadgeColorConverter.ResolveColor("PrimaryText", Colors.White);
    }

    private void GuardarEstado()
    {
        var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (!cuentaFiscalId.HasValue) return;

        var estado = new FiltrosPersistidos(
            EntryTexto.Text,
            SelectorEstado.IndiceSeleccionado,
            ChkCaptura.IsChecked,
            SelectorOrden.IndiceSeleccionado,
            _ordenAscendente);

        var json = JsonConvert.SerializeObject(estado);
        Preferences.Default.Set($"{PrefsKeyFiltros}_{cuentaFiscalId.Value}", json);
    }

    public void RestaurarEstado()
    {
        var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (!cuentaFiscalId.HasValue) return;

        var json = Preferences.Default.Get($"{PrefsKeyFiltros}_{cuentaFiscalId.Value}", string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        var estado = JsonConvert.DeserializeObject<FiltrosPersistidos>(json);
        if (estado is null) return;

        EntryTexto.Text = estado.Texto ?? string.Empty;

        if (estado.EstadoIndex >= 0 && estado.EstadoIndex < _estados.Count)
            SelectorEstado.IndiceSeleccionado = estado.EstadoIndex;

        ChkCaptura.IsChecked = estado.Captura;

        if (estado.OrdenIndex >= 0 && estado.OrdenIndex < _camposOrden.Count)
            SelectorOrden.IndiceSeleccionado = estado.OrdenIndex;

        _ordenAscendente = estado.OrdenAscendente;
        IconOrden.Text = _ordenAscendente
            ? FluentUI.arrow_up_20_regular
            : FluentUI.arrow_down_20_regular;

        ActualizarIndicadorFiltros();
    }

    private record FiltrosPersistidos(
        string? Texto,
        int EstadoIndex,
        bool Captura,
        int OrdenIndex,
        bool OrdenAscendente
    );

    public static void LimpiarEstadoPersistido()
    {
        var ids = new HashSet<Guid>();

        if (AppState.Instance.CuentaFiscalActual is not null)
            ids.Add(AppState.Instance.CuentaFiscalActual.CuentaFiscalId);

        if (AppState.Instance.CuentasFiscales is not null)
        {
            foreach (var cuenta in AppState.Instance.CuentasFiscales)
                ids.Add(cuenta.CuentaFiscalId);
        }

        foreach (var id in ids)
            Preferences.Default.Remove($"{PrefsKeyFiltros}_{id}");
    }
}
