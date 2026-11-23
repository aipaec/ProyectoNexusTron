using System;
using System.Collections.Generic;
using System.Text;

namespace NexusCore
{
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

        public bool SincronizacionTotal =>
            CoordenadaAnomalia &&
            DiscoMadreObtenido &&
            EncomIOPurificado &&
            ExtraccionCompletada;
    }
}
