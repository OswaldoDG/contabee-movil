# ContaBee — App Store & Play Store Submission Review

**App:** Contabee (Servicio de Facturación)  
**Bundle ID:** `mx.contabee.app`  
**Versión:** 1.0.28 (Build 28)  
**Organización:** Neurofant Mexico S.A.P.I. de C.V.  
**Plataformas:** iOS 15.0+, Android 23.0+, MacCatalyst 15.0+  
**Framework:** .NET MAUI / net10.0  
**Fecha de revisión:** 2026-04-28  

---

## Veredicto general

> **NO LISTA — 5 problemas bloqueantes y 8 adicionales deben resolverse antes de enviar.**

| Área | Estado |
|---|---|
| Privacy Manifest (iOS) | ⚠ Incompleto |
| Declaración de cifrado | ✗ Ausente en iOS y MacCatalyst |
| Términos de Servicio | ✗ Ausentes |
| Categoría MacCatalyst | ✗ Incorrecta |
| Privacy Manifest (Share Extension) | ✗ Ausente |
| Permisos Android | ⚠ Deprecados |
| Android Backup | ⚠ Sin restricciones |
| Developer mode en producción | ⚠ Debe compilarse fuera |
| APIs privadas | ✓ Ninguna encontrada |
| IDFA / Tracking | ✓ No utilizado |
| Permisos cámara/fotos (iOS) | ✓ Correctamente declarados |
| HTTPS / ATS | ✓ Todo HTTPS |
| Flujo IAP | ✓ Correcto |
| Eliminación de cuenta | ✓ Implementada |

---

## BLOQUEANTES — La submission será rechazada

### 1. Términos de Servicio ausentes

**Plataformas:** iOS, Android, MacCatalyst  
**Prioridad:** Bloqueante

El app tiene aviso de privacidad (`Resources/Raw/privacidad.html`) pero **ningún documento de Términos de Servicio**. Apple y Google requieren ToS cuando la app:

- Crea cuentas de usuario (`PaginaRegistro`)
- Procesa transacciones financieras (`TiendaPage` con IAP)
- Maneja datos fiscales (captura y procesamiento de RFC)

Adicionalmente, Apple App Store Connect requiere una **URL pública** tanto para la Política de Privacidad como para los Términos de Servicio. El HTML embebido en el app no es suficiente.

**Acciones requeridas:**
- Crear `ContaBeeMovil/Resources/Raw/terminos.html` con el contenido completo de los ToS.
- Agregar una entrada "Términos de Servicio" en el menú flyout de [AppShell.xaml](ContaBeeMovil/AppShell.xaml) junto al existente "Aviso de privacidad".
- Publicar ambos documentos en URLs estables antes de hacer la submission (ej. `https://contabee.mx/privacidad` y `https://contabee.mx/terminos`).
- Registrar ambas URLs en App Store Connect → App Information y en Google Play Console → Store listing.

---

### 2. Categoría incorrecta en MacCatalyst — `lifestyle` en lugar de `business`

**Archivo:** [ContaBeeMovil/Platforms/MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist) línea 19  
**Plataforma:** Mac App Store  
**Prioridad:** Bloqueante

```xml
<!-- Actual (INCORRECTO) -->
<key>LSApplicationCategoryType</key>
<string>public.app-category.lifestyle</string>

<!-- Correcto -->
<string>public.app-category.business</string>
```

ContaBee es una app de facturación fiscal y contabilidad. Submitirla bajo la categoría `lifestyle` causa rechazo directo o enrutamiento incorrecto a un revisor sin contexto de compliance fiscal.  
Alternativa aceptable: `public.app-category.finance`.

---

### 3. `ITSAppUsesNonExemptEncryption` ausente en iOS y MacCatalyst

**Archivos:**  
- [ContaBeeMovil/Platforms/iOS/Info.plist](ContaBeeMovil/Platforms/iOS/Info.plist) — clave completamente ausente  
- [ContaBeeMovil/Platforms/MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist) líneas 5-8 — comentado  

**Plataformas:** iOS, MacCatalyst  
**Prioridad:** Bloqueante

App Store Connect retiene el binario y solicita una declaración de export compliance de EE.UU. si esta clave está ausente. Como el app solo usa HTTPS/TLS estándar (sin cifrado propietario), califica para la exención:

```xml
<key>ITSAppUsesNonExemptEncryption</key>
<false/>
```

**Acción requerida:** Agregar esta clave en **ambos** archivos Info.plist. En MacCatalyst ya existe el bloque comentado en las líneas 5-8 — descomentarlo y establecerlo en `<false/>`.

Sin esta declaración, cada submission genera una revisión manual de compliance que agrega 2-5 días hábiles.

---

### 4. `NSPrivacyAccessedAPICategoryUserDefaults` no declarado en PrivacyInfo.xcprivacy

**Archivo:** [ContaBeeMovil/Platforms/iOS/Resources/PrivacyInfo.xcprivacy](ContaBeeMovil/Platforms/iOS/Resources/PrivacyInfo.xcprivacy) líneas 39-48  
**Plataforma:** iOS  
**Prioridad:** Bloqueante

El app usa `NSUserDefaults` (a través de `Preferences.Default`) en **al menos 10 archivos fuente** incluyendo `ServicioSesion.cs`, `AppStateService.cs`, `App.xaml.cs`, `TiendaPage.xaml.cs`, y el `SharedImageHandler.cs` de la Share Extension. En iOS, `Preferences.Default` mapea directamente a `NSUserDefaults`.

Apple exige desde mayo 2024 que este acceso esté declarado en el privacy manifest. La entrada **ya existe en el archivo pero está comentada** en las líneas 39-48:

```xml
<!--
    The entry below is only needed when you're using the Preferences API in your app.
<dict>
    <key>NSPrivacyAccessedAPIType</key>
    <string>NSPrivacyAccessedAPICategoryUserDefaults</string>
    ...
</dict> -->
```

El comentario en el template dice "solo si usas la Preferences API" — y sí se usa. El escáner automatizado de Apple detectará la llamada y rechazará si no coincide con el manifest.

**Acción requerida:** Descomentar ese bloque con el reason code `CA92.1`:

```xml
<dict>
    <key>NSPrivacyAccessedAPIType</key>
    <string>NSPrivacyAccessedAPICategoryUserDefaults</string>
    <key>NSPrivacyAccessedAPITypeReasons</key>
    <array>
        <string>CA92.1</string>
    </array>
</dict>
```

---

### 5. Share Extension no tiene su propio `PrivacyInfo.xcprivacy`

**Directorio:** [ContaBeeShareExtension/](ContaBeeShareExtension/)  
**Plataforma:** iOS  
**Prioridad:** Bloqueante

Apple requiere que **cada target de extensión** tenga su propio Privacy Manifest si accede a alguna API de reason requerido. La Share Extension en [ShareViewController.cs](ContaBeeShareExtension/ShareViewController.cs) realiza las siguientes operaciones que requieren declaración:

- Accede a timestamps de archivos al guardar la imagen compartida en el App Group container (`NSPrivacyAccessedAPICategoryFileTimestamp`)
- Escribe en `NSUserDefaults` a través del App Group (`NSPrivacyAccessedAPICategoryUserDefaults`)

Actualmente el directorio `ContaBeeShareExtension/` contiene solo: `ContaBeeShareExtension.csproj`, `Entitlements.plist`, `Info.plist`, `ShareViewController.cs`. Sin manifest propio, la extensión falla la validación.

**Acción requerida:** Crear `ContaBeeShareExtension/PrivacyInfo.xcprivacy` con al menos estas dos entradas:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>NSPrivacyAccessedAPITypes</key>
    <array>
        <dict>
            <key>NSPrivacyAccessedAPIType</key>
            <string>NSPrivacyAccessedAPICategoryFileTimestamp</string>
            <key>NSPrivacyAccessedAPITypeReasons</key>
            <array>
                <string>C617.1</string>
            </array>
        </dict>
        <dict>
            <key>NSPrivacyAccessedAPIType</key>
            <string>NSPrivacyAccessedAPICategoryUserDefaults</string>
            <key>NSPrivacyAccessedAPITypeReasons</key>
            <array>
                <string>CA92.1</string>
            </array>
        </dict>
    </array>
</dict>
</plist>
```

También registrar el archivo en [ContaBeeShareExtension.csproj](ContaBeeShareExtension/ContaBeeShareExtension.csproj) como `BundleResource`.

---

## ALTA PRIORIDAD — Causarán retraso o rechazo durante el proceso del binario

### 6. Developer mode activable por cualquier usuario en producción

**Archivos:**  
- [ContaBeeMovil/Pages/Login/PaginaLogin.xaml.cs](ContaBeeMovil/Pages/Login/PaginaLogin.xaml.cs) — gesto de 10 taps en el logo  
- [ContaBeeMovil/Pages/Dev/LogsPage.xaml.cs](ContaBeeMovil/Pages/Dev/LogsPage.xaml.cs)  
- [ContaBeeMovil/Pages/Tienda/TiendaPage.xaml.cs](ContaBeeMovil/Pages/Tienda/TiendaPage.xaml.cs) líneas 52-54 — botón de compra debug  

**Plataformas:** iOS, MacCatalyst  

Dar 10 taps en el logo activa un modo developer que expone: un visor de logs completo, un botón de "Compra Directa Debug" para IAP, y potencialmente tokens de autenticación y respuestas del servidor. Cualquier usuario puede descubrirlo.

Apple Guideline 2.3.1 prohíbe funcionalidad oculta en builds de producción. Un revisor que encuentre el logs viewer con el botón de debug IAP rechazará la build.

**Opciones:**

A. Compilar fuera de Release con directiva de preprocesador:
```csharp
// En AppShell.xaml.cs, registro de rutas:
#if DEBUG
Routing.RegisterRoute(nameof(LogsPage), typeof(LogsPage));
#endif
```

B. Constante de build-time:
```csharp
#if DEBUG
private const bool DevModeAllowed = true;
#else
private const bool DevModeAllowed = false;
#endif
```

---

### 7. MacCatalyst Info.plist sin descripción de permisos de cámara y fotos

**Archivo:** [ContaBeeMovil/Platforms/MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist)  
**Plataforma:** MacCatalyst  

El archivo MacCatalyst no contiene `NSCameraUsageDescription`, `NSPhotoLibraryUsageDescription` ni `NSPhotoLibraryAddUsageDescription`. Sin embargo, el app incluye `QRPage`, `CamaraPage` y `TomarFotoPage` que usan `AVCaptureSession`. Xcode fallará la build o App Store rechazará el binario al detectar el uso de cámara sin los strings de propósito.

**Acción requerida:** Agregar a [MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist):

```xml
<key>NSCameraUsageDescription</key>
<string>ContaBee necesita acceso a la cámara para escanear códigos QR de constancias fiscales y tomar fotos de tickets.</string>
<key>NSPhotoLibraryUsageDescription</key>
<string>ContaBee necesita acceso a tu galería para adjuntar imágenes de tickets.</string>
<key>NSPhotoLibraryAddUsageDescription</key>
<string>ContaBee necesita permiso para guardar imágenes de tickets y comprobantes en tu galería.</string>
```

---

### 8. Permiso de notificaciones solicitado en cada arranque en frío

**Archivo:** [ContaBeeMovil/Platforms/iOS/AppDelegate.cs](ContaBeeMovil/Platforms/iOS/AppDelegate.cs) líneas ~20-22  
**Plataforma:** iOS  

`UNUserNotificationCenter` authorization se solicita incondicionalmente en cada arranque. Las notificaciones solo son un fallback cuando la Share Extension no puede abrir el app via URL scheme (restricción de iOS 17+). Solicitar el permiso en el primer launch — antes de que el usuario haya compartido alguna imagen — es UX confusa y puede ser marcada como permiso innecesario bajo Guideline 5.1.1.

**Acción requerida:** Convertir la solicitud en lazy — pedirla solo la primera vez que una imagen compartida llega y el URL scheme fallback falla.

---

### 9. Android — `allowBackup="true"` sin reglas de exclusión

**Archivo:** [ContaBeeMovil/Platforms/Android/AndroidManifest.xml](ContaBeeMovil/Platforms/Android/AndroidManifest.xml) línea 3  
**Plataforma:** Android  

```xml
<application android:allowBackup="true" ... >
```

Con `allowBackup="true"` y sin reglas `android:fullBackupContent`, Android Auto Backup incluye los `SharedPreferences` en backups de Google Drive. Esto expone datos de preferencias sensibles como `tienda.compras_pendientes` (estado de compras serializado) y `TieneSesion` (flag de sesión). Los tokens en `SecureStorage` están protegidos por Android Keystore, pero las `Preferences` planas no.

**Acción requerida:** Deshabilitar backup o agregar reglas de exclusión:

```xml
<!-- Opción simple -->
<application android:allowBackup="false" ... >
```

```xml
<!-- Opción con exclusión selectiva — res/xml/backup_rules.xml -->
<full-backup-content>
    <exclude domain="sharedpref" path="." />
</full-backup-content>
```

---

### 10. Android — Permisos de almacenamiento deprecados

**Archivo:** [ContaBeeMovil/Platforms/Android/AndroidManifest.xml](ContaBeeMovil/Platforms/Android/AndroidManifest.xml) líneas 7-8  
**Plataforma:** Android  

```xml
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
```

- `WRITE_EXTERNAL_STORAGE` es no-op desde Android 10 (API 29) para apps que target API 29+.
- `READ_EXTERNAL_STORAGE` fue reemplazado por `READ_MEDIA_IMAGES` en Android 13 (API 33).

Google Play puede marcar el app por usar permisos deprecados sin restricción de versión.

**Acción requerida:**

```xml
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE"
    android:maxSdkVersion="32" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE"
    android:maxSdkVersion="28" />
<uses-permission android:name="android.permission.READ_MEDIA_IMAGES" />
```

---

## PRIORIDAD MEDIA — Buenas prácticas / riesgo de rechazo futuro

### 11. `banditoth.MAUI.DeviceId` — cobertura de Privacy Manifest sin confirmar

**Archivo:** [ContaBeeMovil/ContaBeeMovil.csproj](ContaBeeMovil/ContaBeeMovil.csproj) — `banditoth.MAUI.DeviceId` v1.0.2  
**Plataforma:** iOS  

Los identificadores de dispositivo están entre las APIs que Apple audita más agresivamente. Verificar que el paquete:
- No accede a `ASIdentifierManager` (IDFA)
- Tiene su propio `PrivacyInfo.xcprivacy` o sus accesos están cubiertos por el tuyo

```bash
# Verificar en caché de NuGet:
find ~/.nuget -path "*banditoth*" -name "PrivacyInfo.xcprivacy"
```

Si el paquete genera IDs usando system-boot-time o file-timestamp y no tiene manifest propio, agregar esos reason codes al `PrivacyInfo.xcprivacy` del app principal.

---

### 12. 14 llamadas `Debug.WriteLine` en código de producción

**Plataforma:** iOS, Android  

En Release el runtime de .NET elimina `Debug.WriteLine`, por lo que no hay impacto de rendimiento. Sin embargo, si alguna de las 14 instancias loguea tokens de autenticación, RFCs, o respuestas del servidor, podrían aparecer en los device logs de Xcode durante la sesión de revisión de un reviewer.

**Acción requerida:** Revisar cada instancia y guardar bajo directiva:

```csharp
#if DEBUG
Debug.WriteLine($"token: {token}");
#endif
```

---

### 13. Versiones de la Share Extension no sincronizadas con el app principal

**Archivo:** [ContaBeeShareExtension/Info.plist](ContaBeeShareExtension/Info.plist) líneas 12-14  
**Plataforma:** iOS  

```xml
<key>CFBundleVersion</key>
<string>1</string>          <!-- El app principal es 28 -->
<key>CFBundleShortVersionString</key>
<string>1.0</string>        <!-- El app principal es 1.0.28 -->
```

Apple permite que las extensiones tengan versión ≤ al app contenedor, pero la discrepancia puede provocar advertencias en la validación del archive y confusión en rollouts escalonados.

**Acción requerida:** Sincronizar con el app principal vía `.csproj`:
```xml
<ApplicationVersion>28</ApplicationVersion>
<ApplicationDisplayVersion>1.0.28</ApplicationDisplayVersion>
```

---

### 14. `NSPhotoLibraryAddUsageDescription` demasiado genérico

**Archivo:** [ContaBeeMovil/Platforms/iOS/Info.plist](ContaBeeMovil/Platforms/iOS/Info.plist)  
**Plataforma:** iOS  

```
Actual:   "ContaBee necesita permiso para guardar imágenes en tu galería."
Mejorado: "ContaBee guarda las fotos de tus tickets y comprobantes fiscales en tu galería para que puedas acceder a ellas fuera de la app."
```

Los revisores de Apple en ocasiones rechazan strings de propósito que no explican el **por qué** concreto. Una descripción más específica reduce el riesgo bajo Guideline 5.1.1.

---

### 15. MacCatalyst Info.plist sin `CFBundleDisplayName` ni `CFBundleIdentifier`

**Archivo:** [ContaBeeMovil/Platforms/MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist)  
**Plataforma:** MacCatalyst  

Estos valores los inyecta el build system desde el `.csproj`, pero su ausencia explícita en el plist puede causar diferencias silenciosas en distribución ad-hoc, notarización o TestFlight.

**Acción requerida:**
```xml
<key>CFBundleDisplayName</key>
<string>Contabee</string>
<key>CFBundleIdentifier</key>
<string>mx.contabee.app</string>
```

---

## BAJA PRIORIDAD — Metadatos y documentación

### 16. Política de Privacidad no accesible públicamente en la web

**Plataformas:** iOS, Android  

El app muestra `privacidad.html` solo dentro del app. App Store Connect requiere una **URL pública** de Privacy Policy en la página del producto. Lo mismo aplica para Google Play.

**Acción requerida:**
- Publicar la política en `https://contabee.mx/privacidad` (URL estable).
- Registrar esa URL en App Store Connect → App Information → Privacy Policy URL.
- Registrar en Google Play Console → Store listing → Privacy Policy.

---

## Lo que está correcto

### Autenticación y seguridad

| Item | Estado | Detalle |
|---|---|---|
| Almacenamiento de tokens | ✓ Seguro | JWT en `SecureStorage` (iOS Keychain / Android Keystore) |
| Rotación de tokens | ✓ Implementada | Double-check locking en `AuthHandler.cs` |
| HTTPS en todos los endpoints | ✓ Confirmado | `https://api.contabee.mx` — sin mixed content |
| `NSAllowsArbitraryLoads` | ✓ No declarado | iOS aplica HTTPS por defecto |
| `UIFileSharingEnabled` | ✓ No declarado | Sin exposición vía iTunes File Sharing |
| Secrets hardcodeados | ✓ Ninguno | Sin API keys, passwords ni tokens en el fuente |
| APIs privadas | ✓ Ninguna | Sin `objc_msgSend`, sin clases privadas |
| IDFA / ASIdentifierManager | ✓ No utilizado | Sin `NSUserTrackingUsageDescription` necesario |

### Permisos y Privacy Manifest (iOS)

| Item | Estado | Detalle |
|---|---|---|
| `NSCameraUsageDescription` | ✓ Presente | Descripción clara en español |
| `NSPhotoLibraryUsageDescription` | ✓ Presente | Descripción clara en español |
| `NSPhotoLibraryAddUsageDescription` | ✓ Presente | Ver ítem 14 para mejora |
| `NSPrivacyAccessedAPICategoryFileTimestamp` | ✓ Declarado | Reason C617.1 |
| `NSPrivacyAccessedAPICategorySystemBootTime` | ✓ Declarado | Reason 35F9.1 |
| `NSPrivacyAccessedAPICategoryDiskSpace` | ✓ Declarado | Reason E174.1 |
| Entitlements iOS | ✓ Mínimos | Solo App Groups para Share Extension |
| Entitlements Share Extension | ✓ Correctos | Mismo App Group `group.mx.contabee.app` |

### In-App Purchases

| Item | Estado | Detalle |
|---|---|---|
| Framework IAP | ✓ Plugin.InAppBilling v10.0.0 | Implementación MAUI correcta |
| Consulta de productos | ✓ Dinámica | IDs desde backend, precios desde la tienda |
| Flujo de compra | ✓ Completo | Verify → Complete → Consume |
| Restaurar compras | ✓ Implementado | `RestaurarComprasAsync()` al cargar la página |
| Compras diferidas / aprobación parental | ✓ Manejado | `PurchaseState.Deferred` retorna correctamente |
| Fallback de compras pendientes | ✓ Implementado | Reintento local con `tienda.compras_pendientes` |
| Precios hardcodeados | ✓ Ninguno | Todos los precios vienen del App Store / Play Store |
| Links de pago externo | ✓ Ninguno | Solo IAP; sin web checkout |

### Ciclo de vida de cuenta

| Item | Estado | Detalle |
|---|---|---|
| Registro de cuenta | ✓ `PaginaRegistro` | Email + contraseña |
| Verificación de email | ✓ `ConfirmarCuentaPage` | Deep link con token |
| Recuperación de contraseña | ✓ `RecuperarPassPage` | Deep link `contabee://contrasena/recuperar?token=` |
| Eliminación de cuenta | ✓ `EliminarCuentaPage` | Requerido por Apple desde junio 2023 |
| Sign in with Apple | N/A | No requerido — el app usa email/contraseña |

### Deep Linking

| Item | Estado |
|---|---|
| Scheme `contabee://` registrado en `CFBundleURLTypes` | ✓ |
| Deep link reset de contraseña: `contabee://contrasena/recuperar?token=` | ✓ |
| Deep link confirmación de cuenta: `contabee://cuenta/confirmar?token=` | ✓ |
| Sin open-redirect vulnerabilities detectados | ✓ |

### Dependencias de terceros

| SDK | Versión | Propósito | Riesgo Privacy |
|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | Framework MVVM | Ninguno |
| CommunityToolkit.Maui | 12.3.0 | UI helpers | Ninguno |
| Syncfusion.Maui.Toolkit | 1.0.9 | Componentes UI | Ninguno |
| ZXing.Net.Maui | 0.7.4 | Escaneo QR | Ninguno |
| Plugin.InAppBilling | 10.0.0 | In-app purchases | Ninguno |
| Newtonsoft.Json | 13.0.4 | Serialización JSON | Ninguno |
| SkiaSharp | 3.119.2 | Procesamiento de imágenes | Ninguno |
| banditoth.MAUI.DeviceId | 1.0.2 | ID de dispositivo anónimo | **Verificar** (ítem 11) |

---

## Checklist de pre-submission

### Crítico — Resolver antes de enviar

- [ ] Crear `ContaBeeMovil/Resources/Raw/terminos.html` con Términos de Servicio completos
- [ ] Agregar enlace "Términos de Servicio" en [AppShell.xaml](ContaBeeMovil/AppShell.xaml)
- [ ] Cambiar `LSApplicationCategoryType` en [MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist) línea 19 a `public.app-category.business`
- [ ] Agregar `ITSAppUsesNonExemptEncryption = false` en [iOS/Info.plist](ContaBeeMovil/Platforms/iOS/Info.plist)
- [ ] Descomentar `ITSAppUsesNonExemptEncryption = false` en [MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist) líneas 5-8
- [ ] Descomentar `NSPrivacyAccessedAPICategoryUserDefaults` (CA92.1) en [PrivacyInfo.xcprivacy](ContaBeeMovil/Platforms/iOS/Resources/PrivacyInfo.xcprivacy) líneas 39-48
- [ ] Crear `ContaBeeShareExtension/PrivacyInfo.xcprivacy` con `FileTimestamp` (C617.1) y `UserDefaults` (CA92.1)
- [ ] Registrar `PrivacyInfo.xcprivacy` de la extension en [ContaBeeShareExtension.csproj](ContaBeeShareExtension/ContaBeeShareExtension.csproj) como `BundleResource`

### Alta prioridad — Resolver antes de enviar

- [ ] Compilar fuera de Release: LogsPage, botón debug IAP, gesto de 10 taps (`#if DEBUG`)
- [ ] Agregar `NSCameraUsageDescription`, `NSPhotoLibraryUsageDescription`, `NSPhotoLibraryAddUsageDescription` en [MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist)
- [ ] Convertir solicitud de `UNUserNotificationCenter` a lazy en [AppDelegate.cs](ContaBeeMovil/Platforms/iOS/AppDelegate.cs)
- [ ] Establecer `android:allowBackup="false"` o agregar reglas de exclusión en [AndroidManifest.xml](ContaBeeMovil/Platforms/Android/AndroidManifest.xml)
- [ ] Agregar `maxSdkVersion` a permisos de almacenamiento y agregar `READ_MEDIA_IMAGES` en [AndroidManifest.xml](ContaBeeMovil/Platforms/Android/AndroidManifest.xml)

### Media prioridad

- [ ] Verificar que `banditoth.MAUI.DeviceId` tenga su propio `PrivacyInfo.xcprivacy` o cubrir sus accesos en el del app
- [ ] Auditar las 14 llamadas `Debug.WriteLine` por datos sensibles y guardarlas con `#if DEBUG`
- [ ] Sincronizar `CFBundleVersion` / `CFBundleShortVersionString` de [ContaBeeShareExtension/Info.plist](ContaBeeShareExtension/Info.plist) con el app principal
- [ ] Mejorar `NSPhotoLibraryAddUsageDescription` con descripción más específica en [iOS/Info.plist](ContaBeeMovil/Platforms/iOS/Info.plist)
- [ ] Agregar `CFBundleDisplayName` y `CFBundleIdentifier` explícitos en [MacCatalyst/Info.plist](ContaBeeMovil/Platforms/MacCatalyst/Info.plist)

### Antes de submission — Metadatos y documentación

- [ ] Publicar Privacy Policy en URL pública (ej. `https://contabee.mx/privacidad`)
- [ ] Publicar Terms of Service en URL pública (ej. `https://contabee.mx/terminos`)
- [ ] Registrar ambas URLs en App Store Connect y Google Play Console
- [ ] Preparar credenciales de cuenta de prueba para revisores de Apple (incluir en "Notes for Reviewer" en App Store Connect)
- [ ] Escribir descripción del App Store enfatizando facturación fiscal y compliance con el SAT — evitar lenguaje de "banca" o "finanzas" que activa revisión regulatoria adicional
- [ ] Preparar screenshots mostrando los prompts de permiso (cámara, galería)
- [ ] Verificar que `AppState.Instance.EsDev` sea siempre `false` en todas las builds de Release

---

## Referencias

- [Apple Privacy Manifest — Required Reason APIs](https://developer.apple.com/documentation/bundleresources/privacy_manifest_files/describing_use_of_required_reason_api)
- [App Store Review Guidelines 2.3 — Accurate Metadata](https://developer.apple.com/app-store/review/guidelines/#accurate-metadata)
- [App Store Review Guidelines 5.1.1 — Data Collection and Storage](https://developer.apple.com/app-store/review/guidelines/#data-collection-and-storage)
- [Export Compliance — ITSAppUsesNonExemptEncryption](https://developer.apple.com/documentation/security/complying-with-encryption-export-regulations)
- [App Extensions Programming Guide](https://developer.apple.com/library/archive/documentation/General/Conceptual/ExtensibilityPG/)
- [Android Auto Backup — Exclusion Rules](https://developer.android.com/guide/topics/data/autobackup)
- [Android Media Permissions Migration](https://developer.android.com/about/versions/13/behavior-changes-13#granular-media-permissions)
- [.NET MAUI Privacy Manifest](https://aka.ms/maui-privacy-manifest)
