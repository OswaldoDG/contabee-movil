using System.Windows.Input;
using ContaBeeMovil.Services.Device;
using MauiIcons.Material;
using Busqueda = Contabee.Api.Transcript.Busqueda;
using Filtro = Contabee.Api.Transcript.Filtro;
using Operador = Contabee.Api.Transcript.Operador;
using Paginado = Contabee.Api.Transcript.Paginado;

namespace ContaBeeMovil.Views;

public partial class FiltrosDevolucionesView : ContentView
{
    public static string PrefijoPreferencias => PrefsKeyFiltros;

    private readonly List<string> _destinatariosIds = [];

    private SelectorFlotante? SelectorDestinatarioView => FindByName("SelectorDestinatario") as SelectorFlotante;

    private static readonly List<string> _meses =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    private static readonly Dictionary<string, string> _mesesAbreviados = new()
    {
        ["Febrero"] = "Feb",
        ["Agosto"] = "Ago",
        ["Septiembre"] = "Sep",
        ["Octubre"] = "Oct",
        ["Noviembre"] = "Nov",
        ["Diciembre"] = "Dic"
    };

    private static readonly List<string> _estados =
        ["Todos", "Creada", "Admitida", "Aceptada", "Declinada"];

    private static readonly List<string> _estadosFiltroTodos =
        ["Creada", "Admitida", "Aceptada", "Declinada"];

    private static readonly List<string> _camposOrden =
        ["Apertura", "Monto"];

    private bool _expandido = true;
    private bool _ordenAscendente;
    private const string PrefsKeyFiltros = "FiltrosDevoluciones_UltimaConsulta";

    public static readonly BindableProperty BuscarCommandProperty =
        BindableProperty.Create(
            nameof(BuscarCommand),
            typeof(ICommand),
            typeof(FiltrosDevolucionesView));

    public ICommand? BuscarCommand
    {
        get => (ICommand?)GetValue(BuscarCommandProperty);
        set => SetValue(BuscarCommandProperty, value);
    }

    public static readonly BindableProperty PeriodoTextoProperty =
        BindableProperty.Create(
            nameof(PeriodoTexto),
            typeof(string),
            typeof(FiltrosDevolucionesView),
            defaultValue: string.Empty);

    public string PeriodoTexto
    {
        get => (string)GetValue(PeriodoTextoProperty);
        set => SetValue(PeriodoTextoProperty, value);
    }

    public FiltrosDevolucionesView()
    {
        InitializeComponent();
        InicializarSelectores();
        ActualizarPeriodoTexto();

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.MisUsuarios) or nameof(AppState.CuentaFiscalActual))
                CargarDestinatarios();
        };

        Loaded += (_, _) => CargarDestinatarios();
    }

    private void InicializarSelectores()
    {
        var anioActual = DateTime.Now.Year;
        SelectorAnio.Elementos = new List<string> { anioActual.ToString(), (anioActual - 1).ToString() };
        SelectorAnio.ElementoSeleccionado = anioActual.ToString();

        SelectorMes.Elementos = _meses;
        SelectorMes.IndiceSeleccionado = DateTime.Now.Month - 1;

        SelectorEstado.Elementos = _estados;
        SelectorEstado.IndiceSeleccionado = 0;

        CargarDestinatarios();

        SelectorOrden.Elementos = _camposOrden;
        SelectorOrden.IndiceSeleccionado = 0;

        RestaurarEstado();

        SelectorAnio.IndiceCambiado += (_, _) => ActualizarPeriodoTexto();
        SelectorMes.IndiceCambiado += (_, _) => ActualizarPeriodoTexto();
    }

    public void RestaurarEstado()
    {
        CargarDestinatarios();

        var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (!cuentaFiscalId.HasValue) return;

        var json = Preferences.Default.Get($"{PrefsKeyFiltros}_{cuentaFiscalId.Value}", string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        var estado = Newtonsoft.Json.JsonConvert.DeserializeObject<FiltrosPersistidos>(json);
        if (estado is null) return;

        if (!string.IsNullOrWhiteSpace(estado.Anio)
            && SelectorAnio.Elementos?.Contains(estado.Anio) == true)
            SelectorAnio.ElementoSeleccionado = estado.Anio;

        if (estado.MesIndex >= 0 && estado.MesIndex < _meses.Count)
            SelectorMes.IndiceSeleccionado = estado.MesIndex;

        if (estado.EstadoIndex >= 0 && SelectorEstado.Elementos is { Count: > 0 } e && estado.EstadoIndex < e.Count)
            SelectorEstado.IndiceSeleccionado = estado.EstadoIndex;

        if (estado.DestinatarioIndex > 0
            && SelectorDestinatarioView?.Elementos is { Count: > 0 } d
            && estado.DestinatarioIndex < d.Count)
            SelectorDestinatarioView.IndiceSeleccionado = estado.DestinatarioIndex;

        if (estado.OrdenIndex >= 0 && SelectorOrden.Elementos is { Count: > 0 } o && estado.OrdenIndex < o.Count)
            SelectorOrden.IndiceSeleccionado = estado.OrdenIndex;

        _ordenAscendente = estado.OrdenAscendente;
        IconOrden.Icon = _ordenAscendente
            ? MaterialIcons.ArrowUpward
            : MaterialIcons.ArrowDownward;
    }

    private void GuardarEstado()
    {
        var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (!cuentaFiscalId.HasValue) return;

        var estado = new FiltrosPersistidos(
            SelectorAnio.ElementoSeleccionado as string,
            SelectorMes.IndiceSeleccionado,
            SelectorEstado.IndiceSeleccionado,
            SelectorDestinatarioView?.IndiceSeleccionado ?? -1,
            SelectorOrden.IndiceSeleccionado,
            _ordenAscendente);

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(estado);
        Preferences.Default.Set($"{PrefsKeyFiltros}_{cuentaFiscalId.Value}", json);
    }

    public Busqueda BusquedaActual
    {
        get
        {
            var filtros = new List<Filtro>();

            Guid? cuentaFiscalIdSeleccionada = null;
            var indiceDestinatario = SelectorDestinatarioView?.IndiceSeleccionado ?? 0;
            if (indiceDestinatario >= 0
                && indiceDestinatario < _destinatariosIds.Count
                && Guid.TryParse(_destinatariosIds[indiceDestinatario], out var cfidDestino))
            {
                cuentaFiscalIdSeleccionada = cfidDestino;
            }

            var cuentaFiscalId = cuentaFiscalIdSeleccionada ?? AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
            if (cuentaFiscalId.HasValue)
            {
                filtros.Add(new Filtro
                {
                    Propiedad = "CuentaFiscalId",
                    Operador = Operador.Igual,
                    Valores = [cuentaFiscalId.Value.ToString()]
                });
            }

            if (SelectorAnio.ElementoSeleccionado is string anioStr
                && int.TryParse(anioStr, out var anio)
                && SelectorMes.IndiceSeleccionado >= 0)
            {
                var mes = SelectorMes.IndiceSeleccionado + 1;
                var ultimoDia = DateTime.DaysInMonth(anio, mes);
                var inicio = $"{anio:D4}-{mes:D2}-01 06:00:00.000Z";
                var fin = $"{anio:D4}-{mes:D2}-{ultimoDia:D2} 06:00:00.000Z";

                filtros.Add(new Filtro
                {
                    Propiedad = "Creacion",
                    Operador = Operador.Entre,
                    Valores = [inicio, fin]
                });
            }

        var estados = SelectorEstado.IndiceSeleccionado <= 0
            ? _estadosFiltroTodos
            : SelectorEstado.ElementoSeleccionado is string estadoSeleccionado
                ? [estadoSeleccionado]
                : _estadosFiltroTodos;

        filtros.Add(new Filtro
        {
            Propiedad = "Estado",
            Operador = Operador.Igual,
            Valores = estados
        });

            return new Busqueda
            {
                Filtros = filtros,
                OrdernarDesc = !_ordenAscendente,
                OrdenarPropiedad = MapearCampoOrden(SelectorOrden.ElementoSeleccionado as string ?? "Apertura"),
                Paginado = new Paginado { Pagina = 1, TamanoPagina = 10 },
                Contar = true
            };
        }
    }

    public void SeleccionarPeriodoActual()
    {
        var ahora = DateTime.Now;
        var anio = ahora.Year.ToString();

        if (SelectorAnio.Elementos?.Contains(anio) == true)
            SelectorAnio.ElementoSeleccionado = anio;

        SelectorMes.IndiceSeleccionado = ahora.Month - 1;
        ActualizarPeriodoTexto();
        GuardarEstado();
    }

    private static string MapearCampoOrden(string campo) => campo switch
    {
        "Apertura" => "Creacion",
        _ => campo
    };

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
    }

    private void OnToggleOrden(object sender, TappedEventArgs e)
    {
        _ordenAscendente = !_ordenAscendente;
        IconOrden.Icon = _ordenAscendente
            ? MaterialIcons.ArrowUpward
            : MaterialIcons.ArrowDownward;
        EjecutarBusqueda(BusquedaActual);
    }

    private void ActualizarPeriodoTexto()
    {
        if (SelectorAnio.ElementoSeleccionado is string anioStr
            && SelectorMes.IndiceSeleccionado >= 0
            && SelectorMes.IndiceSeleccionado < _meses.Count)
        {
            var mes = _meses[SelectorMes.IndiceSeleccionado];
            var mesParaMostrar = _mesesAbreviados.GetValueOrDefault(mes, mes);
            PeriodoTexto = $"{mesParaMostrar}/{anioStr}";
        }
        else
        {
            var hoy = DateTime.Now;
            var mes = _meses[hoy.Month - 1];
            var mesParaMostrar = _mesesAbreviados.GetValueOrDefault(mes, mes);
            PeriodoTexto = $"{mesParaMostrar}/{hoy.Year}";
        }
    }

    private void CargarDestinatarios()
    {
        var usuarios = AppState.Instance.MisUsuarios ?? [];
        var cuentaFiscalActualId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;

        _destinatariosIds.Clear();
        if (SelectorDestinatarioView is null) return;

        var elementosDestinatario = new List<string> { "Todos" };
        _destinatariosIds.Add(string.Empty);

        if (cuentaFiscalActualId.HasValue)
        {
            elementosDestinatario.Add(ObtenerEtiquetaUsuarioSesion());
            _destinatariosIds.Add(cuentaFiscalActualId.Value.ToString());
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
                elementosDestinatario.Add($"{nombre} (Secundaria)");
                _destinatariosIds.Add(u.CuentaFiscalId?.ToString() ?? cuentaFiscalActualId?.ToString() ?? string.Empty);
            }
        }

        SelectorDestinatarioView.Elementos = elementosDestinatario;
        SelectorDestinatarioView.IndiceSeleccionado = 0;
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

    private static bool EsCapturista(Contabee.Api.Identidad.TipoCuentaUsuario tipoCuenta)
        => tipoCuenta is Contabee.Api.Identidad.TipoCuentaUsuario.UsuarioCaptura;

    private record FiltrosPersistidos(
        string? Anio,
        int MesIndex,
        int EstadoIndex,
        int DestinatarioIndex,
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
