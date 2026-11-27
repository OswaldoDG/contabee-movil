using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Contabee.Api.abstractions;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Invitar;

public partial class FilaCorreo : ObservableObject
{
    [ObservableProperty]
    private string _texto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneErrorVisible))]
    private string? _error;

    [ObservableProperty]
    private bool _esValida;

    // El error de formato sólo se muestra cuando el usuario sale de la fila (o la pegó),
    // para no marcar error mientras escribe.
    public bool Tocada { get; set; }

    public bool TieneErrorVisible => !string.IsNullOrEmpty(Error);
}

public partial class InvitarPage : ContentPage
{
    // Caracteres que sólo llegan al pegar una lista (o "Nombre <correo>"); el espacio se
    // trata aparte (sólo reparte si separa dos correos).
    private static readonly char[] SeparadoresPegado = [',', ';', '\r', '\n', '\t', '<'];

    private readonly IServicioIdentidad _identidad;
    private readonly IServicioAlerta _alerta;
    private readonly IServicioToast _toast;
    private readonly IServicioLogs _logs;

    private bool _repartiendo;
    private bool _enviando;

    public ObservableCollection<FilaCorreo> Filas { get; } = [new FilaCorreo()];

    public bool PuedeQuitar => Filas.Count > 1;
    public bool PuedeAgregar => Filas.Count < CorreosInvitacion.Maximo;
    public string TextoContador => $"{Filas.Count} de {CorreosInvitacion.Maximo}";

    private bool _hayErrores;
    public bool HayErrores
    {
        get => _hayErrores;
        private set { if (_hayErrores == value) return; _hayErrores = value; OnPropertyChanged(); }
    }

    public InvitarPage(IServicioIdentidad identidad, IServicioAlerta alerta, IServicioToast toast, IServicioLogs logs)
    {
        InitializeComponent();
        _identidad = identidad;
        _alerta = alerta;
        _toast = toast;
        _logs = logs;

        Filas.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PuedeQuitar));
            OnPropertyChanged(nameof(PuedeAgregar));
            OnPropertyChanged(nameof(TextoContador));
            Revalidar();
        };
        BindingContext = this;
    }

    private void OnCorreoTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_repartiendo || sender is not BindableObject { BindingContext: FilaCorreo fila }) return;

        var texto = e.NewTextValue ?? string.Empty;
        var partes = CorreosInvitacion.Separar(texto);
        if (partes.Count > 1 || texto.IndexOfAny(SeparadoresPegado) >= 0)
            // Fuera del TextChanged: cambiar el Text del Entry dentro de su propio evento falla en Android.
            Dispatcher.Dispatch(() => Repartir(fila, partes));
        else
            Revalidar();
    }

    // Pegaron una lista en una fila: la fila se queda con el primero y el resto va en filas nuevas debajo.
    private async void Repartir(FilaCorreo fila, List<string> partes)
    {
        var sobrantes = 0;
        _repartiendo = true;
        try
        {
            // Sin repetidos: ni dentro de lo pegado ni contra lo que ya está en otras filas,
            // para no gastar cupos de los 5.
            var existentes = Filas.Where(f => f != fila).Select(f => CorreosInvitacion.Normalizar(f.Texto)).ToHashSet();
            var nuevos = partes.Distinct().Where(c => !existentes.Contains(c)).ToList();

            fila.Texto = nuevos.FirstOrDefault() ?? string.Empty;
            fila.Tocada = true;

            var indice = Filas.IndexOf(fila);
            foreach (var correo in nuevos.Skip(1))
            {
                if (Filas.Count >= CorreosInvitacion.Maximo) { sobrantes++; continue; }
                Filas.Insert(++indice, new FilaCorreo { Texto = correo, Tocada = true });
            }
        }
        finally
        {
            _repartiendo = false;
        }

        Revalidar();
        if (sobrantes > 0)
            await _toast.MostrarAsync($"Sólo puedes invitar hasta {CorreosInvitacion.Maximo} correos a la vez", ToastIcono.Warning, ToastPosicion.Bottom);
    }

    private void OnCorreoUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is BindableObject { BindingContext: FilaCorreo fila })
        {
            fila.Tocada = true;
            Revalidar();
        }
    }

    private void OnQuitarTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: FilaCorreo fila } && Filas.Count > 1)
            Filas.Remove(fila);
    }

    private void OnAgregarTapped(object? sender, TappedEventArgs e)
    {
        if (PuedeAgregar)
            Filas.Add(new FilaCorreo());
    }

    // Filas vacías se ignoran. Enviar requiere al menos un correo, todos válidos y sin repetidos.
    private void Revalidar()
    {
        var vistos = new HashSet<string>();
        var hayCorreo = false;
        var todoValido = true;

        foreach (var fila in Filas)
        {
            var correo = CorreosInvitacion.Normalizar(fila.Texto);
            string? error = null;
            var valida = false;

            if (correo.Length == 0) { }
            else if (!CorreosInvitacion.EsValido(correo))
            {
                todoValido = false;
                if (fila.Tocada) error = "Revisa este correo, por ejemplo nombre@gmail.com";
            }
            else if (!vistos.Add(correo))
            {
                todoValido = false;
                error = "Ya agregaste este correo";
            }
            else
            {
                hayCorreo = true;
                valida = true;
            }

            fila.Error = error;
            fila.EsValida = valida;
        }

        HayErrores = Filas.Any(f => f.TieneErrorVisible);
        if (BtnEnviar is not null)
            BtnEnviar.IsEnabled = hayCorreo && todoValido && !_enviando;
    }

    private void MostrarLoader(bool visible)
    {
        Loader.IsRunning = visible;
        Loader.Opacity = visible ? 1 : 0;
    }

    private async void OnEnviarClicked(object? sender, EventArgs e)
    {
        if (_enviando) return;
        _enviando = true;
        BtnEnviar.IsEnabled = false;
        MostrarLoader(true);

        string mensaje;
        try
        {
            var correos = Filas.Select(f => CorreosInvitacion.Normalizar(f.Texto))
                               .Where(c => c.Length > 0)
                               .ToList();

            var resultado = await _identidad.InvitarPorEmail(correos);
            if (resultado.Ok)
            {
                MostrarLoader(false);
                await _alerta.MostrarAsync("Invitación enviada", "Enviamos la invitación a tus contactos.",
                    verBotonCancelar: false, confirmarText: "OK");
                // Con red lenta el usuario pudo haberse ido de la página: no cerrar otra.
                if (Shell.Current.CurrentPage == this)
                    await Shell.Current.GoToAsync("..");
                return;
            }

            // Mensaje fijo por código: el Mensaje de ErrorGenerico sale del texto crudo de la excepción.
            _logs.Warn($"[InvitarPage] InvitarPorEmail falló: {resultado.Error?.HttpCode}");
            mensaje = resultado.Error?.HttpCode switch
            {
                HttpStatusCode.BadRequest => $"Puedes invitar de 1 a {CorreosInvitacion.Maximo} correos a la vez.",
                HttpStatusCode.ServiceUnavailable => "No fue posible enviar las invitaciones, intenta más tarde.",
                _ => "Ocurrió un error al enviar las invitaciones."
            };
        }
        catch (Exception ex)
        {
            _logs.Error($"[InvitarPage] {ex.GetType().Name}: {ex.Message}");
            mensaje = "Ocurrió un error al enviar las invitaciones.";
        }

        // Reactivar antes del toast: MostrarAsync tarda ~3.5 s y el 503 invita a reintentar.
        MostrarLoader(false);
        _enviando = false;
        Revalidar();
        await _toast.MostrarAsync(mensaje, ToastIcono.Error, ToastPosicion.Bottom);
    }
}
