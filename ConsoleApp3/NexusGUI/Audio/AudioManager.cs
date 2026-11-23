using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace NexusGUI.Audio
{
    public sealed class AudioManager : IDisposable
    {
        private readonly object gate = new();
        private readonly object musicGate = new();
        private readonly HashSet<SoundPlayback> efectosActivos = new();
        private SoundPlayback? musicaActual;
        private float volumenMusica = 0.5f;
        private float volumenEfectos = 1.0f;
        private bool disposed;

        public event Action<string>? Diagnostico;

        public float VolumenMusica
        {
            get { lock (gate) return volumenMusica; }
            set
            {
                SoundPlayback? playback;
                float volumen;
                lock (gate)
                {
                    if (disposed) return;
                    volumenMusica = LimitarVolumen(value);
                    volumen = volumenMusica;
                    playback = musicaActual;
                }
                playback?.AjustarVolumen(volumen);
            }
        }

        public float VolumenEfectos
        {
            get { lock (gate) return volumenEfectos; }
            set { lock (gate) { if (!disposed) volumenEfectos = LimitarVolumen(value); } }
        }

        public void ReproducirMusica(string ruta, float? volumen = null)
        {
            lock (musicGate) ReproducirMusicaInterno(ruta, volumen, repetir: false);
        }

        public void ReproducirMusicaEnBucle(string ruta, float? volumen = null)
        {
            lock (musicGate) ReproducirMusicaInterno(ruta, volumen, repetir: true);
        }

        private void ReproducirMusicaInterno(string ruta, float? volumen, bool repetir)
        {
            if (!ArchivoDisponible(ruta)) return;

            SoundPlayback? anterior;
            lock (gate)
            {
                if (disposed) return;
                anterior = musicaActual;
                musicaActual = null;
            }
            anterior?.DetenerYLiberar();

            SoundPlayback? nueva = null;
            try
            {
                float nivel;
                lock (gate)
                {
                    if (disposed) return;
                    if (volumen.HasValue) volumenMusica = LimitarVolumen(volumen.Value);
                    nivel = volumenMusica;
                }
                nueva = new SoundPlayback(ruta, nivel, repetir);
                lock (gate)
                {
                    if (disposed)
                    {
                        nueva.DetenerYLiberar();
                        return;
                    }
                    nueva.FinReproduccion += MusicaFinalizada;
                    musicaActual = nueva;
                }
                nueva.Iniciar();
            }
            catch (Exception ex)
            {
                lock (gate)
                {
                    if (nueva != null && ReferenceEquals(musicaActual, nueva))
                        musicaActual = null;
                }
                if (nueva != null)
                {
                    InformarErrorUnaVez(nueva, ex, "música");
                    nueva.DetenerYLiberar();
                }
                else
                {
                    Informar($"No se pudo preparar la música '{ruta}': {ex.Message}");
                }
            }
        }

        public void DetenerMusica()
        {
            lock (musicGate) DetenerMusicaInterno();
        }

        private void DetenerMusicaInterno()
        {
            SoundPlayback? anterior;
            lock (gate)
            {
                anterior = musicaActual;
                musicaActual = null;
            }
            anterior?.DetenerYLiberar();
        }

        public void ReproducirEfecto(string ruta, float? volumen = null)
        {
            if (!ArchivoDisponible(ruta)) return;

            SoundPlayback efecto;
            lock (gate)
            {
                if (disposed) return;
                try
                {
                    efecto = new SoundPlayback(ruta, LimitarVolumen(volumen ?? volumenEfectos), repetir: false);
                    efecto.FinReproduccion += EfectoFinalizado;
                    efectosActivos.Add(efecto);
                }
                catch (Exception ex)
                {
                    Informar($"No se pudo preparar el efecto '{ruta}': {ex.Message}");
                    return;
                }
            }

            try
            {
                efecto.Iniciar();
            }
            catch (Exception ex)
            {
                LiberarAlFinalizar(efecto, esMusica: false);
                InformarErrorUnaVez(efecto, ex, "efecto");
            }
        }

        private void EfectoFinalizado(SoundPlayback efecto, StoppedEventArgs args)
        {
            InformarErrorDeReproduccion(efecto, args, "efecto");
            LiberarAlFinalizar(efecto, esMusica: false);
        }

        private void MusicaFinalizada(SoundPlayback musica, StoppedEventArgs args)
        {
            InformarErrorDeReproduccion(musica, args, "música");
            LiberarAlFinalizar(musica, esMusica: true);
        }

        private void InformarErrorDeReproduccion(
            SoundPlayback playback,
            StoppedEventArgs args,
            string tipo)
        {
            if (args.Exception != null)
                InformarErrorUnaVez(playback, args.Exception, tipo);
        }

        private void InformarErrorUnaVez(
            SoundPlayback playback,
            Exception error,
            string tipo)
        {
            if (playback.MarcarErrorComoInformado())
                Informar($"Error al reproducir {tipo} '{playback.Ruta}': {error}");
        }

        private void LiberarAlFinalizar(SoundPlayback playback, bool esMusica)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                lock (gate)
                {
                    if (esMusica)
                    {
                        if (ReferenceEquals(musicaActual, playback)) musicaActual = null;
                    }
                    else
                    {
                        efectosActivos.Remove(playback);
                    }
                }
                playback.DetenerYLiberar();
            });
        }

        private bool ArchivoDisponible(string ruta)
        {
            if (!string.IsNullOrWhiteSpace(ruta) && File.Exists(ruta)) return true;
            Informar($"No se encontró el archivo de audio: '{ruta}'.");
            return false;
        }

        private void Informar(string mensaje)
        {
            try { Diagnostico?.Invoke(mensaje); }
            catch { /* Un problema en el receptor del diagnóstico no debe cerrar el juego. */ }
        }

        private static float LimitarVolumen(float volumen) =>
            float.IsFinite(volumen) ? Math.Clamp(volumen, 0f, 1f) : 0f;

        public void Dispose()
        {
            lock (musicGate) DisposeInterno();
        }

        private void DisposeInterno()
        {
            SoundPlayback[] activos;
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                activos = new SoundPlayback[efectosActivos.Count + (musicaActual == null ? 0 : 1)];
                efectosActivos.CopyTo(activos);
                efectosActivos.Clear();
                if (musicaActual != null)
                {
                    activos[activos.Length - 1] = musicaActual;
                    musicaActual = null;
                }
            }

            foreach (SoundPlayback playback in activos)
                playback.DetenerYLiberar();
        }

        private sealed class SoundPlayback
        {
            private readonly object lifecycle = new();
            private readonly WaveOut reproductor;
            private readonly WaveStream fuente;
            private readonly WaveChannel32 canal;
            private bool liberado;
            private int errorInformado;

            public string Ruta { get; }
            public event Action<SoundPlayback, StoppedEventArgs>? FinReproduccion;

            public SoundPlayback(string ruta, float volumen, bool repetir)
            {
                Ruta = ruta;
                WaveStream lector = string.Equals(Path.GetExtension(ruta), ".mp4",
                    StringComparison.OrdinalIgnoreCase)
                    ? new MediaFoundationReader(ruta)
                    : new AudioFileReader(ruta);
                fuente = repetir ? new LoopingWaveStream(lector) : lector;
                canal = new WaveChannel32(fuente) { Volume = volumen };
                WaveOut? nuevoReproductor = null;
                try
                {
                    nuevoReproductor = new WaveOut();
                    nuevoReproductor.PlaybackStopped += Reproductor_PlaybackStopped;
                    nuevoReproductor.Init(canal);
                    reproductor = nuevoReproductor;
                }
                catch
                {
                    if (nuevoReproductor != null)
                    {
                        nuevoReproductor.PlaybackStopped -= Reproductor_PlaybackStopped;
                        nuevoReproductor.Dispose();
                    }
                    canal.Dispose();
                    throw;
                }
            }

            public void Iniciar()
            {
                lock (lifecycle)
                {
                    if (!liberado) reproductor.Play();
                }
            }

            public void AjustarVolumen(float volumen)
            {
                lock (lifecycle)
                {
                    if (!liberado) canal.Volume = volumen;
                }
            }

            public bool MarcarErrorComoInformado() =>
                Interlocked.Exchange(ref errorInformado, 1) == 0;

            private void Reproductor_PlaybackStopped(object? sender, StoppedEventArgs e)
            {
                try { FinReproduccion?.Invoke(this, e); }
                catch { /* El callback de audio no debe propagar excepciones a WinForms. */ }
            }

            public void DetenerYLiberar()
            {
                lock (lifecycle)
                {
                    if (liberado) return;
                    liberado = true;
                    FinReproduccion = null;
                    reproductor.PlaybackStopped -= Reproductor_PlaybackStopped;
                    try { reproductor.Stop(); }
                    catch (ExternalException) { }

                    try { reproductor.Dispose(); }
                    catch (ExternalException) { }
                    finally { canal.Dispose(); }
                }
            }
        }

        private sealed class LoopingWaveStream : WaveStream
        {
            private readonly WaveStream fuente;

            public LoopingWaveStream(WaveStream fuente)
            {
                this.fuente = fuente;
            }

            public override WaveFormat WaveFormat => fuente.WaveFormat;
            public override long Length => fuente.Length;
            public override long Position
            {
                get => fuente.Position;
                set => fuente.Position = value;
            }

            public override int Read(Span<byte> buffer)
            {
                if (fuente.Length <= 0) return 0;

                int totalLeido = 0;
                while (totalLeido < buffer.Length)
                {
                    int leido = fuente.Read(buffer[totalLeido..]);
                    if (leido == 0)
                    {
                        if (fuente.Position <= 0) break;
                        fuente.Position = 0;
                        continue;
                    }

                    totalLeido += leido;
                }

                return totalLeido;
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                Read(buffer.AsSpan(offset, count));

            protected override void Dispose(bool disposing)
            {
                if (disposing) fuente.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
