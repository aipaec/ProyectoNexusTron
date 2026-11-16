# Arquitectura actual del sistema NEXUS H3

## 1. Introducción

Este documento describe la arquitectura actual de NEXUS H3, desarrollado en C#. Se identifican las clases principales, sus responsabilidades, las funcionalidades implementadas y las limitaciones que deben considerarse para su evolución hacia NEXUS V2.0: TRON GRID EDITION.

El análisis se basa en la estructura y el código existente del proyecto. No se consideran como implementadas las funcionalidades que todavía no están conectadas o desarrolladas.

## 2. Tecnologías utilizadas

- Lenguaje de programación: C#.
- Plataforma: .NET.
- Aplicación original: consola.
- Interfaz gráfica inicial: Windows Forms.
- Base de datos: no implementada.
- Persistencia de datos: no implementada.

## 3. Clases principales

| Clase | Responsabilidad actual |
|---|---|
| `Program` | Punto de entrada y ejecución de la aplicación. |
| `SistemaTronGrid` | Controla el desarrollo general de la partida y coordina sus sistemas. |
| `UsuarioGrid` | Representa al jugador y sus características dentro de la Grid. |
| `EntidadRed` | Clase base para las entidades del entorno digital. |
| `SondaCentinela` | Representa una entidad de seguridad enemiga. |
| `RectificadorCLU` | Representa al enemigo CLU e interviene en los enfrentamientos. |
| `NodoMemoria` | Representa los objetos que pueden encontrarse y utilizarse durante la partida. |
| `SubsistemaMemoria` | Administra el inventario y los nodos de memoria. |
| `DirectivaDirectorio` | Administra los objetivos y el avance de las misiones. |
| `VisualesTron` | Presenta información y elementos visuales mediante la consola. |

El proyecto también contiene enumeraciones y componentes relacionados con la navegación, el combate, la energía y la integridad.

## 4. Funcionalidades implementadas en H3

### 4.1. Inicio de partida

El sistema permite identificar al usuario e iniciar una simulación dentro de la Grid.

### 4.2. Exploración y movimiento

- El jugador puede desplazarse mediante coordenadas X/Y.
- El área de exploración está limitada aproximadamente a los sectores comprendidos entre -3 y 3.
- Cada desplazamiento consume 4 unidades de energía.
- Durante la exploración pueden producirse encuentros y eventos aleatorios.

### 4.3. Eventos aleatorios

Durante la exploración se contemplan diferentes resultados:

- 30 %: aparición de un nodo de memoria.
- 30 %: aparición de un enemigo, como una Sonda Centinela o CLU.
- 20 %: pérdida de energía.
- 20 %: no ocurre ningún evento.

Estos porcentajes describen la distribución prevista en la lógica del sistema.

### 4.4. Combate y supervivencia

El sistema contempla enfrentamientos contra entidades enemigas, consumo de recursos y reducción de integridad.

- La integridad inicial es de 100 unidades.
- El disco de protección dispone de 80 unidades.
- La derrota se produce cuando se pierde la integridad necesaria para continuar.
- La energía y la integridad son valores distintos: quedarse sin energía no provoca por sí solo la derrota.

### 4.5. Inventario

El inventario permite almacenar hasta 8 nodos de memoria. Existen objetos relacionados con energía, protección, combate y depuración.

Algunos efectos de los objetos todavía son parciales o se limitan a mostrar mensajes.

### 4.6. Misiones

El sistema contiene objetivos relacionados con:

1. Investigar la anomalía ubicada en las coordenadas (2,2).
2. Encontrar el Disco Madre.
3. Neutralizar a CLU.
4. Llegar al Portal I/O ubicado en (0,0).

La victoria se relaciona con alcanzar el Portal I/O, mientras que la pérdida de integridad puede provocar la derrota.

## 5. Arquitectura de Windows Forms existente

El repositorio incluye un proyecto inicial llamado `NexusGUI`.

Este proyecto utiliza Windows Forms y contiene:

- `Form1.cs`: clase principal del formulario.
- `Form1.Designer.cs`: configuración visual del formulario.
- `Form1.resx`: recursos del formulario.
- `Program.cs`: punto de entrada de la aplicación gráfica.
- `NexusGUI.csproj`: configuración del proyecto.

El formulario contiene una etiqueta con el nombre NEXUS y un botón de inicio.

Sin embargo, el botón todavía no tiene conectado un evento que inicie la partida. Además, el proyecto gráfico no tiene una referencia configurada al proyecto que contiene la lógica de H3.

Por tanto, esta interfaz representa un primer avance visual y todavía no constituye una integración funcional del videojuego.

## 6. Limitaciones identificadas

Durante la revisión del código se identificaron las siguientes limitaciones:

1. La lógica principal está orientada a una aplicación de consola.
2. La interfaz Windows Forms todavía no está conectada con la lógica de H3.
3. No existe una base de datos SQL Server configurada.
4. No hay mecanismos implementados para guardar y recuperar partidas.
5. Algunos efectos de los objetos del inventario están incompletos.
6. El comportamiento de los enemigos no está desarrollado con el mismo nivel de detalle para todas las entidades.
7. El objetivo de neutralizar a CLU presenta una inconsistencia: el indicador correspondiente puede marcarse al derrotar cualquier entidad, no exclusivamente a CLU.
8. No se cuenta todavía con una separación completa entre interfaz, lógica del juego y acceso a datos.

Estas limitaciones deberán considerarse durante el diseño de la nueva arquitectura.

## 7. Estado actual del proyecto

| Componente | Estado |
|---|---|
| Lógica de exploración | Implementada en H3 |
| Movimiento por coordenadas | Implementado en H3 |
| Eventos aleatorios | Implementados en H3 |
| Sistema de combate | Implementado parcialmente |
| Inventario | Implementado con limitaciones |
| Sistema de misiones | Implementado con una inconsistencia identificada |
| Interfaz Windows Forms | Estructura inicial creada |
| Integración de Windows Forms con H3 | Pendiente |
| Base de datos SQL Server | Pendiente |
| Guardado y recuperación de partidas | Pendiente |

## 8. Conclusión

NEXUS H3 cuenta con una base funcional de exploración, movimiento, combate, inventario y misiones. Esta lógica constituye el punto de partida para desarrollar NEXUS V2.0: TRON GRID EDITION.

El proyecto Windows Forms ya tiene una estructura inicial, pero todavía necesita conectarse con la lógica existente. También será necesario diseñar una arquitectura organizada que permita separar la interfaz gráfica, las reglas del videojuego y la gestión de datos mediante SQL Server.

La evolución deberá conservar las funcionalidades existentes que sean útiles y corregir las limitaciones identificadas antes de incorporar nuevas características.
