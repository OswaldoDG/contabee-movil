using Contabee.Api.Transcript;
using ContaBeeMovil.Services.Comunes;
using ContaBeeMovil.Services.Device;
using Fonts;
using Newtonsoft.Json;
using System.Windows.Input;

namespace ContaBeeMovil.Views;

public partial class FiltrosFacturasView : ContentView
{
    // ── Listas fijas ────────────────────────────────────────────────────────────

    private static readonly List<string> _meses =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    private static readonly List<string> _estados =
        ["Estado", "Nuevos", "En Proceso", "Reprogramados", "Finalizados", "Error"];

    private static readonly Dictionary<string, string> _estadoEnum = new()
    {
        ["Nuevos"]       = nameof(EstadoFactura.CargaCompleta),
        ["En Proceso"]   = nameof(EstadoFactura.EnProceso),
        ["Reprogramados"]= nameof(EstadoFactura.Reprogramado),
        ["Finalizados"]  = nameof(EstadoFactura.Finalizado),
        ["Error"]        = nameof(EstadoFactura.FinalizadoError),
    };

    private static readonly List<string> _envios =
        ["Envio", "Foto", "Email"];

    private static readonly List<string> _tipos =
        ["Tipo", "Captura individual", "Comprobaçión", "Devolucion"];

    private static readonly Dictionary<string, string> _tipoEnum = new()
    {
        ["Captura individual"] = nameof(TipoProcesoCaptura.FacturaIndividual),
        ["Comprobaçión"]       = nameof(TipoProcesoCaptura.Comprobacion),
        ["Devolucion"]         = nameof(TipoProcesoCaptura.Devolucion),
    };

    private static readonly List<string> _camposOrden =
        ["Creacion", "Monto",];

    // ── Estado interno ───────────────────────────────────────────────────────────

    private bool _expandido = true;
    private bool _ordenAscendente = false;
    private bool _restaurando;

    private const string PrefsKeyFiltros = "FiltrosFacturas_UltimaConsulta";

    // ── BindableProperties ───────────────────────────────────────────────────────

    public static readonly BindableProperty CreadoresProperty =
        BindableProperty.Create(
            nameof(Creadores),
            typeof(IList<string>),
            typeof(FiltrosFacturasView),
            defaultValue: null,
            propertyChanged: OnCreadoresChanged);

    public IList<string>? Creadores
    {
        get => (IList<string>?)GetValue(CreadoresProperty);
        set => SetValue(CreadoresProperty, value);
    }

    public static readonly BindableProperty CreadoresIdsProperty =
        BindableProperty.Create(
            nameof(CreadoresIds),
            typeof(IList<string>),
            typeof(FiltrosFacturasView),
            defaultValue: null);

    public IList<string>? CreadoresIds
    {
        get => (IList<string>?)GetValue(CreadoresIdsProperty);
        set => SetValue(CreadoresIdsProperty, value);
    }

    public static readonly BindableProperty BuscarCommandProperty =
        BindableProperty.Create(
            nameof(BuscarCommand),
            typeof(ICommand),
            typeof(FiltrosFacturasView));

    public ICommand? BuscarCommand
    {
        get => (ICommand?)GetValue(BuscarCommandProperty);
        set => SetValue(BuscarCommandProperty, value);
    }

    // ── Constructor ──────────────────────────────────────────────────────────────

    public FiltrosFacturasView()
    {
        InitializeComponent();
        InicializarPickers();
        ActualizarLabelPeriodo();
    }

    private void InicializarPickers()
    {
        int anioActual = DateTime.Now.Year;
        var anios = Enumerable.Range(anioActual - 1, 2).Reverse().Select(a => a.ToString()).ToList();

        PickerAnio.ItemsSource = anios;
        PickerAnio.SelectedItem = anioActual.ToString();

        PickerMes.ItemsSource = _meses;
        PickerMes.SelectedIndex = DateTime.Now.Month - 1;

        PickerEstado.ItemsSource = _estados;
        PickerEstado.SelectedIndex = 0;

        PickerEnvio.ItemsSource = _envios;
        PickerEnvio.SelectedIndex = 0;

        PickerTipo.ItemsSource = _tipos;
        PickerTipo.SelectedIndex = 0;

        PickerOrden.ItemsSource = _camposOrden;
        PickerOrden.SelectedIndex = 0;

        PickerOrden.SelectedIndexChanged += (_, _) =>
        {
            if (!_restaurando) EjecutarBusqueda(BusquedaActual);
        };
    }

    // ── Persistencia de estado ───────────────────────────────────────────────────

    private void GuardarEstado()
    {
        var estado = new FiltrosPersistidos(
            PickerAnio.SelectedItem as string,
            PickerMes.SelectedIndex,
            PickerEstado.SelectedIndex,
            PickerCreador.SelectedIndex,
            PickerEnvio.SelectedIndex,
            PickerTipo.SelectedIndex,
            EntryRfc.Text,
            PickerOrden.SelectedIndex,
            _ordenAscendente
        );
        Preferences.Default.Set(PrefsKeyFiltros, JsonConvert.SerializeObject(estado));
    }

    public void RestaurarEstado()
    {
        var json = Preferences.Default.Get(PrefsKeyFiltros, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        var estado = JsonConvert.DeserializeObject<FiltrosPersistidos>(json);
        if (estado is null) return;

        _restaurando = true;
        try
        {
            if (estado.Anio is not null)
                PickerAnio.SelectedItem = estado.Anio;

            if (estado.MesIndex >= 0)
                PickerMes.SelectedIndex = estado.MesIndex;

            if (estado.EstadoIndex >= 0)
                PickerEstado.SelectedIndex = estado.EstadoIndex;

            if (estado.CreadorIndex >= 0
                && PickerCreador.ItemsSource is System.Collections.IList c
                && estado.CreadorIndex < c.Count)
                PickerCreador.SelectedIndex = estado.CreadorIndex;

            if (estado.EnvioIndex >= 0)
                PickerEnvio.SelectedIndex = estado.EnvioIndex;

            if (estado.TipoIndex >= 0)
                PickerTipo.SelectedIndex = estado.TipoIndex;

            EntryRfc.Text = estado.Rfc ?? string.Empty;

            if (estado.OrdenIndex >= 0)
                PickerOrden.SelectedIndex = estado.OrdenIndex;

            if (_ordenAscendente != estado.OrdenAscendente)
            {
                _ordenAscendente = estado.OrdenAscendente;
                IconOrden.Text = _ordenAscendente
                    ? FluentUI.arrow_sort_up_lines_20_regular
                    : FluentUI.arrow_sort_down_lines_20_regular;
            }
        }
        finally
        {
            _restaurando = false;
        }
    }

    // ── Busqueda actual ──────────────────────────────────────────────────────────

    public Busqueda BusquedaActual
    {
        get
        {
            var filtros = new List<Filtro>();

            // CuentaFiscalId — siempre requerido
            var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
            if (cuentaFiscalId.HasValue)
                filtros.Add(new Filtro { Propiedad = "CuentaFiscalId", Operador = Operador.Igual, Valores = [cuentaFiscalId.Value.ToString()] });

            // Rango de fecha: primer día del mes seleccionado → último día del mes
            if (PickerAnio.SelectedItem is string anioStr && int.TryParse(anioStr, out int anio) && PickerMes.SelectedIndex >= 0)
            {
                int mes = PickerMes.SelectedIndex + 1;
                int ultimoDia = DateTime.DaysInMonth(anio, mes);
                string inicio = $"{anio:D4}-{mes:D2}-01 06:00:00.000Z";
                string fin    = $"{anio:D4}-{mes:D2}-{ultimoDia:D2} 06:00:00.000Z";
                filtros.Add(new Filtro { Propiedad = "FechaCreacion", Operador = Operador.Entre, Valores = [inicio, fin] });
            }

            // Estado (índice 0 = "sin filtro")
            if (PickerEstado.SelectedIndex > 0 && PickerEstado.SelectedItem is string estado
                && _estadoEnum.TryGetValue(estado, out string? estadoVal))
                filtros.Add(new Filtro { Propiedad = "Estado", Operador = Operador.Igual, Valores = [estadoVal] });

            // Creador (índice 0 = "Creador" / sin filtro)
            if (PickerCreador.SelectedIndex > 0
                && CreadoresIds is not null
                && PickerCreador.SelectedIndex < CreadoresIds.Count)
                filtros.Add(new Filtro { Propiedad = "creador", Operador = Operador.Igual, Valores = [CreadoresIds[PickerCreador.SelectedIndex]] });

            // Envío (índice 0 = "sin filtro")
            if (PickerEnvio.SelectedIndex > 0 && PickerEnvio.SelectedItem is string envio)
                filtros.Add(new Filtro { Propiedad = "TipoRecepcion", Operador = Operador.Igual, Valores = [envio] });

            // Tipo (índice 0 = "sin filtro")
            if (PickerTipo.SelectedIndex > 0 && PickerTipo.SelectedItem is string tipo
                && _tipoEnum.TryGetValue(tipo, out string? tipoVal))
                filtros.Add(new Filtro { Propiedad = "Tipo", Operador = Operador.Igual, Valores = [tipoVal] });

            // RFC contiene
            if (!string.IsNullOrWhiteSpace(EntryRfc.Text))
                filtros.Add(new Filtro { Propiedad = "RfcEmisor", Operador = Operador.Contiene, Valores = [EntryRfc.Text.Trim()] });

            return new Busqueda
            {
                Filtros = filtros,
                OrdernarDesc = !_ordenAscendente,
                OrdenarPropiedad = MapearCampoOrden(PickerOrden.SelectedItem as string ?? "Creacion"),
                Paginado = new Paginado { Pagina = 1, TamanoPagina = 10 },
                Contar = true
            };
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────────────

    private void OnLabelPeriodoTapped(object sender, TappedEventArgs e)
    {
        var filtros = new List<Filtro>();

        var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (cuentaFiscalId.HasValue)
            filtros.Add(new Filtro { Propiedad = "CuentaFiscalId", Operador = Operador.Igual, Valores = [cuentaFiscalId.Value.ToString()] });

        var hoy = DateTime.Now;
        PickerAnio.SelectedItem = hoy.Year.ToString();
        PickerMes.SelectedIndex = hoy.Month - 1;
        int ultimoDia = DateTime.DaysInMonth(hoy.Year, hoy.Month);
        string inicio = $"{hoy.Year:D4}-{hoy.Month:D2}-01 06:00:00.000Z";
        string fin    = $"{hoy.Year:D4}-{hoy.Month:D2}-{ultimoDia:D2} 06:00:00.000Z";
        filtros.Add(new Filtro { Propiedad = "FechaCreacion", Operador = Operador.Entre, Valores = [inicio, fin] });

        // Limpiar los demás filtros
        PickerEstado.SelectedIndex  = 0;
        PickerCreador.SelectedIndex = -1;
        PickerEnvio.SelectedIndex   = 0;
        PickerTipo.SelectedIndex    = 0;
        EntryRfc.Text               = string.Empty;

        var busqueda = new Busqueda { Filtros = filtros, Paginado = new Paginado { Pagina = 1, TamanoPagina = 10 }, Contar = true };
        EjecutarBusqueda(busqueda);
    }

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

    private async void OnToggleExpandir(object sender, TappedEventArgs e)
    {
        _expandido = !_expandido;

        IconExpandir.Text = _expandido
            ? FluentUI.chevron_up_20_regular
            : FluentUI.chevron_down_20_regular;

        if (_expandido)
        {
            PanelFiltros.IsVisible = true;
            await PanelFiltros.FadeTo(1, 200);
        }
        else
        {
            await PanelFiltros.FadeTo(0, 150);
            PanelFiltros.IsVisible = false;
        }
    }

    private void OnToggleOrden(object sender, TappedEventArgs e)
    {
        _ordenAscendente = !_ordenAscendente;
        IconOrden.Text = _ordenAscendente
            ? FluentUI.arrow_sort_up_lines_20_regular
            : FluentUI.arrow_sort_down_lines_20_regular;
        EjecutarBusqueda(BusquedaActual);
    }

    private static void OnCreadoresChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FiltrosFacturasView view)
        {
            view.PickerCreador.ItemsSource = (System.Collections.IList)(newValue as IList<string>);
            if (view.PickerCreador.ItemsSource?.Count > 0)
                view.PickerCreador.SelectedIndex = -1;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string MapearCampoOrden(string campo) => campo switch
    {
        "Creacion" => "FechaCreacion",
        _          => campo
    };

    private void ActualizarLabelPeriodo()
    {
        var hoy = DateTime.Now;
        LabelPeriodo.Text = $"{_meses[hoy.Month - 1][..3]}/{hoy.Year}";
    }

    // ── DTO persistencia ─────────────────────────────────────────────────────────

    private record FiltrosPersistidos(
        string? Anio,
        int MesIndex,
        int EstadoIndex,
        int CreadorIndex,
        int EnvioIndex,
        int TipoIndex,
        string? Rfc,
        int OrdenIndex,
        bool OrdenAscendente
    );
}
