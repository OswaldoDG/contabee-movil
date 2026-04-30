using Contabee.Api.Transcript;
using ContaBeeMovil.Services.Comunes;
using ContaBeeMovil.Services.Device;
using MauiIcons.Material;
using Newtonsoft.Json;
using System.Windows.Input;

namespace ContaBeeMovil.Views;

public partial class FiltrosFacturasView : ContentView
{
    private static readonly List<string> _meses =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    private static readonly Dictionary<string, string> _mesesAbreviados = new()
    {
        ["Febrero"]    = "Feb",
        ["Agosto"]     = "Ago",
        ["Septiembre"] = "Sep",
        ["Octubre"]    = "Oct",
        ["Noviembre"]  = "Nov",
        ["Diciembre"]  = "Dic"
    };

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

    private bool _expandido = true;
    private bool _ordenAscendente = false;
    private bool _restaurando;

    private const string PrefsKeyFiltros = "FiltrosFacturas_UltimaConsulta";

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

    public static readonly BindableProperty PeriodoTextoProperty =
        BindableProperty.Create(
            nameof(PeriodoTexto),
            typeof(string),
            typeof(FiltrosFacturasView),
            defaultValue: string.Empty);

    public string PeriodoTexto
    {
        get => (string)GetValue(PeriodoTextoProperty);
        set => SetValue(PeriodoTextoProperty, value);
    }

    public static readonly BindableProperty PeriodoTextoCompletoProperty =
        BindableProperty.Create(
            nameof(PeriodoTextoCompleto),
            typeof(string),
            typeof(FiltrosFacturasView),
            defaultValue: string.Empty);

    public string PeriodoTextoCompleto
    {
        get => (string)GetValue(PeriodoTextoCompletoProperty);
        set => SetValue(PeriodoTextoCompletoProperty, value);
    }

    public FiltrosFacturasView()
    {
        InitializeComponent();
        InicializarSelectores();
        ActualizarPeriodoTexto();
    }

    private void InicializarSelectores()
    {
        int anioActual = DateTime.Now.Year;
        var anios = Enumerable.Range(anioActual - 1, 2).Reverse().Select(a => a.ToString()).ToList();

        SelectorAnio.Elementos = anios;
        SelectorAnio.ElementoSeleccionado = anioActual.ToString();

        SelectorMes.Elementos = _meses;
        SelectorMes.IndiceSeleccionado = DateTime.Now.Month - 1;

        SelectorEstado.Elementos = _estados;
        SelectorEstado.IndiceSeleccionado = 0;

        SelectorEnvio.Elementos = _envios;
        SelectorEnvio.IndiceSeleccionado = 0;

        SelectorTipo.Elementos = _tipos;
        SelectorTipo.IndiceSeleccionado = 0;

        SelectorOrden.Elementos = _camposOrden;
        SelectorOrden.IndiceSeleccionado = 0;

        SelectorOrden.IndiceCambiado += (_, _) =>
        {
            if (!_restaurando) EjecutarBusqueda(BusquedaActual);
        };
    }

    private void GuardarEstado()
    {
        var estado = new FiltrosPersistidos(
            SelectorAnio.ElementoSeleccionado as string,
            SelectorMes.IndiceSeleccionado,
            SelectorEstado.IndiceSeleccionado,
            SelectorCreador.IndiceSeleccionado,
            SelectorEnvio.IndiceSeleccionado,
            SelectorTipo.IndiceSeleccionado,
            EntryRfc.Text,
            SelectorOrden.IndiceSeleccionado,
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
                SelectorAnio.ElementoSeleccionado = estado.Anio;

            if (estado.MesIndex >= 0)
                SelectorMes.IndiceSeleccionado = estado.MesIndex;

            if (estado.EstadoIndex >= 0)
                SelectorEstado.IndiceSeleccionado = estado.EstadoIndex;

            if (estado.CreadorIndex >= 0
                && SelectorCreador.Elementos is { Count: > 0 } c
                && estado.CreadorIndex < c.Count)
                SelectorCreador.IndiceSeleccionado = estado.CreadorIndex;

            if (estado.EnvioIndex >= 0)
                SelectorEnvio.IndiceSeleccionado = estado.EnvioIndex;

            if (estado.TipoIndex >= 0)
                SelectorTipo.IndiceSeleccionado = estado.TipoIndex;

            EntryRfc.Text = estado.Rfc ?? string.Empty;

            if (estado.OrdenIndex >= 0)
                SelectorOrden.IndiceSeleccionado = estado.OrdenIndex;

            if (_ordenAscendente != estado.OrdenAscendente)
            {
                _ordenAscendente = estado.OrdenAscendente;
                IconOrden.Icon = _ordenAscendente
                    ? MaterialIcons.ArrowUpward
                    : MaterialIcons.ArrowDownward;
            }

            ActualizarPeriodoTexto();
        }
        finally
        {
            _restaurando = false;
        }
    }

    public Busqueda BusquedaActual
    {
        get
        {
            var filtros = new List<Filtro>();

            var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
            if (cuentaFiscalId.HasValue)
                filtros.Add(new Filtro { Propiedad = "CuentaFiscalId", Operador = Operador.Igual, Valores = [cuentaFiscalId.Value.ToString()] });

            if (SelectorAnio.ElementoSeleccionado is string anioStr && int.TryParse(anioStr, out int anio) && SelectorMes.IndiceSeleccionado >= 0)
            {
                int mes = SelectorMes.IndiceSeleccionado + 1;
                int ultimoDia = DateTime.DaysInMonth(anio, mes);
                string inicio = $"{anio:D4}-{mes:D2}-01 06:00:00.000Z";
                string fin    = $"{anio:D4}-{mes:D2}-{ultimoDia:D2} 06:00:00.000Z";
                filtros.Add(new Filtro { Propiedad = "FechaCreacion", Operador = Operador.Entre, Valores = [inicio, fin] });
            }

            if (SelectorEstado.IndiceSeleccionado > 0 && SelectorEstado.ElementoSeleccionado is string estado
                && _estadoEnum.TryGetValue(estado, out string? estadoVal))
                filtros.Add(new Filtro { Propiedad = "Estado", Operador = Operador.Igual, Valores = [estadoVal] });

            if (SelectorCreador.IndiceSeleccionado > 0
                && CreadoresIds is not null
                && SelectorCreador.IndiceSeleccionado < CreadoresIds.Count)
                filtros.Add(new Filtro { Propiedad = "creador", Operador = Operador.Igual, Valores = [CreadoresIds[SelectorCreador.IndiceSeleccionado]] });

            if (SelectorEnvio.IndiceSeleccionado > 0 && SelectorEnvio.ElementoSeleccionado is string envio)
                filtros.Add(new Filtro { Propiedad = "TipoRecepcion", Operador = Operador.Igual, Valores = [envio] });

            if (SelectorTipo.IndiceSeleccionado > 0 && SelectorTipo.ElementoSeleccionado is string tipo
                && _tipoEnum.TryGetValue(tipo, out string? tipoVal))
                filtros.Add(new Filtro { Propiedad = "Tipo", Operador = Operador.Igual, Valores = [tipoVal] });

            if (!string.IsNullOrWhiteSpace(EntryRfc.Text))
                filtros.Add(new Filtro { Propiedad = "RfcEmisor", Operador = Operador.Contiene, Valores = [EntryRfc.Text.Trim()] });

            return new Busqueda
            {
                Filtros = filtros,
                OrdernarDesc = !_ordenAscendente,
                OrdenarPropiedad = MapearCampoOrden(SelectorOrden.ElementoSeleccionado as string ?? "Creacion"),
                Paginado = new Paginado { Pagina = 1, TamanoPagina = 10 },
                Contar = true
            };
        }
    }

    public void IrARecientes()
    {
        var filtros = new List<Filtro>();

        var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (cuentaFiscalId.HasValue)
            filtros.Add(new Filtro { Propiedad = "CuentaFiscalId", Operador = Operador.Igual, Valores = [cuentaFiscalId.Value.ToString()] });

        var hoy = DateTime.Now;
        SelectorAnio.ElementoSeleccionado = hoy.Year.ToString();
        SelectorMes.IndiceSeleccionado = hoy.Month - 1;
        int ultimoDia = DateTime.DaysInMonth(hoy.Year, hoy.Month);
        string inicio = $"{hoy.Year:D4}-{hoy.Month:D2}-01 06:00:00.000Z";
        string fin    = $"{hoy.Year:D4}-{hoy.Month:D2}-{ultimoDia:D2} 06:00:00.000Z";
        filtros.Add(new Filtro { Propiedad = "FechaCreacion", Operador = Operador.Entre, Valores = [inicio, fin] });

        SelectorEstado.IndiceSeleccionado  = 0;
        SelectorCreador.IndiceSeleccionado = -1;
        SelectorEnvio.IndiceSeleccionado   = 0;
        SelectorTipo.IndiceSeleccionado    = 0;
        EntryRfc.Text                      = string.Empty;

        ActualizarPeriodoTexto();
        ActualizarIndicadorFiltros();
        var busqueda = new Busqueda { Filtros = filtros, Paginado = new Paginado { Pagina = 1, TamanoPagina = 10 }, Contar = true };
        EjecutarBusqueda(busqueda);
    }

    private void OnBuscarTapped(object sender, TappedEventArgs e)
    {
        EjecutarBusqueda(BusquedaActual);
    }

    private void EjecutarBusqueda(Busqueda busqueda)
    {
        ActualizarPeriodoTexto();
        GuardarEstado();
        if (BuscarCommand?.CanExecute(busqueda) == true)
            BuscarCommand.Execute(busqueda);
    }

    private async void OnToggleExpandir(object sender, TappedEventArgs e)
    {
        _expandido = !_expandido;

        IconExpandir.Icon = _expandido
            ? MaterialIcons.KeyboardArrowUp
            : MaterialIcons.KeyboardArrowDown;

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
        var hoy = DateTime.Now;
        SelectorAnio.ElementoSeleccionado = hoy.Year.ToString();
        SelectorMes.IndiceSeleccionado = hoy.Month - 1;

        SelectorEstado.IndiceSeleccionado  = 0;
        SelectorCreador.IndiceSeleccionado = -1;
        SelectorEnvio.IndiceSeleccionado   = 0;
        SelectorTipo.IndiceSeleccionado    = 0;
        EntryRfc.Text                      = string.Empty;

        ActualizarIndicadorFiltros();
        ActualizarPeriodoTexto();
        EjecutarBusqueda(BusquedaActual);
    }

    private bool TieneFiltrosExtra()
    {
        return SelectorEstado.IndiceSeleccionado > 0
            || SelectorCreador.IndiceSeleccionado > 0
            || SelectorEnvio.IndiceSeleccionado > 0
            || SelectorTipo.IndiceSeleccionado > 0
            || !string.IsNullOrWhiteSpace(EntryRfc.Text);
    }

    private void ActualizarIndicadorFiltros()
    {
        if (TieneFiltrosExtra())
        {
            IconFiltro.IconColor = ContaBeeMovil.Converters.EstadoBadgeColorConverter.ResolveColor("Primary", Colors.Orange);
        }
        else
        {
            IconFiltro.IconColor = ContaBeeMovil.Converters.EstadoBadgeColorConverter.ResolveColor("PrimaryText", Colors.White);
        }
    }

    private void OnToggleOrden(object sender, TappedEventArgs e)
    {
        _ordenAscendente = !_ordenAscendente;
        IconOrden.Icon = _ordenAscendente
            ? MaterialIcons.ArrowUpward
            : MaterialIcons.ArrowDownward;
        EjecutarBusqueda(BusquedaActual);
    }

    private static void OnCreadoresChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FiltrosFacturasView view)
        {
            view.SelectorCreador.Elementos = (newValue as IList<string>)?.ToList();
            if (view.SelectorCreador.Elementos?.Count > 0)
                view.SelectorCreador.IndiceSeleccionado = -1;
        }
    }

    private static string MapearCampoOrden(string campo) => campo switch
    {
        "Creacion" => "FechaCreacion",
        _          => campo
    };

    private void ActualizarPeriodoTexto()
    {
        if (SelectorAnio.ElementoSeleccionado is string anioStr
            && SelectorMes.IndiceSeleccionado >= 0
            && SelectorMes.IndiceSeleccionado < _meses.Count)
        {
            string mes = _meses[SelectorMes.IndiceSeleccionado];
            string mesParaMostrar = _mesesAbreviados.GetValueOrDefault(mes, mes);
            PeriodoTexto = $"{mesParaMostrar} {anioStr}";
            PeriodoTextoCompleto = $"Comprobantes {mesParaMostrar} {anioStr}";
        }
        else
        {
            var hoy = DateTime.Now;
            string mes = _meses[hoy.Month - 1];
            string mesParaMostrar = _mesesAbreviados.GetValueOrDefault(mes, mes);
            PeriodoTexto = $"{mesParaMostrar} {hoy.Year}";
            PeriodoTextoCompleto = $"Comprobantes {mesParaMostrar} {hoy.Year}";
        }
    }

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
