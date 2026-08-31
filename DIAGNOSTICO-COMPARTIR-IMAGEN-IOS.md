# Diagnóstico: compartir una imagen hacia ContaBee en iOS

Fecha del análisis: 2026-08-31  
Repositorio: `contabee-movil`  
Estado: diagnóstico realizado; todavía no se modificó la implementación.

## Objetivo de este documento

Conservar todo el contexto necesario para continuar el trabajo desde una Mac sin repetir el análisis. El siguiente paso requiere una compilación para dispositivo iOS, inspección de la firma efectiva y logs de la Share Extension.

## Síntoma reproducido

1. Desde Fotos en iOS se elige una imagen y se toca **Compartir**.
2. ContaBee aparece como opción.
3. Al elegir ContaBee parece abrirse brevemente una ventana, pero se cierra.
4. La aplicación principal no se abre.
5. No aparece ninguna notificación.
6. Si después se abre ContaBee manualmente y se entra a Captura, la imagen tampoco aparece.

Android funciona correctamente; el problema está limitado al flujo iOS.

## Conclusión principal

Existen dos problemas distintos:

1. **Una Share Extension de iOS no puede abrir directamente su aplicación contenedora.** El intento actual de abrir `contabee://shared-image` no es una API válida para este tipo de extensión.
2. **La imagen tampoco está llegando al App Group.** Esto se deduce porque, después de compartir, abrir ContaBee manualmente tampoco recupera la imagen. Por lo tanto, el fallo real ocurre antes de la navegación: la extensión se cae o cancela antes de terminar de escribir el archivo y la clave `pendingSharedImage`.

El punto exacto del segundo problema todavía no se puede afirmar sin logs del dispositivo porque la implementación actual oculta todas las excepciones.

## Arquitectura implementada actualmente

### Share Extension

Proyecto:

- `ContaBeeShareExtension/ContaBeeShareExtension.csproj`
- Bundle ID: `mx.contabee.app.shareextension`
- Target: `net10.0-ios`
- Extension point: `com.apple.share-services`

Configuración:

- `ContaBeeShareExtension/Info.plist` acepta una imagen mediante `NSExtensionActivationSupportsImageWithMaxCount = 1`.
- `ContaBeeShareExtension/Entitlements.plist` declara `group.mx.contabee.app`.
- La aplicación referencia la extensión como `IsAppExtension` desde `ContaBeeMovil/ContaBeeMovil.csproj`.

Flujo de `ContaBeeShareExtension/ShareViewController.cs`:

1. Obtiene el primer `NSExtensionItem` y busca un attachment compatible con `public.image`.
2. Decide entre `public.png`, `public.jpeg` y `public.image`.
3. Solicita el contenedor `group.mx.contabee.app`.
4. Carga los datos mediante `LoadDataRepresentation`.
5. Guarda un archivo `shared_yyyyMMddHHmmss.ext` en el App Group.
6. Guarda el nombre en `NSUserDefaults`, clave `pendingSharedImage`.
7. Programa una notificación local.
8. Intenta abrir `contabee://shared-image`.
9. Completa o cancela la extensión.

### Aplicación principal

Archivos involucrados:

- `ContaBeeMovil/Platforms/iOS/Entitlements.plist`
- `ContaBeeMovil/Platforms/iOS/Info.plist`
- `ContaBeeMovil/Platforms/iOS/AppDelegate.cs`
- `ContaBeeMovil/App.xaml.cs`
- `ContaBeeMovil/Helpers/SharedImageHandler.cs`
- `ContaBeeMovil/Pages/Captura/PaginaCaptura.xaml.cs`

Flujo esperado:

1. `SharedImageHandler.NotifyAppReady()` revisa siempre el App Group al iniciar.
2. `App.OnResume()` vuelve a revisarlo al regresar a primer plano en iOS.
3. `ReadAndCopyFromAppGroupAsync()` obtiene `pendingSharedImage`.
4. Copia el archivo desde el App Group a `FileSystem.AppDataDirectory`.
5. Si hay sesión activa, navega a `PaginaCaptura`.
6. `PaginaCaptura` consume el nombre pendiente y agrega un `CapturaLote` marcado como `EsCompartida`.

Esta parte de la aplicación principal es conceptualmente correcta y también cubre la apertura manual. El síntoma indica que no encuentra ni la clave ni el archivo compartido.

## Hallazgos confirmados

### 1. La apertura automática no está permitida

En `ShareViewController.cs` se llama a:

```csharp
UIApplication.SharedApplication.OpenUrl(...)
```

y como fallback:

```csharp
ExtensionContext?.OpenUrl(...)
```

`UIApplication` está marcado como no disponible para extensiones. Además, Apple solo documenta `NSExtensionContext.openURL` para puntos de extensión concretos, como Today e iMessage; una Share Extension no puede usarlo para lanzar su aplicación contenedora.

El comentario actual que dice que `UIApplication.SharedApplication` obtiene la aplicación host (Fotos) es incorrecto. La extensión es un binario/proceso separado y no obtiene el `UIApplication` de Fotos.

Referencias:

- <https://developer.apple.com/documentation/foundation/nsextensioncontext/open(_:completionhandler:)>
- <https://developer.apple.com/library/archive/documentation/General/Conceptual/ExtensibilityPG/ExtensionOverview.html>
- <https://developer.apple.com/forums/thread/764570>

### 2. Se ignora si la apertura falla

Los completion handlers no examinan el valor `success`. Tanto `true` como `false` terminan ejecutando `CompleteExtension()`. Esto explica por qué la hoja desaparece aunque ContaBee no se abra.

### 3. La notificación solo se programa después de guardar la imagen

`ScheduleNotification()` se ejecuta después de:

- obtener el App Group;
- cargar la representación de la imagen;
- escribir el archivo;
- escribir `pendingSharedImage`.

Como no hay notificación **y** la aplicación principal tampoco recupera el archivo, es muy probable que la ejecución no alcance ese punto.

### 4. Los errores están totalmente ocultos

La extensión contiene varios `catch { }` y cancela sin mostrar mensajes. También ignora el `NSError` de `AddNotificationRequest`.

Esto impide distinguir entre:

- App Group no autorizado;
- `GetContainerUrl` devolviendo `null`;
- error de `NSItemProvider`;
- error al guardar;
- error de notificación;
- crash o terminación por iOS.

### 5. Antes existía instrumentación y fue eliminada

El commit `00e043e` eliminó un log `shareext_log.txt` que registraba cada etapa del proceso.

Se puede consultar la versión anterior con:

```bash
git show 00e043e^:ContaBeeShareExtension/ShareViewController.cs
```

Ese log era útil, pero no suficiente si el App Group mismo es inaccesible. La nueva instrumentación debe escribir también mediante `NSLog`, para que los errores aparezcan en la consola del dispositivo.

## Causas probables, en orden de prioridad

### A. App Group ausente en la firma o en los perfiles efectivos

Los archivos fuente declaran `group.mx.contabee.app` en ambos targets, pero eso no garantiza que los perfiles con los que se firmó la instalación lo contengan.

El App Group debe estar asignado en Apple Developer a estos dos App IDs:

- `mx.contabee.app`
- `mx.contabee.app.shareextension`

Y se deben regenerar los cuatro perfiles utilizados por el proyecto:

- `VS: mx.contabee.app Development`
- `ContaBee ShareExtension Development`
- `ContaBee_AppStore`
- `ContaBee ShareExtension AppStore`

Apple indica que al modificar capacidades se deben regenerar los perfiles que usan el App ID:

- <https://developer.apple.com/help/account/identifiers/enable-app-capabilities>
- <https://developer.apple.com/documentation/xcode/configuring-app-groups>

Si `NSFileManager.DefaultManager.GetContainerUrl("group.mx.contabee.app")` devuelve `null`, el código actual cancela inmediatamente y el usuario solo ve cómo se cierra la ventana.

### B. La extensión se cae antes o durante `ViewDidLoad`

La ventana breve también es compatible con un crash del proceso de la extensión. Hay que buscar en la consola del iPhone:

- `ContaBeeShareExtension`
- `mx.contabee.app.shareextension`
- `JetsamEvent`
- `containermanagerd`
- `UNErrorDomain`
- errores de `Foundation`, `UIKit` o del runtime de .NET

### C. Fallo cargando el attachment de Fotos

Las fotografías de iPhone pueden entregarse como HEIC/HEIF. El código actual usa `public.image` como fallback pero siempre asigna la extensión `.jpg` cuando no es PNG. Esto puede guardar datos HEIC con nombre JPEG o puede fallar dependiendo de la representación ofrecida por el proveedor.

Debe registrarse `RegisteredTypeIdentifiers` y el error devuelto por `LoadDataRepresentation`. Posteriormente conviene preservar el tipo real o convertir explícitamente la imagen a JPEG.

### D. La solicitud de notificación termina tarde o devuelve error

`AddNotificationRequest` es asíncrono, pero la extensión no espera su resultado y descarta el error. Incluso después de corregir el guardado, la notificación no debe ser el único mecanismo de éxito: iOS no garantiza su presentación y el usuario puede tener Focus, resumen programado o restricciones del sistema.

La experiencia compatible debe confirmar dentro de la extensión que la imagen quedó lista y pedir abrir ContaBee manualmente. La notificación puede conservarse como ayuda adicional, después de validar su comportamiento en el dispositivo objetivo.

## Verificaciones necesarias en la Mac

### 1. Revisar Apple Developer

En **Certificates, Identifiers & Profiles**:

1. Abrir el App ID `mx.contabee.app`.
2. Confirmar que App Groups está habilitado y contiene `group.mx.contabee.app`.
3. Abrir `mx.contabee.app.shareextension`.
4. Confirmar exactamente el mismo grupo.
5. Regenerar y descargar perfiles Development y App Store para ambos IDs.
6. Eliminar perfiles antiguos de la caché de la Mac o asegurarse de que el build selecciona los nuevos.

### 2. Inspeccionar los entitlements del producto firmado

Después de compilar, ejecutar sustituyendo la ruta real de la aplicación:

```bash
codesign -d --entitlements :- "/ruta/ContaBeeMovil.app"
codesign -d --entitlements :- "/ruta/ContaBeeMovil.app/PlugIns/ContaBeeShareExtension.appex"
```

En ambos resultados debe existir:

```xml
<key>com.apple.security.application-groups</key>
<array>
    <string>group.mx.contabee.app</string>
</array>
```

También conviene inspeccionar los perfiles embebidos:

```bash
security cms -D -i "/ruta/ContaBeeMovil.app/embedded.mobileprovision" \
  | plutil -extract Entitlements xml1 -o - -

security cms -D -i "/ruta/ContaBeeMovil.app/PlugIns/ContaBeeShareExtension.appex/embedded.mobileprovision" \
  | plutil -extract Entitlements xml1 -o - -
```

No basta con revisar `Entitlements.plist`; hay que revisar la aplicación y la extensión ya firmadas.

### 3. Adjuntar el depurador a la extensión

En Xcode:

1. Instalar y abrir ContaBee al menos una vez.
2. Usar **Debug > Attach to Process by PID or Name**.
3. Escribir `ContaBeeShareExtension` y esperar el lanzamiento.
4. Ir a Fotos y compartir una imagen hacia ContaBee.
5. Capturar la primera excepción o mensaje que aparezca.

Como alternativa, abrir **Console.app**, seleccionar el iPhone conectado y filtrar por los nombres indicados anteriormente.

## Cambios recomendados en el código

Estos cambios todavía no están implementados.

### Fase 1: diagnóstico observable

1. Agregar una función de logging que siempre llame a `NSLog`.
2. Si el App Group está disponible, escribir además `shareext_log.txt` dentro del grupo.
3. Registrar:
   - inicio de `ViewDidLoad`;
   - attachments y tipos registrados;
   - resultado de `GetContainerUrl`;
   - tipo solicitado al proveedor;
   - longitud de datos y `NSError` de carga;
   - resultado de guardado y existencia final del archivo;
   - escritura y lectura inmediata de `pendingSharedImage`;
   - estado y error de notificaciones;
   - llamada a `CompleteRequest` o `CancelRequest`.
4. Mostrar una interfaz mínima con estado en vez de cerrar inmediatamente.
5. Hacer que la aplicación principal lea `shareext_log.txt` y lo copie a `IServicioLogs`, si el contenedor está disponible.

### Fase 2: corregir el flujo

1. Eliminar `UIApplication.SharedApplication.OpenUrl` y el fallback `ExtensionContext.OpenUrl`.
2. Manejar correctamente JPEG, PNG y HEIC/HEIF; idealmente normalizar a JPEG antes de entregar a Captura.
3. Escribir primero a un archivo temporal y moverlo al nombre final únicamente cuando termine la escritura.
4. Guardar `pendingSharedImage` solo después de confirmar que el archivo final existe y tiene contenido.
5. Esperar el completion de la solicitud de notificación y registrar su error.
6. Mostrar éxito dentro de la extensión: “Imagen guardada. Abre ContaBee para continuar”.
7. Completar la extensión después de que el usuario toque **Listo**, o tras una pausa breve una vez que el estado se haya presentado.
8. Al copiar la imagen en la app principal, eliminar el archivo original del App Group para evitar acumulación.
9. Si se desea soportar varias imágenes o varias acciones seguidas, reemplazar la única clave `pendingSharedImage` por una cola. La configuración actual acepta solamente una imagen por invocación.

## Pruebas de aceptación

Realizar todas en un iPhone físico; no considerar suficiente el simulador.

### Matriz básica

- App cerrada + imagen JPEG.
- App cerrada + imagen HEIC tomada con el iPhone.
- App en segundo plano + JPEG.
- App en segundo plano + HEIC.
- App abierta en `PaginaCaptura` + nueva imagen compartida.
- Notificaciones permitidas.
- Notificaciones denegadas.
- Build Debug con perfiles Development.
- Build TestFlight/Release con perfiles App Store.

### Resultado esperado compatible con iOS

1. Al compartir, la extensión muestra que la imagen se guardó correctamente.
2. No intenta abrir ContaBee automáticamente.
3. Si iOS entrega la notificación, al tocarla se abre ContaBee.
4. Si no hay notificación, abrir ContaBee manualmente produce el mismo resultado.
5. ContaBee navega a Captura y agrega la imagen como si se hubiera tomado desde la aplicación.
6. No quedan archivos huérfanos en el App Group después de copiarlos.

## Logs ya existentes en la aplicación principal

`SharedImageHandler` genera mensajes con prefijo `[SharedImage]`, entre ellos:

- `app lista`
- `leyendo App Group...`
- `archivo listo`
- `no se encontró archivo en App Group`
- `archivo copiado desde App Group`
- `navegando a PaginaCaptura`
- errores de copia o navegación

Estos logs ayudan a verificar la mitad de la aplicación principal, pero no muestran los fallos internos de la extensión porque son procesos separados.

## Observaciones secundarias

- `ContaBeeShareExtension/Info.plist` tiene versiones fijas `1.0.32` y `32`, mientras la aplicación principal está en `2.5.10` y build `65`. No explica este fallo, pero conviene comprobar que las propiedades pasadas desde el proyecto principal realmente actualicen la versión final del `.appex`.
- El nombre del archivo usa precisión de segundos. Dos compartidos durante el mismo segundo podrían colisionar; usar un GUID sería más seguro.
- `NSUserDefaults.Synchronize()` no sustituye la validación de lectura inmediata ni un mecanismo de cola.
- La aplicación ya revisa el App Group al arrancar y al reanudarse; no hace falta depender del deep link para procesar una imagen guardada correctamente.

## Estado para retomar el trabajo

- No se modificó código funcional durante este análisis.
- El siguiente paso recomendado es verificar entitlements/perfiles efectivos en la Mac y capturar el primer log o crash de la extensión.
- Si los perfiles contienen correctamente el App Group, implementar la Fase 1 de instrumentación antes de cambiar el mecanismo de lectura de imágenes.
- No volver a invertir tiempo intentando abrir la aplicación directamente desde la Share Extension: esa ruta no es compatible con las restricciones de iOS.
