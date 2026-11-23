using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using NexusGUI.Audio;

namespace NexusGUI
{
    /// <summary>Arranque cinematográfico; al finalizar, Program abre el juego como formulario raíz.</summary>
    public sealed class IntroForm : Form
    {
        private const double Duracion = 5.5;
        private const double InicioGlitch = 1.8;
        private const double Transformacion = 3.6;
        private const double FinFlash = 3.78;
        private static readonly Color Fondo = Color.FromArgb(5, 7, 12);
        private static readonly Color Cian = Color.FromArgb(0, 232, 255);
        private static readonly Color Magenta = Color.FromArgb(245, 48, 164);

        private readonly Stopwatch reloj = new();
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
        private Font tituloFont = new("Bahnschrift", 96f, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font subtituloFont = new("Consolas", 22f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font estadoFont = new("Consolas", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font ayudaFont = new("Consolas", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly StringFormat centrado = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        private readonly SolidBrush tituloMagenta = new(Magenta);
        private readonly SolidBrush estadoCian = new(Color.FromArgb(180, Cian));
        private readonly SolidBrush estadoMagenta = new(Color.FromArgb(210, Magenta));
        private readonly SolidBrush flashBrush = new(Color.FromArgb(0, Magenta));
        private readonly Pen reticulaPen = new(Color.FromArgb(38, Cian), 1f);
        private readonly Pen perspectivaPen = new(Color.FromArgb(62, Cian), 1f);
        private readonly Pen lineaEnergiaPen = new(Color.FromArgb(90, Cian), 2f);
        private readonly Pen glitchCianPen = new(Color.FromArgb(100, Cian), 2f);
        private readonly Pen glitchMagentaPen = new(Color.FromArgb(95, Magenta), 2f);
        private Bitmap? bufferEscena;
        private Bitmap? bufferSalida;
        private Graphics? graficoEscena;
        private Graphics? graficoSalida;
        private AudioManager? audioIntro;
        private bool completada;
        private bool recursosLiberados;

        public IntroForm()
        {
            Text = "NEXUS // SYSTEM INITIALIZATION";
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(640, 360);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Fondo;
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            timer.Tick += Actualizar;
            Shown += (_, _) =>
            {
                ReproducirJingle();
                reloj.Restart();
                timer.Start();
            };
            KeyDown += (_, e) =>
            {
                if (e.KeyCode is Keys.Space or Keys.Enter)
                {
                    CompletarIntro();
                    e.Handled = true;
                }
            };
            MouseDown += (_, _) => CompletarIntro();
            Resize += (_, _) => CrearBuffers();
            CrearBuffers();
        }

        protected override bool IsInputKey(Keys keyData) =>
            (keyData & Keys.KeyCode) is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

        private void Actualizar(object? sender, EventArgs e)
        {
            if (reloj.Elapsed.TotalSeconds >= Duracion)
            {
                CompletarIntro();
                return;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (bufferEscena is null || bufferSalida is null || graficoEscena is null || graficoSalida is null)
                return;

            double tiempo = reloj.Elapsed.TotalSeconds;
            DibujarEscena(graficoEscena, tiempo);
            graficoSalida.Clear(Fondo);
            graficoSalida.DrawImageUnscaled(bufferEscena, 0, 0);
            if (tiempo >= InicioGlitch && tiempo < 4.2)
                AplicarGlitch(graficoSalida, tiempo);
            if (tiempo >= Transformacion && tiempo < FinFlash)
            {
                int alpha = (int)(70 * (1d - (tiempo - Transformacion) / (FinFlash - Transformacion)));
                flashBrush.Color = Color.FromArgb(Math.Clamp(alpha, 0, 255), Magenta);
                graficoSalida.FillRectangle(flashBrush, ClientRectangle);
            }
            e.Graphics.DrawImageUnscaled(bufferSalida, 0, 0);
        }

        private void DibujarEscena(Graphics g, double tiempo)
        {
            int ancho = ClientSize.Width;
            int alto = ClientSize.Height;
            g.Clear(Fondo);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float entrada = Suavizar((float)(tiempo / 1.2));
            reticulaPen.Color = Color.FromArgb((int)(42 * entrada), Cian);
            for (int x = 0; x < ancho; x += 64)
            {
                float deriva = (float)(Math.Sin(tiempo * 0.7 + x * 0.009) * 4);
                g.DrawLine(reticulaPen, x + deriva, 0, x + deriva, alto);
            }
            for (int y = 0; y < alto; y += 64)
                g.DrawLine(reticulaPen, 0, y, ancho, y);
            perspectivaPen.Color = Color.FromArgb((int)(64 * entrada), Cian);
            float horizonte = alto * 0.63f;
            for (int i = -8; i <= 8; i++)
                g.DrawLine(perspectivaPen, ancho / 2f, horizonte,
                    ancho / 2f + i * ancho / 7f, alto);
            float pulso = (float)(0.5 + 0.5 * Math.Sin(tiempo * 3.4));
            lineaEnergiaPen.Color = Color.FromArgb((int)((48 + 48 * pulso) * entrada), Cian);
            g.DrawLine(lineaEnergiaPen, ancho * 0.12f, alto * 0.73f, ancho * 0.88f, alto * 0.73f);

            DibujarTransformacion(g, tiempo, ancho, alto);
            string estado = tiempo < InicioGlitch ? "CORE ONLINE // SYNCHRONIZING" :
                tiempo < Transformacion ? "ERROR // MEMORY CORRUPTION" : "REALITY BREACH DETECTED";
            g.DrawString(estado, estadoFont, tiempo < Transformacion ? estadoCian : estadoMagenta, 26f, alto - 52f);
            g.DrawString("SPACE / ENTER / CLICK TO SKIP", ayudaFont, estadoCian, Math.Max(12, ancho - 290), alto - 48f);
        }

        private void DibujarTransformacion(Graphics g, double tiempo, int ancho, int alto)
        {
            float centroY = alto * 0.46f;
            float pulso = tiempo >= Transformacion ? 1f + (float)Math.Sin(tiempo * 15f) * 0.012f :
                1f + (float)Math.Sin(tiempo * 3f) * 0.008f;
            float tamano = tituloFont.Size * pulso;
            Font fuente = tituloFont;
            RectangleF destino = new(0, centroY - tamano, ancho, tamano * 1.5f);
            if (tiempo < Transformacion)
            {
                using SolidBrush b = new(Color.FromArgb((int)(255 * Suavizar((float)(tiempo / 0.9))), Cian));
                g.DrawString("NEXUS", fuente, b, destino, centrado);
            }
            else if (tiempo < 3.9)
            {
                float transicion = Suavizar((float)((tiempo - Transformacion) / 0.3));
                using SolidBrush anterior = new(Color.FromArgb((int)(255 * (1f - transicion)), Cian));
                using SolidBrush nuevo = new(Color.FromArgb((int)(255 * transicion), Magenta));
                g.DrawString("NEXUS", fuente, anterior, destino, centrado);
                g.DrawString("NOCTRYX", fuente, nuevo, destino, centrado);
            }
            else
                g.DrawString("NOCTRYX", fuente, tituloMagenta, destino, centrado);

            string subtitulo = tiempo < Transformacion ? "SYSTEM INITIALIZATION" : "SYSTEM COLLAPSE";
            using SolidBrush bSubtitulo = new(Color.FromArgb(180, tiempo < Transformacion ? Cian : Magenta));
            g.DrawString(subtitulo, subtituloFont, bSubtitulo,
                new RectangleF(0, centroY + tamano * 0.48f, ancho, 42f), centrado);
        }

        private void AplicarGlitch(Graphics g, double tiempo)
        {
            int ancho = ClientSize.Width;
            int alto = ClientSize.Height;
            if (ancho < 10 || alto < 10 || bufferEscena is null)
                return;
            double progreso = Math.Clamp((tiempo - InicioGlitch) / (Transformacion - InicioGlitch), 0d, 1d);
            double intensidad = tiempo < Transformacion ? 0.12 + progreso * 0.75 : 0.6;
            Random rng = new((int)(tiempo * 24d) * 7919 + 37);
            int cantidad = (int)(3 + intensidad * 14);
            for (int i = 0; i < cantidad; i++)
            {
                int y = rng.Next(alto);
                int h = Math.Min(rng.Next(2, 16), alto - y);
                int limite = Math.Max(1, (int)(ancho * 0.08 * intensidad));
                int desplazamiento = rng.Next(-limite, limite + 1);
                g.DrawImage(bufferEscena, new Rectangle(desplazamiento, y, ancho, h),
                    new Rectangle(0, y, ancho, h), GraphicsUnit.Pixel);
            }
            int lineas = 5 + (int)(intensidad * 20);
            for (int i = 0; i < lineas; i++)
            {
                int y = rng.Next(alto);
                int x = rng.Next(ancho);
                int largo = rng.Next(12, Math.Max(13, ancho / 4));
                g.DrawLine((i & 1) == 0 ? glitchCianPen : glitchMagentaPen,
                    x, y, Math.Min(ancho - 1, x + largo), y);
            }
        }

        private void CrearBuffers()
        {
            LiberarBuffers();
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;
            tituloFont.Dispose();
            float tamanoTitulo = Math.Clamp(Math.Min(ClientSize.Height * 0.145f,
                ClientSize.Width * 0.105f), 48f, 112f);
            tituloFont = new Font("Bahnschrift", tamanoTitulo, FontStyle.Bold, GraphicsUnit.Pixel);
            bufferEscena = new Bitmap(ClientSize.Width, ClientSize.Height);
            bufferSalida = new Bitmap(ClientSize.Width, ClientSize.Height);
            graficoEscena = Graphics.FromImage(bufferEscena);
            graficoSalida = Graphics.FromImage(bufferSalida);
        }

        private void LiberarBuffers()
        {
            graficoEscena?.Dispose();
            graficoSalida?.Dispose();
            bufferEscena?.Dispose();
            bufferSalida?.Dispose();
            graficoEscena = graficoSalida = null;
            bufferEscena = bufferSalida = null;
        }

        private void ReproducirJingle()
        {
            try
            {
                string ruta = Path.Combine(AppContext.BaseDirectory,
                    "Resources", "Audio", "Cinematics", "NoctryxIntro.wav");
                audioIntro = new AudioManager();
                audioIntro.ReproducirMusica(ruta, volumen: 0.82f);
            }
            catch
            {
                // La cinemática visual continúa aunque el audio no esté disponible.
            }
        }

        private void CompletarIntro()
        {
            if (completada)
                return;
            completada = true;
            timer.Stop();
            reloj.Stop();
            audioIntro?.DetenerMusica();
            DialogResult = DialogResult.OK;
        }

        private static float Suavizar(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !recursosLiberados)
            {
                recursosLiberados = true;
                timer.Stop();
                timer.Dispose();
                audioIntro?.Dispose();
                LiberarBuffers();
                tituloFont.Dispose();
                subtituloFont.Dispose();
                estadoFont.Dispose();
                ayudaFont.Dispose();
                centrado.Dispose();
                tituloMagenta.Dispose();
                estadoCian.Dispose();
                estadoMagenta.Dispose();
                flashBrush.Dispose();
                reticulaPen.Dispose();
                perspectivaPen.Dispose();
                lineaEnergiaPen.Dispose();
                glitchCianPen.Dispose();
                glitchMagentaPen.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
