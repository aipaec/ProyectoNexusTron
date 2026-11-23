using System;
using System.Collections.Generic;
using System.Text;

namespace NexusCore
{
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
            IntegridadCiclo = System.Math.Clamp(
                IntegridadCiclo - dano,
                0,
                IntegridadMaxima
            );
        }
    }
}
