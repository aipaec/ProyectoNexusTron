using System.Diagnostics;
using System.Drawing;
using NexusGUI.Audio;
using NexusGUI.Database;

namespace NexusGUI.Gameplay
{
    public sealed class GamePrototypeForm : Form
    {
        private readonly GameSurface superficie;
        private readonly AudioManager audio = new();
        private readonly System.Windows.Forms.Timer temporizadorFade = new() { Interval = 40 };
        private readonly Stopwatch relojFade = new();
        private EstadoFadeMusica estadoFade;
        private string? pistaActualId;
        private string? pistaPendienteId;
        private string? pistaPendienteRuta;
        private float volumenSalidaFade;
        private float volumenPendiente;
        private bool recursosLiberados;

        private const float VolumenMenu = 0.32f;
        private const float VolumenMapa = 0.38f;
        private const double DuracionEntradaFade = 1.8;
        private const double DuracionSalidaFade = 0.55;
        private const string ArchivoMenu = "Tron.mp4";
        private const string ArchivoMapa1 = "Daft Punk – Legacy Open (The Grid Instrumental ver.).mp4";
        private const string ArchivoMapa2 = "Hans Zimmer - Mountains (Interstellar Soundtrack).mp4";

        private enum EstadoFadeMusica
        {
            Ninguno,
            Entrada,
            Salida
        }

        public GamePrototypeForm()
        {
            Text = "NOCTRYX: SYSTEM COLLAPSE — Prototipo 2D";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(800, 500);

            superficie = new GameSurface
            {
                Dock = DockStyle.Fill
            };

            superficie.MusicaMenuSolicitada += CambiarMusicaMenu;
            superficie.MusicaMapaSolicitada += CambiarMusicaMapa;
            superficie.BaseDatosSolicitada += AbrirGestorPartidas;
            superficie.GestionarPartidasSolicitada += AbrirGestorPartidas;
            audio.Diagnostico += mensaje => Debug.WriteLine(mensaje);
            temporizadorFade.Tick += (_, _) => ActualizarFade();

            Controls.Add(superficie);
            Shown += (_, _) =>
            {
                superficie.Focus();
                CambiarMusicaMenu(true);
            };
            FormClosing += (_, _) => DetenerMusicaActual();
        }

        private void AbrirGestorPartidas()
        {
            try
            {
                using var formulario = new SaveRecordsForm(superficie.CapturarProgreso, RestaurarProgreso);
                formulario.ShowDialog(this);
                superficie.Focus();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message, "Guardar partida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string? RestaurarProgreso(GameProgressSnapshot snapshot) =>
            superficie.RestaurarProgreso(snapshot, out string error) ? null : error;

        private void CambiarMusicaMenu(bool reproducir)
        {
            if (reproducir)
                SolicitarPista("menu", ArchivoMenu, VolumenMenu);
            else
                DetenerMusicaActual();
        }

        private void CambiarMusicaMapa(int numeroMapa)
        {
            if (numeroMapa == 1)
                SolicitarPista("mapa1", ArchivoMapa1, VolumenMapa);
            else if (numeroMapa == 2)
                SolicitarPista("mapa2", ArchivoMapa2, VolumenMapa);
        }

        private void SolicitarPista(string id, string archivo, float volumenObjetivo)
        {
            if (recursosLiberados || IsDisposed)
                return;

            if (string.Equals(pistaActualId, id, StringComparison.Ordinal) &&
                estadoFade != EstadoFadeMusica.Salida)
                return;

            pistaPendienteId = id;
            pistaPendienteRuta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", archivo);
            volumenPendiente = volumenObjetivo;

            if (pistaActualId is null)
            {
                IniciarPistaPendiente();
                return;
            }

            volumenSalidaFade = audio.VolumenMusica;
            estadoFade = EstadoFadeMusica.Salida;
            relojFade.Restart();
            temporizadorFade.Start();
        }

        private void IniciarPistaPendiente()
        {
            if (pistaPendienteId is null || pistaPendienteRuta is null)
            {
                estadoFade = EstadoFadeMusica.Ninguno;
                temporizadorFade.Stop();
                relojFade.Stop();
                return;
            }

            if (!File.Exists(pistaPendienteRuta))
            {
                Debug.WriteLine($"No se encontró la música local: {pistaPendienteRuta}");
                pistaPendienteId = null;
                pistaPendienteRuta = null;
                pistaActualId = null;
                estadoFade = EstadoFadeMusica.Ninguno;
                temporizadorFade.Stop();
                relojFade.Stop();
                return;
            }

            audio.ReproducirMusicaEnBucle(pistaPendienteRuta, volumen: 0f);
            pistaActualId = pistaPendienteId;
            pistaPendienteId = null;
            pistaPendienteRuta = null;
            volumenSalidaFade = volumenPendiente;
            estadoFade = EstadoFadeMusica.Entrada;
            relojFade.Restart();
            temporizadorFade.Start();
        }

        private void ActualizarFade()
        {
            double duracion = estadoFade == EstadoFadeMusica.Salida
                ? DuracionSalidaFade
                : DuracionEntradaFade;
            float progreso = (float)Math.Clamp(relojFade.Elapsed.TotalSeconds / duracion, 0d, 1d);
            float suave = progreso * progreso * (3f - 2f * progreso);

            if (estadoFade == EstadoFadeMusica.Salida)
            {
                audio.VolumenMusica = volumenSalidaFade * (1f - suave);
                if (progreso >= 1f)
                {
                    audio.DetenerMusica();
                    pistaActualId = null;
                    IniciarPistaPendiente();
                }
            }
            else if (estadoFade == EstadoFadeMusica.Entrada)
            {
                audio.VolumenMusica = volumenSalidaFade * suave;
                if (progreso >= 1f)
                {
                    estadoFade = EstadoFadeMusica.Ninguno;
                    temporizadorFade.Stop();
                    relojFade.Stop();
                }
            }
            else
            {
                temporizadorFade.Stop();
                relojFade.Stop();
            }
        }

        private void DetenerMusicaActual()
        {
            pistaPendienteId = null;
            pistaPendienteRuta = null;
            pistaActualId = null;
            estadoFade = EstadoFadeMusica.Ninguno;
            temporizadorFade.Stop();
            relojFade.Stop();
            audio.DetenerMusica();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !recursosLiberados)
            {
                recursosLiberados = true;
                DetenerMusicaActual();
                temporizadorFade.Dispose();
                audio.Dispose();
                relojFade.Stop();
            }

            base.Dispose(disposing);
        }
    }
}
