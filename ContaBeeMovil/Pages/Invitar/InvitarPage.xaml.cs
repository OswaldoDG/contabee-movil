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

    // "Correo no válido" sólo se muestra cuando el usuario sale de la fila (o la pegó),
    // para no marcar error mientras escribe.
    public bool Tocada { get; set; }

    public bool TieneErrorVisible => !string.IsNullOrEmpty(Error);
}

public partial class InvitarPage : ContentPage
{
    // Caracteres que sólo llegan al pegar una lista; el espacio se trata aparte
    // (sólo reparte si separa dos correos).
    private static readonly char[] SeparadoresPegado = [',', ';', '\r', '\n', '\t'];

    private readonly IServicioIdentidad _identidad;
    private readonly IServicioAlerta _alerta;
    private readonly IServicioToast _toast;
    private readonly IServicioLogs _logs;

    private bool _repartiendo;
    private bool _enviando;

    public ObservableCollection<FilaCorreo> Filas { get; } = [new FilaCorreo()];

    public bool PuedeQuitar => Filas.Count > 1;
    public bool PuedeAgregar => Filas.Count < CorreosInvitacion.Maximo;
    public string TextoAgregar => $"Agregar otro correo ({Filas.Count}/{CorreosInvitacion.Maximo})";

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
            OnPropertyChanged(nameof(TextoAgregar));
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
            fila.Texto = partes.FirstOrDefault() ?? string.Empty;
            fila.Tocada = true;

            var indice = Filas.IndexOf(fila);
            foreach (var correo in partes.Skip(1))
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

            if (correo.Length == 0) { }
            else if (!CorreosInvitacion.EsValido(correo))
            {
                todoValido = false;
                if (fila.Tocada) error = "Correo no válido";
            }
            else if (!vistos.Add(correo))
            {
                todoValido = false;
                error = "Correo repetido";
            }
            else hayCorreo = true;

            fila.Error = error;
        }

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

        try
        {
            var correos = Filas.Select(f => CorreosInvitacion.Normalizar(f.Texto))
                               .Where(c => c.Length > 0)
                               .ToList();

            var resultado = await _identidad.InvitarPorEmail(correos);
            MostrarLoader(false);

            if (resultado.Ok)
            {
                await _alerta.MostrarAsync("Invitación enviada", "Enviamos la invitación a tus contactos.",
                    verBotonCancelar: false, confirmarText: "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Mensaje fijo por código: el Mensaje de ErrorGenerico sale del texto crudo de la excepción.
            _logs.Warn($"[InvitarPage] InvitarPorEmail falló: {resultado.Error?.HttpCode}");
            var mensaje = resultado.Error?.HttpCode switch
            {
                HttpStatusCode.BadRequest => $"Puedes invitar de 1 a {CorreosInvitacion.Maximo} correos a la vez.",
                HttpStatusCode.ServiceUnavailable => "No fue posible enviar las invitaciones, intenta más tarde.",
                _ => "Ocurrió un error al enviar las invitaciones."
            };
            await _toast.MostrarAsync(mensaje, ToastIcono.Error, ToastPosicion.Bottom);
        }
        catch (Exception ex)
        {
            MostrarLoader(false);
            _logs.Error($"[InvitarPage] {ex.GetType().Name}: {ex.Message}");
            await _toast.MostrarAsync("Ocurrió un error al enviar las invitaciones.", ToastIcono.Error, ToastPosicion.Bottom);
        }
        finally
        {
            _enviando = false;
        }

        Revalidar();
    }
}
