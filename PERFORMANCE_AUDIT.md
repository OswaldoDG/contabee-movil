# Diagnóstico de rendimiento — ContaBee Movil (MAUI)

> Auditoría basada en análisis estático de código. Fecha: 2026-05-25.  
> Prioridad enfocada en dispositivos gama baja/media (Android 6-9, chips MediaTek A-series / Snapdragon 4xx).

---

## Hallazgos

### 🔴 CRÍTICO — 1. SecureStorage sin caché en memoria

**Archivos:**
- [ContaBeeMovil/Services/ServicioSesion.cs:73-117](ContaBeeMovil/Services/ServicioSesion.cs#L73-L117)
- [ContaBeeMovil/Services/AuthHandler.cs:65-75](ContaBeeMovil/Services/AuthHandler.cs#L65-L75)

**Causa:** `LeeAccessTokenAsync()`, `LeeExpiracionAsync()` y `LeeRefreshTokenAsync()` llaman directamente a `SecureStorage.GetAsync()` sin ningún caché en memoria. Cada request HTTP autenticado lee el keystore de Android 2-3 veces. En Android, el Keystore puede tardar 20-60 ms por lectura (cifrado por hardware). Con varias pantallas cargando en paralelo al inicio de sesión, hay 10+ lecturas bloqueantes simultáneas.

**Por qué afecta más a gama baja:** El Android Keystore en chips lentos (MediaTek A-series, Snapdragon 4xx) es 3-5× más lento que en flagship.

**Solución — antes:**
```csharp
public Task<string?> LeeAccessTokenAsync() => LeeContenidoClave(CLAVE_ACCESS_TOKEN);
// → SecureStorage.GetAsync() en cada llamada, sin caché
```

**Solución — después:**
```csharp
// En ServicioSesion — agregar campos privados de caché
private string? _cachedAccessToken;
private string? _cachedRefreshToken;
private DateTime? _cachedExpiracion;

public async Task<string?> LeeAccessTokenAsync()
{
    if (_cachedAccessToken != null) return _cachedAccessToken;
    _cachedAccessToken = await LeeContenidoClave(CLAVE_ACCESS_TOKEN);
    return _cachedAccessToken;
}

public async Task GuardaTokenAsync(string accessToken, string refreshToken)
{
    _cachedAccessToken  = accessToken;
    _cachedRefreshToken = refreshToken;
    await SecureStorage.SetAsync(CLAVE_ACCESS_TOKEN, accessToken);
    await SecureStorage.SetAsync(CLAVE_REFRESH_TOKEN, refreshToken);
    Preferences.Set("TieneSesion", true);
}

public Task LimpiaTokensAsync()
{
    _cachedAccessToken  = null;
    _cachedRefreshToken = null;
    _cachedExpiracion   = null;
    SecureStorage.Remove(CLAVE_ACCESS_TOKEN);
    // ... resto igual
}
```

**Impacto esperado:** Elimina 20-180 ms de latencia acumulada por request en gama baja. La primera lectura sigue yendo a SecureStorage; las siguientes son en memoria (< 0.01 ms).

---

### 🔴 CRÍTICO — 2. File I/O síncrono en el hilo UI

**Archivo:** [ContaBeeMovil/Pages/Captura/PaginaCaptura.xaml.cs:178-187](ContaBeeMovil/Pages/Captura/PaginaCaptura.xaml.cs#L178-L187)

**Causa:** `File.Exists()` y `File.GetLastWriteTimeUtc()` se llaman en un loop síncrono dentro de `VerificarFotosGuardadasAsync()`. Aunque el método es `async Task`, estas APIs de disco son completamente síncronas y bloquean el hilo de ejecución. En Android con almacenamiento eMMC de gama baja, cada acceso puede tardar 5-15 ms, y hay múltiples accesos en el loop.

**Por qué afecta más a gama baja:** El almacenamiento eMMC de chips de entrada tiene ~10× menor IOPS que el UFS 3.x de gama alta.

**Solución — antes:**
```csharp
foreach (var c in capturasGuardadas)
    var existe = File.Exists(c.Path);   // síncrono, bloquea el hilo

capturasGuardadas = capturasGuardadas.Where(c => File.Exists(c.Path)).ToList();
capturasGuardadas = capturasGuardadas
    .OrderByDescending(c => File.GetLastWriteTimeUtc(c.Path))  // síncrono, bloquea
    .ToList();
```

**Solución — después:**
```csharp
capturasGuardadas = await Task.Run(() =>
    capturasGuardadas
        .Where(c => File.Exists(c.Path))
        .OrderByDescending(c => File.GetLastWriteTimeUtc(c.Path))
        .ToList());
```

**Impacto esperado:** Elimina el freeze visual al abrir PaginaCaptura cuando hay fotos de sesiones anteriores.

---

### 🟠 ALTA — 3. ObservableCollection.Add() en bucle (N re-renders de UI)

**Archivo:** [ContaBeeMovil/Pages/Captura/PaginaCaptura.xaml.cs:201-203](ContaBeeMovil/Pages/Captura/PaginaCaptura.xaml.cs#L201-L203)

**Causa:** Al restaurar fotos guardadas, se llama `.Add()` por cada foto en un `foreach`. Cada `Add()` dispara `CollectionChanged → OnCapturasCollectionChanged → re-render completo del CollectionView`. Con 5 fotos = 5 ciclos de layout en lugar de 1.

**Solución — antes:**
```csharp
foreach (var c in capturasGuardadas)
    _capturas.Add(c);  // 1 CollectionChanged + 1 re-render por item
```

**Solución — después:**
```csharp
// CommunityToolkit.Maui ya está en el proyecto — tiene extensión AddRange
_capturas.AddRange(capturasGuardadas);  // 1 solo CollectionChanged total

// Alternativa si AddRange no está disponible:
_capturas = new ObservableCollection<CapturaLote>(capturasGuardadas);
OnPropertyChanged(nameof(TieneCapturas));
```

**Impacto esperado:** N→1 pasos de layout del CollectionView al restaurar capturas.

---

### 🟠 ALTA — 4. Lookup O(n×m) de RFC al construir listas

**Archivos:**
- [ContaBeeMovil/Pages/Comprobaciones/PaginaComprobaciones.xaml.cs:205-207](ContaBeeMovil/Pages/Comprobaciones/PaginaComprobaciones.xaml.cs#L205-L207)
- [ContaBeeMovil/Pages/Devoluciones/PaginaDevoluciones.xaml.cs](ContaBeeMovil/Pages/Devoluciones/PaginaDevoluciones.xaml.cs) — mismo patrón
- [ContaBeeMovil/Pages/FacturacionPage.xaml.cs](ContaBeeMovil/Pages/FacturacionPage.xaml.cs) — mismo patrón

**Causa:** `ResolverRfcCuentaFiscal(e)` hace `FirstOrDefault()` sobre `AppState.CuentasFiscales` por cada elemento de la página al construir la lista. Con 20 comprobaciones y 5 cuentas fiscales = 100 comparaciones de string. El costo escala con el producto de ambas listas.

**Solución — antes:**
```csharp
Elementos = elementosPagina
    .Select((e, i) => new ItemConConsecutivo(offset + i + 1, e, ResolverRfcCuentaFiscal(e)))
    .ToList();
// ResolverRfcCuentaFiscal hace FirstOrDefault() en CuentasFiscales por cada 'e'
```

**Solución — después:**
```csharp
// Pre-calcular el diccionario una sola vez antes del Select
var rfcPorCfid = AppState.Instance.CuentasFiscales?
    .ToDictionary(cf => cf.CuentaFiscalId, cf => cf.Rfc)
    ?? new Dictionary<Guid, string>();

Elementos = elementosPagina
    .Select((e, i) => new ItemConConsecutivo(
        offset + i + 1,
        e,
        rfcPorCfid.TryGetValue(e.CuentaFiscalId, out var rfc) ? rfc : string.Empty))
    .ToList();
```

**Impacto esperado:** O(n×m) → O(n+m). Reducción de CPU time en el hilo UI al paginar cualquier lista.

---

### 🟠 ALTA — 5. AppState.PropertyChanged dispara cargas sin cancelación

**Archivo:** [ContaBeeMovil/Pages/Dashboard/DashboardViewModel.cs:59-70](ContaBeeMovil/Pages/Dashboard/DashboardViewModel.cs#L59-L70)

**Causa:** La suscripción a `AppState.PropertyChanged` lanza `CargarEstadisticasAsync()` y `CargarCuponBienvenidaAsync()` como fire-and-forget (`_ =`). Si el usuario cambia de cuenta fiscal 3 veces seguidas, hay 3 cargas en vuelo paralelas escribiendo sobre las mismas propiedades del ViewModel (race condition). Además, el setter de `CuentaFiscalActual` en AppState también dispara `PropertyChanged` de `DireccionFiscalActual`, potencialmente doblando las cargas.

**Solución — antes:**
```csharp
AppState.Instance.PropertyChanged += (_, e) =>
{
    if (e.PropertyName is nameof(AppState.CuentaFiscalActual))
    {
        _ = CargarEstadisticasAsync(forzarActualizacion: true);  // fire & forget
        _ = CargarCuponBienvenidaAsync();                         // fire & forget
    }
};
```

**Solución — después:**
```csharp
private CancellationTokenSource? _ctsEstadisticas;

AppState.Instance.PropertyChanged += (_, e) =>
{
    if (e.PropertyName is nameof(AppState.CuentaFiscalActual))
    {
        _ctsEstadisticas?.Cancel();
        _ctsEstadisticas = new CancellationTokenSource();
        var token = _ctsEstadisticas.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(50, token); // debounce: colapsa cambios rápidos
                await CargarEstadisticasAsync(forzarActualizacion: true);
                await CargarCuponBienvenidaAsync();
            }
            catch (OperationCanceledException) { }
        }, token);
    }
};
```

**Impacto esperado:** Elimina requests redundantes al cambiar de cuenta. Evita condiciones de carrera en las propiedades del ViewModel.

---

### 🟡 MEDIA — 6. logo512.png usado a 48pt

**Archivos:**
- [ContaBeeMovil/AppShell.xaml:29](ContaBeeMovil/AppShell.xaml#L29) — `WidthRequest="48"` con `Source="logo512.png"`
- [ContaBeeMovil/Pages/Perfil/TarjetasPage.xaml:71](ContaBeeMovil/Pages/Perfil/TarjetasPage.xaml#L71) — mismo caso

**Causa:** Una imagen de 512×512 px se carga completamente en memoria de textura y luego se escala a 48pt. En @2x (mayoría de Android gama media/baja) se necesitan 96×96 px pero se cargan 512×512. La textura ocupa ~786 KB en lugar de los ~37 KB necesarios.

**Solución:** Agregar `logo96.png` (96×96 px) a los recursos de imagen y usar ese source. O usar el sistema de asset density de MAUI (`@2x`, `@3x`) con el tamaño base correcto.

**Impacto esperado:** ~750 KB menos de presión sobre la memoria de textura GPU por instancia.

---

### 🟡 MEDIA — 7. Sombras (Shadow) extensivas en toda la app

**Magnitud:** 843+ ocurrencias de `<Shadow>` detectadas en el XAML del proyecto.

**Causa:** En Android < 9 (API 28), las sombras de MAUI se renderizan en software (CPU canvas), no en la GPU. Cada elemento con `<Shadow>` agrega un paso extra de composición en el hilo de UI. En DataTemplates de CollectionView, esto se multiplica por cada ítem visible simultáneamente.

**Por qué afecta más a gama baja:** Los dispositivos con Android 6-8 (segmento relevante en México) no tienen aceleración hardware de shadows. El costo es 100% CPU.

**Prioridad de optimización:**
1. Eliminar `<Shadow>` de DataTemplates dentro de `CollectionView` — mayor impacto en listas
2. Reducir `Opacity` de shadows de 0.3-0.5 → 0.06-0.12 en elementos estáticos
3. Reducir `Radius` a valores ≤ 6 donde sea posible

---

### 🟡 MEDIA — 8. BuildChartData — ObservableCollection con Add() en bucle

**Archivo:** [ContaBeeMovil/Pages/Dashboard/DashboardViewModel.cs:399-408](ContaBeeMovil/Pages/Dashboard/DashboardViewModel.cs#L399-L408)

**Causa:** `BuildChartData()` agrega hasta 31 ítems uno a uno a un `ObservableCollection`, disparando 31 eventos `CollectionChanged` internos. En este caso específico no hay observers mientras se construye (se asigna a la propiedad después), pero el patrón es frágil y puede romper si se agregan observers en el futuro.

**Solución — antes:**
```csharp
var items = new ObservableCollection<DiaActividadItem>();
for (int i = 0; i < dias; i++)
    items.Add(new DiaActividadItem { ... });
return items;
```

**Solución — después:**
```csharp
var lista = Enumerable.Range(0, dias).Select(i => new DiaActividadItem
{
    Dia         = i + 1,
    Emitidas    = i < emitidas.Count    ? emitidas[i]    : 0,
    Solicitadas = i < solicitadas.Count ? solicitadas[i] : 0,
}).ToList();
return new ObservableCollection<DiaActividadItem>(lista);
// El constructor IEnumerable NO dispara CollectionChanged por item
```

---

### 🟢 BAJA — 9. Serialización Newtonsoft en caché del Dashboard

**Archivo:** [ContaBeeMovil/Pages/Dashboard/DashboardViewModel.cs:373-378](ContaBeeMovil/Pages/Dashboard/DashboardViewModel.cs#L373-L378)

**Causa:** `LeerCache()` y `GuardarCache()` usan `Newtonsoft.Json`, que es ~3× más lento que `System.Text.Json` y tiene mayor overhead de memoria. Se ejecuta en el hilo UI.

**Solución:** Reemplazar `JsonConvert.SerializeObject/DeserializeObject` por `System.Text.Json.JsonSerializer`. Ya incluido en .NET, sin dependencia extra.

---

### 🟢 BAJA — 10. Lecturas de token secuenciales en AuthHandler

**Archivo:** [ContaBeeMovil/Services/AuthHandler.cs:65-75](ContaBeeMovil/Services/AuthHandler.cs#L65-L75)

`LeeAccessTokenAsync()` y `LeeExpiracionAsync()` son llamados secuencialmente. Este issue desaparece automáticamente al implementar el caché del punto #1. Sin ese fix, pueden paralelizarse con `Task.WhenAll`.

---

## Top 5 Quick Wins

> Mayor impacto, menor esfuerzo — ordenados por ROI.

| # | Fix | Archivos | Esfuerzo estimado | Impacto |
|---|-----|----------|-------------------|---------|
| 1 | Caché en memoria del access token | `ServicioSesion.cs` | ~30 min | Elimina 20-180 ms de latencia por request |
| 2 | File I/O a `Task.Run()` | `PaginaCaptura.xaml.cs:178-187` | ~10 min | Elimina freeze al abrir pantalla de Captura |
| 3 | `ObservableCollection.Add()` → `AddRange` | `PaginaCaptura.xaml.cs:201-203` | ~5 min | N→1 re-renders al restaurar fotos |
| 4 | Pre-calcular diccionario RFC | `PaginaComprobaciones`, `PaginaDevoluciones` | ~15 min | O(n×m)→O(n) al paginar listas |
| 5 | `BuildChartData` con constructor `IEnumerable` | `DashboardViewModel.cs:391-409` | ~5 min | 31→0 notificaciones internas al cargar gráfica |

---

## Top 3 Cambios Estructurales (mediano plazo)

1. **Auditar y reducir shadows en DataTemplates de listas** — Revisar todos los `CollectionView`/`ListView` y eliminar o simplificar `<Shadow>` en sus `DataTemplate`. Es el cambio con mayor impacto en scroll smoothness en Android 6-8. Requiere QA visual completo.

2. **Cancelación y debounce en todos los suscriptores de AppState.PropertyChanged** — Implementar `CancellationTokenSource` en `DashboardViewModel`, `PaginaCaptura`, y cualquier otro ViewModel que reaccione a cambios de AppState. Evita requests zombies y race conditions.

3. **Sistema de assets por densidad** — Migrar todos los PNGs genéricos a assets con sufijos `@2x`/`@3x` apropiados usando el sistema de Image Resources de MAUI. Reduce presión de textura GPU y tiempo de decodificación.

---

## Para profundizar el análisis

Compartir los siguientes archivos para análisis complementario:

- `PaginaComprobaciones.xaml`, `PaginaDevoluciones.xaml`, `FacturacionPage.xaml` — para cuantificar shadow count exacto por ítem de lista y complejidad del árbol de vistas.
- Implementación de `ResolverRfcCuentaFiscal()` — para confirmar complejidad real del lookup.
- `AppStateService.cs` completo — para verificar cuántos `PropertyChanged` encadena al cambiar `CuentaFiscalActual`.
- Estadísticas de versiones de Android en uso real (Firebase / Play Console) — determina si Android 6-8 sigue siendo segmento relevante y prioriza el fix de shadows.

---

## Cómo probar los fixes

1. **Fix #1 (caché token):** Cambiar cuenta fiscal → verificar en `LogsPage` (modo dev) que no hay hits repetidos de SecureStorage.
2. **Fix #2 (File I/O):** Capturar fotos → cerrar app → reabrir PaginaCaptura → medir que el dialog de conservar aparece sin freeze visible.
3. **Fix #3 + #8 (ObservableCollection):** Usar dotnet-trace / Visual Studio Profiler → verificar que eventos `CollectionChanged` bajan de N a 1.
4. **Fix #4 (RFC lookup):** Paginar comprobaciones con 20+ resultados → medir tiempo de construcción de lista en profiler.
5. **Todos los fixes:** Probar en emulador Android API 26 (Android 8.0) con CPU throttle ×4 — proxy útil para gama baja sin dispositivo físico.
