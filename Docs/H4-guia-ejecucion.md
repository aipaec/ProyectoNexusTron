# NOCTRYX H4 — Guía de clonación y ejecución

> Esta guía describe la rama de entrega H4 `hito4-noctryx`. Esa rama es local y aún no está publicada en GitHub; los comandos de clonación funcionarán cuando se publique con autorización. `main` todavía contiene la base heredada NEXUS.

## Requisitos

- Windows 10/11 de 64 bits.
- .NET 10 SDK para compilar (o .NET 10 Desktop Runtime para ejecutar el paquete framework-dependent).
- SQL Server LocalDB (`MSSQLLocalDB`) para la persistencia H4. SSMS es opcional; también se puede ejecutar el script con `sqlcmd`.
- El paquete se distribuye sin pistas de audio. Las pistas del menú y los mapas son opcionales y se buscan en la carpeta `Downloads` del usuario; si no están, el juego sigue sin esa música.

## Clonar la entrega

Cuando la rama esté publicada:

```powershell
git clone --single-branch --branch hito4-noctryx https://github.com/aipaec/ProyectoNexusTron.git NOCTRYX-H4
Set-Location NOCTRYX-H4
```

No clonar `main` para esta entrega hasta que contenga NOCTRYX H4.

## Crear la base de datos

La conexión predeterminada de la aplicación es `(localdb)\MSSQLLocalDB` con autenticación integrada de Windows. El script solo crea `NOCTRYX_H4` si no existe y no borra bases existentes.

Desde PowerShell, con `sqlcmd` instalado:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\sql\crear_base_noctryx_h4.sql
```

Alternativamente, abrir `sql/crear_base_noctryx_h4.sql` en SSMS y ejecutarlo. La tabla `dbo.RegistrosPartida` guarda nombre, mapa, integridad, coordenadas y fecha.

## Compilar y ejecutar

Desde la raíz del clon:

```powershell
dotnet restore .\ConsoleApp3\NexusGUI\NexusGUI.csproj
dotnet build .\ConsoleApp3\NexusGUI\NexusGUI.csproj -c Release
dotnet run --project .\ConsoleApp3\NexusGUI\NexusGUI.csproj -c Release
```

La aplicación inicia con la cinemática y luego el menú NOCTRYX. Para usar un ejecutable compilado, abrir `ConsoleApp3\NexusGUI\bin\Release\net10.0-windows\NexusGUI.exe`; no requiere Visual Studio abierto, pero sí Windows y el .NET 10 Desktop Runtime. La base LocalDB debe estar instalada y el script ejecutado para las funciones SQL.

## Comprobar H4

1. En el menú, abrir **PARTIDAS GUARDADAS** para consultar las partidas disponibles.
2. Iniciar una partida; pulsar **ESC** y seleccionar **GUARDAR / CARGAR**.
3. Escribir un identificador, pulsar **GUARDAR PARTIDA** y cerrar completamente la aplicación.
4. Abrirla de nuevo, entrar a **PARTIDAS GUARDADAS**, seleccionar una fila con coordenadas y pulsar **CARGAR**.
5. En el mapa, comprobar que regresan el mapa, la posición e integridad guardados.

Los guardados de juego conservan mapa, posición e integridad de ECHO-0. El estado de enemigos y los registros narrativos no se guardan.

## Evidencias de evaluación pendientes

- Capturas propias de SELECT, INSERT, UPDATE, DELETE y mensajes de validación; no reutilizar capturas de código como si fueran ejecución.
- Evidencia de conexión fallida seguida de recuperación.
- ActivityWatch: no está instalado ni hubo registro; cualquier evidencia de tiempo debe recopilarse en el equipo del estudiante.
- Commits identificables en la rama de entrega.
- Publicar y verificar la rama de pruebas en GitHub.
- Crear el GitHub Release solo después de revisar el código, ejecutar las pruebas y autorizar la publicación.

Para probar sin acceder a las partidas habituales, crea una instancia LocalDB dedicada, instala allí el esquema y configura NOCTRYX_LOCALDB_SERVER en la sesión que iniciará el juego. La aplicación usa MSSQLLocalDB cuando no se define esa variable.
