using System;
using System.Collections.Generic;
using System.Text;

namespace NexusCore
{
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

        public void ModificarEnergia(int delta)
        {
            NivelCargaEnergia = System.Math.Clamp(
                NivelCargaEnergia + delta, 0, 100);
        }

        public void ModificarDisco(int delta)
        {
            DiscoProteccion = System.Math.Clamp(
                DiscoProteccion + delta, 0, 100);
        }

        public override void RecibirEfectoDerez(int cantidad)
        {
            if (DiscoProteccion > 0)
            {
                int absorbido = System.Math.Min(DiscoProteccion, cantidad);
                DiscoProteccion -= absorbido;
                cantidad -= absorbido;
            }

            if (cantidad > 0)
                base.RecibirEfectoDerez(cantidad);
        }

        public void AcumularCiclos(int xp)
        {
            CiclosCPU += xp;
        }
    }
}
