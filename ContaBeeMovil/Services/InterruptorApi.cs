namespace ContaBeeMovil.Services;

/// <summary>
/// Corta-circuitos para las llamadas al API.
///
/// Cuando el backend se cae, la app no hace una llamada: hace decenas en paralelo (perfil,
/// asociaciones, tarjetas, licencia, más lo que pida cada pestaña). Sin freno, todas esperan
/// su timeout completo —la UI queda congelada— y el servidor recibe una tormenta de
/// reintentos justo mientras intenta levantarse.
///
/// Tras <see cref="UmbralFallos"/> fallos de infraestructura consecutivos el interruptor
/// abre: las llamadas fallan al instante sin tocar la red. Mientras está abierto se deja
/// pasar UNA sola llamada de prueba a la vez, para notar la recuperación sin esperar a que
/// expire el temporizador. Cualquier respuesta buena lo cierra de inmediato.
///
/// Solo cuentan los fallos de infraestructura (5xx, timeout, socket). Un 400/403/404 es una
/// respuesta legítima del backend: significa que está vivo, así que cierra el interruptor.
/// </summary>
public sealed class InterruptorApi
{
    private const int UmbralFallos = 3;
    private static readonly TimeSpan _ventanaAbierto = TimeSpan.FromSeconds(20);

    private readonly Lock _lock = new();
    private int _fallosConsecutivos;
    private DateTime _abiertoHasta = DateTime.MinValue;
    private bool _pruebaEnVuelo;

    public bool Abierto
    {
        get { lock (_lock) return DateTime.Now < _abiertoHasta; }
    }

    /// <summary>
    /// true = la llamada puede salir a la red. false = circuito abierto, responder 503 ya
    /// mismo sin gastar la red ni el timeout.
    /// </summary>
    public bool PermitirLlamada()
    {
        lock (_lock)
        {
            if (DateTime.Now >= _abiertoHasta)
                return true;

            // Abierto: una sola sonda a la vez detecta que el backend volvió.
            if (_pruebaEnVuelo)
                return false;

            _pruebaEnVuelo = true;
            return true;
        }
    }

    public void RegistrarExito() => Reiniciar();

    /// <summary>
    /// Borra el historial y deja pasar el tráfico de inmediato. Se usa cuando algo externo
    /// invalida lo aprendido: el usuario pidió "Reintentar" explícitamente, o volvió la red
    /// (los fallos anteriores eran del dispositivo, no del servidor).
    /// </summary>
    public void Reiniciar()
    {
        lock (_lock)
        {
            _fallosConsecutivos = 0;
            _abiertoHasta = DateTime.MinValue;
            _pruebaEnVuelo = false;
        }
    }

    /// <summary>Devuelve true si este fallo fue el que abrió el circuito (para loguear una vez).</summary>
    public bool RegistrarFallo()
    {
        lock (_lock)
        {
            _pruebaEnVuelo = false;

            bool estabaAbierto = DateTime.Now < _abiertoHasta;
            _fallosConsecutivos++;

            if (_fallosConsecutivos < UmbralFallos)
                return false;

            _abiertoHasta = DateTime.Now.Add(_ventanaAbierto);
            return !estabaAbierto;
        }
    }
}
