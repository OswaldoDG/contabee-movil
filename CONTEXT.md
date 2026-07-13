# ContaBee Movil — Contexto acumulado

> Este archivo se actualiza al final de cada conversación relevante.  
> Formato: sección más reciente arriba. Incluir fecha, qué se hizo, qué quedó pendiente.

---

## 2026-07-13 — Visor PDF: overlay de descarga, título oculto y fix tamaño iOS

**Hecho:**
- Nuevo `Views/CargandoPopup` (toolkit `Popup` con spinner + scrim, `PageOverlayColor #66000000`, no descartable). En `LoteCapturaCardView.DescargarYCompartir` se muestra a pantalla completa **mientras se descarga** el archivo (se conserva además el mini-spinner del botón vía `SetBusy`), y se cierra antes de navegar al visor o abrir la hoja de compartir. Aplica a los 3 botones (PDF/XML/cámara). Cierre robusto: en `finally` best-effort, sin doble cierre.
- `PaginaVisorPdfPropio.Titulo`: ya NO asigna `Title` (solo guarda `_nombreArchivo` para guardar/compartir). El nombre de las capturas es un número (`captura_12345.pdf`) y se veía raro en la barra superior; ahora queda vacía (se conserva el botón de regreso).
- **Fix documento diminuto en iOS (causa raíz REAL, resuelta y verificada en dispositivo).** Tres capas, en orden de descubrimiento:
  1. *Ancho de despliegue:* se calculaba `MainDisplayInfo.Width / Density` (en iOS `Width` viene en puntos → re-dividir por densidad dejaba ⅓ del tamaño). Ahora `_anchoBaseDips` sale del ancho REAL del lienzo vía `Lienzo.SizeChanged` + debounce (siempre DIPs); `_anchoPxObjetivo = ancho × densidad × Multiplicador` (densidad solo como nitidez). Confirmado por log: `ancho=390dips densidad=3`.
  2. *Frame de la página:* cada página se envuelve en un `Grid` de tamaño fijo (`WidthRequest`/`HeightRequest`) y el `Image` lo rellena. En iOS un `Image` con fuente async (`FromStream`) + `AspectFit` dentro de un stack auto-medido no aplica de forma fiable su propio tamaño (el frame colapsa). `ActualizarPill` ahora itera los contenedores (`View`), no `Image`.
  3. **La verdadera causa:** en `ServicioRenderPdf` (iOS) `GetDrawingTransform` hacia un rect **en píxeles** NO ampliaba la página → se dibujaba a ~1:1 en puntos, diminuta dentro de un lienzo grande (blanco sobre fondo blanco = parecía flotar). Fix: `ScaleCTM(ancho/anchoPt)` explícito puntos→píxeles y `GetDrawingTransform` mapea solo a un rect del tamaño visual **en puntos** (conserva el `/Rotate` intrínseco). Diagnóstico: se pintó el fondo del render de azul y se confirmó que el contenido quedaba diminuto rodeado de azul. Android intacto (su `PdfRenderer` ya escala al bitmap).

**Pendiente:** probar en iOS el resto del visor (zoom/pan, rotar ±90°, multipágina/pill, restaurar); descarga lenta → overlay → visor con spinner; compartir PDF/XML; caso sin conexión.

---

## 2026-07-11 — Widget Android "Capturar ticket": correcciones + rediseño (2 widgets)

**Correcciones al flujo** (probadas en dispositivo; flujo: widget → `MainActivity` → `DeepLinkHandler.NavegarACaptura` → `PaginaCaptura` con `tipo=FacturaIndividual`):
- `MainActivity.HandleIntent(intent, desdeOnCreate)`: consume el extra (`RemoveExtra`, evita re-disparo al `Recreate()`) y descarta la re-entrega desde Recientes (`LaunchedFromHistory`) **solo en OnCreate** — ⚠ hallazgo: algunos launchers marcan ese flag también en taps legítimos vía `OnNewIntent`; filtrarlo ahí rompe el widget en caliente.
- `DeepLinkHandler.NavegarACaptura`: sin sesión conserva la intención como link pendiente y `CoordinadorSesion.NavegarAsync(AppShell)` la reprocesa tras login (`ProcesarLinkPendiente()`). Espera de `Shell.Current` con reintentos (20×250ms). Guard anti-duplicados si `PaginaCaptura` ya es la página actual.
- `FacturacionPage.TieneCreditos`: considera créditos Captura **o** Autoservicio (antes solo Captura).
- `PaginaCaptura`: fix fuga — suscripción a `AppState.PropertyChanged` movida de lambda-en-constructor a handler en `OnAppearing`/`OnDisappearing`.
- `PendingIntentFlags.UpdateCurrent|Immutable` unificado (minSdk 23).

**Rediseño — ahora hay 2 widgets** (elegidos por el usuario de una galería dev de 6 propuestas, ya eliminada):
- `WidgetCaptura` (3×1, redimensionable): card con logo + badge de cámara (círculo oscuro, cámara amarilla, aro blanco), "Capturar ticket" / "Toca y fotografía tu ticket", chevron. **Temática**: card blanca (values) / oscura #1e1e1e (values-night). Bloque de logo 42dp para caber en 1 celda sin recorte.
- `WidgetCapturaCompacto` (1×1, nuevo provider): logo 48dp + badge + etiqueta "Capturar" blanca con sombra; fijo en ambos temas. Reusa `WidgetCaptura.ActualizarWidget(…, layoutId)`.
- Recursos: `widget_card`/`widget_badge`/`ic_widget_chevron`/`ic_widget_camera` (amarillo), colores `widget_*` renovados en values/values-night, `previewLayout`+`description` en ambos providers (strings.xml).

**Decisiones vigentes:**
- Widget sin créditos → abre igual (la página muestra "Sin créditos" y bloquea envío).
- Widget sin sesión → retoma la intención y abre captura tras el login.

---

## 2026-07-10 — Visor de PDF de capturas: visor PROPIO (único y definitivo)

**Contexto:** esta app **solo corre en Android e iOS** (los TFM de Windows quedan solo para dev). El visor replica el de la app de escritorio: documento a pantalla completa sobre fondo oscuro (#141414) y botones flotantes cuadrados redondeados (Primary/OnPrimary, íconos **FluentUIFilled**). Historia: se probó PDF.js en HybridWebView (no funcionó en dispositivo) y `Eightbot.MauiNativePdfView` (funcionaba pero sin rotación) — **ambos eliminados**; queda solo el visor propio.

**Hecho:**
- **Visor propio** (`Pages/VisorPdf/PaginaVisorPdfPropio`, abierto desde el ícono de cámara de `LoteCapturaCardView` cuando el contenido es PDF por magic bytes `%PDF`): servicio `Services/Pdf/IServicioRenderPdf`/`ServicioRenderPdf` (singleton DI) renderiza páginas a JPEG con APIs del sistema — `android.graphics.pdf.PdfRenderer` (Android) y `CoreGraphics.CGPDFDocument` + `GetDrawingTransform` (iOS) — sin dependencias; rotación del usuario (0/90/180/270) en código compartido con SkiaSharp (patrón `NormalizarOrientacionExif`). Render a ~2.5× ancho de pantalla (tope 4096 px/lado), fondo blanco explícito, `Task.Run` + cancelación en `OnNavigatedFrom`.
- **Interacción por transforms, sin ScrollView** (el ScrollView se comía el pinch): un `Grid` "Lienzo" opaco recibe TODOS los gestos (pinch + pan + doble tap; el contenedor de páginas es `InputTransparent` con cascade) y el zoom/paneo se aplican como `Scale`/`Translation` sobre el contenedor con clamps (`MaxTx/MaxTy`); zoom alrededor del centro (la traslación escala con el factor relativo); posición inicial = inicio del documento (`_ty = MaxTy()`).
- Controles: zoom ± (paso 1.25, 1–4), pinch (en vivo), doble tap 1x/2x, restaurar (zoom 1 + rotación 0), rotar ±90° (re-render sub-segundo), pill "n / N" (calculada desde la traslación), descargar (`FileSaver`) y compartir (siempre el PDF original).
- **Eliminado el visor nativo**: página `PaginaVisorPdf`, ruta, botón ⇄, paquete `Eightbot.MauiNativePdfView` y `UseMauiNativePdfView()`; **minSdk Android revertido 24 → 23** (el 24 era exigencia de los AAR de ese paquete). CLAUDE.md actualizado.

**Decisiones tomadas:**
- Visor propio elegido tras evaluación en dispositivo; los PDFs son ligeros y de resolución acotada (los genera la app: foto→PDF 1 página con OpenCV/normalización), así que re-renderizar al rotar es barato.
- Recordatorio: capturas viejas (JPEG, pre poc/pdf) comparten en vez de abrir visor; probar con capturas recientes.

**Pendiente / próximos pasos:**
- Probar en dispositivo (equipo compila/corre; sin builds de Claude por locks de `obj\`): pinch/zoom ±/doble tap, paneo, rotar ×4, restaurar, descargar, compartir, multipágina (pill), PDF corrupto (toast+regreso), y en iOS validar el render (PDFKit/CG).
- Si se desea, reusar el visor para el botón PDF del CFDI (`BtnPdf`): mismo bloque condicional en `DescargarYCompartir`.

---

## 2026-07-06 — Aviso de nueva versión disponible (backend-driven)

**Hecho:**
- Nuevo `Services/ServicioActualizacion.cs` (`IServicioActualizacion`, singleton en DI): al arrancar (`App.OnStart`, fire-and-forget con delay de 3s) consulta `GET https://api.contabee.mx/api/identity/app/version-movil` y compara contra `AppInfo.Current.Version`:
  - `< versionMinima` → popup **obligatorio** (solo botón "Actualizar").
  - `< versionRecomendada` → popup "Ignorar / Actualizar" vía `IServicioAlerta`. "Ignorar" guarda la versión en `Preferences` (`Actualizacion_VersionIgnorada`) para no repetir el aviso de esa misma versión.
  - "Actualizar" abre la tienda (`Launcher`): URLs del backend con fallback a Play Store / App Store (`id6761437536`).
- Contrato de respuesta: `{ versionMinima, versionRecomendada, urlAndroid, urlIos, mensaje }` (todo opcional; `mensaje` reemplaza el texto default del popup).
- **Fallback provisional a tiendas** mientras el backend regrese 404: iOS consulta iTunes Lookup (verificado funcionando); Android scrapea la versión del HTML de Play Store. El fallback solo produce aviso "amable", nunca obligatorio. El backend siempre tiene prioridad.

**Decisiones tomadas:**
- **El endpoint del backend AÚN NO EXISTE ni se deployará junto con este cambio.** El servicio falla en silencio, así que el front puede publicarse ya; cuando el back exponga el endpoint la feature se activa sola sin nueva release.
- **Hallazgo:** la ficha de Play Store de `mx.contabee.app` regresa 404 (app en testing cerrado) → el fallback Android no hará nada hasta que la ficha sea pública; iOS sí funciona desde ya.

**Pendiente / próximos pasos:**
- Implementar el endpoint en el pod identity del backend (spec entregada: controller `[Route("app")]`, `GET version-movil`, `[AllowAnonymous]`, valores desde configuración).
- Probar en dispositivo cuando el endpoint exista (incluye caso obligatorio).

---

## 2026-06-26 — Fix: listados con datos de la cuenta fiscal anterior al cambiar de cuenta

**Bug:** Al cambiar de cuenta fiscal, las pestañas de listado (Facturación, Devoluciones, Comprobaciones) seguían mostrando los resultados de la cuenta anterior (son singletons embebidos en `MainTabbedPage` que cachean resultados).

**Hecho:**
- `MainTabbedPage`: reacciona a la transición de `AppState.EstaActualizandoCF` `true → false` → invalida el caché de las 3 páginas (`InvalidarConsulta()`) y reactiva la pestaña visible.

**Decisiones tomadas:**
- Se escucha `EstaActualizandoCF` (no `CuentaFiscalActual`) para evitar una carrera con la recarga de licencia/usuarios.
- **Hallazgo clave (sigue vigente):** las pestañas viven como `Content` dentro de `MainTabbedPage`; su `OnAppearing` NO se dispara al volver de una página navegada. El hook correcto es `OnTabActivated()`.

---

## Pendiente acumulado

- **Tarjetas (backend + frontend):** Integración CouchDB completa. Falta probar flujo completo en dispositivo con backend levantado y verificar migración desde `SecureStorage`.
- **Widget iOS** equivalente al widget Android de captura (requiere Swift + WidgetKit).
- **Capturas — filtro "recién creado":** la ventana por `FechaCreacion` es heurística (±min); si el reloj del dispositivo difiere mucho del servidor podría fallar; vigilar.

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
