# ContaBee Movil — Contexto acumulado

> Este archivo se actualiza al final de cada conversación relevante.  
> Formato: sección más reciente arriba. Incluir fecha, qué se hizo, qué quedó pendiente.

---

## 2026-06-26 — Fix: listados con datos de la cuenta fiscal anterior al cambiar de cuenta

**Bug:** Al cambiar de cuenta fiscal, las pestañas de listado (Facturación, Devoluciones, Comprobaciones) seguían mostrando los resultados de la búsqueda hecha con la cuenta anterior.

**Causa:** Las páginas de listado son singletons embebidos en `MainTabbedPage` (swap de `Content`, nunca se destruyen) y cachean sus resultados (`Elementos`, `_ultimaBusqueda`, `ConsultaEjecutada`). El selector de cuenta vive en el header global (`RfcCpBarView`). Al cambiar de cuenta se refrescaban licencia/usuarios/dashboard/filtros, pero **ninguna página de listado invalidaba su caché**, y `OnTabActivated` solo recarga si hay flag `PendienteActualizar*`.

**Hecho:**
- `MainTabbedPage`: reacciona a la transición de `AppState.EstaActualizandoCF` `true → false` (`OnEstadoActualizacionCFCambiado` → `RefrescarListadosPorCambioDeCuenta`). Invalida el caché de las 3 páginas y reactiva la pestaña visible (`ActivarTabActual`) para recarga inmediata.
- `FacturacionPage`, `PaginaDevoluciones`, `PaginaComprobaciones`: nuevo método público `InvalidarConsulta()` que limpia resultados/paginación/`_ultimaBusqueda`, baja `ConsultaEjecutada` y activa el flag `PendienteActualizar*`.

**Decisiones tomadas:**
- Se escucha `EstaActualizandoCF` (no `CuentaFiscalActual`) para evitar una carrera: `CuentaFiscalActual` cambia *antes* de refrescar licencia/usuarios; recargar ahí resolvería nombres con la lista de usuarios vieja. La transición a `false` garantiza datos consistentes.
- Pestaña visible recarga al instante; las inactivas recargan al activarse (sin queries de red de más). El Dashboard ya estaba cubierto por su propia suscripción a `CuentaFiscalActual`.
## 2026-06-25 — Acceso suspendido loginless (desactivar/reactivar asociación)

**Problema:** Al desactivar una asociación (reversible), el backend devuelve 403 `"no pertenece a la cuenta fiscal"` y `GetAsociacionesFiscales` deja de listar la cuenta — idéntico a una desvinculación real. La app trataba ambos casos igual y `LimpiaTokensAsync` borraba `CLAVE_TOKEN_LOGINLESS`, así que al reactivar el usuario loginless ya no podía auto-loguearse.

**Hecho (todo en `ServicioSesion.cs` + nueva página):**
- `LimpiaTokensAsync(bool conservarLoginLess = false)`: para loginless conserva el token (sigue válido tras reactivar). Logout manual (`CerrarSesionAsync`) sigue borrándolo.
- `ProcesarDesvinculacionAsync`: si es loginless y se queda sin cuentas → conserva token y navega a `PaginaAccesoSuspendido` (no a `PaginaLogin`).
- `IntentarReanudarLoginLessAsync()`: re-ejecuta `IniciarSesion(tokenLoginLess,…)`; si OK, `PosLogin` + navega a `AppShell`; si falla, sigue suspendido.
- `VerificarSesionAlReanudarAsync`: si no hay sesión pero sí token loginless → auto-reintenta al reanudar la app.
- Nueva `Pages/AccesoSuspendido/PaginaAccesoSuspendido` (registrada en DI): "Tu acceso fue desactivado" con botones **Reintentar** y **Volver a iniciar sesión** (este último sí limpia el token loginless).

**Decisiones:** confirmado con backend que el token loginless sigue válido tras reactivar, así que no se distingue desactivada vs desvinculada en el momento del 403 — se conserva el token y se deja que el reintento resuelva (si es desvinculación real, simplemente nunca funciona y el usuario usa "Volver a login").

---

## 2026-06-11 — Mostrar solo lo recién creado + color botones DatePicker

**Hecho:**
- **DatePicker (Android):** los botones Aceptar/Cancelar del diálogo nativo de calendario eran invisibles (texto casi blanco sobre fondo blanco). Solución en `ContaBeeDatePickerHandler.cs` (registrado en `MauiProgram.cs` bajo `#if ANDROID`): se colorean los botones por código en `OnShow` con el color `colorBrand` (DayNight: negro `#1e1e1e` en claro, blanco `#ffffff` en oscuro). El encabezado/círculo gris se mantienen.
- **Mostrar solo lo recién creado tras crear:**
  - Comprobaciones y Devoluciones: tras crear, filtran por `Id` del registro devuelto (`respuesta.Payload.Id`) + `CuentaFiscalId` (helper `ConstruirFiltrosPorId`). Fallback al periodo si no hay payload.
  - Capturas: al enviar el lote se guarda `DateTimeOffset.UtcNow` (`FacturacionPage.CapturaRecienCreadaFiltroFecha`) y al volver se filtra `FechaCreacion` con `Entre` en ventana [envío −3 min, +10 min] (`CrearBusquedaPorFechaCreacion`).
- **Página Equipo (`EquipoPage.xaml`):** se reemplazó el swipe a la derecha (abrir propiedades) por un `TapGestureRecognizer` en la card → un tap abre `PropiedadesUsuarioPopup` vía `ConfigurarCommand`. El swipe a la izquierda (eliminar) se mantiene. `ConfigurarCommand` solo se asigna en cuenta primaria, así que el tap respeta el gating sin necesidad de `MostrarConfigurar`.

**Decisiones tomadas:**
- DatePicker: NO se tocó el `colorAccent` global (es gris a propósito; afecta cursores/selección en toda la app). El handler aplica el color solo a los botones del diálogo.
- Capturas: se intentó filtrar por `LoteCapturaId`, pero **el backend ignora silenciosamente las propiedades de filtro desconocidas y devuelve todo** — por eso se usa `FechaCreacion`.
- **Hallazgo clave:** `FacturacionPage` (y las demás pestañas) viven embebidas como `Content` dentro de `MainTabbedPage`, así que su `OnAppearing` NO se dispara al volver de una página navegada (p. ej. `PaginaCaptura`). El hook correcto para recargar es `OnTabActivated()`, que `MainTabbedPage` invoca desde su `OnAppearing`/`SwitchToTab`. La lógica de recarga de capturas se movió ahí.

**Pendiente / próximos pasos:**
- La ventana de fecha en capturas es heurística (margen ±min). Si el reloj del dispositivo difiere mucho del servidor podría fallar; vigilar.

---

## 2026-06-09 — Widget Android (lanzador de PaginaCaptura)

**Hecho:**
- Implementado widget Android 2×1 en `ContaBeeMovil/Platforms/Android/`:
  - `WidgetCaptura.cs` — `AppWidgetProvider` con atributos .NET Android (no toca AndroidManifest)
  - `Resources/layout/widget_captura.xml` — RemoteViews con ícono + texto "Capturar ticket"
  - `Resources/drawable/widget_background.xml` — fondo redondeado con soporte dark mode
  - `Resources/xml/widget_captura_info.xml` — metadatos del AppWidget (2×1, sin refresh)
  - Colores `widget_bg`/`widget_text` en `values/colors.xml` y `values-night/colors.xml`
- `DeepLinkHandler.cs`: nuevo método `HandleWidgetCaptura()` + `NavegarACaptura()` que usa `Shell.Current.GoToAsync("PaginaCaptura")` replicando el patrón de `SharedImageHandler`
- `MainActivity.cs`: detección del widget por boolean extra (`mx.contabee.app.WIDGET_CAPTURA`), navegación diferida a `OnResume` para garantizar que MAUI esté listo

**Decisiones tomadas:**
- Detección por `intent.GetBooleanExtra(...)` en vez de URI parsing (más confiable con `PendingIntentFlags.Immutable`)
- Navegación disparada en `OnResume`, no en `OnNewIntent`, para evitar timing issues
- **Al actualizar el widget es necesario quitarlo y volverlo a agregar** cuando cambia el `PendingIntent`

**Pendiente:**
- Implementar widget equivalente en iOS (requiere Swift + WidgetKit, extensión separada)

---

## 2026-05-26 — Limpieza de archivos locales y gitignore

**Hecho:**
- Agregado al `.gitignore`: `.codegraph/`, `.mcp.json`, `.claude/settings.local.json`.
- Eliminado directorio `.cursor/` (config auto-generada de Cursor IDE, no se usa).
- Eliminado directorio `graphify-out/` (salida regenerable de `/graphify`).
- Limpiado `CONTEXT.md` (entradas obsoletas eliminadas).

---

## 2026-05-25 — Merge de release1.5 en release2.0 + corrección de conflictos

**Hecho:**
- Merge `release1.5` → `release2.0` para unir todos los módulos en una sola rama.
- Corregidos conflictos en `SimpleTabBar.xaml`, `SimpleTabBar.xaml.cs` y `MainTabbedPage.xaml.cs`.
- Layout de tabs final: 0=Inicio, 1=Facturación, 2=Devoluciones, 3=Comprobaciones, 4=Equipo.
- Tab Equipo se oculta dinámicamente con `SetEquipoVisible(!AppState.EsLoginLess)`.

**Pendiente:**
- Probar en dispositivo/emulador que los 5 tabs navegan correctamente.
- Push de `release2.0` al remoto cuando esté validado.

---

## Pendiente acumulado

- **Tarjetas (backend + frontend):** Integración CouchDB completa. Falta probar flujo completo en dispositivo con backend levantado y verificar migración desde `SecureStorage`.

---

## Plantilla para nuevas entradas

```
## YYYY-MM-DD — Título corto de la sesión

**Hecho:**
- ...

**Decisiones tomadas:**
- ...

**Pendiente / próximos pasos:**
- ...
```
