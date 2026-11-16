# Análisis técnico — NEXUS V2.0: TRON GRID EDITION

## 1. Introducción

Este documento presenta el análisis técnico de NEXUS V2.0. Se explican las tecnologías seleccionadas, las alternativas consideradas, sus ventajas y desventajas y las razones para elegir una arquitectura basada en C#, Windows Forms y SQL Server.

El análisis toma como referencia el código existente de NEXUS H3 y los documentos de diseño y arquitectura propuestos.

## 2. Tecnologías seleccionadas

### 2.1. C#

Se mantendrá C# como lenguaje principal porque el proyecto H3 ya está desarrollado con este lenguaje.

**Ventajas:**
- Permite reutilizar las clases existentes.
- Facilita la programación orientada a objetos.
- Se integra con Windows Forms.
- Permite trabajar con SQL Server.

**Desventajas:**
- Requiere conocimientos de programación en .NET.
- Algunas configuraciones dependen del entorno de desarrollo.

**Decisión:** mantener C# para evitar comenzar el proyecto desde cero y aprovechar el trabajo realizado en H3.

### 2.2. Windows Forms

Se utilizará Windows Forms para construir la interfaz gráfica del videojuego.

**Ventajas:**
- Permite crear ventanas y controles visuales.
- Tiene un diseñador integrado en Visual Studio.
- Se puede programar utilizando C#.
- Facilita la creación de menús, botones y paneles.

**Desventajas:**
- Está orientado a aplicaciones de escritorio para Windows.
- Para crear una interfaz de videojuego con efectos visuales avanzados puede requerir controles personalizados.
- Es necesario conectar los formularios con la lógica existente.

**Decisión:** utilizar Windows Forms porque ya existe un proyecto inicial llamado `NexusGUI` en el repositorio y es la tecnología establecida para la evolución del videojuego.

### 2.3. SQL Server

Se utilizará SQL Server para almacenar las partidas y el progreso del jugador.

**Ventajas:**
- Permite organizar información en tablas relacionadas.
- Utiliza claves primarias y foráneas.
- Permite realizar consultas y actualizaciones.
- Soporta transacciones para guardar operaciones relacionadas.

**Desventajas:**
- Necesita instalación y configuración.
- Requiere establecer una conexión desde C#.
- Se deben administrar las tablas y los datos almacenados.

**Decisión:** utilizar SQL Server porque permite implementar el almacenamiento estructurado de partidas y cumple con las condiciones del proyecto.

### 2.4. Acceso a datos desde C#

Se propone utilizar `Microsoft.Data.SqlClient` para conectar la aplicación con SQL Server.

Este proveedor permite trabajar con conexiones, comandos, parámetros y transacciones desde C#.

**Ventajas:**
- Permite ejecutar consultas SQL.
- Facilita guardar y recuperar información.
- Permite utilizar parámetros en las consultas.
- Se integra con la capa de acceso a datos propuesta.

**Desventajas:**
- Requiere instalar y configurar el paquete.
- Es necesario controlar errores de conexión y consultas.

**Decisión:** utilizar `Microsoft.Data.SqlClient` como mecanismo de comunicación con SQL Server durante la implementación.

## 3. Alternativas consideradas

| Alternativa | Ventajas | Desventajas | Decisión |
|---|---|---|---|
| Windows Forms | Integración con C# y proyecto gráfico existente. | Orientado a Windows y requiere diseñar los controles. | Seleccionada. |
| WPF | Permite crear interfaces de escritorio más flexibles. | Requiere adaptar el proyecto a otra tecnología de interfaz. | No seleccionada. |
| SQL Server | Relaciones, consultas y transacciones. | Requiere instalación y configuración. | Seleccionada. |
| SQLite | Base de datos ligera y local. | No es la tecnología establecida para este proyecto. | No seleccionada. |
| MySQL | Permite almacenar datos relacionales. | Requeriría utilizar otro sistema de base de datos. | No seleccionada. |

Las alternativas se compararon considerando las condiciones del proyecto, el código existente y la facilidad de integración.

## 4. Análisis de la arquitectura

La arquitectura propuesta separa el sistema en tres capas:

**Presentación:**
Contendrá los formularios de Windows Forms y los controles que utilizará el jugador.

**Lógica del juego:**
Contendrá las reglas de exploración, movimiento, combate, inventario y misiones. Se aprovecharán las clases de H3, adaptándolas cuando sea necesario.

**Acceso a datos:**
Se encargará de comunicarse con SQL Server para guardar y recuperar partidas.

Esta organización permite mantener separadas las responsabilidades y facilita realizar cambios sin modificar todo el sistema.

## 5. Relación con el código existente de H3

La propuesta no busca reemplazar todo el trabajo realizado anteriormente.

Se pretende reutilizar las siguientes clases:

- `SistemaTronGrid`: coordinación de la partida.
- `UsuarioGrid`: información del jugador.
- `EntidadRed`: estructura base de las entidades.
- `SondaCentinela` y `RectificadorCLU`: enemigos.
- `NodoMemoria` y `SubsistemaMemoria`: objetos e inventario.
- `DirectivaDirectorio`: objetivos de las misiones.

Será necesario adaptar la presentación que actualmente utiliza la consola para que funcione mediante Windows Forms.

También deberán corregirse las limitaciones identificadas en el documento de arquitectura actual, especialmente la validación del objetivo de derrotar a CLU.

## 6. Ventajas generales de la propuesta

- Aprovecha el código desarrollado en H3.
- Incorpora una interfaz gráfica de escritorio.
- Organiza las responsabilidades del sistema.
- Permite almacenar el progreso del jugador.
- Facilita agregar nuevas misiones y objetos.
- Mejora la organización y el mantenimiento del código.

## 7. Riesgos y dificultades técnicas

Durante el desarrollo se deberán considerar los siguientes puntos:

1. Integrar la lógica de consola con los formularios de Windows Forms.
2. Evitar que las reglas del juego dependan directamente de los controles visuales.
3. Configurar correctamente la conexión con SQL Server.
4. Evitar la pérdida de información durante el guardado.
5. Validar que una partida recuperada conserve correctamente sus valores.
6. Corregir las limitaciones existentes en algunas mecánicas de H3.

Estos puntos deberán resolverse progresivamente durante la implementación.

## 8. Fuentes técnicas consultadas

Las siguientes fuentes oficiales sirven como referencia para las tecnologías seleccionadas:

1. Microsoft Learn — Documentación de Windows Forms:
   https://learn.microsoft.com/dotnet/desktop/winforms/

2. Microsoft Learn — Introducción a Windows Forms:
   https://learn.microsoft.com/dotnet/desktop/winforms/overview/

3. Microsoft Learn — Proveedor Microsoft.Data.SqlClient:
   https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace

4. Microsoft Learn — Instalación y uso de Microsoft.Data.SqlClient:
   https://learn.microsoft.com/sql/connect/ado-net/download-microsoft-sqlclient-data-provider

Estas fuentes explican las características de Windows Forms y el acceso a SQL Server desde aplicaciones .NET.

## 9. Verificación del uso de inteligencia artificial

Se utilizó inteligencia artificial como herramienta de apoyo para organizar la documentación, estructurar las propuestas técnicas y mejorar la claridad de las explicaciones.

La información fue contrastada con:

- La estructura del repositorio NEXUS.
- Las clases y funcionalidades existentes en H3.
- El proyecto inicial de Windows Forms.
- La documentación oficial de Microsoft.

La inteligencia artificial no sustituye la revisión del código ni demuestra que las funcionalidades propuestas ya estén implementadas. Las decisiones finales y la verificación del proyecto corresponden al equipo de desarrollo.

## 10. Conclusión

La combinación de C#, Windows Forms y SQL Server representa una alternativa adecuada para la evolución de NEXUS V2.0, porque aprovecha el trabajo realizado en H3 y permite incorporar una interfaz gráfica y persistencia de datos.

La arquitectura propuesta busca mantener el proyecto organizado y facilitar su crecimiento, sin confundir las funcionalidades existentes con las que todavía deben desarrollarse.
