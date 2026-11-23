
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NexusCore;
using NexusGUI.Audio;
using NexusGUI.Gameplay;

namespace NexusGUI
{
    public partial class Form1 : Form
    {
        private readonly AudioManager audioManager;
        private SistemaTronGrid sistema;
        private Label lblEstado;
        private Label lblMensaje;
        private TextBox txtUsuario;
        private TextBox txtSector;
        private Button btnIniciar;
        private Button btnNorte;
        private Button btnSur;
        private Button btnEste;
        private Button btnOeste;
        private Button btnMisiones;
        private Button btnInventario;
        private Button btnPortal;

        public Form1()
        {
            InitializeComponent();
            audioManager = new AudioManager();
            audioManager.Diagnostico += mensaje => Trace.WriteLine(mensaje);
            FormClosed += (_, _) => audioManager.Dispose();
            ConfigurarInterfaz();
        }

        private void ConfigurarInterfaz()
        {
            Text = "NEXUS V2.0 - TRON GRID EDITION";
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(10, 14, 30);
            ForeColor = Color.Cyan;

            Label titulo = new Label
            {
                Text = "NEXUS V2.0",
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                ForeColor = Color.Cyan,
                AutoSize = true,
                Location = new Point(30, 20)
            };
            Controls.Add(titulo);

            Label lblId = new Label
            {
                Text = "IDENTIFICADOR:",
                Location = new Point(35, 100),
                AutoSize = true
            };
            Controls.Add(lblId);

            txtUsuario = new TextBox
            {
                Location = new Point(170, 95),
                Width = 200
            };
            Controls.Add(txtUsuario);

            Label lblSec = new Label
            {
                Text = "SECTOR:",
                Location = new Point(400, 100),
                AutoSize = true
            };
            Controls.Add(lblSec);

            txtSector = new TextBox
            {
                Location = new Point(480, 95),
                Width = 150
            };
            Controls.Add(txtSector);

            btnIniciar = CrearBoton("INICIAR RED", 680, 92);
            btnIniciar.Click += BtnIniciar_Click;
            Controls.Add(btnIniciar);

            Button btnPrototipo2D = CrearBoton("JUGAR 2D", 820, 92);
            btnPrototipo2D.Click += BtnPrototipo2D_Click;
            Controls.Add(btnPrototipo2D);

            lblEstado = new Label
            {
                Location = new Point(35, 160),
                Size = new Size(900, 110),
                Font = new Font("Consolas", 12),
                ForeColor = Color.Lime
            };
            Controls.Add(lblEstado);

            lblMensaje = new Label
            {
                Location = new Point(35, 290),
                Size = new Size(900, 100),
                Font = new Font("Consolas", 11),
                ForeColor = Color.White
            };
            Controls.Add(lblMensaje);

            btnNorte = CrearBoton("NORTE", 420, 420);
            btnSur = CrearBoton("SUR", 420, 510);
            btnEste = CrearBoton("ESTE", 520, 465);
            btnOeste = CrearBoton("OESTE", 320, 465);

            btnNorte.Click += (s, e) => Desplazar(1);
            btnSur.Click += (s, e) => Desplazar(2);
            btnEste.Click += (s, e) => Desplazar(3);
            btnOeste.Click += (s, e) => Desplazar(4);

            Controls.AddRange(new Control[]
            {
                btnNorte, btnSur, btnEste, btnOeste
            });

            btnMisiones = CrearBoton("MISIONES", 700, 420);
            btnInventario = CrearBoton("INVENTARIO", 700, 470);
            btnPortal = CrearBoton("PORTAL I/O", 700, 520);

            btnMisiones.Click += BtnMisiones_Click;
            btnInventario.Click += BtnInventario_Click;
            btnPortal.Click += BtnPortal_Click;

            Controls.AddRange(new Control[]
            {
                btnMisiones, btnInventario, btnPortal
            });
        }

        private Button CrearBoton(string texto, int x, int y)
        {
            return new Button
            {
                Text = texto,
                Location = new Point(x, y),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(20, 35, 65),
                ForeColor = Color.Cyan,
                FlatStyle = FlatStyle.Flat
            };
        }

        private void BtnIniciar_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsuario.Text) ||
                string.IsNullOrWhiteSpace(txtSector.Text))
            {
                MessageBox.Show(
                    "Ingresa el identificador y el sector.",
                    "NEXUS");
                return;
            }

            sistema = new SistemaTronGrid();

            try
            {
                sistema.InicializarRed(txtUsuario.Text, txtSector.Text);
                lblMensaje.Text = "RED INICIADA. Bienvenido al Grid.";
                ActualizarEstado();
                string sonidoInicio = Path.Combine(
                    AppContext.BaseDirectory,
                    "Resources", "Audio", "SFX", "system_start.wav");
                audioManager.ReproducirEfecto(sonidoInicio);

                string musicaIntroduccion = Path.Combine(
                    AppContext.BaseDirectory,
                    "Resources", "Audio", "Music", "Trisha's Lullaby.mp3");
                audioManager.ReproducirMusicaEnBucle(musicaIntroduccion, volumen: 0.35f);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error de inicialización");
            }
        }

        private void BtnPrototipo2D_Click(object? sender, EventArgs e)
        {
            using var prototipo = new GamePrototypeForm();
            prototipo.ShowDialog(this);
        }

        private void Desplazar(int direccion)
        {
            if (sistema == null)
            {
                lblMensaje.Text = "Primero debes iniciar la red.";
                return;
            }

            lblMensaje.Text = sistema.Desplazar(direccion);
            ActualizarEstado();
        }

        private void ActualizarEstado()
        {
            if (sistema?.UsuarioActual == null)
                return;

            UsuarioGrid usuario = sistema.UsuarioActual;

            lblEstado.Text =
                $"USUARIO: {usuario.IdentificadorUsuario}\n" +
                $"SECTOR: [{sistema.GridX}, {sistema.GridY}]\n" +
                $"INTEGRIDAD: {usuario.IntegridadCiclo}%\n" +
                $"DISCO DE PROTECCIÓN: {usuario.DiscoProteccion}%\n" +
                $"ENERGÍA: {usuario.NivelCargaEnergia}%\n" +
                $"CICLOS CPU: {usuario.CiclosCPU}";
        }

        private void BtnMisiones_Click(object sender, EventArgs e)
        {
            if (sistema == null)
                return;

            DirectivaDirectorio d = sistema.Directiva;

            MessageBox.Show(
                $"1. Anomalía: {d.CoordenadaAnomalia}\n" +
                $"2. Disco Madre: {d.DiscoMadreObtenido}\n" +
                $"3. Neutralizar CLU: {d.EncomIOPurificado}\n" +
                $"4. Extracción: {d.ExtraccionCompletada}",
                "DIRECTIVAS NEXUS");
        }

        private void BtnInventario_Click(object sender, EventArgs e)
        {
            if (sistema == null)
                return;

            var nodos = sistema.UsuarioActual.Memoria.ObtenerNodos();

            string contenido = "";

            for (int i = 0; i < nodos.Count; i++)
            {
                contenido +=
                    $"{i + 1}. {nodos[i].Identificador} - " +
                    $"{nodos[i].Tipo}\n";
            }

            MessageBox.Show(
                contenido == "" ? "Inventario vacío." : contenido,
                "MATRIZ DE MEMORIA");
        }

        private void BtnPortal_Click(object sender, EventArgs e)
        {
            if (sistema == null)
                return;

            lblMensaje.Text = sistema.ProcesarSalidaPortal();
            ActualizarEstado();

            if (sistema.JuegoTerminado &&
                sistema.Directiva.SincronizacionTotal)
            {
                MessageBox.Show(
                    "¡MISIÓN COMPLETADA! Has alcanzado el Portal I/O.",
                    "NEXUS V2.0");
            }
        }
    }
}
