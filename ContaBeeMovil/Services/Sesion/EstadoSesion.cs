namespace ContaBeeMovil.Services.Sesion;

// Destinos de navegación de nivel-sesión. El CoordinadorSesion es el ÚNICO que
// asigna Application.Current.Windows[0].Page en base a estos destinos.
public enum DestinoNavegacion
{
    Login,
    AppShell,
    Cargando
}

// Motivo por el que se termina/reevalúa una sesión. El motivo determina la política
// del token loginless (regla de oro) y el destino de navegación. Ver plan de sesión.
public enum MotivoCierre
{
    // Borran el token loginless → Login
    LogoutManual,       // el usuario cerró sesión explícitamente
    CuentaEliminada,    // se eliminó la cuenta
    LoginOtraCuenta,    // se inició sesión con otra cuenta (el loginless previo quedó obsoleto)
    TokenRevocado,      // rechazo definitivo del backend (invalid_grant / 401 tras agotar refresh)
    VolverALogin,       // el loginless eligió "Volver a Login" desde acceso suspendido
    ExpiradoSinRefresh, // token expirado y sin posibilidad de refresh (Normal sin Recordarme)

    // Conservan el token loginless (loginless resiliente: no expulsan)
    Transitorio,        // error no-401 (500/timeout) cargando datos vitales
    SinRed              // sin conectividad
}
