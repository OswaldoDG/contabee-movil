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

## Tipos de crédito que maneja ContaBee

El modelo de negocio se basa en **créditos**. Cada crédito habilita un flujo distinto de captura y generación de CFDI:

| Tipo de crédito | Quién captura el ticket | Quién genera el CFDI | Dónde |
|---|---|---|---|
| **Captura** | El usuario | **ContaBee** (delegado) | El usuario toma la captura de su ticket y delega la captura y generación del CFDI a ContaBee |
| **Autoservicio** | El usuario | **El propio usuario** | El usuario toma la captura de su ticket y él mismo realiza la captura y generación del CFDI desde la app de escritorio |
| **Colaboración** | — | — | Habilita al usuario para crear una **comprobación** o una **devolución** |

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
| Íconos | **FluentUI** (preferido, `Resources/Fonts/FluentUI.cs`) + MauiIcons (Material + FontAwesome) |
| API clients | NSwag (auto-generados en `Contabee.Api/`) |
| Auth | JWT + refresh via `AuthHandler.cs` (HTTP message handler) |

---

## Proyectos en la solución

- `ContaBeeMovil/` — App principal MAUI
- `Contabee.Api/` — Clientes HTTP auto-generados (NSwag): `ServicioIdentidad`, `ServicioCrm`, `ServicioEcommerce`, `ServicioTranscript`
- `ContaBeeShareExtension/` — iOS Share Extension

---

## Navegación principal

`MainTabbedPage` es el contenedor raíz tras el login. Usa **swap manual de `Content`** — no usa `TabbedPage` nativa de MAUI (decisión intencional para mayor control). `SimpleTabBar` es el control custom de tabs.

| Índice | Página | Visible |
|---|---|---|
| 0 | DashboardPage | Siempre |
| 1 | FacturacionPage | Siempre |
| 2 | PaginaDevoluciones | Siempre |
| 3 | PaginaComprobaciones | Siempre |
| 4 | EquipoPage | Solo si `!AppState.EsLoginLess` |

---

## Convenciones

- Código en **español** (clases, métodos, variables)
- No hay carpeta `ViewModels/` — el code-behind actúa como ViewModel o usa CommunityToolkit.Mvvm directamente
- Popups viven en `Views/` y usan `CommunityToolkit.Maui` popups
- `AppState` — singleton de estado global (`EsDev`, `EsLoginLess`)
- **Íconos: prioriza SIEMPRE FluentUI.** Al agregar cualquier ícono usa la fuente FluentUI antes que Material/FontAwesome u otros sets. Solo recurre a otro set si el glifo no existe en FluentUI.

```xml
<!-- namespace en la raíz del XAML -->
xmlns:f="clr-namespace:Fonts"

<!-- uso: FontFamily="FluentUI" + glifo como constante -->
<Label Text="{x:Static f:FluentUI.arrow_clockwise_20_regular}"
       FontFamily="FluentUI" FontSize="20" />
```

Glifos en `Resources/Fonts/FluentUI.cs` (regular) y `FluentUIFilled.cs` (filled, `FontFamily="FluentUIFilled"`). Nombres tipo `arrow_clockwise_20_regular`, `chevron_left_20_regular`, etc.

---

## ⚠️ Swagger: parche manual OBLIGATORIO al re-descargar

Los JSON de `Contabee.Api/swagger/` **se editan a mano** después de bajarlos del backend. El cliente NSwag NO se commitea: se genera en cada build (`OpenApiReference` → `Contabee.Api/obj/Servicio*Client.cs`), así que "regenerar el cliente" = **compilar**.

**Cada vez que re-descargues un swagger, vuelve a aplicar esto** (se pierde siempre, ya pasó en `cbf0e4d` y se volvió a perder en la re-descarga de julio 2026):

```jsonc
// TODA propiedad "paginado" debe llevar nullable: true
"paginado": {
  "$ref": "#/components/schemas/comunes.busqueda.Paginado",
  "nullable": true          // ← AGREGAR A MANO, SIEMPRE
},
```

**Por qué:** sin `nullable`, NSwag genera `Required.DisallowNull` y Newtonsoft **lanza excepción al deserializar** si el backend responde `"paginado": null` → truenan los listados (Facturación, Devoluciones, Comprobaciones, Equipo). Con `nullable: true` genera `Required.Default`, que tolera el null. Es una relajación pura: no puede romper nada.

Ubicaciones (9): `ServicioTranscript.json` → `comunes.busqueda.Busqueda`, 4× `comunes.busqueda.ResultadoPaginado<T>`, `BusquedaCaptura`. `ServicioIdentidad.json` → `Busqueda`, `CuentaClienteResultadoPaginado`, `CuentaUsuarioResultadoPaginado`.

Verificación rápida tras re-descargar — no debe imprimir nada:

```bash
python -c "
import json,glob
for f in glob.glob('Contabee.Api/swagger/*.json'):
    d=json.load(open(f,encoding='utf-8-sig'))
    for k,v in d.get('components',{}).get('schemas',{}).items():
        p=v.get('properties',{}).get('paginado') if isinstance(v,dict) else None
        if p and p.get('nullable') is not True: print('FALTA nullable:',f,k)
"
```

---

## Servicios custom — OBLIGATORIO usarlos siempre

> **NUNCA** uses `DisplayAlert`, `Console.WriteLine`, `Debug.WriteLine` ni toasts de terceros. Siempre usa los servicios propios del proyecto.

### Toast — `IServicioToast` (`Services/Notifications/ServicioToast.cs`)

```csharp
// Inyectado por DI o resuelto con _serviceProvider
await _toast.MostrarAsync("Mensaje");
await _toast.MostrarAsync("Mensaje", ToastIcono.Error);
await _toast.MostrarAsync("Mensaje", ToastIcono.Warning, ToastPosicion.Top);
// Enums: ToastIcono { Info, Warning, Error } | ToastPosicion { Top, Center, Bottom }
```

### Diálogo/alerta — `AlertaPopup` (`Views/AlertaPopup.xaml.cs`)

```csharp
var popup = new AlertaPopup(
    titulo: "Confirmar",
    mensaje: "¿Desea continuar?",
    verBotonCancelar: true,   // default true
    verBotonConfirmar: true,  // default true
    cancelarText: "Cancelar", // default "Cancelar"
    confirmarText: "Si");     // default "Si"
await this.ShowPopupAsync(popup);
bool confirmado = popup.Confirmado;
```

### Logs — `IServicioLogs` (`Services/Dev/IServicioLogs.cs`)

```csharp
// Inyectado por DI — métodos disponibles:
_logs.Info("mensaje");
_logs.Warn("mensaje");
_logs.Error("mensaje");
_logs.Log("mensaje");   // alias de Info
```

---

## Plataformas y versiones mínimas

| Plataforma | Min OS |
|---|---|
| Android | 23.0 (Android 6.0) |
| iOS | 15.0 |
| MacCatalyst | 15.0 |
| Windows | 10.0.17763 |

**Versión actual:** 1.0.38 (build 38) | **Rama activa:** `release2.0` | **Rama principal:** `main`

| Rama | Contenido |
|---|---|
| `release1.0` | Home + Facturación |
| `release1.5` | Equipo + Vinculación |
| `release2.0` | Devoluciones + Comprobaciones + merge release1.5 |

---

## Proceso de desarrollo

- **Git flow:** feature branches → release branches → main
- **GitHub repo:** OswaldoDG/contabee-movil
- **iOS signing:** Apple Development (debug) / Apple Distribution: Neurofant Mexico (release)
- **Provisioning:** `mx.contabee.app Development` (debug) / `ContaBee_AppStore` (release)

---

## Backend relacionado

`c:\dev\contabee\contabee-transcript-backend` — ASP.NET Core con OpenIddict, Azure Monitor, Consul.  
Pod de identidad: `src/pods/identity/contabee.api.identity/`

---

## Mantenimiento de CONTEXT.md

- Solo incluir lo relevante **ahora**: decisiones vigentes, pendientes activos, últimas 2-3 sesiones.
- Features terminadas y mergeadas se eliminan — para historial detallado existe `git log`.
- Mantener entre **50-100 líneas**. Entradas más recientes arriba.
