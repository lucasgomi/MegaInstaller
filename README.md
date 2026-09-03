# MegaInstaller

**MegaInstaller** es un hub de escritorio para Windows que centraliza tus
instaladores (programas descargados o copiados a mano) en una sola carpeta y
los instala en lote, de forma automática y silenciosa cuando es posible.

- Añade instaladores **copiándolos a la carpeta**, **desde un archivo** o
  **desde una URL** (con barra de progreso de descarga en tiempo real).
- Un botón para elegir la carpeta de instaladores.
- Un botón para instalarlos todos (o solo los seleccionados), con registro
  de instalación en tiempo real y barra de progreso.
- Todo lo necesario para configurar cómo se instala cada programa
  (flags silenciosos, carpeta destino, ejecución como administrador) vive
  **dentro de la propia aplicación** - no hay que editar nada a mano, aunque
  el archivo generado es JSON legible por si quieres hacerlo.

## Cómo funciona

Cada carpeta de instaladores contiene un archivo `megainstaller.json` (se
crea solo la primera vez que añades algo). Es la "ficha técnica" de esa
carpeta: para cada instalador guarda su nombre, el tipo detectado, los
argumentos con los que se debe ejecutar, si necesita permisos de
administrador y en qué orden instalarlo. Gracias a ese archivo, MegaInstaller
puede reinstalar exactamente el mismo lote, con los mismos flags, en
cualquier máquina - basta con copiar la carpeta entera (instaladores +
`megainstaller.json`).

Ejemplo de `megainstaller.json`:

```json
{
  "version": 1,
  "items": [
    {
      "id": "3f6d9c2a1234...",
      "name": "7-Zip",
      "fileName": "7z2408-x64.exe",
      "sourceUrl": "https://www.7-zip.org/a/7z2408-x64.exe",
      "type": "Nsis",
      "arguments": "/S",
      "targetInstallDir": null,
      "runAsAdmin": true,
      "enabled": true,
      "order": 10,
      "notes": "",
      "addedUtc": "2026-01-01T12:00:00Z"
    }
  ]
}
```

Puedes editar este archivo a mano si lo prefieres; MegaInstaller lo vuelve a
leer la próxima vez que abras esa carpeta.

### Detección de tipo de instalador y flags silenciosos

Al añadir un instalador, MegaInstaller inspecciona el archivo (sin
ejecutarlo) buscando cabeceras/firmas conocidas y sugiere flags silenciosos
según el tipo detectado:

| Tipo           | Detección                          | Flags sugeridos                              |
|----------------|-------------------------------------|-----------------------------------------------|
| MSI            | extensión `.msi` o cabecera OLE     | `/qn /norestart`                               |
| NSIS           | cadena "Nullsoft" en el binario     | `/S`                                           |
| Inno Setup     | cadena "Inno Setup" en el binario   | `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`|
| InstallShield  | cadena "InstallShield" en el binario| `/s /v"/qn /norestart"`                        |
| Desconocido    | sin coincidencias                   | (ninguno; se lanza el instalador tal cual)     |

Estas sugerencias solo rellenan el campo de argumentos, que es totalmente
editable - nunca se aplican flags "a ciegas" ni de forma invisible.

También puedes indicar una **carpeta de destino** para MSI, Inno Setup y
NSIS; el botón "Insertar carpeta en argumentos" añade el flag correcto
(`INSTALLDIR="..."`, `/DIR="..."` o `/D=...` respectivamente) al final de los
argumentos. Para InstallShield y tipos desconocidos no se inserta nada
automáticamente porque no existe un flag universal fiable - hay que añadirlo
a mano si se conoce.

### Instalación en lote

Los instaladores se ejecutan **secuencialmente** (nunca en paralelo): muchos
usan mutex globales o el servicio del instalador de Windows y no toleran
ejecutarse a la vez. Por cada uno se muestra en tiempo real:

- Para instaladores **sin** "Ejecutar como administrador": la salida de
  consola del propio instalador, línea a línea.
- Para instaladores **con** "Ejecutar como administrador": el ciclo de vida
  (inicio, código de salida) - Windows no permite capturar la consola de un
  proceso lanzado con elevación (UAC), así que ahí no hay salida en directo,
  solo el resultado final.

Hay una casilla "Detener si falla uno" para cortar el lote ante el primer
fallo, y un botón "Detener" para cancelar una instalación en curso.

## Compilar desde el código fuente

Requiere el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
(se puede compilar tanto en Windows como en Linux/macOS gracias a
`EnableWindowsTargeting`; el `.exe` resultante solo se puede *ejecutar* en
Windows).

```bash
# Restaurar y compilar todo
dotnet build MegaInstaller.sln

# Ejecutar los tests (lógica pura, sin UI - se ejecutan en cualquier SO)
dotnet test tests/MegaInstaller.Core.Tests/MegaInstaller.Core.Tests.csproj

# Publicar el .exe autocontenido (no necesita el runtime de .NET instalado)
dotnet publish src/MegaInstaller.App/MegaInstaller.App.csproj -c Release -r win-x64 -o publish/win-x64
```

El ejecutable queda en `publish/win-x64/MegaInstaller.exe` (~70 MB, incluye
el runtime de .NET).

### Releases

Cada tag `vX` (por ejemplo `v1`) dispara el workflow
`.github/workflows/release.yml`, que compila el `.exe` y publica una
[GitHub Release](../../releases) con el ejecutable y un `.zip` adjuntos.
También se puede lanzar manualmente desde la pestaña *Actions* (
`workflow_dispatch`).

## Estructura del repositorio

```
src/
  MegaInstaller.Core/   Lógica de negocio (multiplataforma, sin UI):
                        manifest, detección de tipo, catálogo de flags,
                        construcción del comando, descargas, ajustes.
  MegaInstaller.App/    Interfaz WinForms (Windows) que usa Core.
tests/
  MegaInstaller.Core.Tests/  Tests unitarios de Core.
```

## Limitaciones conocidas

- La detección de tipo de instalador es heurística (búsqueda de cadenas
  conocidas en el binario), no infalible. Si falla, puedes fijar el tipo y
  los argumentos a mano en "Editar".
- No hay captura de salida de consola en tiempo real para instaladores
  elevados (UAC) - es una limitación de Windows, no de la aplicación.
- El nombre de la propiedad de carpeta destino en instaladores MSI
  (`INSTALLDIR`) varía según el paquete; si no funciona, hay que ajustarlo
  a mano según la documentación del instalador concreto.
- El código de salida de un instalador no siempre refleja fielmente si la
  instalación tuvo éxito (algunos "bootstrappers" relanzan otro proceso y
  terminan enseguida); en caso de duda, revisa el registro de instalación.
