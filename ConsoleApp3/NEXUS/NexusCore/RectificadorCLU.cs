using System;
using System.Collections.Generic;
using System.Text;

namespace NexusCore
{
    public class RectificadorCLU : EntidadRed
    {
        public int FuerzaDerez { get; }

        public RectificadorCLU(string id, int fuerza) : base(id, 90)
        {
            FuerzaDerez = fuerza;
        }
    }
}
