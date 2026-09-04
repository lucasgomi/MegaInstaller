# MegaInstaller

**MegaInstaller** es un hub de escritorio para Windows que centraliza tus
instaladores (programas descargados o copiados a mano) en una sola carpeta y
los instala en lote, de forma automática y silenciosa cuando es posible.

- Añade instaladores **copiándolos a la carpeta**, **desde un archivo** o
  **desde una URL** (con barra de progreso de descarga en tiempo real). Cada
  uno aparece en la lista con su propio icono (el mismo que muestra Windows).
- Agrupa instaladores en **instancias** ("packs"): un mismo programa puede
  pertenecer a varias instancias sin duplicar el archivo ni la carpeta, y
  cada instancia puede llevar un icono propio de un paquete incluido con
  la aplicación.
- Instala una instancia entera en **modo fácil** (todo, rutas automáticas) o
  **modo avanzado** (excluye instaladores concretos y/o fuerza una carpeta
  de instalación distinta para esa ejecución), con registro en tiempo real,
  barra de progreso e instalación concurrente cuando es seguro hacerlo.
- Todo lo necesario para configurar cómo se instala cada programa
  (flags silenciosos, carpeta destino, ejecución como administrador,
  pertenencia a instancias) vive **dentro de la propia aplicación** - no hay
  que editar nada a mano, aunque el archivo generado es JSON legible por si
  quieres hacerlo.

## Pantallas

- **Al abrir la app** (la primera vez en cada sesión de Windows): una
  ventana para elegir la carpeta de instaladores. Si ya la habías elegido
  antes en esta misma sesión, MegaInstaller la recuerda y no vuelve a
  preguntar hasta el siguiente inicio de sesión.
- **Inicio**: la pantalla principal. Aquí gestionas las instancias (crear,
  editar, eliminar, instalar); un botón de **Ajustes** en la esquina
  superior permite cambiar la carpeta de instaladores en cualquier momento.
- **Editor de programas** (botón "Editor de programas..." desde Inicio): la
  biblioteca completa de instaladores de esa carpeta - añadir, editar,
  detectar tipo, quitar, o instalar programas sueltos sin pasar por una
  instancia. Tiene un buscador (por nombre, archivo o tag) y un botón
  "Editar marcados..." para cambiar de golpe argumentos, administrador,
  orden o tags de todos los programas con la casilla marcada.

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
      "tags": ["compresion", "utilidad"],
      "addedUtc": "2026-01-01T12:00:00Z"
    }
  ],
  "instances": [
    {
      "id": "8a1b2c3d4e...",
      "name": "Pack básico",
      "description": "Lo mínimo para un equipo nuevo",
      "iconKey": "briefcase-fill",
      "installerIds": ["3f6d9c2a1234..."],
      "order": 10,
      "addedUtc": "2026-01-01T12:00:00Z"
    }
  ]
}
```

Una instancia solo guarda una lista de `installerIds` que apuntan a
elementos de `items` - añadir el mismo instalador a varias instancias no
duplica nada. Puedes editar este archivo a mano si lo prefieres;
MegaInstaller lo vuelve a leer la próxima vez que abras esa carpeta.

Los `tags` son independientes de las instancias: sirven para organizar o
buscar programas dentro del Editor (por ejemplo "dev", "juegos"), sin que
eso implique que vayan a instalarse juntos - para eso están las instancias.

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

### Iconos

- **Instaladores**: el icono que se muestra en la lista es el que Windows
  asocia a ese archivo (el mismo que verías en el Explorador) - se lee del
  propio `.exe`/`.msi`, no hay que configurar nada.
- **Instancias**: se eligen de un pequeño paquete de iconos de código
  abierto incluido con la aplicación ([Bootstrap Icons](https://icons.getbootstrap.com/),
  licencia MIT - ver `src/MegaInstaller.App/Resources/InstanceIcons/THIRD-PARTY-NOTICES.md`),
  desde el selector en "Editar instancia".

### Instalación en lote: orden y concurrencia

Para instalar lo más rápido posible sin arriesgar el resultado, los
instaladores se agrupan en "oleadas" según su campo **Orden**:

- Las oleadas se ejecutan **estrictamente en secuencia**: una oleada termina
  del todo antes de que empiece la siguiente. Si un instalador depende de
  otro (por ejemplo, un runtime que otra app necesita), dale al primero un
  Orden menor para garantizar que termina antes.
- Los instaladores que **comparten el mismo Orden** se consideran
  independientes entre sí y se instalan **en paralelo** dentro de esa
  oleada (hasta 4 a la vez). Dar el mismo Orden a varios programas de una
  instancia es la forma de decirle a MegaInstaller "estos pueden ir a la
  vez, adelante".
- Como máximo hay **una** instalación elevada (administrador/UAC) en curso
  a la vez, aunque comparta oleada con otras no elevadas, para que nunca se
  amontonen varias ventanas de UAC.

Por cada instalador se muestra en tiempo real:

- Para instaladores **sin** "Ejecutar como administrador": la salida de
  consola del propio instalador, línea a línea.
- Para instaladores **con** "Ejecutar como administrador": el ciclo de vida
  (inicio, código de salida) - Windows no permite capturar la consola de un
  proceso lanzado con elevación (UAC), así que ahí no hay salida en directo,
  solo el resultado final.

Hay una casilla "Detener si falla uno" para cortar el lote antes de la
siguiente oleada si algo de la actual falla (la oleada en curso siempre
termina), y un botón "Detener" para cancelar - los instaladores ya
lanzados se intentan terminar, pero uno que ignore la señal de cierre
podría seguir instalando en segundo plano.

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
- "Cada sesión de Windows" se detecta mediante el id de sesión de Windows
  del proceso actual; en la inmensa mayoría de los casos esto equivale a
  "cada vez que inicias sesión en Windows", pero en configuraciones poco
  habituales (por ejemplo, ciertos escenarios de Escritorio remoto) el id
  podría reutilizarse y saltarse la ventana de selección.
