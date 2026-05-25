# ContaBee Movil — Guía del proyecto para Claude

> **Instrucción:** Al inicio de cada conversación, lee este archivo Y `CONTEXT.md`. Al finalizar una conversación con cambios relevantes, actualiza `CONTEXT.md` con lo que se hizo.

---

## Descripción general

App móvil de **facturación fiscal y contabilidad** para México, dirigida a personas físicas y pequeñas empresas que necesitan capturar y gestionar sus comprobantes (CFDI).

- **Empresa:** Neurofant Mexico S.A.P.I. de C.V.
- **App ID:** `mx.contabee.app`
- **Bundle/deep-link scheme:** `contabee://`
- **API base:** `https://api.contabee.mx`

---

## Stack técnico

| Capa | Tecnología |
|---|---|
| Framework UI | .NET MAUI 10 (net10.0-android / net10.0-ios / net10.0-windows) |
| Patrón | MVVM — CommunityToolkit.Mvvm 8.4.2 |
| UI components | CommunityToolkit.Maui 14.1.1, Syncfusion.Maui.Toolkit 1.0.9 |
| QR / cámara | ZXing.Net.Maui 0.7.4 |
| In-App Billing | Plugin.InAppBilling 10.0.0 |
| Serialización | Newtonsoft.Json 13.0.4 |
| Imágenes | SkiaSharp 3.119.2 |
| Íconos | MauiIcons (Material + FontAwesome) |
| API clients | NSwag (auto-generados en `Contabee.Api/`) |
| Auth | JWT + refresh via `AuthHandler.cs` (HTTP message handler) |

---

## Proyectos en la solución

```
contabee-movil/
├── ContaBeeMovil/          ← App principal MAUI
├── Contabee.Api/           ← Clientes HTTP auto-generados (NSwag)
└── ContaBeeShareExtension/ ← iOS Share Extension (compartir imágenes al app)
```

### Contabee.Api — Clientes de servicio

| Archivo | Servicio |
|---|---|
| `ServicioIdentidad.cs` | Login, registro, tokens |
| `ServicioCrm.cs` | CRM / datos de cliente |
| `ServicioEcommerce.cs` | Tienda / suscripciones |
| `ServicioTranscript.cs` | Procesamiento de comprobantes |

---

## Estructura de ContaBeeMovil

```
ContaBeeMovil/
├── Pages/                  ← Vistas XAML + code-behind
│   ├── MainTabbedPage.xaml ← Contenedor principal de navegación por tabs (5 tabs)
│   ├── Login/              ← PaginaLogin
│   ├── Registro/           ← PaginaRegistro
│   ├── Confirmar/          ← ConfirmarCuentaPage
│   ├── RecuperarPass/      ← RecuperarPassPage, RestablecerContrasenaPage
│   ├── DashboardPage.xaml  ← Tab 0: Inicio
│   ├── FacturacionPage.xaml← Tab 1: Facturación
│   ├── Captura/            ← PaginaCaptura, VisorImagenPage
│   ├── Comprobaciones/     ← Tab 3: PaginaComprobaciones, DetalleComprobacionPage
│   ├── Devoluciones/       ← Tab 2: PaginaDevoluciones, DetalleDevolucionPage
│   ├── Equipo/             ← Tab 4: EquipoPage (visible solo si !EsLoginLess)
│   ├── Cupones/            ← PaginaCupones
│   ├── Tienda/             ← TiendaPage (IAP)
│   ├── Perfil/             ← MiCuentaPage, RFCsPage, TarjetasPage, CambiarContrasena, EliminarCuenta
│   ├── Camara/             ← CamaraPage, QRPage, TomarFotoPage
│   ├── AcercaDe/
│   ├── Sugerencias/
│   └── Dev/                ← LogsPage (DEBUG only)
├── Views/                  ← Popups (CrearDevolucion, ActualizarDevolucion, etc.)
├── Services/               ← Servicios de app
│   ├── ServicioSesion.cs   ← Gestión de sesión / auth
│   ├── AuthHandler.cs      ← HTTP handler JWT refresh
│   ├── ServicioConfiguracion.cs
│   └── ServicioSalud.cs
├── Models/                 ← Modelos de dominio
├── Controls/               ← Controles custom
├── Converters/             ← Value converters XAML
├── Helpers/
├── Utilities/
├── Config/
├── Platforms/
│   ├── Android/            ← AndroidManifest.xml, AppDelegate, etc.
│   ├── iOS/                ← Info.plist, PrivacyInfo.xcprivacy, AppDelegate
│   ├── MacCatalyst/
│   └── Windows/
└── Resources/
    ├── Raw/                ← privacidad.html, etc.
    ├── Styles/             ← AppStyles.xaml
    ├── Fonts/
    └── Images/
```

---

## Navegación principal

`MainTabbedPage` es el contenedor raíz después del login. Gestiona 5 tabs con swap manual de `Content` (no usa `TabbedPage` de MAUI):

| Índice | Página | Visible |
|---|---|---|
| 0 | DashboardPage | Siempre |
| 1 | FacturacionPage | Siempre |
| 2 | PaginaDevoluciones | Siempre |
| 3 | PaginaComprobaciones | Siempre |
| 4 | EquipoPage | Solo si `!AppState.EsLoginLess` |

`SimpleTabBar` (`Controls/`) es el control custom de tabs — 5 columnas `*`, con `SetEquipoVisible(bool)` para ocultar/mostrar el tab de Equipo dinámicamente.

---

## Servicios clave

- **`ServicioSesion`** — gestiona tokens JWT en `SecureStorage`, flag `TieneSesion` en `Preferences`
- **`AuthHandler`** — delegating handler que renueva el access token automáticamente
- **`AppState`** — singleton de estado global (incluye `EsDev` para modo developer, `EsLoginLess` para ocultar tab Equipo)
- **`IServicioAlmacenamiento`** — abstracción sobre `SecureStorage` / `Preferences`

## Convenciones

- Código en **español** (clases, métodos, variables)
- No hay carpeta separada `ViewModels/` — el code-behind de cada Page actúa como ViewModel o usa CommunityToolkit.Mvvm directamente
- Los popups viven en `Views/` y usan `CommunityToolkit.Maui` popups

---

## Plataformas y versiones mínimas

| Plataforma | Min OS |
|---|---|
| Android | 23.0 (Android 6.0) |
| iOS | 15.0 |
| MacCatalyst | 15.0 |
| Windows | 10.0.17763 |

**Versión actual:** 1.0.38 (build 38)  
**Rama principal:** `main` | **Rama de trabajo activa:** `release2.0`

### Historial de ramas release

| Rama | Contenido |
|---|---|
| `release1.0` | Home + Facturación (base) |
| `release1.5` | Team (Equipo) + Vinculación |
| `release2.0` | Devoluciones + Comprobaciones + merge de release1.5 → todos los módulos |

---

## Proceso de desarrollo

- **Git flow:** feature branches → release branches → main
- **GitHub repo:** OswaldoDG/contabee-movil
- **iOS signing:** Apple Development (debug) / Apple Distribution: Neurofant Mexico (release)
- **Provisioning:** `mx.contabee.app Development` (debug) / `ContaBee_AppStore` (release)

---

## Backend relacionado

`c:\dev\contabee\contabee-transcript-backend` — ASP.NET Core con OpenIddict, Azure Monitor, Consul.  
El pod de identidad está en `src/pods/identity/contabee.api.identity/`.

---

## Archivos de contexto

- **`CLAUDE.md`** (este archivo) — información estable del proyecto. Actualizar cuando cambie el stack o la arquitectura.
- **`CONTEXT.md`** — historial de trabajo, decisiones tomadas, tareas pendientes. Actualizar al final de cada sesión relevante.

---

## Flujo de trabajo con Claude

### Al iniciar una conversación nueva
Claude carga `CLAUDE.md` automáticamente. Además lee `CONTEXT.md` por la instrucción al inicio de este archivo. No es necesario pedir nada.

### Comandos útiles para decirle a Claude

| Qué quieres hacer | Qué escribirle a Claude |
|---|---|
| Guardar lo que se hizo en la sesión | `"actualiza CONTEXT.md"` |
| Limpiar entradas viejas que ya no aplican | `"limpia el CONTEXT.md"` |
| Actualizar info del proyecto (nuevo stack, nueva feature, etc.) | `"actualiza CLAUDE.md con [descripción]"` |
| Generar revisión de App Store antes de un release | `"haz una revisión de App Store del proyecto"` |

### Reglas de mantenimiento de CONTEXT.md

- Solo incluir lo que sigue siendo relevante **ahora**: decisiones de arquitectura vigentes, tareas pendientes activas, últimas 2-3 sesiones.
- Cuando una feature está terminada y mergeada, su entrada puede eliminarse — para historial detallado existe `git log`.
- Mantener el archivo entre **50-100 líneas** para no consumir contexto innecesario.
- Las entradas van **más recientes arriba**.
