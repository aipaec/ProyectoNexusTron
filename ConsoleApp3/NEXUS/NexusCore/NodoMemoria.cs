using System;
using System.Collections.Generic;
using System.Text;

namespace NexusCore
{
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
}
