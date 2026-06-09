# ContaBee Movil — Contexto acumulado

> Este archivo se actualiza al final de cada conversación relevante.  
> Formato: sección más reciente arriba. Incluir fecha, qué se hizo, qué quedó pendiente.

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
