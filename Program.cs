using System;
using System.Collections.Generic;
using System.Threading;

namespace NexusTronLegacy
{
    #region --- CONSTANTES Y ENUMS ---

    public enum TipoNodo { Energia, DiscoProteccion, CodigoCombate, Depurador }

    public static class VisualesTron
    {
        public static void ImprimirEncabezado(string titulo)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("┌──────────────────────────────────────────────────────────────────┐");
            Console.WriteLine($"│ {titulo.PadRight(64)} │");
            Console.WriteLine("└──────────────────────────────────────────────────────────────────┘");
            Console.ResetColor();
        }

        public static void GlitchTexto(string texto)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.Write(" [SYSTEM_CORRUPTION]: ");
            foreach (char c in texto)
            {
                Console.Write(c);
                Thread.Sleep(8);
            }
            Console.WriteLine();
            Console.ResetColor();
        }
    }

    #endregion

    #region --- MEMORIA DE SUBSISTEMAS (INVENTARIO Y NODOS) ---

    public class NodoMemoria
    {
        public string Identificador { get; }
        public TipoNodo Tipo { get; }
        public int CapacidadProcesamiento { get; }

        public NodoMemoria(string identificador, TipoNodo tipo, int capacidad)
        {
            Identificador = identificador;
            Tipo = tipo;
            CapacidadProcesamiento = capacidad;
        }
    }

    public class SubsistemaMemoria
    {
        private readonly List<NodoMemoria> _bloques = new List<NodoMemoria>();
        private const int CapacidadBloques = 8;

        public bool AsignarNodo(NodoMemoria nodo)
        {
            if (_bloques.Count >= CapacidadBloques)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[MEMORIA AGOTADA]: Superada la capacidad máxima de la matriz.");
                Console.ResetColor();
                return false;
            }
            _bloques.Add(nodo);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[ASIGNACIÓN EXITOSA]: Sector cargado << {nodo.Identificador} >>");
            Console.ResetColor();
            return true;
        }

        public void RenderizarMemoria()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("\n=== MATRIZ DE MEMORIA I/O ===");
            if (_bloques.Count == 0)
            {
                Console.WriteLine("[REGISTROS VACÍOS]");
                Console.ResetColor();
                return;
            }

            for (int i = 0; i < _bloques.Count; i++)
            {
                Console.WriteLine($" ├─ [{i + 1}] {_bloques[i].Identificador.PadRight(22)} | CLASE: {_bloques[i].Tipo,-15} | RENDIMIENTO: +{_bloques[i].CapacidadProcesamiento}");
            }
            Console.ResetColor();
        }

        public NodoMemoria DescargarNodo(int indice)
        {
            if (indice < 0 || indice >= _bloques.Count) return null;
            NodoMemoria nodo = _bloques[indice];
            _bloques.RemoveAt(indice);
            return nodo;
        }

        public bool TieneDepurador() => _bloques.Exists(x => x.Tipo == TipoNodo.Depurador);
        public int NodosCargados => _bloques.Count;
    }

    #endregion

    #region --- ALGORITMOS Y ENTIDADES ---

    public abstract class EntidadRed
    {
        public string IdentificadorUsuario { get; protected set; }
        public int IntegridadCiclo { get; protected set; }
        public int IntegridadMaxima { get; protected set; }

        public EntidadRed(string id, int maxCiclos)
        {
            IdentificadorUsuario = id;
            IntegridadMaxima = maxCiclos;
            IntegridadCiclo = maxCiclos;
        }

        public bool AlgoritmoActivo => IntegridadCiclo > 0;

        public virtual void RecibirEfectoDerez(int dano)
        {
            IntegridadCiclo = Math.Clamp(IntegridadCiclo - dano, 0, IntegridadMaxima);
        }

        public abstract void EjecutarInteraccion(UsuarioGrid usuario, ref bool estadoCombate);
    }

    public class UsuarioGrid : EntidadRed
    {
        public int NivelCargaEnergia { get; private set; }
        public int DiscoProteccion { get; private set; }
        public int CiclosCPU { get; private set; }
        public SubsistemaMemoria Memoria { get; }

        public UsuarioGrid(string usuarioId) : base(usuarioId, 100)
        {
            NivelCargaEnergia = 100;
            DiscoProteccion = 80;
            CiclosCPU = 0;
            Memoria = new SubsistemaMemoria();
        }

        public void ModificarEnergia(int delta) => NivelCargaEnergia = Math.Clamp(NivelCargaEnergia + delta, 0, 100);
        public void ModificarDisco(int delta) => DiscoProteccion = Math.Clamp(DiscoProteccion + delta, 0, 100);

        public override void RecibirEfectoDerez(int cantidad)
        {
            if (DiscoProteccion > 0)
            {
                int absorbido = Math.Min(DiscoProteccion, cantidad);
                DiscoProteccion -= absorbido;
                cantidad -= absorbido;
            }

            if (cantidad > 0)
            {
                base.RecibirEfectoDerez(cantidad);
            }
        }

        public void AcumularCiclos(int xp)
        {
            CiclosCPU += xp;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"\n[PROCESAMIENTO]: +{xp} Ciclos de Reloj sincronizados. (Total: {CiclosCPU} Hz)");
            Console.ResetColor();
        }

        public override void EjecutarInteraccion(UsuarioGrid usuario, ref bool estadoCombate) { }
    }

    public class SondaCentinela : EntidadRed
    {
        public SondaCentinela(string id) : base(id, 70) { }

        public override void EjecutarInteraccion(UsuarioGrid usuario, ref bool estadoCombate)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[ALERTA DE SEGURIDAD]: {IdentificadorUsuario} ha proyectado un firewall pasivo.");
            Console.ResetColor();
        }
    }

    public class RectificadorCLU : EntidadRed
    {
        public int FuerzaDerez { get; }

        public RectificadorCLU(string id, int fuerza) : base(id, 90)
        {
            FuerzaDerez = fuerza;
        }

        public override void EjecutarInteraccion(UsuarioGrid usuario, ref bool estadoCombate)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[INTRUSIÓN MASCULINA]: {IdentificadorUsuario} (Sujeto de Rectificación) ha bloqueado la salida. Ejecutando protocolo de eliminación.");
            Console.ResetColor();
            estadoCombate = true;
        }
    }

    #endregion

    #region --- NÚCLEO TRON Y DIRECTIVAS ---

    public class DirectivaDirectorio
    {
        public string NombreVector { get; }
        public bool CoordenadaAnomalia { get; set; }
        public bool DiscoMadreObtenido { get; set; }
        public bool EncomIOPurificado { get; set; }
        public bool ExtraccionCompletada { get; set; }

        public DirectivaDirectorio(string nombre)
        {
            NombreVector = nombre;
        }

        public bool SincronizacionTotal => CoordenadaAnomalia && DiscoMadreObtenido && EncomIOPurificado && ExtraccionCompletada;

        public void MostrarEstadoVector()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"\n=== VECTOR DE INSTRUCCIONES: {NombreVector} ===");
            Console.WriteLine($" [{(CoordenadaAnomalia ? "OK" : "  ")}] Localizar la anomalía de la Red.");
            Console.WriteLine($" [{(DiscoMadreObtenido ? "OK" : "  ")}] Recuperar el Disco de Código Fuente.");
            Console.WriteLine($" [{(EncomIOPurificado ? "OK" : "  ")}] Sobrevivir a la neutralización del Rectificador.");
            Console.WriteLine($" [{(ExtraccionCompletada ? "OK" : "  ")}] Alcanzar el Portal I/O de salida.");
            Console.ResetColor();
        }
    }

    public class SistemaTronGrid
    {
        private UsuarioGrid _usuarioActual;
        private DirectivaDirectorio _directiva;
        private readonly Random _rand = new Random();
        private int _gridX = 0;
        private int _gridY = 0;
        private const int MaxSector = 3;

        public void InicializarRed()
        {
            Console.Clear();
            VisualesTron.ImprimirEncabezado(" ENCOM OS // RED DE SIMULACIÓN MULTIDIMENSIONAL v2.97");

            string idCadete = LeerComandoValido("\n INGRESE IDENTIFICADOR DE USUARIO (ID): ");
            _usuarioActual = new UsuarioGrid(idCadete);

            string sectorTarget = LeerComandoValido(" SECTOR DE INSERCIÓN EN LA RED: ");
            _directiva = new DirectivaDirectorio("SECTOR ISOMÓRFICO " + sectorTarget.ToUpper());

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[INSERCIÓN]: Usuario {idCadete} digitalizado en el Grid.");
            Console.ResetColor();
            EsperarCiclo();

            BucleDeCiclos();
        }

        private void BucleDeCiclos()
        {
            bool redConectada = true;

            while (redConectada && _usuarioActual.AlgoritmoActivo && !_directiva.SincronizacionTotal)
            {
                RenderizarInterfazGrid();
                Console.WriteLine("\n [DIRECTIVAS DISPONIBLES]");
                Console.WriteLine(" 1. Desplazar Moto de Luz (Cambiar Sector)");
                Console.WriteLine(" 2. Inspeccionar Subsitios de Memoria");
                Console.WriteLine(" 3. Mapear Directivas del Sistema");
                Console.WriteLine(" 4. Ejecutar Salida por Portal I/O (Sector 0,0)");
                Console.WriteLine(" 5. Desconectar de la Red");

                int comando = LeerOpcionEntrada("SELECCIONE DIRECTIVA: ", 1, 5);

                switch (comando)
                {
                    case 1: DesplazarVectorSistemas(); break;
                    case 2: GestionarMemoriaGrid(); break;
                    case 3: _directiva.MostrarEstadoVector(); break;
                    case 4: ProcesarSalidaPortal(); break;
                    case 5: redConectada = false; break;
                }

                if (_usuarioActual.AlgoritmoActivo) EsperarCiclo();
            }

            ProcesarDerezFinal();
        }

        private void RenderizarInterfazGrid()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"USUARIO: {_usuarioActual.IdentificadorUsuario,-12} | SECTOR GRID: [{_gridX},{_gridY}] | RENDIMIENTO: {_usuarioActual.CiclosCPU} Hz");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("──────────────────────────────────────────────────────────────────");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(" INTEGRIDAD CICLO: ");
            DibujarBarraProgreso(_usuarioActual.IntegridadCiclo, _usuarioActual.IntegridadMaxima, ConsoleColor.Cyan);

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write(" DISCO PROTECCIÓN: ");
            DibujarBarraProgreso(_usuarioActual.DiscoProteccion, 100, ConsoleColor.DarkYellow);

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(" ENERGÍA DE RED:   ");
            DibujarBarraProgreso(_usuarioActual.NivelCargaEnergia, 100, ConsoleColor.Magenta);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("──────────────────────────────────────────────────────────────────");
            Console.ResetColor();
        }

        private void DibujarBarraProgreso(int actual, int max, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            int bloques = (actual * 20) / max;
            Console.Write("[");
            Console.Write(new string('█', bloques));
            Console.Write(new string('░', 20 - bloques));
            Console.WriteLine($"] {actual}%");
            Console.ResetColor();
        }

        private void DesplazarVectorSistemas()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n [VECTOR DE NAVEGACIÓN]");
            Console.WriteLine(" 1. Norte (+Y) | 2. Sur (-Y) | 3. Este (+X) | 4. Oeste (-X)");
            Console.ResetColor();

            int dir = LeerOpcionEntrada(">> SELECCIONE DIRECCIÓN: ", 1, 4);

            int targetX = _gridX;
            int targetY = _gridY;

            if (dir == 1) targetY++;
            if (dir == 2) targetY--;
            if (dir == 3) targetX++;
            if (dir == 4) targetX--;

            if (Math.Abs(targetX) > MaxSector || Math.Abs(targetY) > MaxSector)
            {
                VisualesTron.GlitchTexto("LÍMITE DE LA RED DETECTADO. MATRIZ NO CONFIGURADA.");
                return;
            }

            _gridX = targetX;
            _gridY = targetY;
            _usuarioActual.ModificarEnergia(-4);

            Console.WriteLine($"\n[MOTO DE LUZ]: Desplazando a Sector ({_gridX},{_gridY})...");
            Thread.Sleep(300);

            EscanearAreaSector();
        }

        private void EscanearAreaSector()
        {
            if (_gridX == 2 && _gridY == 2 && !_directiva.CoordenadaAnomalia)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("\n⚡ [ANOMALÍA ISOMÓRFICA]: Punto de origen ISO localizado.");
                _directiva.CoordenadaAnomalia = true;

                if (!_directiva.DiscoMadreObtenido)
                {
                    Console.WriteLine(" [CÓDIGO DE ORIGEN]: Extrayendo Disco Madre del Sistema.");
                    _usuarioActual.Memoria.AsignarNodo(new NodoMemoria("Disco Madre ISO", TipoNodo.DiscoProteccion, 100));
                    _directiva.DiscoMadreObtenido = true;
                }
                Console.ResetColor();
                return;
            }

            int valorProbabilidad = _rand.Next(1, 101);

            if (valorProbabilidad <= 30)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n [NODO DETECTADO]: Bloque de código encontrado en la sub-red.");
                Console.ResetColor();
                _usuarioActual.Memoria.AsignarNodo(GenerarNodoAleatorio());
            }
            else if (valorProbabilidad <= 60)
            {
                EntidadRed programa = (valorProbabilidad % 2 == 0)
                    ? (EntidadRed)new SondaCentinela("Sonda de Reconocimiento")
                    : new RectificadorCLU("Rectificador CLU v4.0", 18);

                EjecutarEnfrentamiento(programa);
            }
            else if (valorProbabilidad <= 80)
            {
                VisualesTron.GlitchTexto("DESCARGA DE VOLTAJE EN LA MATRIZ DE ENERGÍA.");
                int perdida = _rand.Next(10, 20);
                _usuarioActual.ModificarEnergia(-perdida);
                Console.WriteLine($" [PÉRDIDA DE CICLOS]: -{perdida}% de Energía.");
            }
            else
            {
                Console.WriteLine("\n[SECTOR VACÍO]: Sin programas o intrusiones detectadas.");
            }
        }

        private NodoMemoria GenerarNodoAleatorio()
        {
            int r = _rand.Next(1, 5);
            return r switch
            {
                1 => new NodoMemoria("Célula Bit Energética", TipoNodo.Energia, 30),
                2 => new NodoMemoria("Sub-rutina Escudo", TipoNodo.DiscoProteccion, 25),
                3 => new NodoMemoria("Script de Combate I/O", TipoNodo.CodigoCombate, 20),
                _ => new NodoMemoria("Analizador Depurador", TipoNodo.Depurador, 0)
            };
        }

        private void EjecutarEnfrentamiento(EntidadRed programa)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n 👾 [ENTIDAD DETECTADA]: {programa.IdentificadorUsuario}");
            Console.ResetColor();

            bool combateActivo = false;
            programa.EjecutarInteraccion(_usuarioActual, ref combateActivo);

            bool interactuando = true;

            while (interactuando && programa.AlgoritmoActivo && _usuarioActual.AlgoritmoActivo)
            {
                Console.WriteLine("\n Directiva de Respuesta:");
                Console.WriteLine(" 1. Lanzar Disco de Protección");
                Console.WriteLine(" 2. Ejecutar Maniobra de Escape");
                Console.WriteLine(" 3. Cargar Sub-rutina de Memoria");
                Console.WriteLine(" 4. Depurar Algoritmo Enemigo");

                int eleccion = LeerOpcionEntrada(">> ", 1, 4);

                switch (eleccion)
                {
                    case 1:
                        int impacto = 20;
                        programa.RecibirEfectoDerez(impacto);
                        Console.WriteLine($"\n[LANZAMIENTO]: Disco impacta en {programa.IdentificadorUsuario} dealocando {impacto} ciclos de vida.");

                        if (!programa.AlgoritmoActivo)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"\n[DEREZ]: {programa.IdentificadorUsuario} ha sido desintegrado de la Red.");
                            Console.ResetColor();
                            _usuarioActual.AcumularCiclos(60);
                            _directiva.EncomIOPurificado = true;
                        }
                        break;

                    case 2:
                        if (_rand.Next(1, 100) <= 55)
                        {
                            Console.WriteLine("\n[DESCONEXIÓN LOGRADA]: Escapas por la línea de datos.");
                            interactuando = false;
                        }
                        else
                        {
                            VisualesTron.GlitchTexto("ESCAPE INTERCEPTADO POR EL FIREWALL ENEMIGO.");
                        }
                        break;

                    case 3:
                        GestionarMemoriaGrid();
                        break;

                    case 4:
                        if (_usuarioActual.Memoria.TieneDepurador())
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"\n🔬 [DEPURACIÓN]: {programa.IdentificadorUsuario} Analizado. Puntos débiles expuestos.");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.WriteLine("\n[ANALIZADOR AUSENTE]: Requiere nodo Depurador en memoria.");
                        }
                        break;
                }

                if (programa.AlgoritmoActivo && combateActivo && interactuando)
                {
                    if (programa is RectificadorCLU r)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\n🚨 {r.IdentificadorUsuario} ataca con Disco de Derez.");
                        _usuarioActual.RecibirEfectoDerez(r.FuerzaDerez);
                        Console.WriteLine($" Recibes {r.FuerzaDerez} de daño en tus ciclos.");
                        Console.ResetColor();
                    }
                }
            }
        }

        private void GestionarMemoriaGrid()
        {
            _usuarioActual.Memoria.RenderizarMemoria();
            if (_usuarioActual.Memoria.NodosCargados == 0) return;

            Console.WriteLine("\n Acciones: [1] Cargar/Ejecutar Nodo | [2] Cancelar");
            int op = LeerOpcionEntrada(">> ", 1, 2);

            if (op == 1)
            {
                int index = LeerOpcionEntrada("Seleccione índice de nodo: ", 1, _usuarioActual.Memoria.NodosCargados) - 1;
                NodoMemoria nodo = _usuarioActual.Memoria.DescargarNodo(index);

                if (nodo == null)
                {
                    Console.WriteLine("\n[ERROR]: Nodo no válido.");
                    return;
                }

                EjecutarNodoMemoria(nodo);
            }
        }

        private void EjecutarNodoMemoria(NodoMemoria nodo)
        {
            switch (nodo.Tipo)
            {
                case TipoNodo.Energia:
                    _usuarioActual.ModificarEnergia(nodo.CapacidadProcesamiento);
                    Console.WriteLine($"\n🔋 Sub-rutina {nodo.Identificador} recarga energía.");
                    break;

                case TipoNodo.DiscoProteccion:
                    _usuarioActual.ModificarDisco(nodo.CapacidadProcesamiento);
                    Console.WriteLine($"\n🛡️ Matriz del Disco recalibrada (+{nodo.CapacidadProcesamiento}%).");
                    break;

                case TipoNodo.CodigoCombate:
                    Console.WriteLine($"\n🔫 Script {nodo.Identificador} listo para el próximo ciclo.");
                    break;

                case TipoNodo.Depurador:
                    Console.WriteLine($"\n🔬 Depurador asignado a la consola principal.");
                    break;
            }
        }

        private void ProcesarSalidaPortal()
        {
            if (_gridX == 0 && _gridY == 0)
            {
                if (_directiva.CoordenadaAnomalia && _directiva.DiscoMadreObtenido && _directiva.EncomIOPurificado)
                {
                    _directiva.ExtraccionCompletada = true;
                }
                else
                {
                    Console.WriteLine("\n[PORTAL BLOQUEADO]: Falta la compilación de directivas primarias.");
                }
            }
            else
            {
                Console.WriteLine("\n[ERROR DE UBICACIÓN]: El Portal I/O está localizado en las coordenadas (0,0).");
            }
        }

        private void ProcesarDerezFinal()
        {
            Console.Clear();
            if (!_usuarioActual.AlgoritmoActivo)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                    🚨 D E R E Z E D 🚨                          ║");
                Console.WriteLine("║         EL PROGRAMA HA PERDIDO LA INTEGRIDAD EN EL GRID          ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
            }
            else if (_directiva.SincronizacionTotal)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║            🟢 PORTAL I/O ALCANZADO: MISIÓN COMPLETADA 🟢        ║");
                Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
                Console.WriteLine("║ +100 Hz Ciclos acumulados                                        ║");
                Console.WriteLine("║ +1 Disco de Identidad Isomórfico [Extraído]                       ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                _usuarioActual.AcumularCiclos(100);
            }
            else
            {
                Console.WriteLine("\n[DESCONEXIÓN]: Salida manual de la simulación del Grid.");
            }
        }

        #region --- CONTROL DE ERRORES Y VALIDACIONES DE ENTRADA ---

        private string LeerComandoValido(string prompt)
        {
            string entrada;
            do
            {
                Console.Write(prompt);
                entrada = Console.ReadLine()?.Trim();
            } while (string.IsNullOrEmpty(entrada));
            return entrada;
        }

        private int LeerOpcionEntrada(string prompt, int min, int max)
        {
            int opcion;
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine();

                if (int.TryParse(input, out opcion) && opcion >= min && opcion <= max)
                {
                    return opcion;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ENTRADA DEREZ]: Por favor ingrese un comando válido ({min}-{max}).");
                Console.ResetColor();
            }
        }

        private void EsperarCiclo()
        {
            Console.WriteLine("\nPresione cualquier tecla para continuar el ciclo de reloj...");
            Console.ReadKey(true);
        }

        #endregion
    }

    #endregion

    #region --- ENTRADA DEL SISTEMA ---

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                SistemaTronGrid gridSystem = new SistemaTronGrid();
                gridSystem.InicializarRed();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FALLO CRÍTICO DE LA RED]: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    #endregion
}