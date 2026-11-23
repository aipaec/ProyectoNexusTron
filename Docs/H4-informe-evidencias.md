# Informe local de entrega de NOCTRYX H4

23 de noviembre de 2026. Copia NEXUS-clon-http11.

## Git y respaldo

- Respaldo NEXUS-clon-http11-respaldo-H4-20261123-170807, junto a la copia original. Se copiaron 388 archivos y la comprobación reportó cero diferencias.
- 6c766cf incorpora la GUI H4, NexusCore y el SQL. fbf9219 actualiza la guía. Las ramas hito4-noctryx y hito4-pruebas apuntan a fbf9219fce39dca03118d46608726547c51e87ec. El checkout de pruebas está limpio.
- La lista exacta de archivos de los commits está en output/verification/evidencias/archivos-commits.txt. Los cambios previos de consola, la solución Nexus y los archivos de audio locales siguen fuera de los commits.
- No se ejecutó push ni merge y no se creó un Release.

## Pruebas

- Compilación local Release win-x64 desde el checkout de pruebas. Cero errores y 25 advertencias de nulabilidad en NexusCore y Form1.
- Esquema SQL aplicado dos veces en una instancia LocalDB dedicada con tres filas sintéticas.
- Menú de cuatro opciones sin solapamientos, animación visible, Controles y regreso al menú desde el mapa. No se midieron FPS.
- La interfaz consultó las tres filas, guardó una cuarta y actualizó esa misma fila sin duplicarla. Después de cerrar y reabrir, cargó Ciudad en Colapso con integridad 100/100. SQL registró X 150 e Y 534.
- La interfaz mostró el botón de guardado deshabilitado con el campo vacío. SQL rechazó integridad 101 y eliminó exactamente la fila sintética 4.
- Se provocó un fallo con una instancia inexistente. El aviso permitió volver al menú. Con el servidor correcto, el ejecutable extraído mostró las tres filas sintéticas restantes.
- Once capturas y registros de compilación y SQL en output/verification/evidencias. Las partidas originales quedaron fuera de la conexión de prueba.

## Paquete

- output/release/NOCTRYX-H4-win-x64-fbf9219.zip
- SHA-256 4657777823AA198DCCED92B55E45F2CD93098AE7E26DDC94797EBEEF601A9486
- 49 archivos. Incluye NexusGUI.exe, dependencias, símbolos, LEEME, guía, esquema SQL y manifiesto con el commit y los hashes de los archivos. Sin pistas de audio ni bases de datos.
- Se verificó el tamaño y SHA-256 de cada archivo extraído, se inició el juego y se consultó LocalDB.
- Requiere Windows x64 y .NET 10 Desktop Runtime. Para las partidas guardadas requiere LocalDB con el esquema.

## Uso de inteligencia artificial y ActivityWatch

Durante la creación y el desarrollo de NOCTRYX: SYSTEM COLLAPSE se utilizaron ChatGPT y Codex como apoyo para la planificación, el diseño de interfaces, la programación, la depuración, la integración con SQL Server y la preparación de la entrega final H4. Estas herramientas asistieron en la generación de propuestas, la implementación de funcionalidades, la identificación de errores y las comprobaciones técnicas.

El estudiante participó en la definición de requisitos, la toma de decisiones y las pruebas manuales, incluida la comprobación de los mapas y de la persistencia de partidas.

ActivityWatch no se utilizó como herramienta de seguimiento verificable durante el desarrollo. No existe un registro verificable de esa actividad y no deben inventarse registros, capturas ni tiempos de actividad. Las evidencias técnicas y las pruebas documentadas se presentan por separado.\n\n## Pendientes

- Contrastar la entrega con la rúbrica externa, si aplica.
- Autorización para publicar la rama de pruebas, etiqueta y Release en GitHub.
