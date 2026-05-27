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
        ["Todos", "Nuevos", "En Proceso", "Reprogramados", "Finalizados", "Error"];

    private static readonly Dictionary<string, string> _estadoEnum = new()
    {
        ["Nuevos"]       = nameof(EstadoFactura.CargaCompleta),
        ["En Proceso"]   = nameof(EstadoFactura.EnProceso),
        ["Reprogramados"]= nameof(EstadoFactura.Reprogramado),
        ["Finalizados"]  = nameof(EstadoFactura.Finalizado),
        ["Error"]        = nameof(EstadoFactura.FinalizadoError),
    };

    private static readonly List<string> _envios =
        ["Todos", "Foto", "Email"];

    private static readonly List<string> _tipos =
        ["Todos", "Captura individual", "Comprobaçión", "Devolucion"];

    private static readonly Dictionary<string, string> _tipoEnum = new()
    {
        ["Captura individual"] = nameof(TipoProcesoCaptura.FacturaIndividual),
        ["Comprobaçión"]       = nameof(TipoProcesoCaptura.Comprobacion),
        ["Devolucion"]         = nameof(TipoProcesoCaptura.Devolucion),
    };

    private static readonly List<string> _camposOrden =
        ["Creacion", "Monto",];

    private readonly List<string> _creadoresIds = [];

    private bool _expandido = true;
    private bool _ordenAscendente = false;
    private bool _restaurando;

    private const string PrefsKeyFiltros = "FiltrosFacturas_UltimaConsulta";

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

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.MisUsuarios) or nameof(AppState.CuentaFiscalActual))
                CargarCreadores();
        };

        Loaded += (_, _) => CargarCreadores();
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

        CargarCreadores();

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
        CargarCreadores();

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

            Guid? cuentaFiscalIdSeleccionada = null;
            var indiceCreador = SelectorCreador.IndiceSeleccionado;
            if (indiceCreador > 0
                && indiceCreador < _creadoresIds.Count
                && Guid.TryParse(_creadoresIds[indiceCreador], out var cfidDestino))
            {
                cuentaFiscalIdSeleccionada = cfidDestino;
            }

            var cuentaFiscalId = cuentaFiscalIdSeleccionada ?? AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
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
        SelectorCreador.IndiceSeleccionado = 0;
        SelectorEnvio.IndiceSeleccionado   = 0;
        SelectorTipo.IndiceSeleccionado    = 0;
        EntryRfc.Text                      = string.Empty;

        ActualizarPeriodoTexto();
        ActualizarIndicadorFiltros();
        var busqueda = new Busqueda
        {
            Filtros = filtros,
            OrdernarDesc = !_ordenAscendente,
            OrdenarPropiedad = MapearCampoOrden(SelectorOrden.ElementoSeleccionado as string ?? "Creacion"),
            Paginado = new Paginado { Pagina = 1, TamanoPagina = 10 },
            Contar = true
        };
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
        SelectorCreador.IndiceSeleccionado = 0;
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

    private void CargarCreadores()
    {
        var usuarios = AppState.Instance.MisUsuarios ?? [];
        var cuentaFiscalActualId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;

        _creadoresIds.Clear();

        var elementosCreador = new List<string> { "Todos" };
        _creadoresIds.Add(string.Empty);

        if (cuentaFiscalActualId.HasValue)
        {
            elementosCreador.Add(ObtenerEtiquetaUsuarioSesion());
            _creadoresIds.Add(cuentaFiscalActualId.Value.ToString());
        }

        if (!AppState.Instance.EsLoginLess && EsUsuarioPrimario())
        {
            var usuarioSesionId = (usuarios
                .FirstOrDefault(u => !u.EsCaptura && u.CuentaFiscalId == cuentaFiscalActualId)
                ?? usuarios.FirstOrDefault(u => !u.EsCaptura))?.Id;

            var secundarios = usuarios
                .Where(u => !EsCapturista(u.TipoCuenta) && u.Id != usuarioSesionId)
                .ToList();

            foreach (var u in secundarios)
            {
                var nombre = u.Nombre ?? u.UserName ?? u.Email ?? u.Id.ToString();
                elementosCreador.Add($"{nombre} (Secundaria)");
                _creadoresIds.Add(u.CuentaFiscalId?.ToString() ?? cuentaFiscalActualId?.ToString() ?? string.Empty);
            }
        }

        SelectorCreador.Elementos = elementosCreador;
        SelectorCreador.IndiceSeleccionado = 0;
    }

    private static bool EsUsuarioPrimario()
    {
        var cuentaActual = AppState.Instance.CuentaFiscalActual;
        if (cuentaActual is null) return true;
        return !EsCuentaSecundaria(cuentaActual.TipoCuenta.ToString());
    }

    private static bool EsCuentaSecundaria(string? tipoCuenta)
        => string.Equals(tipoCuenta, "Secundaria", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoCuenta, "Secondary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoCuenta, "Empleado", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoCuenta, "EmpleadoCliente", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoCuenta, "UsuarioCaptura", StringComparison.OrdinalIgnoreCase);

    private static string ObtenerEtiquetaUsuarioSesion()
    {
        var nombre = AppState.Instance.Perfil?.DisplayName;

        if (string.IsNullOrWhiteSpace(nombre))
        {
            var cuentaFiscalActualId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
            var usuarioSesion = (AppState.Instance.MisUsuarios ?? [])
                .FirstOrDefault(u => !u.EsCaptura && u.CuentaFiscalId == cuentaFiscalActualId)
                ?? (AppState.Instance.MisUsuarios ?? []).FirstOrDefault(u => !u.EsCaptura)
                ?? (AppState.Instance.MisUsuarios ?? []).FirstOrDefault();

            nombre = usuarioSesion?.Nombre ?? usuarioSesion?.UserName ?? usuarioSesion?.Email;
        }

        if (string.IsNullOrWhiteSpace(nombre))
            nombre = "Yo";

        if (nombre.Contains('@'))
            nombre = nombre[..nombre.IndexOf('@')];

        return $"{nombre} (Yo)";
    }

    private static bool EsCapturista(Contabee.Api.Identidad.TipoCuenta tipoCuenta)
        => tipoCuenta is Contabee.Api.Identidad.TipoCuenta.UsuarioCaptura;

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
