using System.Windows.Input;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using Contabee.Api;
using ContaBeeMovil.Config;
using ContaBeeMovil.Pages.Devoluciones;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using Contabee.Api.Logging;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Pages.Devoluciones;

public partial class PaginaDevoluciones : ContentPage
{
    internal static bool PendienteActualizarListado { get; set; }

	private readonly IServicioTranscript _servicioTranscript;
	private readonly IServicioAlerta _servicioAlerta;
	private readonly IServicioLogs _logs;
  private readonly IAppLogger _logger;
	private Busqueda? _ultimaBusqueda;
	private int _tamanoPaginaEfectivo = AppSettings.Consulta.TamanoPagina;
	private bool _abriendoPopup;

	private static readonly PopupOptions _popupOpts = new()
	{
		PageOverlayColor = Color.FromArgb("#66000000"),
		CanBeDismissedByTappingOutsideOfPopup = false,
	};

	private bool _estaCargando;
	public bool EstaCargando
	{
		get => _estaCargando;
		private set { _estaCargando = value; OnPropertyChanged(); }
	}

	private bool _mostrarOverlayCarga;
	public bool MostrarOverlayCarga
	{
		get => _mostrarOverlayCarga;
		private set { _mostrarOverlayCarga = value; OnPropertyChanged(); }
	}

	private string _textoOverlayCarga = "Cargando devoluciones...";
	public string TextoOverlayCarga
	{
		get => _textoOverlayCarga;
		private set { _textoOverlayCarga = value; OnPropertyChanged(); }
	}

	private IEnumerable<ItemConConsecutivo>? _elementos;
	public IEnumerable<ItemConConsecutivo>? Elementos
	{
		get => _elementos;
		private set { _elementos = value; OnPropertyChanged(); }
	}

    public record ItemConConsecutivo(int Consecutivo, Devolucion Datos, string CuentaFiscalRfc);

	private long _totalEncontrados;
	public long TotalEncontrados
	{
		get => _totalEncontrados;
		private set { _totalEncontrados = value; OnPropertyChanged(); }
	}

	private int _paginaActual = 1;
	public int PaginaActual
	{
		get => _paginaActual;
		private set { _paginaActual = value; OnPropertyChanged(); }
	}

	private int _totalPaginas = 1;
	public int TotalPaginas
	{
		get => _totalPaginas;
		private set { _totalPaginas = value; OnPropertyChanged(); }
	}

	private bool _consultaEjecutada;
	public bool ConsultaEjecutada
	{
		get => _consultaEjecutada;
		private set { _consultaEjecutada = value; OnPropertyChanged(); }
	}

	public ICommand BuscarDevolucionesCommand { get; }
	public ICommand PaginaAnteriorCommand { get; }
	public ICommand PaginaSiguienteCommand { get; }
	public ICommand AbrirDetalleCommand { get; }

	public FiltrosDevolucionesView Filtros => PanelFiltros;

	public PaginaDevoluciones(
		IServicioTranscript servicioTranscript,
		IServicioAlerta servicioAlerta,
     IServicioLogs logs,
		IAppLogger logger)
	{
		_servicioTranscript = servicioTranscript;
		_servicioAlerta = servicioAlerta;
		_logs = logs;
		_logger = logger;

		BuscarDevolucionesCommand = new Command<Busqueda>(async b => await OnBuscarDevoluciones(b));
		PaginaAnteriorCommand = new Command(async () => await EjecutarBusqueda(PaginaActual - 1));
		PaginaSiguienteCommand = new Command(async () => await EjecutarBusqueda(PaginaActual + 1));
		AbrirDetalleCommand = new Command<ItemConConsecutivo>(async item => await AbrirDetalleAsync(item));

      InitializeComponent();
		BindingContext = this;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
     if (!ConsultaEjecutada)
		{
			_ = OnBuscarDevoluciones(PanelFiltros.BusquedaActual);
           return;
		}

		if (PendienteActualizarListado)
		{
			PendienteActualizarListado = false;
			_ = RecargarConUltimosFiltrosAsync();
		}
	}

	private async Task RecargarConUltimosFiltrosAsync()
	{
		if (_ultimaBusqueda is null)
		{
			await OnBuscarDevoluciones(PanelFiltros.BusquedaActual);
			return;
		}

		await EjecutarBusqueda(Math.Max(1, PaginaActual));
	}

	private async Task AbrirDetalleAsync(ItemConConsecutivo? item)
	{
		if (item is null) return;

		try
		{
			_logger.Info("Devoluciones.AbrirDetalle", "Inicio de navegación a detalle de devolución.");
			await Shell.Current.GoToAsync(nameof(DetalleDevolucionPage),
				new Dictionary<string, object>
				{
					["devolucionId"] = item.Datos.Id,
					["rfc"] = item.CuentaFiscalRfc
				});
			_logger.Info("Devoluciones.AbrirDetalleExitoso", "Navegación a detalle de devolución completada.");
		}
		catch (Exception ex)
		{
			_logger.Debug("Devoluciones.AbrirDetalleException", "Excepción no controlada al navegar al detalle de devolución.", ex);
			await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el detalle de la devolución.", verBotonCancelar: false, confirmarText: "OK");
		}
	}

	public void OnTabActivated()
	{
     if (!PendienteActualizarListado) return;

		PendienteActualizarListado = false;
		_ = RecargarConUltimosFiltrosAsync();
	}

	private async Task OnBuscarDevoluciones(Busqueda busqueda)
	{
		if (AppState.Instance.CuentaFiscalActual is null)
		{
         _logger.Info("Devoluciones.SeleccionarCuentaFiscal", "No hay cuenta fiscal activa, se mostrará selector.");
			await this.ShowPopupAsync(new CuentaFiscalSelectorPopup());
			return;
		}

		_ultimaBusqueda = busqueda;
      _tamanoPaginaEfectivo = AppSettings.Consulta.TamanoPagina;
		await EjecutarBusqueda(1);
	}

	private async Task EjecutarBusqueda(int pagina)
	{
		if (_ultimaBusqueda is null) return;

		_ultimaBusqueda.Paginado = new Paginado
		{
			Pagina = pagina,
			TamanoPagina = AppSettings.Consulta.TamanoPagina
		};

		var filtrosTxt = _ultimaBusqueda.Filtros is null
			? "(sin filtros)"
			: string.Join(" | ", _ultimaBusqueda.Filtros.Select(f =>
				$"{f.Propiedad}:{f.Operador}=[{string.Join(",", f.Valores ?? [])}]"));

        EstaCargando = true;
		try
		{
          _logger.Info("Devoluciones.EjecutarBusqueda", "Inicio de búsqueda de devoluciones.");
          _logs.Log($"[PaginaDevoluciones] Ejecutando búsqueda. Pagina={pagina}, Filtros={_ultimaBusqueda.Filtros?.Count ?? 0}, Orden={_ultimaBusqueda.OrdenarPropiedad}, Desc={_ultimaBusqueda.OrdernarDesc}, Payload={filtrosTxt}");
			var resultado = await _servicioTranscript.BusquedaDevoluciones(_ultimaBusqueda);

          var elementosPagina = resultado.Elementos?.ToList() ?? [];
			if (pagina == 1 && elementosPagina.Count > 0)
				_tamanoPaginaEfectivo = elementosPagina.Count;

			var offset = (pagina - 1) * _tamanoPaginaEfectivo;
			Elementos = elementosPagina
				.Select((e, i) => new ItemConConsecutivo(offset + i + 1, e, ResolverRfcCuentaFiscal(e)))
				.ToList();

			TotalEncontrados = resultado.Total;
			PaginaActual = pagina;
          var divisorPaginas = Math.Max(1, _tamanoPaginaEfectivo);
			TotalPaginas = (int)Math.Ceiling((double)resultado.Total / divisorPaginas);
			if (TotalPaginas < 1) TotalPaginas = 1;
			ConsultaEjecutada = true;
          _logger.Info("Devoluciones.EjecutarBusquedaExitoso", "Búsqueda de devoluciones completada.");
		}
        catch (ApiException ex)
		{
          _logger.Debug("Devoluciones.EjecutarBusquedaApiException", "La API devolvió error en búsqueda de devoluciones.", new Dictionary<string, object?>
			{
              ["StatusCode"] = ex.StatusCode,
				["Mensaje"] = ex.Message
			});
			_logs.Log($"[PaginaDevoluciones] ApiException Status={ex.StatusCode} Body={ex.Response}");
			await _servicioAlerta.MostrarAsync(
				"Error",
				!string.IsNullOrWhiteSpace(ex.Response)
					? ex.Response
					: "No se pudieron cargar las devoluciones. Intenta de nuevo.",
				verBotonCancelar: false,
				confirmarText: "OK");
		}
		catch (Exception ex)
		{
          _logger.Debug("Devoluciones.EjecutarBusquedaException", "Excepción no controlada al buscar devoluciones.", ex);
			_logs.Log($"[PaginaDevoluciones] {ex.GetType().Name}: {ex.Message}");
			await _servicioAlerta.MostrarAsync(
				"Error",
				"No se pudieron cargar las devoluciones. Intenta de nuevo.",
				verBotonCancelar: false,
				confirmarText: "OK");
		}
		finally
		{
			EstaCargando = false;
		}
	}

  private async void OnCrearDevolucionClicked(object sender, TappedEventArgs e)
	{
      if (_abriendoPopup) return;
		_abriendoPopup = true;

       try
		{
           _logger.Info("Devoluciones.Crear", "Inicio del flujo de creación de devolución.");
           var hostPage = ObtenerPaginaActivaParaPopup() ?? this;
			_logs.Log($"[PaginaDevoluciones-Crear] HostPage={hostPage.GetType().Name}");

			if (AppState.Instance.CuentaFiscalActual is null)
			{
                await hostPage.ShowPopupAsync(new CuentaFiscalSelectorPopup(), _popupOpts, CancellationToken.None);
				return;
			}

			string? descripcion = null;
          await hostPage.ShowPopupAsync(
            new CrearDevolucionPopup(result => descripcion = result),
				_popupOpts,
				CancellationToken.None);

            if (descripcion is null) return;

            var request = new CreaDevolucion
			{
				CuentaFiscalId = AppState.Instance.CuentaFiscalActual.CuentaFiscalId,
				Descripcion = descripcion
			};

            TextoOverlayCarga = "Creando devolución...";
			MostrarOverlayCarga = true;
			var respuesta = await _servicioTranscript.CrearDevolucionAsync(request);
			if (!respuesta.Ok)
			{
             _logger.Debug("Devoluciones.CrearError", "La API devolvió error al crear devolución.", new Dictionary<string, object?>
				{
					["Codigo"] = respuesta.Error?.Codigo,
					["Mensaje"] = respuesta.Error?.Mensaje,
					["HttpCode"] = (int?)respuesta.Error?.HttpCode
				});
             MostrarOverlayCarga = false;
				await _servicioAlerta.MostrarAsync(
					"Error",
					respuesta.Error?.Mensaje ?? "No se pudo crear la devolución.",
					verBotonCancelar: false,
					confirmarText: "OK");
				return;
			}

            PanelFiltros.SeleccionarPeriodoActual();
			if (_ultimaBusqueda is null)
				_ultimaBusqueda = PanelFiltros.BusquedaActual;
			else
				_ultimaBusqueda = PanelFiltros.BusquedaActual;

          MostrarOverlayCarga = false;
			await EjecutarBusqueda(1);
           _logger.Info("Devoluciones.CrearExitoso", "Devolución creada correctamente.");
		}
        catch (Exception ex)
		{
         _logger.Debug("Devoluciones.CrearException", "Excepción no controlada al crear devolución.", ex);
			_logs.Log($"[PaginaDevoluciones-Crear] {ex.GetType().Name}: {ex.Message}");
			await _servicioAlerta.MostrarAsync(
				"Error",
				"No se pudo abrir o crear la devolución.",
				verBotonCancelar: false,
				confirmarText: "OK");
		}
       finally
		{
         MostrarOverlayCarga = false;
			_abriendoPopup = false;
		}
	}

	private static Page? ObtenerPaginaActivaParaPopup()
	{
		var page = Application.Current?.Windows.FirstOrDefault()?.Page;

		if (page is Shell shell)
			page = shell.CurrentPage;

		if (page is NavigationPage navigationPage)
			page = navigationPage.CurrentPage;

		if (page is TabbedPage tabbedPage)
			page = tabbedPage.CurrentPage;

		return page;
	}

	private static string ResolverRfcCuentaFiscal(Devolucion devolucion)
	{
		var cuentaFiscalId = ObtenerCuentaFiscalIdComoTexto(devolucion);
		if (string.IsNullOrWhiteSpace(cuentaFiscalId))
			return "RFC no disponible";

		var cuenta = AppState.Instance.CuentasFiscales?
			.FirstOrDefault(c => c.CuentaFiscalId.ToString().Equals(cuentaFiscalId, StringComparison.OrdinalIgnoreCase));

		return string.IsNullOrWhiteSpace(cuenta?.Rfc) ? "RFC no disponible" : cuenta.Rfc;
	}

	private static string? ObtenerCuentaFiscalIdComoTexto(Devolucion devolucion)
	{
		var prop = devolucion.GetType().GetProperty("CuentaFiscalId");
		return prop?.GetValue(devolucion)?.ToString();
	}
}
