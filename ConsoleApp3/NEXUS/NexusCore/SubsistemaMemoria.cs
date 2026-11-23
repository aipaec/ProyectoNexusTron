using System;
using System.Collections.Generic;
using System.Text;

namespace NexusCore
{
    public class SubsistemaMemoria
    {
        private readonly List<NodoMemoria> _bloques = new List<NodoMemoria>();
        private const int CapacidadBloques = 8;

        public bool AsignarNodo(NodoMemoria nodo)
        {
            if (nodo == null || _bloques.Count >= CapacidadBloques)
                return false;

            _bloques.Add(nodo);
            return true;
        }

        public NodoMemoria DescargarNodo(int indice)
        {
            if (indice < 0 || indice >= _bloques.Count)
                return null;

            NodoMemoria nodo = _bloques[indice];
            _bloques.RemoveAt(indice);
            return nodo;
        }

        public bool TieneDepurador()
        {
            return _bloques.Exists(x => x.Tipo == TipoNodo.Depurador);
        }

        public int NodosCargados => _bloques.Count;

        public IReadOnlyList<NodoMemoria> ObtenerNodos()
        {
            return _bloques.AsReadOnly();
        }
    }
}
