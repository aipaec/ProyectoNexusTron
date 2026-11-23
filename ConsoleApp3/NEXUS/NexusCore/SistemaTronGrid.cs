using System;
using System.Collections.Generic;
using System.Text;

namespace NexusCore
{
    public class SistemaTronGrid
    {
        private readonly Random _rand = new Random();

        public UsuarioGrid UsuarioActual { get; private set; }
        public DirectivaDirectorio Directiva { get; private set; }

        public int GridX { get; private set; }
        public int GridY { get; private set; }

        public EntidadRed EntidadDetectada { get; private set; }

        public const int MaxSector = 3;

        public bool JuegoTerminado =>
            UsuarioActual != null &&
            (!UsuarioActual.AlgoritmoActivo ||
             Directiva.SincronizacionTotal);

        public SistemaTronGrid()
        {
            GridX = 0;
            GridY = 0;
        }

        public void InicializarRed(string idCadete, string sectorTarget)
        {
            if (string.IsNullOrWhiteSpace(idCadete))
                throw new ArgumentException("El identificador es obligatorio.");

            if (string.IsNullOrWhiteSpace(sectorTarget))
                throw new ArgumentException("El sector de inserción es obligatorio.");

            UsuarioActual = new UsuarioGrid(idCadete.Trim());

            Directiva = new DirectivaDirectorio(
                "SECTOR ISOMÓRFICO " + sectorTarget.Trim().ToUpper());

            GridX = 0;
            GridY = 0;
            EntidadDetectada = null;
        }

        public string Desplazar(int direccion)
        {
            if (UsuarioActual == null)
                return "ERROR: La red no ha sido inicializada.";

            if (JuegoTerminado)
                return "La simulación ha terminado.";

            int targetX = GridX;
            int targetY = GridY;

            switch (direccion)
            {
                case 1: targetY++; break;
                case 2: targetY--; break;
                case 3: targetX++; break;
                case 4: targetX--; break;

                default:
                    return "Dirección no válida.";
            }

            if (Math.Abs(targetX) > MaxSector ||
                Math.Abs(targetY) > MaxSector)
            {
                return "LÍMITE DEL GRID: No puedes avanzar más.";
            }

            GridX = targetX;
            GridY = targetY;

            UsuarioActual.ModificarEnergia(-4);

            return EscanearAreaSector();
        }

        private string EscanearAreaSector()
        {
            EntidadDetectada = null;

            if (GridX == 2 && GridY == 2 &&
                !Directiva.CoordenadaAnomalia)
            {
                Directiva.CoordenadaAnomalia = true;

                if (!Directiva.DiscoMadreObtenido)
                {
                    UsuarioActual.Memoria.AsignarNodo(
                        new NodoMemoria(
                            "Disco Madre ISO",
                            TipoNodo.DiscoProteccion,
                            100));

                    Directiva.DiscoMadreObtenido = true;
                }

                return "ANOMALÍA ISOMÓRFICA LOCALIZADA. " +
                       "Se ha recuperado el Disco Madre.";
            }

            int probabilidad = _rand.Next(1, 101);

            if (probabilidad <= 30)
            {
                NodoMemoria nodo = GenerarNodoAleatorio();

                bool agregado = UsuarioActual.Memoria.AsignarNodo(nodo);

                return agregado
                    ? "NODO DETECTADO: " + nodo.Identificador
                    : "NODO DETECTADO, pero la memoria está llena.";
            }

            if (probabilidad <= 60)
            {
                EntidadDetectada = probabilidad % 2 == 0
                    ? new SondaCentinela("Sonda de Reconocimiento")
                    : new RectificadorCLU("Rectificador CLU v4.0", 18);

                return "ENTIDAD DETECTADA: " +
                       EntidadDetectada.IdentificadorUsuario;
            }

            if (probabilidad <= 80)
            {
                int perdida = _rand.Next(10, 20);
                UsuarioActual.ModificarEnergia(-perdida);

                return "DESCARGA DE VOLTAJE: -" +
                       perdida + "% de energía.";
            }

            return "SECTOR VACÍO: No se detectaron anomalías.";
        }

        private NodoMemoria GenerarNodoAleatorio()
        {
            int r = _rand.Next(1, 5);

            return r switch
            {
                1 => new NodoMemoria(
                    "Célula Bit Energética", TipoNodo.Energia, 30),

                2 => new NodoMemoria(
                    "Sub-rutina Escudo", TipoNodo.DiscoProteccion, 25),

                3 => new NodoMemoria(
                    "Script de Combate I/O", TipoNodo.CodigoCombate, 20),

                _ => new NodoMemoria(
                    "Analizador Depurador", TipoNodo.Depurador, 0)
            };
        }


        public string AtacarEntidad()
        {
            if (EntidadDetectada == null ||
                !EntidadDetectada.AlgoritmoActivo)
            {
                return "No hay ninguna entidad enemiga activa.";
            }

            int impacto = 20;
            EntidadDetectada.RecibirEfectoDerez(impacto);

            string resultado =
                "DISCO LANZADO: -" + impacto +
                " de integridad a " +
                EntidadDetectada.IdentificadorUsuario + ".";

            if (!EntidadDetectada.AlgoritmoActivo)
            {
                string enemigoDerrotado =
                    EntidadDetectada.IdentificadorUsuario;

                resultado += "\nENTIDAD DESINTEGRADA. +60 ciclos CPU.";

                UsuarioActual.AcumularCiclos(60);

                if (EntidadDetectada is RectificadorCLU)
                {
                    Directiva.EncomIOPurificado = true;
                    resultado += "\nDIRECTIVA CLU COMPLETADA.";
                }
                else
                {
                    resultado += "\nLa directiva CLU continúa pendiente.";
                }

                EntidadDetectada = null;

                return resultado;
            }

            if (EntidadDetectada is RectificadorCLU rectificador)
            {
                UsuarioActual.RecibirEfectoDerez(
                    rectificador.FuerzaDerez);

                resultado += "\nCONTRAATAQUE CLU: -" +
                             rectificador.FuerzaDerez +
                             " de integridad, después de la protección.";
            }

            return resultado;
        }

        public string IntentarEscapar()
        {
            if (EntidadDetectada == null)
                return "No hay ninguna entidad de la que escapar.";

            if (_rand.Next(1, 101) <= 55)
            {
                string nombre = EntidadDetectada.IdentificadorUsuario;
                EntidadDetectada = null;

                return "ESCAPE LOGRADO. Te has alejado de " + nombre + ".";
            }

            if (EntidadDetectada is RectificadorCLU rectificador)
            {
                UsuarioActual.RecibirEfectoDerez(
                    rectificador.FuerzaDerez);

                return "ESCAPE INTERCEPTADO. El CLU contraataca.";
            }

            return "ESCAPE INTERCEPTADO. Inténtalo nuevamente.";
        }

        public string UsarNodoMemoria(int indice)
        {
            NodoMemoria nodo = UsuarioActual.Memoria.DescargarNodo(indice);

            if (nodo == null)
                return "No existe un nodo en esa posición.";

            switch (nodo.Tipo)
            {
                case TipoNodo.Energia:
                    UsuarioActual.ModificarEnergia(
                        nodo.CapacidadProcesamiento);

                    return "ENERGÍA RECUPERADA: +" +
                           nodo.CapacidadProcesamiento + "%.";

                case TipoNodo.DiscoProteccion:
                    UsuarioActual.ModificarDisco(
                        nodo.CapacidadProcesamiento);

                    return "DISCO DE PROTECCIÓN RECARGADO: +" +
                           nodo.CapacidadProcesamiento + "%.";

                case TipoNodo.CodigoCombate:
                    return "SCRIPT DE COMBATE PREPARADO.";

                case TipoNodo.Depurador:
                    return "DEPURADOR ACTIVADO EN LA MEMORIA.";

                default:
                    return "TIPO DE NODO DESCONOCIDO.";
            }
        }

        public string ProcesarSalidaPortal()
        {
            if (GridX != 0 || GridY != 0)
                return "PORTAL NO LOCALIZADO. Debes regresar a (0,0).";

            if (!Directiva.CoordenadaAnomalia ||
                !Directiva.DiscoMadreObtenido ||
                !Directiva.EncomIOPurificado)
            {
                return "PORTAL BLOQUEADO: faltan directivas primarias.";
            }

            Directiva.ExtraccionCompletada = true;

            return "PORTAL I/O ALCANZADO. MISIÓN COMPLETADA.";
        }


    }


}
