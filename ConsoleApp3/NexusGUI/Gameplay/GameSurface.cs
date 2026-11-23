using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NexusGUI.Database;

namespace NexusGUI.Gameplay
{
    public sealed class GameSurface : Control
    {
        public event Action<bool>? MusicaMenuSolicitada;
        public event Action<int>? MusicaMapaSolicitada;
        public event Action? BaseDatosSolicitada;
        public event Action? GestionarPartidasSolicitada;

        private enum TipoEnemigo
        {
            Centinela,
            Anomalia
        }

        private enum MapaActivo
        {
            CiudadEnColapso,
            RealidadHostil
        }

        private enum EstadoInterfaz
        {
            MenuPrincipal,
            Controles,
            Introduccion,
            Jugando,
            Pausa
        }

        private sealed class Enemigo
        {
            private readonly float posicionInicialX;
            private readonly float posicionInicialY;

            public TipoEnemigo Tipo { get; }
            public float X { get; set; }
            public float Y { get; set; }
            public float PosicionInicialX => posicionInicialX;
            public float PosicionInicialY => posicionInicialY;
            public float Ancho { get; }
            public float Alto { get; }
            public float LimiteIzquierdo { get; }
            public float LimiteDerecho { get; }
            public float Direccion { get; set; } = 1f;
            public float Fase { get; set; }
            public int IntegridadMaxima { get; }
            public int Integridad { get; private set; }
            public bool Vivo { get; private set; } = true;
            public bool EnAlerta { get; set; }
            public float TiempoDestello { get; set; }
            public float TiempoDesintegracion { get; set; }
            public PointF[] Forma { get; } = new PointF[8];

            public RectangleF Limites => new(X, Y, Ancho, Alto);

            public Enemigo(TipoEnemigo tipo, float x, float y, float ancho, float alto,
                int integridadMaxima, float limiteIzquierdo, float limiteDerecho)
            {
                Tipo = tipo;
                posicionInicialX = x;
                posicionInicialY = y;
                X = x;
                Y = y;
                Ancho = ancho;
                Alto = alto;
                IntegridadMaxima = integridadMaxima;
                Integridad = integridadMaxima;
                LimiteIzquierdo = limiteIzquierdo;
                LimiteDerecho = limiteDerecho;
            }

            public void RecibirDano(int cantidad)
            {
                if (!Vivo || cantidad <= 0)
                    return;

                Integridad = Math.Max(0, Integridad - cantidad);
                TiempoDestello = 0.14f;
                if (Integridad == 0)
                {
                    Vivo = false;
                    EnAlerta = false;
                    TiempoDesintegracion = 0.52f;
                }
            }

            public void Reiniciar()
            {
                X = posicionInicialX;
                Y = posicionInicialY;
                Direccion = 1f;
                Fase = 0f;
                Integridad = IntegridadMaxima;
                Vivo = true;
                EnAlerta = false;
                TiempoDestello = 0f;
                TiempoDesintegracion = 0f;
            }
        }

        private enum EstadoTransicionGrieta
        {
            Inactiva,
            Activacion,
            Resonancia,
            Convergencia,
            Estabilizacion
        }

        private const float AnchoVista = 1280f;
        private const float AltoVista = 720f;
        private const float AnchoNivel = 3600f;
        private const float SueloY = 612f;
        private const float AnchoMapaRealidadHostil = 2800f;
        private const float AnchoJugador = 42f;
        private const float AltoJugador = 78f;
        private const int IntegridadMaximaJugador = 100;
        private const int DanioAtaqueJugador = 20;
        private const float DuracionInvulnerabilidad = 0.9f;
        private const float DuracionAtaque = 0.18f;
        private const float TiempoReutilizacionAtaque = 0.42f;
        private const float AlcanceAtaque = 62f;
        private const float RadioDeteccionCentinela = 180f;
        private const float PasoFijo = 1f / 60f;
        private const float Gravedad = 1750f;
        private const float ImpulsoSalto = -650f;
        private const float VelocidadHorizontal = 300f;
        private static readonly RectangleF puertaFinal = new(3420f, 444f, 76f, 168f);
        private static readonly RectangleF zonaInteraccionGrieta =
            new(680f, SueloY - 230f, 260f, 230f);
        private static readonly RectangleF zonaRegistroCiudad = new(470f, 430f, 140f, 90f);
        private static readonly RectangleF zonaRegistroRealidad = new(2470f, 470f, 140f, 142f);
        private static readonly RectangleF[] botonesMenuPrincipal =
        {
            new(500f, 340f, 280f, 54f), new(500f, 410f, 280f, 54f),
            new(500f, 480f, 280f, 54f), new(500f, 550f, 280f, 54f)
        };
        private static readonly RectangleF[] botonesPausa =
        {
            new(500f, 340f, 280f, 52f), new(500f, 410f, 280f, 52f),
            new(500f, 480f, 280f, 52f), new(500f, 550f, 280f, 52f)
        };
        private static readonly RectangleF[] botonesResultado =
        {
            new(500f, 420f, 280f, 48f), new(500f, 484f, 280f, 48f)
        };
        private static readonly RectangleF[] botonesVictoria =
        {
            new(500f, 490f, 280f, 48f), new(500f, 552f, 280f, 48f)
        };
        private static readonly RectangleF botonVolverControles = new(500f, 570f, 280f, 48f);
        private static readonly string[] textoIntroduccion =
        {
            "Integridad dimensional comprometida.",
            "Unidad ECHO-0 detectada.",
            "Memoria fragmentada. Origen desconocido.",
            "Las realidades están colapsando.",
            "Encuentra la fuente de la señal."
        };
        private const float DuracionIntroduccion = 8f;
        private const float DuracionLineaIntroduccion = 1.6f;
        private const float DuracionRegistro = 8f;
        private const string TextoRegistroCiudad =
            "El sistema registró una anomalía antes del colapso. Todas las rutas conducen a una misma señal. Identificación: ECHO-0.";
        private const string TextoRegistroRealidad =
            "Estos recuerdos no pertenecen a esta realidad. Ella existió aquí... o quizá en un mundo que ya no existe.";

        private readonly RectangleF[] plataformasCiudad =
        {
            new(0f, 612f, 740f, 108f),
            new(900f, 612f, 620f, 108f),
            new(1660f, 612f, 740f, 108f),
            new(2580f, 612f, 1020f, 108f),
            new(440f, 520f, 220f, 22f),
            new(730f, 445f, 200f, 22f),
            new(1060f, 520f, 190f, 22f),
            new(1280f, 450f, 190f, 22f),
            new(1430f, 520f, 200f, 22f),
            new(1610f, 510f, 190f, 22f),
            new(1800f, 440f, 200f, 22f),
            new(2070f, 520f, 190f, 22f),
            new(2320f, 520f, 280f, 22f),
            new(2630f, 520f, 200f, 22f),
            new(2860f, 450f, 190f, 22f),
            new(3090f, 520f, 200f, 22f)
        };

        private readonly Enemigo[] enemigosCiudad =
        {
            new(TipoEnemigo.Centinela, 1110f, SueloY - 50f, 46f, 50f,
                60, 930f, 1490f),
            new(TipoEnemigo.Centinela, 3070f, SueloY - 50f, 46f, 50f,
                60, 2800f, 3310f),
            new(TipoEnemigo.Anomalia, 1840f, 398f, 42f, 42f,
                40, 0f, AnchoNivel)
        };

        private readonly RectangleF[] plataformasRealidadHostil =
        {
            new(0f, 612f, 390f, 108f),
            new(430f, 535f, 150f, 22f),
            new(625f, 470f, 145f, 22f),
            new(815f, 535f, 155f, 22f),
            new(1010f, 465f, 150f, 22f),
            new(1200f, 530f, 165f, 22f),
            new(1405f, 462f, 155f, 22f),
            new(1600f, 525f, 165f, 22f),
            new(1810f, 450f, 155f, 22f),
            new(2010f, 520f, 165f, 22f),
            new(2220f, 455f, 155f, 22f),
            new(2425f, 612f, 375f, 108f)
        };

        private readonly RectangleF[] peligrosRealidadHostil =
        {
            new(850f, 517f, 54f, 18f),
            new(1642f, 507f, 58f, 18f),
            new(2254f, 437f, 52f, 18f)
        };

        private readonly Enemigo[] enemigosRealidadHostil =
        {
            new(TipoEnemigo.Centinela, 470f, 485f, 46f, 50f, 60, 438f, 580f),
            new(TipoEnemigo.Anomalia, 1245f, 390f, 42f, 42f, 40, 0f, AnchoMapaRealidadHostil),
            new(TipoEnemigo.Centinela, 2070f, 470f, 46f, 50f, 60, 2010f, 2175f)
        };

        private readonly RectangleF objetivoFinalRealidad = new(2625f, 462f, 76f, 150f);

        private readonly System.Windows.Forms.Timer temporizador;
        private readonly Stopwatch reloj = new();
        private readonly Brush fondoBrush = new SolidBrush(Color.FromArgb(7, 10, 22));
        private readonly LinearGradientBrush atmosferaBrush = new(
            new Rectangle(0, 0, (int)AnchoVista, (int)AltoVista),
            Color.FromArgb(8, 13, 28), Color.FromArgb(18, 9, 31),
            LinearGradientMode.Vertical);
        private readonly LinearGradientBrush neblinaBrush = new(
            new Rectangle(0, 205, (int)AnchoVista, 350),
            Color.FromArgb(0, 48, 89, 128), Color.FromArgb(30, 48, 89, 128),
            LinearGradientMode.Vertical);
        private readonly Brush sombraBrush = new SolidBrush(Color.FromArgb(16, 23, 39));
        private readonly Brush panelBrush = new SolidBrush(Color.FromArgb(25, 34, 53));
        private readonly Brush metalBrush = new SolidBrush(Color.FromArgb(45, 57, 77));
        private readonly Brush detalleBrush = new SolidBrush(Color.FromArgb(64, 104, 135));
        private readonly Brush sueloBrush = new SolidBrush(Color.FromArgb(19, 27, 43));
        private readonly Brush pisoInteriorBrush = new SolidBrush(Color.FromArgb(22, 32, 49));
        private readonly Brush ruinaLejanaBrush = new SolidBrush(Color.FromArgb(17, 23, 42));
        private readonly Brush arquitecturaMediaBrush = new SolidBrush(Color.FromArgb(12, 18, 34));
        private readonly Brush edificioLejanoBrush = new SolidBrush(Color.FromArgb(20, 30, 48));
        private readonly Brush fachadaMediaBrush = new SolidBrush(Color.FromArgb(25, 37, 57));
        private readonly Brush fachadaOscuraBrush = new SolidBrush(Color.FromArgb(8, 14, 26));
        private readonly Brush residuoCianBrush = new SolidBrush(Color.FromArgb(65, 0, 210, 240));
        private readonly Brush residuoVioletaBrush = new SolidBrush(Color.FromArgb(55, 175, 75, 235));
        private readonly Brush residuoBlancoBrush = new SolidBrush(Color.FromArgb(82, 190, 220, 245));
        private readonly Brush residuoAzulBrush = new SolidBrush(Color.FromArgb(48, 67, 132, 255));
        private readonly LinearGradientBrush realidadBrumaBrush = new(
            new Rectangle(600, 0, 380, 520),
            Color.FromArgb(24, 24, 108, 166), Color.FromArgb(44, 105, 53, 163),
            LinearGradientMode.ForwardDiagonal);
        private readonly SolidBrush realidadFondoBrush = new(Color.FromArgb(72, 35, 68, 126));
        private readonly SolidBrush realidadEstructuraBrush = new(Color.FromArgb(116, 41, 98, 166));
        private readonly SolidBrush realidadFragmentoBrush = new(Color.FromArgb(104, 94, 74, 180));
        private readonly SolidBrush vacioRealidadBrush = new(Color.FromArgb(5, 6, 19));
        private readonly SolidBrush plataformaRealidadBrush = new(Color.FromArgb(30, 22, 42, 70));
        private readonly SolidBrush caraRealidadBrush = new(Color.FromArgb(95, 63, 78, 132));
        private readonly SolidBrush peligroRealidadBrush = new(Color.FromArgb(110, 126, 40, 204));
        private readonly SolidBrush overlayTransicionBrush = new(Color.FromArgb(0, 5, 6, 19));
        private readonly SolidBrush overlayMenuBrush = new(Color.FromArgb(206, 4, 7, 18));
        private readonly SolidBrush rellenoBotonBrush = new(Color.FromArgb(190, 13, 23, 43));
        private readonly SolidBrush rellenoBotonSeleccionadoBrush = new(Color.FromArgb(220, 25, 51, 77));
        private readonly Brush residuoCianTenueBrush = new SolidBrush(Color.FromArgb(28, 0, 210, 240));
        private readonly Brush residuoVioletaTenueBrush = new SolidBrush(Color.FromArgb(25, 175, 75, 235));
        private readonly SolidBrush filtracionCianBrush = new(Color.FromArgb(0, 0, 196, 242));
        private readonly SolidBrush filtracionVioletaBrush = new(Color.FromArgb(0, 157, 93, 241));
        private readonly Pen fragmentoCianPen = new(Color.FromArgb(82, 0, 183, 238), 1f);
        private readonly Pen fragmentoVioletaPen = new(Color.FromArgb(78, 132, 92, 224), 1f);
        private readonly Pen filtracionCianPen = new(Color.FromArgb(0, 88, 214, 255), 1.2f);
        private readonly Pen filtracionVioletaPen = new(Color.FromArgb(0, 180, 112, 255), 1.2f);
        private readonly Pen realidadCianPen = new(Color.FromArgb(150, 82, 211, 241), 1.2f);
        private readonly Pen realidadVioletaPen = new(Color.FromArgb(125, 158, 129, 242), 1f);
        private readonly Pen realidadBlancoPen = new(Color.FromArgb(120, 175, 216, 239), 0.8f);
        private readonly Pen distorsionTransicionPen = new(Color.FromArgb(0, 102, 220, 255), 1.4f);
        private readonly Pen bordePlataformaRealidadPen = new(Color.FromArgb(180, 104, 203, 255), 2f);
        private readonly Pen peligroRealidadPen = new(Color.FromArgb(180, 194, 112, 255), 1.5f);
        private readonly Pen haloGrietaMenuPen = new(Color.FromArgb(26, 0, 207, 255), 18f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };
        private readonly Pen grietaMenuCianPen = new(Color.FromArgb(110, 0, 220, 255), 2.2f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };
        private readonly Pen grietaMenuVioletaPen = new(Color.FromArgb(105, 183, 89, 255), 2.4f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };
        private readonly PointF[] trazadoGrietaMenu = new PointF[8];
        private static readonly float[] particulasMenuX = { 92f, 278f, 398f, 832f, 932f, 1110f, 1190f, 860f, 330f };
        private static readonly float[] particulasMenuY = { 96f, 560f, 220f, 95f, 410f, 210f, 520f, 610f, 118f };
        private static readonly float[] particulasMenuFase = { 0.2f, 1.7f, 3.1f, 0.8f, 2.4f, 4.0f, 5.1f, 2.9f, 4.7f };
        private readonly Brush obsidianaBrush = new SolidBrush(Color.FromArgb(8, 12, 22));
        private readonly Brush armaduraBrush = new SolidBrush(Color.FromArgb(23, 31, 47));
        private readonly Brush placaBrush = new SolidBrush(Color.FromArgb(37, 48, 67));
        private readonly Brush cianBrush = new SolidBrush(Color.FromArgb(0, 225, 255));
        private readonly Brush cianLuzBrush = new SolidBrush(Color.FromArgb(135, 250, 255));
        private readonly Brush violetaBrush = new SolidBrush(Color.FromArgb(190, 85, 255));
        private readonly Brush blancoBrush = new SolidBrush(Color.FromArgb(225, 240, 250));
        private readonly Pen cianPen = new(Color.FromArgb(0, 225, 255), 2f);
        private readonly Pen violetaPen = new(Color.FromArgb(190, 85, 255), 2f);
        private readonly Pen tenuePen = new(Color.FromArgb(64, 104, 135), 1f);
        private readonly Pen grietaPen = new(Color.FromArgb(48, 77, 99), 1f);
        private readonly Pen fachadaPen = new(Color.FromArgb(54, 78, 102), 1f);
        private readonly Pen ataqueHaloPen = new(Color.FromArgb(105, 157, 93, 241), 8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        private readonly Pen ataqueCianPen = new(Color.FromArgb(220, 102, 235, 255), 2.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        private readonly Pen destelloCombatePen = new(Color.FromArgb(210, 190, 245, 255), 2f);
        private readonly Pen haloVioletaGrietaPen = new(Color.FromArgb(24, 165, 70, 255), 40f)
        {
            StartCap = LineCap.Flat,
            EndCap = LineCap.Flat,
            LineJoin = LineJoin.Round
        };
        private readonly Pen haloCianGrietaPen = new(Color.FromArgb(30, 0, 195, 255), 18f)
        {
            StartCap = LineCap.Flat,
            EndCap = LineCap.Flat,
            LineJoin = LineJoin.Round
        };
        private readonly Pen bordeVioletaGrietaPen = new(Color.FromArgb(175, 178, 78, 255), 4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        private readonly Pen bordeCianGrietaPen = new(Color.FromArgb(190, 0, 205, 255), 2.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        private readonly Pen nucleoGrietaPen = new(Color.FromArgb(105, 95, 175, 255), 1.25f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        private readonly Pen siluetaPen = new(Color.FromArgb(5, 8, 16), 8f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        private readonly Pen extremidadPen = new(Color.FromArgb(28, 38, 55), 5f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        private readonly Pen articulacionPen = new(Color.FromArgb(92, 73, 145), 2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        private readonly Pen filoCianPen = new(Color.FromArgb(0, 225, 255), 2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        private readonly Pen filoVioletaPen = new(Color.FromArgb(190, 85, 255), 2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        private readonly Pen visorInteriorPen = new(Color.FromArgb(135, 250, 255), 1f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        private readonly Font tituloFont = new("Segoe UI", 14f, FontStyle.Bold);
        private readonly Font hudFont = new("Consolas", 9f, FontStyle.Bold);
        private readonly Font identificadorFont = new("Consolas", 9f, FontStyle.Bold);
        private readonly Font finalFont = new("Segoe UI", 28f, FontStyle.Bold);
        private readonly Font tituloMenuFont = new("Segoe UI", 48f, FontStyle.Bold);
        private readonly Font subtituloMenuFont = new("Consolas", 18f, FontStyle.Bold);
        private readonly Font opcionMenuFont = new("Consolas", 14f, FontStyle.Bold);
        private readonly Font controlesMenuFont = new("Consolas", 12f, FontStyle.Bold);

        private EstadoInterfaz estadoInterfaz = EstadoInterfaz.MenuPrincipal;
        private int seleccionMenu;
        private int seleccionPausa;
        private int seleccionResultado;
        private int lineaIntroduccion;
        private float tiempoIntroduccion;
        private int registroNarrativoVisible;
        private float tiempoRegistroVisible;
        private bool registroCiudadLeido;
        private bool registroRealidadLeido;
        private bool teclaConfirmacionPulsada;
        private bool teclaEscapePulsada;
        private bool bloquearIzquierdaHastaSoltar;
        private bool bloquearDerechaHastaSoltar;
        private bool bloquearSaltoHastaSoltar;
        private bool bloquearAtaqueHastaSoltar;
        private bool bloquearInteraccionHastaSoltar;
        private bool izquierdaPulsada;
        private bool derechaPulsada;
        private bool mirandoDerecha = true;
        private float mezclaFrontal = 1f;
        private bool saltoPulsado;
        private bool teclaAtaquePulsada;
        private bool enSuelo = true;
        private bool derrotado;
        private int integridadJugador = IntegridadMaximaJugador;
        private float invulnerabilidadRestante;
        private float ataqueActivoRestante;
        private float reutilizacionAtaqueRestante;
        private bool ataqueMirandoDerecha = true;
        private bool ataqueImpactoResuelto;
        private float jugadorX = 150f;
        private float jugadorY = SueloY - AltoJugador;
        private float velocidadY;
        private float camaraX;
        private double acumulador;
        private double tiempoAnimacion;
        private bool demoCompletada;
        private MapaActivo mapaActual;
        private bool transicionMapaEnCurso;
        private bool transicionMapaRealizada;
        private float tiempoTransicionMapa;
        private bool ecoEnZonaGrieta;
        private bool ecoEnZonaObjetivo;
        private bool ecoEnZonaRegistro;
        private bool teclaInteraccionPulsada;
        private EstadoTransicionGrieta estadoTransicionGrieta;
        private float tiempoEtapaGrieta;
        private float factorProximidadGrieta;
        private float factorSecuenciaGrieta;
        private float respuestaVisualGrieta;

        private bool PuedeMoverse => estadoInterfaz == EstadoInterfaz.Jugando &&
            !derrotado && !demoCompletada && !transicionMapaEnCurso;
        private RectangleF[] PlataformasActivas => mapaActual == MapaActivo.CiudadEnColapso
            ? plataformasCiudad : plataformasRealidadHostil;
        private Enemigo[] EnemigosActivos => mapaActual == MapaActivo.CiudadEnColapso
            ? enemigosCiudad : enemigosRealidadHostil;
        private float AnchoMapaActual => mapaActual == MapaActivo.CiudadEnColapso
            ? AnchoNivel : AnchoMapaRealidadHostil;

        public GameProgressSnapshot CapturarProgreso()
        {
            if (derrotado || demoCompletada || transicionMapaEnCurso || integridadJugador <= 0)
                throw new InvalidOperationException("Solo se puede guardar durante una partida activa o en pausa.");

            return new GameProgressSnapshot(
                mapaActual == MapaActivo.CiudadEnColapso ? "CIUDAD EN COLAPSO" : "REALIDAD HOSTIL",
                jugadorX, jugadorY, integridadJugador);
        }

        public bool RestaurarProgreso(GameProgressSnapshot snapshot, out string error)
        {
            MapaActivo nuevoMapa;
            if (snapshot.Map == "CIUDAD EN COLAPSO") nuevoMapa = MapaActivo.CiudadEnColapso;
            else if (snapshot.Map == "REALIDAD HOSTIL") nuevoMapa = MapaActivo.RealidadHostil;
            else { error = "El mapa guardado no es válido."; return false; }

            float ancho = nuevoMapa == MapaActivo.CiudadEnColapso ? AnchoNivel : AnchoMapaRealidadHostil;
            if (snapshot.Integrity is < 1 or > IntegridadMaximaJugador ||
                !float.IsFinite(snapshot.PositionX) || !float.IsFinite(snapshot.PositionY) ||
                snapshot.PositionX < 0f || snapshot.PositionX > ancho - AnchoJugador ||
                snapshot.PositionY < 0f || snapshot.PositionY > AltoVista - AltoJugador)
            { error = "La posición o integridad guardada está fuera de los límites permitidos."; return false; }

            mapaActual = nuevoMapa;
            jugadorX = snapshot.PositionX;
            jugadorY = snapshot.PositionY;
            integridadJugador = snapshot.Integrity;
            velocidadY = 0f;
            enSuelo = false;
            camaraX = Math.Clamp(jugadorX + AnchoJugador / 2f - AnchoVista * 0.42f, 0f, ancho - AnchoVista);
            transicionMapaEnCurso = false;
            transicionMapaRealizada = nuevoMapa == MapaActivo.RealidadHostil;
            tiempoTransicionMapa = 0f;
            derrotado = false;
            demoCompletada = false;
            invulnerabilidadRestante = 0f;
            ataqueActivoRestante = 0f;
            reutilizacionAtaqueRestante = 0f;
            ataqueImpactoResuelto = false;
            LimpiarTeclasDeJuego();
            estadoInterfaz = EstadoInterfaz.Jugando;
            acumulador = 0d;
            reloj.Restart();
            MusicaMenuSolicitada?.Invoke(false);
            NotificarMusicaMapaActual();
            Invalidate();
            error = string.Empty;
            return true;
        }

        private readonly PointF[] mascaraForma = new PointF[13];
        private readonly PointF[] mascaraInteriorForma = new PointF[7];
        private readonly PointF[] torsoForma = new PointF[8];
        private readonly PointF[] torsoInteriorForma = new PointF[8];
        private readonly PointF[] estelaForma = new PointF[4];
        private readonly PointF[] contornoGrieta = new PointF[22];
        private readonly PointF[] bordeIzquierdoGrieta = new PointF[11];
        private readonly PointF[] bordeDerechoGrieta = new PointF[11];
        private readonly PointF[] cavidadProfundaGrieta = new PointF[22];
        private readonly PointF[] fragmentoGrietaUno = new PointF[4];
        private readonly PointF[] fragmentoGrietaDos = new PointF[4];
        private readonly PointF[] fragmentoGrietaTres = new PointF[4];
        private readonly PointF[] fragmentoGrietaCuatro = new PointF[4];
        private readonly PointF[] fragmentoGrietaCinco = new PointF[4];
        private readonly PointF[][] fragmentosDimensionales =
        {
            new PointF[5], new PointF[5], new PointF[5], new PointF[5], new PointF[5]
        };
        private readonly PointF[][] escombrosSuspendidos =
        {
            new PointF[6], new PointF[6], new PointF[6], new PointF[6]
        };
        private readonly PointF[][] formasFiltracion =
        {
            new PointF[4], new PointF[4], new PointF[4]
        };
        private readonly GraphicsPath recorteRealidadGrieta = new();
        private readonly PointF[][] formasRealidadAlternativa =
        {
            new PointF[5], new PointF[5], new PointF[5], new PointF[5], new PointF[5]
        };
        private readonly PointF[] reflejoGrietaEdificioIzquierdo = new PointF[4];
        private readonly PointF[] reflejoGrietaEdificioDerecho = new PointF[4];
        private readonly PointF[][] fragmentosHostiles =
        {
            new PointF[5], new PointF[5], new PointF[5], new PointF[5], new PointF[5], new PointF[5]
        };
        private readonly PointF[][] columnasHostiles =
        {
            new PointF[4], new PointF[4], new PointF[4], new PointF[4], new PointF[4]
        };
        private static readonly float[] fragmentoHostilX = { 325f, 688f, 938f, 1475f, 1902f, 2362f };
        private static readonly float[] fragmentoHostilY = { 196f, 118f, 274f, 154f, 235f, 104f };
        private static readonly float[] luciernagaX =
            { 556f, 612f, 681f, 744f, 824f, 889f, 954f, 1022f, 579f, 657f,
              724f, 858f, 918f, 995f, 638f, 790f, 872f, 966f, 704f, 1036f };
        private static readonly float[] luciernagaY =
            { 83f, 132f, 96f, 167f, 118f, 188f, 79f, 159f, 236f, 286f,
              332f, 261f, 319f, 359f, 365f, 46f, 51f, 224f, 202f, 284f };
        private static readonly float[] luciernagaDeriva =
            { 5f, 7f, 4f, 8f, 5f, 6f, 7f, 5f, 8f, 5f,
              6f, 7f, 4f, 8f, 5f, 4f, 6f, 7f, 5f, 6f };
        private static readonly float[] fragmentoResiduoX = { 646f, 737f, 892f, 970f, 812f };
        private static readonly float[] fragmentoResiduoY = { 170f, 278f, 103f, 347f, 371f };
        private static readonly float[] fragmentoResiduoTamano = { 9f, 13f, 6f, 11f, 7f };
        private static readonly float[] escombroX = { 652f, 911f, 705f, 965f };
        private static readonly float[] escombroY = { 116f, 174f, 256f, 319f };
        private static readonly float[] escombroTamano = { 13f, 8f, 17f, 10f };
        private static readonly float[][] formaEscombro =
        {
            new float[] { -0.68f, -0.25f, -0.20f, -0.62f, 0.48f, -0.49f, 0.70f, 0.13f, 0.11f, 0.46f, -0.52f, 0.31f },
            new float[] { -0.53f, -0.39f, 0.12f, -0.66f, 0.61f, -0.18f, 0.43f, 0.42f, -0.20f, 0.32f, -0.62f, 0.17f },
            new float[] { -0.71f, -0.14f, -0.31f, -0.57f, 0.35f, -0.51f, 0.67f, -0.02f, 0.42f, 0.43f, -0.38f, 0.55f },
            new float[] { -0.56f, -0.42f, 0.06f, -0.58f, 0.58f, -0.22f, 0.35f, 0.49f, -0.26f, 0.39f, -0.65f, 0.12f }
        };
        private static readonly float[] filtracionX = { 724f, 858f, 944f };
        private static readonly float[] filtracionY = { 154f, 236f, 322f };
        private static readonly float[] filtracionTamano = { 6f, 9f, 5f };
        private static readonly float[][] realidadFormaX =
        {
            new float[] { -0.42f, -0.38f, 0f, 0.34f, 0.50f },
            new float[] { -1f, -0.70f, 0.40f, 1f, 0.10f },
            new float[] { -0.70f, -0.10f, 0.65f, 0.20f, -0.40f },
            new float[] { -0.80f, -0.25f, 0.45f, 1f, 0.10f },
            new float[] { -0.35f, -0.90f, 0.30f, 0.75f, -0.10f }
        };
        private static readonly float[][] realidadFormaY =
        {
            new float[] { 0.95f, 0.25f, -1f, -0.60f, 0.45f },
            new float[] { 0f, -0.35f, -0.50f, 0f, 0.40f },
            new float[] { -0.80f, -0.90f, 0.10f, 0.65f, 0.30f },
            new float[] { 0.70f, -0.20f, -0.75f, 0.05f, 0.48f },
            new float[] { -0.65f, -0.10f, -0.90f, 0.48f, 0.72f }
        };
        private static readonly float[] realidadX = { 781f, 800f, 759f, 837f, 799f };
        private static readonly float[] realidadY = { 72f, 121f, 157f, 101f, 181f };
        private static readonly float[] realidadAncho = { 32f, 58f, 34f, 45f, 50f };
        private static readonly float[] realidadAlto = { 78f, 18f, 56f, 35f, 22f };
        private static readonly float[][] formaFragmentoResiduo =
        {
            new float[] { -0.62f, -0.18f, -0.08f, -0.68f, 0.34f, -0.32f, 0.58f, 0.24f, -0.22f, 0.54f },
            new float[] { -0.48f, -0.52f, 0.27f, -0.45f, 0.56f, -0.04f, 0.12f, 0.38f, -0.45f, 0.27f },
            new float[] { -0.36f, -0.32f, 0.02f, -0.62f, 0.38f, -0.18f, 0.55f, 0.35f, -0.18f, 0.24f },
            new float[] { -0.54f, -0.08f, -0.19f, -0.54f, 0.43f, -0.40f, 0.29f, 0.10f, -0.12f, 0.53f },
            new float[] { -0.49f, -0.43f, 0.34f, -0.52f, 0.28f, 0.02f, 0.59f, 0.24f, -0.17f, 0.55f }
        };
        private static readonly float[] grietaIzquierdaX = { -5f, -76f, -29f, -138f, -67f, -112f, -34f, -123f, -58f, -151f, -21f };
        private static readonly float[] grietaDerechaX = { 6f, 28f, 101f, 43f, 149f, 57f, 117f, 31f, 172f, 66f, 19f };
        private static readonly float[] grietaAlturas = { 0f, 38f, 77f, 119f, 164f, 215f, 272f, 330f, 388f, 446f, 500f };
        public GameSurface()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            TabStop = true;
            BackColor = Color.FromArgb(7, 10, 22);

            temporizador = new System.Windows.Forms.Timer { Interval = 16 };
            temporizador.Tick += Temporizador_Tick;
            reloj.Start();
            temporizador.Start();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys tecla = keyData & Keys.KeyCode;
            if (tecla == Keys.Left || tecla == Keys.Right || tecla == Keys.Up ||
                tecla == Keys.Down || tecla == Keys.Escape || tecla == Keys.Enter || tecla == Keys.Space)
                return true;

            return base.IsInputKey(keyData);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            PointF punto = ConvertirPuntoCliente(e.Location);
            if (estadoInterfaz == EstadoInterfaz.MenuPrincipal)
            {
                for (int i = 0; i < botonesMenuPrincipal.Length; i++)
                {
                    if (!botonesMenuPrincipal[i].Contains(punto) || seleccionMenu == i)
                        continue;
                    seleccionMenu = i;
                    Invalidate();
                    break;
                }
            }
            else if (estadoInterfaz == EstadoInterfaz.Pausa)
            {
                for (int i = 0; i < botonesPausa.Length; i++)
                {
                    if (!botonesPausa[i].Contains(punto) || seleccionPausa == i)
                        continue;
                    seleccionPausa = i;
                    Invalidate();
                    break;
                }
            }
            else if (derrotado || demoCompletada)
            {
                RectangleF[] botones = demoCompletada ? botonesVictoria : botonesResultado;
                for (int i = 0; i < botones.Length; i++)
                {
                    if (!botones[i].Contains(punto) || seleccionResultado == i)
                        continue;
                    seleccionResultado = i;
                    Invalidate();
                    break;
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            PointF punto = ConvertirPuntoCliente(e.Location);
            if (estadoInterfaz == EstadoInterfaz.MenuPrincipal)
            {
                for (int i = 0; i < botonesMenuPrincipal.Length; i++)
                {
                    if (!botonesMenuPrincipal[i].Contains(punto))
                        continue;
                    seleccionMenu = i;
                    EjecutarOpcionMenuPrincipal(i);
                    break;
                }
            }
            else if (estadoInterfaz == EstadoInterfaz.Controles)
            {
                if (botonVolverControles.Contains(punto))
                    estadoInterfaz = EstadoInterfaz.MenuPrincipal;
            }
            else if (estadoInterfaz == EstadoInterfaz.Pausa)
            {
                for (int i = 0; i < botonesPausa.Length; i++)
                {
                    if (!botonesPausa[i].Contains(punto))
                        continue;
                    seleccionPausa = i;
                    EjecutarOpcionPausa(i);
                    break;
                }
            }
            else if (derrotado || demoCompletada)
            {
                RectangleF[] botones = demoCompletada ? botonesVictoria : botonesResultado;
                for (int i = 0; i < botones.Length; i++)
                {
                    if (!botones[i].Contains(punto))
                        continue;
                    seleccionResultado = i;
                    EjecutarOpcionResultado(i);
                    break;
                }
            }

            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && teclaEscapePulsada)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                base.OnKeyDown(e);
                return;
            }
            if (e.KeyCode == Keys.Escape)
                teclaEscapePulsada = true;

            if (estadoInterfaz == EstadoInterfaz.MenuPrincipal)
            {
                ProcesarEntradaMenu(e, botonesMenuPrincipal.Length, ref seleccionMenu,
                    EjecutarOpcionMenuPrincipal);
                base.OnKeyDown(e);
                return;
            }

            if (estadoInterfaz == EstadoInterfaz.Introduccion)
            {
                if (e.KeyCode == Keys.Escape)
                    IniciarJuegoTrasIntroduccion();
                else if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) &&
                    !teclaConfirmacionPulsada)
                {
                    teclaConfirmacionPulsada = true;
                    AvanzarIntroduccion();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                base.OnKeyDown(e);
                return;
            }

            if (estadoInterfaz == EstadoInterfaz.Controles)
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
                    estadoInterfaz = EstadoInterfaz.MenuPrincipal;
                e.Handled = true;
                e.SuppressKeyPress = true;
                base.OnKeyDown(e);
                return;
            }

            if (estadoInterfaz == EstadoInterfaz.Pausa)
            {
                if (e.KeyCode == Keys.Escape)
                    ReanudarPartida();
                else
                    ProcesarEntradaMenu(e, botonesPausa.Length, ref seleccionPausa,
                        EjecutarOpcionPausa);
                e.Handled = true;
                e.SuppressKeyPress = true;
                base.OnKeyDown(e);
                return;
            }

            if (derrotado || demoCompletada)
            {
                if (e.KeyCode == Keys.R)
                {
                    ReiniciarPartida();
                    NotificarMusicaMapaActual();
                }
                else if (e.KeyCode == Keys.Escape)
                    VolverAlMenu();
                else
                    ProcesarEntradaMenu(e, botonesResultado.Length, ref seleccionResultado,
                        EjecutarOpcionResultado);
                e.Handled = true;
                e.SuppressKeyPress = true;
                base.OnKeyDown(e);
                return;
            }

            if (e.KeyCode == Keys.Escape && estadoInterfaz == EstadoInterfaz.Jugando)
            {
                estadoInterfaz = EstadoInterfaz.Pausa;
                seleccionPausa = 0;
                CapturarTeclasMantenidas();
                LimpiarTeclasDeJuego();
                Invalidate();
                e.Handled = true;
                e.SuppressKeyPress = true;
                base.OnKeyDown(e);
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.A:
                case Keys.Left:
                    if (PuedeMoverse && !bloquearIzquierdaHastaSoltar)
                    {
                        izquierdaPulsada = true;
                        mirandoDerecha = false;
                    }
                    e.Handled = true;
                    break;

                case Keys.D:
                case Keys.Right:
                    if (PuedeMoverse && !bloquearDerechaHastaSoltar)
                    {
                        derechaPulsada = true;
                        mirandoDerecha = true;
                    }
                    e.Handled = true;
                    break;

                case Keys.Space:
                    if (PuedeMoverse && !saltoPulsado && !bloquearSaltoHastaSoltar)
                    {
                        saltoPulsado = true;
                        if (enSuelo)
                        {
                            velocidadY = ImpulsoSalto;
                            enSuelo = false;
                        }
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.J:
                    if (PuedeMoverse && !teclaAtaquePulsada && !bloquearAtaqueHastaSoltar)
                    {
                        teclaAtaquePulsada = true;
                        IniciarAtaque();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.R:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.E:
                    if (!teclaInteraccionPulsada && !bloquearInteraccionHastaSoltar)
                    {
                        teclaInteraccionPulsada = true;
                        if (PuedeMoverse && ecoEnZonaRegistro &&
                            ((mapaActual == MapaActivo.CiudadEnColapso && !registroCiudadLeido) ||
                             (mapaActual == MapaActivo.RealidadHostil && !registroRealidadLeido)))
                        {
                            MostrarRegistroNarrativo(mapaActual);
                        }
                        else if (PuedeMoverse && mapaActual == MapaActivo.CiudadEnColapso && ecoEnZonaGrieta &&
                            estadoTransicionGrieta == EstadoTransicionGrieta.Inactiva)
                        {
                            estadoTransicionGrieta = EstadoTransicionGrieta.Activacion;
                            tiempoEtapaGrieta = 0f;
                        }
                        else if (PuedeMoverse && mapaActual == MapaActivo.RealidadHostil && ecoEnZonaObjetivo)
                        {
                            CompletarDemo();
                        }
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        private void ProcesarEntradaMenu(KeyEventArgs e, int cantidadOpciones,
            ref int seleccion, Action<int> ejecutar)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W)
                seleccion = (seleccion + cantidadOpciones - 1) % cantidadOpciones;
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S)
                seleccion = (seleccion + 1) % cantidadOpciones;
            else if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) && !teclaConfirmacionPulsada)
            {
                teclaConfirmacionPulsada = true;
                ejecutar(seleccion);
            }
            else if (e.KeyCode == Keys.Escape && estadoInterfaz == EstadoInterfaz.MenuPrincipal)
            {
                FindForm()?.Close();
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            Invalidate();
        }

        private void EjecutarOpcionMenuPrincipal(int opcion)
        {
            switch (opcion)
            {
                case 0:
                    MusicaMenuSolicitada?.Invoke(false);
                    ReiniciarPartida();
                    lineaIntroduccion = 0;
                    tiempoIntroduccion = 0f;
                    estadoInterfaz = EstadoInterfaz.Introduccion;
                    reloj.Restart();
                    break;
                case 1:
                    BaseDatosSolicitada?.Invoke();
                    break;
                case 2:
                    estadoInterfaz = EstadoInterfaz.Controles;
                    break;
                case 3:
                    FindForm()?.Close();
                    break;
            }
        }

        private void AvanzarIntroduccion()
        {
            if (lineaIntroduccion >= textoIntroduccion.Length - 1)
            {
                IniciarJuegoTrasIntroduccion();
                return;
            }

            lineaIntroduccion++;
            tiempoIntroduccion = lineaIntroduccion * DuracionLineaIntroduccion;
        }

        private void IniciarJuegoTrasIntroduccion()
        {
            estadoInterfaz = EstadoInterfaz.Jugando;
            tiempoIntroduccion = 0f;
            acumulador = 0d;
            reloj.Restart();
            NotificarMusicaMapaActual();
        }

        private void NotificarMusicaMapaActual() =>
            MusicaMapaSolicitada?.Invoke(mapaActual == MapaActivo.CiudadEnColapso ? 1 : 2);

        private void EjecutarOpcionPausa(int opcion)
        {
            switch (opcion)
            {
                case 0:
                    ReanudarPartida();
                    break;
                case 1:
                    ReiniciarPartida();
                    estadoInterfaz = EstadoInterfaz.Jugando;
                    reloj.Restart();
                    NotificarMusicaMapaActual();
                    break;
                case 2:
                    VolverAlMenu();
                    break;
                case 3:
                    GestionarPartidasSolicitada?.Invoke();
                    break;
            }
        }

        private void EjecutarOpcionResultado(int opcion)
        {
            if (opcion == 0)
            {
                ReiniciarPartida();
                estadoInterfaz = EstadoInterfaz.Jugando;
                reloj.Restart();
                NotificarMusicaMapaActual();
            }
            else
                VolverAlMenu();
        }

        private void ReanudarPartida()
        {
            LimpiarTeclasDeJuego();
            estadoInterfaz = EstadoInterfaz.Jugando;
            reloj.Restart();
        }

        private void VolverAlMenu()
        {
            ReiniciarPartida();
            LimpiarTeclasDeJuego();
            seleccionMenu = 0;
            estadoInterfaz = EstadoInterfaz.MenuPrincipal;
            reloj.Restart();
            MusicaMenuSolicitada?.Invoke(true);
        }

        private void LimpiarTeclasDeJuego()
        {
            izquierdaPulsada = false;
            derechaPulsada = false;
            saltoPulsado = false;
            teclaAtaquePulsada = false;
            teclaInteraccionPulsada = false;
        }

        private void CapturarTeclasMantenidas()
        {
            bloquearIzquierdaHastaSoltar |= izquierdaPulsada;
            bloquearDerechaHastaSoltar |= derechaPulsada;
            bloquearSaltoHastaSoltar |= saltoPulsado;
            bloquearAtaqueHastaSoltar |= teclaAtaquePulsada;
            bloquearInteraccionHastaSoltar |= teclaInteraccionPulsada;
        }

        private PointF ConvertirPuntoCliente(Point punto)
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return PointF.Empty;

            float escala = Math.Min(ClientSize.Width / AnchoVista, ClientSize.Height / AltoVista);
            float margenX = (ClientSize.Width - AnchoVista * escala) / 2f;
            float margenY = (ClientSize.Height - AltoVista * escala) / 2f;
            return new PointF((punto.X - margenX) / escala, (punto.Y - margenY) / escala);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.A:
                case Keys.Left:
                    izquierdaPulsada = false;
                    bloquearIzquierdaHastaSoltar = false;
                    e.Handled = true;
                    break;

                case Keys.D:
                case Keys.Right:
                    derechaPulsada = false;
                    bloquearDerechaHastaSoltar = false;
                    e.Handled = true;
                    break;

                case Keys.Space:
                    saltoPulsado = false;
                    bloquearSaltoHastaSoltar = false;
                    teclaConfirmacionPulsada = false;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.J:
                    teclaAtaquePulsada = false;
                    bloquearAtaqueHastaSoltar = false;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.E:
                    teclaInteraccionPulsada = false;
                    bloquearInteraccionHastaSoltar = false;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Enter:
                    teclaConfirmacionPulsada = false;
                    e.Handled = true;
                    break;

                case Keys.Escape:
                    teclaEscapePulsada = false;
                    e.Handled = true;
                    break;
            }

            base.OnKeyUp(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            izquierdaPulsada = false;
            derechaPulsada = false;
            saltoPulsado = false;
            teclaAtaquePulsada = false;
            teclaInteraccionPulsada = false;
            teclaConfirmacionPulsada = false;
            teclaEscapePulsada = false;
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.FromArgb(7, 10, 22));

            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;

            float escala = Math.Min(ClientSize.Width / AnchoVista, ClientSize.Height / AltoVista);
            float margenX = (ClientSize.Width - AnchoVista * escala) / 2f;
            float margenY = (ClientSize.Height - AltoVista * escala) / 2f;

            var estado = e.Graphics.Save();
            e.Graphics.TranslateTransform(margenX, margenY);
            e.Graphics.ScaleTransform(escala, escala);
            DibujarEscena(e.Graphics);
            e.Graphics.Restore(estado);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                temporizador.Stop();
                temporizador.Tick -= Temporizador_Tick;
                temporizador.Dispose();
                reloj.Stop();

                fondoBrush.Dispose();
                atmosferaBrush.Dispose();
                neblinaBrush.Dispose();
                sombraBrush.Dispose();
                panelBrush.Dispose();
                metalBrush.Dispose();
                detalleBrush.Dispose();
                sueloBrush.Dispose();
                pisoInteriorBrush.Dispose();
                ruinaLejanaBrush.Dispose();
                arquitecturaMediaBrush.Dispose();
                edificioLejanoBrush.Dispose();
                fachadaMediaBrush.Dispose();
                fachadaOscuraBrush.Dispose();
                residuoCianBrush.Dispose();
                residuoVioletaBrush.Dispose();
                residuoBlancoBrush.Dispose();
                residuoAzulBrush.Dispose();
                residuoCianTenueBrush.Dispose();
                residuoVioletaTenueBrush.Dispose();
                filtracionCianBrush.Dispose();
                filtracionVioletaBrush.Dispose();
                realidadBrumaBrush.Dispose();
                realidadFondoBrush.Dispose();
                realidadEstructuraBrush.Dispose();
                realidadFragmentoBrush.Dispose();
                vacioRealidadBrush.Dispose();
                plataformaRealidadBrush.Dispose();
                caraRealidadBrush.Dispose();
                peligroRealidadBrush.Dispose();
                overlayTransicionBrush.Dispose();
                fragmentoCianPen.Dispose();
                fragmentoVioletaPen.Dispose();
                filtracionCianPen.Dispose();
                filtracionVioletaPen.Dispose();
                realidadCianPen.Dispose();
                realidadVioletaPen.Dispose();
                realidadBlancoPen.Dispose();
                distorsionTransicionPen.Dispose();
                bordePlataformaRealidadPen.Dispose();
                peligroRealidadPen.Dispose();
                haloGrietaMenuPen.Dispose();
                grietaMenuCianPen.Dispose();
                grietaMenuVioletaPen.Dispose();
                recorteRealidadGrieta.Dispose();
                obsidianaBrush.Dispose();
                armaduraBrush.Dispose();
                placaBrush.Dispose();
                cianBrush.Dispose();
                cianLuzBrush.Dispose();
                violetaBrush.Dispose();
                blancoBrush.Dispose();
                cianPen.Dispose();
                violetaPen.Dispose();
                tenuePen.Dispose();
                grietaPen.Dispose();
                fachadaPen.Dispose();
                ataqueHaloPen.Dispose();
                ataqueCianPen.Dispose();
                destelloCombatePen.Dispose();
                haloVioletaGrietaPen.Dispose();
                haloCianGrietaPen.Dispose();
                bordeVioletaGrietaPen.Dispose();
                bordeCianGrietaPen.Dispose();
                nucleoGrietaPen.Dispose();
                siluetaPen.Dispose();
                extremidadPen.Dispose();
                articulacionPen.Dispose();
                filoCianPen.Dispose();
                filoVioletaPen.Dispose();
                visorInteriorPen.Dispose();
                tituloFont.Dispose();
                hudFont.Dispose();
                identificadorFont.Dispose();
                finalFont.Dispose();
                tituloMenuFont.Dispose();
                subtituloMenuFont.Dispose();
                opcionMenuFont.Dispose();
                controlesMenuFont.Dispose();
                overlayMenuBrush.Dispose();
                rellenoBotonBrush.Dispose();
                rellenoBotonSeleccionadoBrush.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Temporizador_Tick(object? sender, EventArgs e)
        {
            double delta = Math.Min(reloj.Elapsed.TotalSeconds, 0.05);
            reloj.Restart();

            if (estadoInterfaz == EstadoInterfaz.Pausa)
            {
                Invalidate();
                return;
            }

            tiempoAnimacion += delta;
            if (estadoInterfaz == EstadoInterfaz.MenuPrincipal ||
                estadoInterfaz == EstadoInterfaz.Controles)
            {
                Invalidate();
                return;
            }

            if (estadoInterfaz == EstadoInterfaz.Introduccion)
            {
                tiempoIntroduccion += (float)delta;
                lineaIntroduccion = Math.Min(textoIntroduccion.Length - 1,
                    (int)(tiempoIntroduccion / DuracionLineaIntroduccion));
                if (tiempoIntroduccion >= DuracionIntroduccion)
                    IniciarJuegoTrasIntroduccion();
                Invalidate();
                return;
            }

            ActualizarCombate((float)delta);
            if (!derrotado && !transicionMapaEnCurso)
            {
                RectangleF limitesJugador = new(jugadorX, jugadorY, AnchoJugador, AltoJugador);
                if (mapaActual == MapaActivo.CiudadEnColapso)
                {
                    ActualizarInteraccionGrieta((float)delta);
                    ecoEnZonaRegistro = limitesJugador.IntersectsWith(zonaRegistroCiudad);
                    ecoEnZonaObjetivo = false;
                }
                else
                {
                    ecoEnZonaObjetivo = limitesJugador.IntersectsWith(objetivoFinalRealidad);
                    ecoEnZonaRegistro = limitesJugador.IntersectsWith(zonaRegistroRealidad);
                }

                if (tiempoRegistroVisible > 0f)
                {
                    tiempoRegistroVisible = Math.Max(0f, tiempoRegistroVisible - (float)delta);
                    if (tiempoRegistroVisible == 0f)
                        registroNarrativoVisible = 0;
                }
            }

            ActualizarTransicionMapa((float)delta);

            float velocidadTransicion = Math.Min(1f, (float)delta * 9f);
            float objetivoFrontal = enSuelo && izquierdaPulsada == derechaPulsada ? 1f : 0f;
            mezclaFrontal += (objetivoFrontal - mezclaFrontal) * velocidadTransicion;

            acumulador += delta;

            while (acumulador >= PasoFijo)
            {
                ActualizarFisica(PasoFijo);
                acumulador -= PasoFijo;
            }

            ActualizarEnemigos((float)delta);

            Invalidate();
        }

        private void ActualizarEnemigos(float delta)
        {
            if (!PuedeMoverse)
                return;

            RectangleF limitesJugador = new(jugadorX, jugadorY, AnchoJugador, AltoJugador);
            float centroJugadorX = limitesJugador.Left + limitesJugador.Width * 0.5f;
            float centroJugadorY = limitesJugador.Top + limitesJugador.Height * 0.5f;

            foreach (Enemigo enemigo in EnemigosActivos)
            {
                enemigo.TiempoDestello = Math.Max(0f, enemigo.TiempoDestello - delta);

                if (!enemigo.Vivo)
                {
                    if (enemigo.TiempoDesintegracion > 0f)
                    {
                        enemigo.TiempoDesintegracion = Math.Max(0f,
                            enemigo.TiempoDesintegracion - delta);
                        enemigo.Fase += delta;
                    }
                    continue;
                }

                enemigo.Fase += delta;
                if (enemigo.Tipo == TipoEnemigo.Centinela)
                    ActualizarCentinela(enemigo, centroJugadorX, centroJugadorY, delta);
                else
                    ActualizarAnomalia(enemigo, centroJugadorX, centroJugadorY, delta);

                if (IntentarRegistrarImpactoAtaque(enemigo.Limites))
                    enemigo.RecibirDano(DanioAtaqueJugador);

                if (enemigo.Vivo && EnemigoAlcanzaJugador(enemigo, limitesJugador))
                {
                    int dano = enemigo.Tipo == TipoEnemigo.Centinela ? 15 : 10;
                    RecibirDano(dano);
                    if (derrotado)
                        return;
                }
            }

            if (mapaActual == MapaActivo.RealidadHostil)
            {
                RectangleF limitesActuales = new(jugadorX, jugadorY, AnchoJugador, AltoJugador);
                foreach (RectangleF peligro in peligrosRealidadHostil)
                {
                    if (limitesActuales.IntersectsWith(peligro))
                    {
                        RecibirDano(12);
                        break;
                    }
                }
            }
        }

        private static void ActualizarCentinela(Enemigo enemigo, float jugadorCentroX,
            float jugadorCentroY, float delta)
        {
            float enemigoCentroX = enemigo.X + enemigo.Ancho * 0.5f;
            float enemigoCentroY = enemigo.Y + enemigo.Alto * 0.5f;
            float diferenciaX = jugadorCentroX - enemigoCentroX;
            float diferenciaY = jugadorCentroY - enemigoCentroY;
            bool perseguir = Math.Abs(diferenciaY) <= 90f &&
                diferenciaX * diferenciaX + diferenciaY * diferenciaY <=
                    RadioDeteccionCentinela * RadioDeteccionCentinela;
            enemigo.EnAlerta = perseguir;

            if (perseguir && Math.Abs(diferenciaX) > 5f)
            {
                enemigo.Direccion = Math.Sign(diferenciaX);
                enemigo.X += enemigo.Direccion * 112f * delta;
            }
            else
            {
                float siguienteX = enemigo.X + enemigo.Direccion * 58f * delta;
                if (siguienteX < enemigo.LimiteIzquierdo ||
                    siguienteX + enemigo.Ancho > enemigo.LimiteDerecho)
                {
                    enemigo.Direccion *= -1f;
                    siguienteX = enemigo.X + enemigo.Direccion * 58f * delta;
                }

                enemigo.X = siguienteX;
            }

            enemigo.X = Math.Clamp(enemigo.X, enemigo.LimiteIzquierdo,
                enemigo.LimiteDerecho - enemigo.Ancho);
        }

        private void ActualizarAnomalia(Enemigo enemigo, float jugadorCentroX,
            float jugadorCentroY, float delta)
        {
            float centroX = enemigo.X + enemigo.Ancho * 0.5f;
            float centroY = enemigo.Y + enemigo.Alto * 0.5f;
            float diferenciaX = jugadorCentroX - centroX;
            float diferenciaY = jugadorCentroY - centroY;
            bool perseguir = diferenciaX * diferenciaX + diferenciaY * diferenciaY <= 430f * 430f;
            enemigo.EnAlerta = false;

            if (perseguir)
            {
                float vaivenX = (float)Math.Sin(enemigo.Fase * 2.1f) * 11f;
                float vaivenY = (float)Math.Sin(enemigo.Fase * 1.7f + 0.8f) * 9f;
                MoverHacia(ref centroX, jugadorCentroX + vaivenX, 92f * delta);
                MoverHacia(ref centroY, jugadorCentroY - 18f + vaivenY, 78f * delta);
                enemigo.X = centroX - enemigo.Ancho * 0.5f;
                enemigo.Y = centroY - enemigo.Alto * 0.5f;
            }
            else
            {
                enemigo.X = enemigo.PosicionInicialX
                    + (float)Math.Sin(enemigo.Fase * 0.43f) * 25f;
                enemigo.Y = enemigo.PosicionInicialY
                    + (float)Math.Sin(enemigo.Fase * 1.23f) * 22f
                    + (float)Math.Sin(enemigo.Fase * 0.61f + 1.2f) * 7f;
            }

            enemigo.X = Math.Clamp(enemigo.X, 0f, AnchoMapaActual - enemigo.Ancho);
            enemigo.Y = Math.Clamp(enemigo.Y, 115f, SueloY - enemigo.Alto - 10f);
        }

        private static void MoverHacia(ref float actual, float objetivo, float pasoMaximo)
        {
            actual += Math.Clamp(objetivo - actual, -pasoMaximo, pasoMaximo);
        }

        private static bool EnemigoAlcanzaJugador(Enemigo enemigo, RectangleF limitesJugador)
        {
            if (enemigo.Tipo == TipoEnemigo.Anomalia)
                return enemigo.Limites.IntersectsWith(limitesJugador);

            RectangleF zonaProximidad = limitesJugador;
            zonaProximidad.Inflate(14f, 3f);
            return enemigo.Limites.IntersectsWith(zonaProximidad);
        }

        private void ActualizarCombate(float delta)
        {
            invulnerabilidadRestante = Math.Max(0f, invulnerabilidadRestante - delta);
            ataqueActivoRestante = Math.Max(0f, ataqueActivoRestante - delta);
            reutilizacionAtaqueRestante = Math.Max(0f, reutilizacionAtaqueRestante - delta);
        }

        private void IniciarAtaque()
        {
            if (derrotado || demoCompletada || reutilizacionAtaqueRestante > 0f)
                return;

            ataqueMirandoDerecha = mirandoDerecha;
            ataqueImpactoResuelto = false;
            ataqueActivoRestante = DuracionAtaque;
            reutilizacionAtaqueRestante = TiempoReutilizacionAtaque;
        }

        /// <summary>
        /// Aplica daño al jugador. Invulnerabilidad evita más de un impacto por ventana temporal.
        /// Los sistemas de enemigos futuros pueden llamar a este método desde el hilo de UI.
        /// </summary>
        internal void RecibirDano(int cantidad)
        {
            if (cantidad <= 0 || derrotado || demoCompletada || invulnerabilidadRestante > 0f)
                return;

            integridadJugador = Math.Max(0, integridadJugador - cantidad);
            invulnerabilidadRestante = DuracionInvulnerabilidad;

            if (integridadJugador == 0)
            {
                derrotado = true;
                izquierdaPulsada = false;
                derechaPulsada = false;
                saltoPulsado = false;
                teclaAtaquePulsada = false;
                ataqueActivoRestante = 0f;
                velocidadY = 0f;
                ecoEnZonaGrieta = false;
            }
        }

        private RectangleF ObtenerZonaAtaque()
        {
            float x = ataqueMirandoDerecha
                ? jugadorX + AnchoJugador - 3f
                : jugadorX - AlcanceAtaque + 3f;
            return new RectangleF(x, jugadorY + 18f, AlcanceAtaque, 42f);
        }

        /// <summary>
        /// Registra como máximo un impacto por ataque cuando el objetivo cruza el alcance frontal.
        /// </summary>
        internal bool IntentarRegistrarImpactoAtaque(RectangleF limitesObjetivo)
        {
            if (ataqueActivoRestante <= 0f || ataqueImpactoResuelto || derrotado || demoCompletada)
                return false;

            if (!ObtenerZonaAtaque().IntersectsWith(limitesObjetivo))
                return false;

            ataqueImpactoResuelto = true;
            return true;
        }

        private void ActualizarInteraccionGrieta(float delta)
        {
            var limitesJugador = new RectangleF(jugadorX, jugadorY, AnchoJugador, AltoJugador);
            ecoEnZonaGrieta = !demoCompletada && limitesJugador.IntersectsWith(zonaInteraccionGrieta);

            float objetivoProximidad = ecoEnZonaGrieta ? 1f : 0f;
            float mezcla = Math.Min(1f, delta * 2.7f);
            factorProximidadGrieta += (objetivoProximidad - factorProximidadGrieta) * mezcla;

            switch (estadoTransicionGrieta)
            {
                case EstadoTransicionGrieta.Inactiva:
                    factorSecuenciaGrieta = 0f;
                    break;

                case EstadoTransicionGrieta.Activacion:
                    tiempoEtapaGrieta += delta;
                    factorSecuenciaGrieta = SuavizarProgreso(tiempoEtapaGrieta / 0.9f) * 0.26f;
                    if (tiempoEtapaGrieta >= 0.9f)
                    {
                        estadoTransicionGrieta = EstadoTransicionGrieta.Resonancia;
                        tiempoEtapaGrieta = 0f;
                    }
                    break;

                case EstadoTransicionGrieta.Resonancia:
                    tiempoEtapaGrieta += delta;
                    factorSecuenciaGrieta = 0.26f + SuavizarProgreso(tiempoEtapaGrieta / 1.8f) * 0.36f;
                    if (tiempoEtapaGrieta >= 1.8f)
                    {
                        estadoTransicionGrieta = EstadoTransicionGrieta.Convergencia;
                        tiempoEtapaGrieta = 0f;
                    }
                    break;

                case EstadoTransicionGrieta.Convergencia:
                    tiempoEtapaGrieta += delta;
                    factorSecuenciaGrieta = 0.62f + SuavizarProgreso(tiempoEtapaGrieta / 2.1f) * 0.38f;
                    if (tiempoEtapaGrieta >= 2.1f)
                    {
                        estadoTransicionGrieta = EstadoTransicionGrieta.Estabilizacion;
                        tiempoEtapaGrieta = 0f;
                        factorSecuenciaGrieta = 1f;
                    }
                    break;

                case EstadoTransicionGrieta.Estabilizacion:
                    factorSecuenciaGrieta = 1f;
                    if (mapaActual == MapaActivo.CiudadEnColapso && !transicionMapaRealizada)
                        IniciarTransicionMapa();
                    break;
            }

            respuestaVisualGrieta = Math.Max(factorSecuenciaGrieta, factorProximidadGrieta * 0.12f);
        }

        private void IniciarTransicionMapa()
        {
            if (mapaActual != MapaActivo.CiudadEnColapso || transicionMapaEnCurso || transicionMapaRealizada)
                return;

            transicionMapaEnCurso = true;
            tiempoTransicionMapa = 0f;
            izquierdaPulsada = false;
            derechaPulsada = false;
            saltoPulsado = false;
            teclaAtaquePulsada = false;
            velocidadY = 0f;
            ataqueActivoRestante = 0f;
            reloj.Restart();
        }

        private void ActualizarTransicionMapa(float delta)
        {
            if (!transicionMapaEnCurso)
                return;

            tiempoTransicionMapa += delta;
            if (!transicionMapaRealizada && tiempoTransicionMapa >= 0.65f)
            {
                transicionMapaRealizada = true;
                mapaActual = MapaActivo.RealidadHostil;
                NotificarMusicaMapaActual();
                jugadorX = 80f;
                jugadorY = plataformasRealidadHostil[0].Top - AltoJugador;
                velocidadY = 0f;
                enSuelo = true;
                camaraX = 0f;
                mirandoDerecha = true;
                mezclaFrontal = 1f;
                teclaInteraccionPulsada = false;
                ataqueActivoRestante = 0f;
                reutilizacionAtaqueRestante = 0f;
                ataqueImpactoResuelto = false;
                ecoEnZonaGrieta = false;
                ecoEnZonaObjetivo = false;
                ecoEnZonaRegistro = false;
                estadoTransicionGrieta = EstadoTransicionGrieta.Inactiva;
                tiempoEtapaGrieta = 0f;
                factorProximidadGrieta = 0f;
                factorSecuenciaGrieta = 0f;
                respuestaVisualGrieta = 0f;
                foreach (Enemigo enemigo in enemigosRealidadHostil)
                    enemigo.Reiniciar();
                acumulador = 0d;
                reloj.Restart();
            }

            if (tiempoTransicionMapa >= 1.3f)
            {
                transicionMapaEnCurso = false;
                tiempoTransicionMapa = 0f;
                reloj.Restart();
            }
        }

        private void CompletarDemo()
        {
            demoCompletada = true;
            izquierdaPulsada = false;
            derechaPulsada = false;
            saltoPulsado = false;
            teclaAtaquePulsada = false;
            velocidadY = 0f;
            ataqueActivoRestante = 0f;
            reutilizacionAtaqueRestante = 0f;
            enSuelo = true;
        }

        private void MostrarRegistroNarrativo(MapaActivo mapa)
        {
            if (mapa == MapaActivo.CiudadEnColapso)
            {
                if (registroCiudadLeido)
                    return;
                registroCiudadLeido = true;
                registroNarrativoVisible = 1;
            }
            else
            {
                if (registroRealidadLeido)
                    return;
                registroRealidadLeido = true;
                registroNarrativoVisible = 2;
            }

            tiempoRegistroVisible = DuracionRegistro;
        }

        private static float SuavizarProgreso(float progreso)
        {
            float t = Math.Clamp(progreso, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private void ActualizarFisica(float delta)
        {
            if (!PuedeMoverse)
                return;

            float pieAnterior = jugadorY + AltoJugador;
            float direccion = (derechaPulsada ? 1f : 0f) - (izquierdaPulsada ? 1f : 0f);
            jugadorX = Math.Clamp(
                jugadorX + direccion * VelocidadHorizontal * delta,
                0f,
                AnchoMapaActual - AnchoJugador);

            velocidadY += Gravedad * delta;
            jugadorY += velocidadY * delta;
            enSuelo = false;

            float pieActual = jugadorY + AltoJugador;
            float? alturaAterrizaje = null;
            float bordeIzquierdo = jugadorX;
            float bordeDerecho = jugadorX + AnchoJugador;

            if (velocidadY >= 0f)
            {
                foreach (RectangleF plataforma in PlataformasActivas)
                {
                    bool cruzaDesdeArriba = pieAnterior <= plataforma.Top &&
                                            pieActual >= plataforma.Top;
                    bool haySolapamientoHorizontal = bordeDerecho > plataforma.Left &&
                                                     bordeIzquierdo < plataforma.Right;

                    if (cruzaDesdeArriba && haySolapamientoHorizontal &&
                        (!alturaAterrizaje.HasValue || plataforma.Top < alturaAterrizaje.Value))
                    {
                        alturaAterrizaje = plataforma.Top;
                    }
                }
            }

            if (alturaAterrizaje.HasValue)
            {
                jugadorY = alturaAterrizaje.Value - AltoJugador;
                velocidadY = 0f;
                enSuelo = true;
            }
            else if (jugadorY > AltoVista + 160f)
            {
                if (mapaActual == MapaActivo.RealidadHostil)
                    RecibirDano(20);
                ReiniciarEnInicio();
                return;
            }

            if (new RectangleF(jugadorX, jugadorY, AnchoJugador, AltoJugador)
                .IntersectsWith(puertaFinal))
            {
                if (mapaActual == MapaActivo.CiudadEnColapso && !transicionMapaRealizada)
                    IniciarTransicionMapa();
            }

            float camaraObjetivo = Math.Clamp(
                jugadorX + AnchoJugador / 2f - AnchoVista * 0.42f,
                0f,
                AnchoMapaActual - AnchoVista);
            camaraX += (camaraObjetivo - camaraX) * Math.Min(1f, delta * 8f);
            if (Math.Abs(camaraObjetivo - camaraX) < 0.5f)
                camaraX = camaraObjetivo;
        }

        private void ReiniciarEnInicio()
        {
            jugadorX = mapaActual == MapaActivo.CiudadEnColapso ? 150f : 80f;
            jugadorY = PlataformasActivas[0].Top - AltoJugador;
            velocidadY = 0f;
            enSuelo = true;
            camaraX = 0f;
        }

        private void ReiniciarPartida()
        {
            mapaActual = MapaActivo.CiudadEnColapso;
            transicionMapaEnCurso = false;
            transicionMapaRealizada = false;
            tiempoTransicionMapa = 0f;
            ReiniciarEnInicio();
            integridadJugador = IntegridadMaximaJugador;
            invulnerabilidadRestante = 0f;
            ataqueActivoRestante = 0f;
            reutilizacionAtaqueRestante = 0f;
            ataqueMirandoDerecha = true;
            ataqueImpactoResuelto = false;
            izquierdaPulsada = false;
            derechaPulsada = false;
            saltoPulsado = false;
            teclaAtaquePulsada = false;
            teclaInteraccionPulsada = false;
            mirandoDerecha = true;
            mezclaFrontal = 1f;
            demoCompletada = false;
            derrotado = false;
            foreach (Enemigo enemigo in enemigosCiudad)
                enemigo.Reiniciar();
            foreach (Enemigo enemigo in enemigosRealidadHostil)
                enemigo.Reiniciar();
            estadoTransicionGrieta = EstadoTransicionGrieta.Inactiva;
            tiempoEtapaGrieta = 0f;
            factorProximidadGrieta = 0f;
            factorSecuenciaGrieta = 0f;
            respuestaVisualGrieta = 0f;
            ecoEnZonaGrieta = false;
            ecoEnZonaObjetivo = false;
            ecoEnZonaRegistro = false;
            registroCiudadLeido = false;
            registroRealidadLeido = false;
            registroNarrativoVisible = 0;
            tiempoRegistroVisible = 0f;
            acumulador = 0d;
            tiempoAnimacion = 0d;
            reloj.Restart();
        }

        private void DibujarEscena(Graphics g)
        {
            if (estadoInterfaz == EstadoInterfaz.MenuPrincipal)
            {
                DibujarMenuPrincipal(g);
                return;
            }
            if (estadoInterfaz == EstadoInterfaz.Controles)
            {
                DibujarMenuControles(g);
                return;
            }
            if (estadoInterfaz == EstadoInterfaz.Introduccion)
            {
                DibujarIntroduccion(g);
                return;
            }

            g.FillRectangle(fondoBrush, 0, 0, AnchoVista, AltoVista);
            if (mapaActual == MapaActivo.CiudadEnColapso)
                DibujarFondoTecnologico(g);
            else
                DibujarFondoRealidadHostil(g);

            var estadoMundo = g.Save();
            g.TranslateTransform(-camaraX, 0f);
            if (mapaActual == MapaActivo.CiudadEnColapso)
            {
                DibujarSuelo(g);
                DibujarPuerta(g);
            }
            else
            {
                DibujarPlataformasHostiles(g);
                DibujarPeligrosHostiles(g);
                DibujarObjetivoFinal(g);
            }
            DibujarEnemigos(g);
            DibujarJugador(g);
            DibujarAtaqueJugador(g);
            g.Restore(estadoMundo);

            DibujarTransicionDimensional(g);
            DibujarInterfaz(g);
        }

        private void DibujarFondoMenu(Graphics g)
        {
            overlayMenuBrush.Color = Color.FromArgb(206, 4, 7, 18);
            g.FillRectangle(fondoBrush, 0f, 0f, AnchoVista, AltoVista);
            DibujarFondoTecnologico(g);
            g.FillRectangle(overlayMenuBrush, 0f, 0f, AnchoVista, AltoVista);
            DibujarAmbienteMenu(g);
            g.DrawLine(cianPen, 98f, 302f, 1182f, 302f);
            g.DrawLine(violetaPen, 98f, 305f, 410f, 305f);
            g.DrawLine(tenuePen, 118f, 636f, 1162f, 636f);
            g.DrawString("NOCTRYX", tituloMenuFont, cianBrush, 118f, 116f);
            g.DrawString("SYSTEM COLLAPSE", subtituloMenuFont, blancoBrush, 126f, 204f);
            g.DrawString("PROTOCOLO DE SUPERVIVENCIA  //  SECTOR 01",
                hudFont, violetaBrush, 128f, 260f);
        }

        private void DibujarAmbienteMenu(Graphics g)
        {
            float pulso = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 1.1);
            float centroX = 1015f + (float)Math.Sin(tiempoAnimacion * 0.17) * 9f;
            haloGrietaMenuPen.Width = 13f + pulso * 7f;
            haloGrietaMenuPen.Color = Color.FromArgb(15 + (int)(pulso * 17f), 0, 207, 255);

            for (int i = 0; i < trazadoGrietaMenu.Length; i++)
            {
                float t = i / (float)(trazadoGrietaMenu.Length - 1);
                float y = 42f + t * 286f;
                float zigzag = ((i & 1) == 0 ? -1f : 1f) * (17f + (i % 3) * 8f);
                float deriva = (float)Math.Sin(tiempoAnimacion * 0.42 + i * 1.3) * 3.5f;
                trazadoGrietaMenu[i] = new PointF(centroX + zigzag + deriva, y);
            }

            for (int i = 0; i < trazadoGrietaMenu.Length - 1; i++)
            {
                g.DrawLine(haloGrietaMenuPen, trazadoGrietaMenu[i], trazadoGrietaMenu[i + 1]);
                Pen borde = (i % 3 == 1) ? grietaMenuCianPen : grietaMenuVioletaPen;
                g.DrawLine(borde, trazadoGrietaMenu[i], trazadoGrietaMenu[i + 1]);
            }

            for (int i = 0; i < particulasMenuX.Length; i++)
            {
                float fase = (float)(tiempoAnimacion * 0.52) + particulasMenuFase[i];
                float x = particulasMenuX[i] + (float)Math.Sin(fase * 0.7f) * 5f;
                float y = particulasMenuY[i] + (float)Math.Sin(fase) * 8f;
                float tamano = 2f + (i % 3);
                Brush brillo = (i % 3) switch
                {
                    0 => residuoCianBrush,
                    1 => residuoVioletaBrush,
                    _ => residuoBlancoBrush
                };
                g.FillEllipse(brillo, x, y, tamano, tamano);
            }

            // Un fragmento angular de baja opacidad acompaña el pulso de la grieta.
            PointF fragmento = trazadoGrietaMenu[3];
            g.DrawLine(grietaMenuCianPen, fragmento.X - 20f, fragmento.Y - 13f,
                fragmento.X + 8f, fragmento.Y - 20f);
            g.DrawLine(grietaMenuVioletaPen, fragmento.X + 8f, fragmento.Y - 20f,
                fragmento.X + 18f, fragmento.Y + 7f);
            g.DrawLine(grietaMenuCianPen, fragmento.X + 18f, fragmento.Y + 7f,
                fragmento.X - 11f, fragmento.Y + 14f);
        }

        private void DibujarMenuPrincipal(Graphics g)
        {
            DibujarFondoMenu(g);
            DibujarBotonInterfaz(g, botonesMenuPrincipal[0], "JUGAR", seleccionMenu == 0);
            DibujarBotonInterfaz(g, botonesMenuPrincipal[1], "PARTIDAS GUARDADAS", seleccionMenu == 1);
            DibujarBotonInterfaz(g, botonesMenuPrincipal[2], "CONTROLES", seleccionMenu == 2);
            DibujarBotonInterfaz(g, botonesMenuPrincipal[3], "SALIR", seleccionMenu == 3);
            g.DrawString("ENTER / CLIC PARA SELECCIONAR    ↑ ↓ NAVEGAR",
                controlesMenuFont, blancoBrush, 430f, 658f);
        }

        private void DibujarMenuControles(Graphics g)
        {
            DibujarFondoMenu(g);
            g.DrawString("CONTROLES", subtituloMenuFont, cianBrush, 492f, 330f);
            g.DrawString("A / ←   MOVER A LA IZQUIERDA", controlesMenuFont, blancoBrush, 430f, 374f);
            g.DrawString("D / →   MOVER A LA DERECHA", controlesMenuFont, blancoBrush, 430f, 406f);
            g.DrawString("ESPACIO  SALTAR", controlesMenuFont, blancoBrush, 430f, 438f);
            g.DrawString("J        ATACAR", controlesMenuFont, blancoBrush, 430f, 470f);
            g.DrawString("E        INTERACTUAR", controlesMenuFont, blancoBrush, 430f, 502f);
            g.DrawString("ESC      PAUSAR / CONTINUAR", controlesMenuFont, blancoBrush, 430f, 534f);
            DibujarBotonInterfaz(g, botonVolverControles, "VOLVER", true);
        }

        private void DibujarIntroduccion(Graphics g)
        {
            g.FillRectangle(fondoBrush, 0f, 0f, AnchoVista, AltoVista);
            DibujarFondoTecnologico(g);
            overlayMenuBrush.Color = Color.FromArgb(218, 4, 7, 18);
            g.FillRectangle(overlayMenuBrush, 0f, 0f, AnchoVista, AltoVista);
            g.DrawLine(cianPen, 140f, 242f, 1140f, 242f);
            g.DrawLine(violetaPen, 140f, 245f, 420f, 245f);
            g.DrawString("SISTEMA NOCTRYX // PROTOCOLO DE EMERGENCIA",
                subtituloMenuFont, cianLuzBrush, 140f, 195f);

            string linea = textoIntroduccion[Math.Clamp(lineaIntroduccion, 0, textoIntroduccion.Length - 1)];
            SizeF medida = g.MeasureString(linea, subtituloMenuFont);
            g.DrawString(linea, subtituloMenuFont, blancoBrush,
                (AnchoVista - medida.Width) * 0.5f, 344f);

            float anchoProgreso = 1000f * Math.Clamp(tiempoIntroduccion / DuracionIntroduccion, 0f, 1f);
            g.FillRectangle(sombraBrush, 140f, 418f, 1000f, 4f);
            if (anchoProgreso > 0f)
                g.FillRectangle(cianBrush, 140f, 418f, anchoProgreso, 3f);
            g.DrawString("ENTER / ESPACIO  CONTINUAR       ESC  OMITIR",
                controlesMenuFont, violetaBrush, 386f, 474f);
        }

        private void DibujarBotonInterfaz(Graphics g, RectangleF limites, string texto, bool seleccionado)
        {
            g.FillRectangle(seleccionado ? rellenoBotonSeleccionadoBrush : rellenoBotonBrush, limites);
            g.DrawRectangle(seleccionado ? cianPen : tenuePen,
                limites.X, limites.Y, limites.Width, limites.Height);
            SizeF tamano = g.MeasureString(texto, opcionMenuFont);
            g.DrawString(texto, opcionMenuFont,
                seleccionado ? cianLuzBrush : blancoBrush,
                limites.Left + (limites.Width - tamano.Width) * 0.5f,
                limites.Top + (limites.Height - tamano.Height) * 0.5f);
        }

        private void DibujarPanelPausa(Graphics g)
        {
            overlayMenuBrush.Color = Color.FromArgb(188, 4, 7, 18);
            g.FillRectangle(overlayMenuBrush, 0f, 0f, AnchoVista, AltoVista);
            g.DrawString("PAUSA", subtituloMenuFont, cianBrush, 572f, 262f);
            DibujarBotonInterfaz(g, botonesPausa[0], "CONTINUAR", seleccionPausa == 0);
            DibujarBotonInterfaz(g, botonesPausa[1], "REINICIAR", seleccionPausa == 1);
            DibujarBotonInterfaz(g, botonesPausa[2], "VOLVER AL MENÚ", seleccionPausa == 2);
            DibujarBotonInterfaz(g, botonesPausa[3], "GUARDAR / CARGAR", seleccionPausa == 3);
        }

        private void DibujarFondoRealidadHostil(Graphics g)
        {
            g.FillRectangle(vacioRealidadBrush, 0f, 0f, AnchoVista, AltoVista);
            g.FillRectangle(atmosferaBrush, 0f, 0f, AnchoVista, AltoVista);
            g.FillRectangle(residuoVioletaTenueBrush, 0f, 180f, AnchoVista, 220f);

            var estadoLejano = g.Save();
            g.TranslateTransform(-camaraX * 0.12f, 0f);
            for (int i = 0; i < fragmentosHostiles.Length; i++)
            {
                float deriva = (float)Math.Sin(tiempoAnimacion * (0.22f + i * 0.025f) + i * 1.7f);
                float x = fragmentoHostilX[i] + deriva * 18f;
                float y = fragmentoHostilY[i] + (float)Math.Sin(tiempoAnimacion * 0.31f + i) * 11f;
                PointF[] forma = fragmentosHostiles[i];
                forma[0] = new PointF(x - 34f, y + 15f);
                forma[1] = new PointF(x - 10f, y - 30f);
                forma[2] = new PointF(x + 16f, y - 17f);
                forma[3] = new PointF(x + 39f, y + 12f);
                forma[4] = new PointF(x + 4f, y + 29f);
                g.FillPolygon(realidadFondoBrush, forma);
                g.DrawPolygon(i % 2 == 0 ? realidadCianPen : realidadVioletaPen, forma);
            }

            // Pilares invertidos y planos quebrados distinguen esta dimensión de la ciudad.
            for (int i = 0; i < 5; i++)
            {
                float x = 170f + i * 250f;
                float fase = (float)Math.Sin(tiempoAnimacion * 0.24f + i * 1.3f) * 9f;
                PointF[] columna = columnasHostiles[i];
                columna[0] = new PointF(x + fase, 74f);
                columna[1] = new PointF(x + 48f, 106f);
                columna[2] = new PointF(x + 24f, 318f);
                columna[3] = new PointF(x - 12f, 279f);
                g.FillPolygon(realidadEstructuraBrush, columna);
                g.DrawLine(realidadBlancoPen, x + fase, 74f, x + 48f, 106f);
                g.DrawLine(realidadCianPen, x + 24f, 318f, x - 12f, 279f);
            }
            g.Restore(estadoLejano);

            var estadoMedio = g.Save();
            g.TranslateTransform(-camaraX * 0.38f, 0f);
            for (int i = 0; i < 4; i++)
            {
                float x = 260f + i * 390f;
                float desplazamiento = (float)Math.Sin(tiempoAnimacion * 0.42f + i) * 7f;
                g.DrawLine(i % 2 == 0 ? realidadVioletaPen : realidadCianPen,
                    x, 330f + desplazamiento, x + 86f, 275f - desplazamiento);
                g.DrawLine(realidadBlancoPen, x + 86f, 275f - desplazamiento,
                    x + 138f, 306f + desplazamiento);
                g.DrawLine(realidadCianPen, x + 138f, 306f + desplazamiento,
                    x + 196f, 232f);
            }
            g.Restore(estadoMedio);
            g.FillRectangle(neblinaBrush, 0f, 350f, AnchoVista, 250f);
        }

        private void DibujarPlataformasHostiles(Graphics g)
        {
            foreach (RectangleF plataforma in plataformasRealidadHostil)
            {
                g.FillRectangle(plataformaRealidadBrush, plataforma);
                g.FillRectangle(caraRealidadBrush, plataforma.Left + 5f, plataforma.Top + 4f,
                    plataforma.Width - 10f, Math.Max(8f, plataforma.Height - 4f));
                g.DrawLine(bordePlataformaRealidadPen, plataforma.Left, plataforma.Top,
                    plataforma.Right - 12f, plataforma.Top);
                g.DrawLine(realidadVioletaPen, plataforma.Right - 12f, plataforma.Top,
                    plataforma.Right, plataforma.Top + 8f);
                g.DrawLine(realidadCianPen, plataforma.Left + 8f, plataforma.Bottom,
                    plataforma.Left + 28f, plataforma.Bottom + 11f);
                g.DrawLine(realidadVioletaPen, plataforma.Right - 9f, plataforma.Bottom,
                    plataforma.Right - 28f, plataforma.Bottom + 13f);
            }
        }

        private void DibujarPeligrosHostiles(Graphics g)
        {
            foreach (RectangleF peligro in peligrosRealidadHostil)
            {
                g.FillRectangle(peligroRealidadBrush, peligro);
                g.DrawLine(peligroRealidadPen, peligro.Left, peligro.Bottom,
                    peligro.Left + 12f, peligro.Top);
                g.DrawLine(peligroRealidadPen, peligro.Left + 12f, peligro.Top,
                    peligro.Left + 25f, peligro.Bottom);
                g.DrawLine(peligroRealidadPen, peligro.Left + 25f, peligro.Bottom,
                    peligro.Right, peligro.Top + 2f);
            }
        }

        private void DibujarObjetivoFinal(Graphics g)
        {
            RectangleF objetivo = objetivoFinalRealidad;
            float pulso = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 1.15f);
            g.FillRectangle(residuoVioletaTenueBrush, objetivo.X - 12f, objetivo.Y - 10f,
                objetivo.Width + 24f, objetivo.Height + 20f);
            g.FillRectangle(obsidianaBrush, objetivo);
            g.DrawRectangle(violetaPen, objetivo.X, objetivo.Y, objetivo.Width, objetivo.Height);
            g.DrawLine(realidadCianPen, objetivo.Left + 13f, objetivo.Top + 18f,
                objetivo.Left + 38f, objetivo.Top + 48f);
            g.DrawLine(realidadBlancoPen, objetivo.Left + 38f, objetivo.Top + 48f,
                objetivo.Left + 22f, objetivo.Top + 79f);
            g.DrawLine(realidadVioletaPen, objetivo.Left + 22f, objetivo.Top + 79f,
                objetivo.Right - 12f, objetivo.Bottom - 15f);
            if (pulso > 0.55f)
                g.FillRectangle(cianLuzBrush, objetivo.Left + 9f, objetivo.Bottom - 22f,
                    objetivo.Width - 18f, 3f);
            g.DrawString("SEÑAL", identificadorFont, blancoBrush,
                objetivo.Left + 7f, objetivo.Top - 24f);
        }

        private void DibujarTransicionDimensional(Graphics g)
        {
            if (!transicionMapaEnCurso)
                return;

            float progreso = Math.Clamp(tiempoTransicionMapa / 1.3f, 0f, 1f);
            float intensidad = progreso < 0.5f ? progreso * 2f : (1f - progreso) * 2f;
            int alfa = (int)(Math.Clamp(intensidad, 0f, 1f) * 150f);
            overlayTransicionBrush.Color = Color.FromArgb(alfa, 8, 9, 31);
            distorsionTransicionPen.Color = Color.FromArgb(Math.Min(230, alfa + 50), 84, 210, 255);
            g.FillRectangle(overlayTransicionBrush, 0f, 0f, AnchoVista, AltoVista);
            for (int i = 0; i < 9; i++)
            {
                float y = 135f + i * 57f;
                float onda = (float)Math.Sin(tiempoAnimacion * 9f + i * 1.8f) * 18f * intensidad;
                g.DrawLine(distorsionTransicionPen, -30f + onda, y,
                    AnchoVista * 0.42f, y - onda * 0.35f);
                g.DrawLine(distorsionTransicionPen, AnchoVista * 0.58f, y + onda * 0.35f,
                    AnchoVista + 30f - onda, y);
            }

            DibujarTextoCentrado(g, "FRACTURA DIMENSIONAL DETECTADA",
                opcionMenuFont, cianLuzBrush, 500f);
            DibujarTextoCentrado(g, "Sincronizando realidad...",
                controlesMenuFont, blancoBrush, 532f);
            DibujarTextoCentrado(g, "ADVERTENCIA: IDENTIDAD INESTABLE",
                opcionMenuFont, violetaBrush, 560f);
        }

        private static void DibujarTextoCentrado(Graphics g, string texto, Font fuente,
            Brush color, float y)
        {
            SizeF medida = g.MeasureString(texto, fuente);
            g.DrawString(texto, fuente, color, (AnchoVista - medida.Width) * 0.5f, y);
        }

        private void DibujarFondoTecnologico(Graphics g)
        {
            g.FillRectangle(atmosferaBrush, 0, 0, AnchoVista, AltoVista);
            g.FillRectangle(sombraBrush, 0, 500f, AnchoVista, 220f);

            var estadoLejano = g.Save();
            g.TranslateTransform(-camaraX * 0.12f, 0f);
            DibujarGrietaDimensional(g);
            DibujarEscombrosSuspendidos(g);
            DibujarDistorsionesEspaciales(g);
            DibujarHorizonteIndustrial(g);
            DibujarReflejoGrietaEnEdificios(g);
            DibujarPuentesLejanos(g);
            DibujarHitoResonador(g);
            g.Restore(estadoLejano);

            // La niebla cae entre la megainstalación lejana y el plano de maquinaria.
            g.FillRectangle(neblinaBrush, 0, 205f, AnchoVista, 350f);

            var estadoMedio = g.Save();
            g.TranslateTransform(-camaraX * 0.38f, 0f);
            DibujarArmazonAbandonado(g);
            DibujarCamaraContencion(g);
            g.Restore(estadoMedio);

            // Una segunda veladura tenue separa los conductos cercanos del conjunto central.
            g.FillRectangle(neblinaBrush, 0, 285f, AnchoVista, 270f);

            var estadoParticulas = g.Save();
            g.TranslateTransform(-camaraX * 0.38f, 0f);
            DibujarResiduosDigitales(g);
            DibujarFiltracionesDimension(g);
            g.Restore(estadoParticulas);

            var estadoCercano = g.Save();
            g.TranslateTransform(-camaraX * 0.72f, 0f);
            DibujarConducciones(g);
            g.Restore(estadoCercano);
        }

        private void DibujarGrietaDimensional(Graphics g)
        {
            // Abertura asimétrica en el plano distante; las fachadas la ocluyen de forma natural.
            const float centroX = 790f;
            const float arribaY = 12f;
            float pulso = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 0.72f);
            float amplitud = 0.96f + pulso * 0.04f;
            const float centroY = 262f;

            for (int i = 0; i < grietaAlturas.Length; i++)
            {
                float y = arribaY + grietaAlturas[i];
                bordeIzquierdoGrieta[i] = new PointF(centroX + grietaIzquierdaX[i] * amplitud, y);
                bordeDerechoGrieta[i] = new PointF(centroX + grietaDerechaX[i] * amplitud, y);
                contornoGrieta[i] = bordeIzquierdoGrieta[i];
                contornoGrieta[contornoGrieta.Length - 1 - i] = bordeDerechoGrieta[i];

                cavidadProfundaGrieta[i] = new PointF(
                    centroX + grietaIzquierdaX[i] * amplitud * 0.56f,
                    centroY + (y - centroY) * 0.91f);
                cavidadProfundaGrieta[cavidadProfundaGrieta.Length - 1 - i] = new PointF(
                    centroX + grietaDerechaX[i] * amplitud * 0.56f,
                    centroY + (y - centroY) * 0.91f);
            }

            // Dos vacíos superpuestos dan profundidad y un borde de anchura cambiante.
            g.FillPolygon(sombraBrush, contornoGrieta);
            g.FillPolygon(obsidianaBrush, cavidadProfundaGrieta);
            DibujarRealidadAlternativaDentroDeGrieta(g, centroX);

            // Halo por tramos: evita el contorno uniforme y deja ver pliegues oscuros.
            haloVioletaGrietaPen.Width = 88f + pulso * 14f + respuestaVisualGrieta * 28f;
            haloCianGrietaPen.Width = 38f + pulso * 8f + respuestaVisualGrieta * 14f;
            haloVioletaGrietaPen.Color = Color.FromArgb(
                24 + (int)(respuestaVisualGrieta * 34f), 165, 70, 255);
            haloCianGrietaPen.Color = Color.FromArgb(
                30 + (int)(respuestaVisualGrieta * 32f), 0, 195, 255);
            bordeVioletaGrietaPen.Color = Color.FromArgb(
                175 + (int)(respuestaVisualGrieta * 24f), 178, 78, 255);
            bordeCianGrietaPen.Color = Color.FromArgb(
                190 + (int)(respuestaVisualGrieta * 24f), 0, 205, 255);
            for (int i = 0; i < bordeIzquierdoGrieta.Length - 1; i++)
            {
                if (i == 0 || i == 1 || i == 6 || i == 7)
                    g.DrawLine(haloVioletaGrietaPen, bordeIzquierdoGrieta[i], bordeIzquierdoGrieta[i + 1]);
                else if (i == 3 || i == 4)
                    g.DrawLine(haloCianGrietaPen, bordeIzquierdoGrieta[i], bordeIzquierdoGrieta[i + 1]);

                if (i == 1 || i == 2 || i == 8 || i == 9)
                    g.DrawLine(haloCianGrietaPen, bordeDerechoGrieta[i], bordeDerechoGrieta[i + 1]);
                else if (i == 5 || i == 6)
                    g.DrawLine(haloVioletaGrietaPen, bordeDerechoGrieta[i], bordeDerechoGrieta[i + 1]);
            }

            // Pliegues angulosos y discontinuos en las dos orillas.
            for (int i = 0; i < bordeIzquierdoGrieta.Length - 1; i++)
            {
                if (i != 2 && i != 6 && i != 9)
                {
                    Pen borde = (i % 3 == 0) ? bordeCianGrietaPen : bordeVioletaGrietaPen;
                    g.DrawLine(borde, bordeIzquierdoGrieta[i], bordeIzquierdoGrieta[i + 1]);
                }

                if (i != 1 && i != 5 && i != 8)
                {
                    Pen borde = (i % 4 == 1) ? bordeCianGrietaPen : bordeVioletaGrietaPen;
                    g.DrawLine(borde, bordeDerechoGrieta[i], bordeDerechoGrieta[i + 1]);
                }
            }

            // Finas caras internas cruzan el vacío de forma desigual, como planos desplazados.
            g.DrawLine(nucleoGrietaPen, bordeIzquierdoGrieta[2], bordeDerechoGrieta[3]);
            g.DrawLine(bordeCianGrietaPen, bordeDerechoGrieta[5], bordeIzquierdoGrieta[6]);
            g.DrawLine(bordeVioletaGrietaPen, bordeIzquierdoGrieta[8], bordeDerechoGrieta[9]);

            // Fragmentos de geometría separados del borde; tamaños y orientaciones no repetidos.
            PrepararFragmentoGrieta(fragmentoGrietaUno, centroX - 205f, arribaY + 126f, 1.00f, pulso, false);
            PrepararFragmentoGrieta(fragmentoGrietaDos, centroX + 214f, arribaY + 92f, 0.72f, pulso, true);
            PrepararFragmentoGrieta(fragmentoGrietaTres, centroX + 236f, arribaY + 278f, 1.18f, pulso, false);
            PrepararFragmentoGrieta(fragmentoGrietaCuatro, centroX - 194f, arribaY + 338f, 0.62f, pulso, true);
            PrepararFragmentoGrieta(fragmentoGrietaCinco, centroX + 93f, arribaY + 452f, 0.58f, pulso, false);
            DibujarFragmentoGrieta(g, fragmentoGrietaUno, bordeVioletaGrietaPen);
            DibujarFragmentoGrieta(g, fragmentoGrietaDos, bordeCianGrietaPen);
            DibujarFragmentoGrieta(g, fragmentoGrietaTres, bordeCianGrietaPen);
            DibujarFragmentoGrieta(g, fragmentoGrietaCuatro, bordeVioletaGrietaPen);
            DibujarFragmentoGrieta(g, fragmentoGrietaCinco, bordeVioletaGrietaPen);
        }

        private void DibujarRealidadAlternativaDentroDeGrieta(Graphics g, float centroX)
        {
            // El paisaje ajeno queda recortado al vacío ya aprobado de la fractura.
            recorteRealidadGrieta.Reset();
            recorteRealidadGrieta.AddPolygon(cavidadProfundaGrieta);
            var estado = g.Save();
            g.SetClip(recorteRealidadGrieta, CombineMode.Intersect);
            g.FillRectangle(realidadBrumaBrush, centroX - 190f, 0f, 380f, 520f);
            realidadFondoBrush.Color = Color.FromArgb(
                72 + (int)(respuestaVisualGrieta * 16f), 35, 68, 126);
            realidadEstructuraBrush.Color = Color.FromArgb(
                116 + (int)(respuestaVisualGrieta * 40f), 41, 98, 166);
            realidadFragmentoBrush.Color = Color.FromArgb(
                104 + (int)(respuestaVisualGrieta * 36f), 94, 74, 180);
            realidadCianPen.Width = 1.2f + respuestaVisualGrieta * 0.5f;
            realidadVioletaPen.Width = 1f + respuestaVisualGrieta * 0.4f;
            realidadBlancoPen.Width = 0.8f + respuestaVisualGrieta * 0.3f;
            realidadCianPen.Color = Color.FromArgb(
                150 + (int)(respuestaVisualGrieta * 32f), 82, 211, 241);
            realidadVioletaPen.Color = Color.FromArgb(
                125 + (int)(respuestaVisualGrieta * 34f), 158, 129, 242);
            realidadBlancoPen.Color = Color.FromArgb(
                120 + (int)(respuestaVisualGrieta * 36f), 175, 216, 239);

            // Planos lejanos: obeliscos y cintas suspendidas, sin fachadas ni retícula industrial.
            for (int indice = 0; indice < formasRealidadAlternativa.Length; indice++)
            {
                float fase = (float)tiempoAnimacion;
                float angulo = (float)Math.Sin(fase * 0.16f + indice * 1.37f)
                    * (0.07f + respuestaVisualGrieta * 0.05f);
                float coseno = (float)Math.Cos(angulo);
                float seno = (float)Math.Sin(angulo);
                float derivaX = (float)Math.Sin(fase * 0.21f + indice * 1.6f)
                    * (3.2f + respuestaVisualGrieta * 3f);
                float derivaY = (float)Math.Sin(fase * 0.18f + indice * 0.9f)
                    * (4.5f + respuestaVisualGrieta * 3.5f);
                PointF[] forma = formasRealidadAlternativa[indice];

                for (int vertice = 0; vertice < forma.Length; vertice++)
                {
                    float localX = realidadFormaX[indice][vertice] * realidadAncho[indice];
                    float localY = realidadFormaY[indice][vertice] * realidadAlto[indice];
                    forma[vertice] = new PointF(
                        realidadX[indice] + derivaX + localX * coseno - localY * seno,
                        realidadY[indice] + derivaY + localX * seno + localY * coseno);
                }

                Brush material = indice < 2 ? realidadFondoBrush
                    : indice == 3 ? realidadFragmentoBrush : realidadEstructuraBrush;
                g.FillPolygon(material, forma);
                if (indice == 0 || indice == 2 || indice == 4)
                    g.DrawPolygon(indice == 2 ? realidadVioletaPen : realidadCianPen, forma);
                if (indice == 1 || indice == 3)
                    g.DrawLine(realidadBlancoPen, forma[0], forma[2]);
            }

            // Una deriva luminosa tenue separa el plano más profundo de los prismas próximos.
            float pulso = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 0.39f);
            g.DrawLine(realidadBlancoPen,
                centroX - 8f, 106f + pulso * 3f, centroX + 11f, 79f + pulso * 3f);
            g.DrawLine(realidadCianPen,
                centroX + 18f, 291f - pulso * 4f, centroX - 12f, 316f - pulso * 4f);
            g.Restore(estado);
        }

        private static void PrepararFragmentoGrieta(PointF[] fragmento, float x, float y,
            float escala, float pulso, bool inclinado)
        {
            float deriva = (pulso - 0.5f) * 3f;
            if (inclinado)
            {
                fragmento[0] = new PointF(x - 5f * escala, y - 10f * escala + deriva);
                fragmento[1] = new PointF(x + 13f * escala, y - 3f * escala);
                fragmento[2] = new PointF(x + 4f * escala, y + 15f * escala - deriva);
                fragmento[3] = new PointF(x - 12f * escala, y + 5f * escala);
            }
            else
            {
                fragmento[0] = new PointF(x - 11f * escala, y - 5f * escala);
                fragmento[1] = new PointF(x + 5f * escala, y - 13f * escala + deriva);
                fragmento[2] = new PointF(x + 12f * escala, y + 7f * escala);
                fragmento[3] = new PointF(x - 3f * escala, y + 12f * escala - deriva);
            }
        }

        private void DibujarFragmentoGrieta(Graphics g, PointF[] fragmento, Pen borde)
        {
            g.FillPolygon(obsidianaBrush, fragmento);
            g.DrawPolygon(borde, fragmento);
        }

        private void DibujarReflejoGrietaEnEdificios(Graphics g)
        {
            // Velos violetas muy tenues en las fachadas próximas a la fractura.
            reflejoGrietaEdificioIzquierdo[0] = new PointF(674f, 220f);
            reflejoGrietaEdificioIzquierdo[1] = new PointF(709f, 210f);
            reflejoGrietaEdificioIzquierdo[2] = new PointF(709f, 407f);
            reflejoGrietaEdificioIzquierdo[3] = new PointF(689f, 384f);
            g.FillPolygon(residuoVioletaTenueBrush, reflejoGrietaEdificioIzquierdo);

            reflejoGrietaEdificioDerecho[0] = new PointF(849f, 238f);
            reflejoGrietaEdificioDerecho[1] = new PointF(902f, 246f);
            reflejoGrietaEdificioDerecho[2] = new PointF(902f, 414f);
            reflejoGrietaEdificioDerecho[3] = new PointF(849f, 400f);
            g.FillPolygon(residuoCianTenueBrush, reflejoGrietaEdificioDerecho);
        }

        private void DibujarHorizonteIndustrial(Graphics g)
        {
            // Bloques urbanos de perfil variable: cada torre se lee como edificio independiente.
            int indice = 0;
            for (float x = -340f; x < AnchoNivel + 700f; indice++)
            {
                // Alterna torres estrechas y altas con bloques anchos de varios cuerpos.
                float ancho = indice % 5 == 1 ? 112f + (indice * 17 % 38)
                    : indice % 5 == 3 ? 286f + (indice * 29 % 72)
                    : 166f + (indice * 43 % 104);
                float alto = indice % 5 == 1 ? 344f + (indice * 31 % 108)
                    : indice % 5 == 3 ? 238f + (indice * 23 % 82)
                    : 222f + (indice * 67 % 206);
                float cima = 520f - alto;
                int pisos = Math.Max(4, (int)(alto / (indice % 5 == 1 ? 42f : 53f)));
                int bahias = ancho < 160f ? 2 : ancho > 280f ? 5 : 3 + indice % 2;

                DibujarFachadaUrbana(g, x, cima, ancho, alto, pisos, bahias,
                    indice, edificioLejanoBrush, true);

                if (indice % 5 == 2)
                {
                    // Una esquina de cubierta arrancada deja un perfil quebrado, no una cornisa limpia.
                    g.FillPolygon(arquitecturaMediaBrush, new[]
                    {
                        new PointF(x + ancho * 0.57f, cima - 1f),
                        new PointF(x + ancho * 0.68f, cima + 8f),
                        new PointF(x + ancho * 0.77f, cima + 3f),
                        new PointF(x + ancho * 0.84f, cima + 23f),
                        new PointF(x + ancho, cima + 15f),
                        new PointF(x + ancho, cima - 1f)
                    });
                    // La placa desplazada queda inclinada sobre el vacío del edificio.
                    g.FillPolygon(fachadaMediaBrush, new[]
                    {
                        new PointF(x + ancho * 0.76f, cima + 20f),
                        new PointF(x + ancho * 0.98f, cima + 28f),
                        new PointF(x + ancho * 0.96f, cima + 39f),
                        new PointF(x + ancho * 0.73f, cima + 30f)
                    });
                }

                // Algunos edificios tienen núcleo de ascensores o una torre de comunicaciones.
                if (indice % 3 == 1)
                {
                    float nucleoW = 30f + indice % 3 * 8f;
                    float nucleoH = alto * 0.66f;
                    float nucleoY = 520f - nucleoH;
                    g.FillRectangle(edificioLejanoBrush, x + ancho - nucleoW * 0.72f,
                        nucleoY, nucleoW, nucleoH);
                    g.FillRectangle(fachadaOscuraBrush, x + ancho - nucleoW * 0.46f,
                        nucleoY + 14f, 7f, nucleoH - 28f);
                    g.DrawLine(grietaPen, x + ancho - nucleoW * 0.72f,
                        nucleoY, x + ancho - nucleoW * 0.72f + nucleoW, nucleoY);
                }

                if (indice % 4 == 0)
                {
                    float antenaX = x + ancho * 0.68f;
                    g.FillRectangle(edificioLejanoBrush, antenaX, cima - 24f, 9f, 24f);
                    g.DrawLine(grietaPen, antenaX + 4f, cima - 24f,
                        antenaX + 4f, cima - 43f);
                    g.DrawLine(grietaPen, antenaX - 6f, cima - 37f,
                        antenaX + 14f, cima - 37f);
                }

                // Pasarelas técnicas de cubierta, depósitos y ménsulas distinguen los perfiles.
                if (indice % 3 == 0)
                {
                    float moduloX = x + ancho * 0.16f;
                    float moduloW = ancho * 0.32f;
                    g.FillRectangle(fachadaMediaBrush, moduloX, cima - 18f, moduloW, 18f);
                    g.DrawRectangle(grietaPen, moduloX, cima - 18f, moduloW, 18f);
                    g.FillRectangle(fachadaOscuraBrush, moduloX + 6f, cima - 13f,
                        Math.Max(8f, moduloW - 12f), 7f);
                }

                if (indice % 6 == 2)
                {
                    float balconX = x + ancho * 0.58f;
                    float balconY = cima + alto * 0.58f;
                    g.DrawLine(fachadaPen, balconX - 12f, balconY, balconX + 34f, balconY + 4f);
                    g.DrawLine(grietaPen, balconX - 12f, balconY, balconX - 12f, balconY + 12f);
                    g.DrawLine(grietaPen, balconX + 34f, balconY + 4f, balconX + 34f, balconY + 16f);
                }

                // Huecos irregulares de cielo dejan leer los volúmenes por separado.
                x += ancho + (indice % 4 == 0 ? 96f : indice % 4 == 1 ? 142f : indice % 4 == 2 ? 74f : 118f);
            }

            // Una línea residual y tenue separa la megaciudad del complejo.
            g.FillRectangle(residuoVioletaTenueBrush, 0f, 500f, AnchoNivel, 2f);
            g.DrawLine(tenuePen, 0f, 512f, AnchoNivel, 512f);
        }

        private void DibujarFachadaUrbana(Graphics g, float x, float techo, float ancho, float alto,
            int pisos, int bahias, int semilla, Brush estructuraBrush, bool lucesDiscretas)
        {
            float baseY = techo + alto;
            float margen = Math.Max(5f, ancho * 0.045f);
            float caraX = x + margen;
            float caraW = ancho - margen * 2f;
            float altoPiso = alto / Math.Max(1, pisos);

            // Cuerpo, cara en sombra y plano frontal crean volumen sin perfilar cada bloque.
            g.FillRectangle(arquitecturaMediaBrush, x, techo, ancho, alto);
            g.FillRectangle(estructuraBrush, caraX, techo + 5f, caraW, alto - 10f);
            g.FillRectangle(fachadaOscuraBrush, x + ancho * 0.78f, techo + 5f,
                ancho * 0.22f, alto - 10f);
            g.FillRectangle(panelBrush, caraX, techo + 5f, caraW * 0.10f, alto - 10f);
            g.FillRectangle(sombraBrush, x, baseY - Math.Max(8f, alto * 0.045f), ancho,
                Math.Max(8f, alto * 0.045f));
            g.FillRectangle(fachadaMediaBrush, x + ancho * 0.08f, techo,
                ancho * 0.84f, Math.Max(5f, alto * 0.025f));

            int estilo = Math.Abs(semilla) % 5;
            float anchoBahia = caraW / Math.Max(1, bahias);
            bool fachadaDeVentanas = estilo == 0 || estilo == 1 || estilo == 4;

            if (fachadaDeVentanas)
            {
                for (int piso = 0; piso < pisos; piso++)
                {
                    float y = techo + piso * altoPiso + altoPiso * 0.24f;
                    if (piso % 2 == 0)
                        g.FillRectangle(arquitecturaMediaBrush, caraX, y - 2f, caraW, 2f);

                    for (int bahia = 0; bahia < bahias; bahia++)
                    {
                        int clave = semilla + piso * 7 + bahia * 11;
                        // Vanos anchos y huecos negros alternan con ventanas cegadas.
                        float ventanaX = caraX + bahia * anchoBahia + anchoBahia * 0.16f;
                        float ventanaY = y + 2f;
                        float ventanaW = anchoBahia * ((clave % 4 == 0) ? 0.69f : 0.48f);
                        float ventanaH = altoPiso * ((clave % 6 == 2) ? 0.53f : 0.34f);
                        Brush vanoBrush = (clave % 7 == 3 || estilo == 4)
                            ? arquitecturaMediaBrush : sombraBrush;
                        g.FillRectangle(vanoBrush, ventanaX, ventanaY, ventanaW, ventanaH);

                        bool luzActiva = clave % (lucesDiscretas ? 23 : 17) == 0;
                        if (luzActiva)
                        {
                            float pulso = 0.5f + 0.5f * (float)Math.Sin(
                                tiempoAnimacion * 0.45f + semilla + piso * 1.4f + bahia);
                            if (pulso > 0.62f)
                                g.FillRectangle((semilla + piso) % 3 == 0
                                        ? residuoVioletaTenueBrush : residuoCianTenueBrush,
                                    ventanaX + 2f, ventanaY + 2f,
                                    Math.Max(2f, ventanaW - 4f), 2f);
                        }
                    }
                }
            }
            else
            {
                // Fachada técnica: grandes registros, shafts y salas apagadas en bloques.
                for (int modulo = 0; modulo < 3; modulo++)
                {
                    float moduloY = techo + alto * (0.17f + modulo * 0.24f);
                    float moduloX = caraX + caraW * ((modulo + estilo) % 2 == 0 ? 0.12f : 0.56f);
                    float moduloW = caraW * (modulo == 1 ? 0.32f : 0.25f);
                    float moduloH = Math.Max(14f, altoPiso * 0.82f);
                    g.FillRectangle(arquitecturaMediaBrush, moduloX, moduloY, moduloW, moduloH);
                    g.FillRectangle(fachadaMediaBrush, moduloX, moduloY,
                        Math.Max(4f, moduloW * 0.10f), moduloH);
                    g.FillRectangle(sombraBrush, moduloX + moduloW * 0.2f,
                        moduloY + 5f, moduloW * 0.65f, Math.Max(5f, moduloH - 10f));
                }
            }

            // Grandes pérdidas de revestimiento y losas desplazadas: daño legible a distancia.
            if (estilo == 2 || estilo == 3)
            {
                float dañoX = caraX + caraW * (estilo == 2 ? 0.18f : 0.58f);
                float dañoY = techo + alto * 0.23f;
                float dañoW = caraW * (estilo == 2 ? 0.38f : 0.28f);
                float dañoH = alto * 0.48f;
                g.FillRectangle(arquitecturaMediaBrush, dañoX, dañoY, dañoW, dañoH);
                g.FillRectangle(sombraBrush, dañoX + dañoW * 0.18f, dañoY + 5f,
                    dañoW * 0.68f, dañoH * 0.78f);
                g.FillRectangle(fachadaMediaBrush, dañoX - 5f, dañoY + dañoH * 0.29f,
                    dañoW * 0.42f, Math.Max(5f, altoPiso * 0.20f));
                g.FillRectangle(panelBrush, dañoX + dañoW * 0.64f,
                    dañoY + dañoH * 0.72f, dañoW * 0.52f, Math.Max(5f, altoPiso * 0.16f));
            }

            // Pocas juntas estructurales, relegadas a las esquinas y a los shafts.
            g.FillRectangle(fachadaMediaBrush, caraX, techo + alto * 0.12f,
                Math.Max(3f, ancho * 0.025f), alto * 0.72f);
            g.FillRectangle(arquitecturaMediaBrush, x + ancho * 0.91f,
                techo + alto * 0.08f, Math.Max(4f, ancho * 0.035f), alto * 0.82f);

            if (semilla % 4 == 1)
            {
                // Un borde de cubierta escalonado sugiere un coronamiento colapsado.
                g.FillRectangle(arquitecturaMediaBrush, x + ancho * 0.55f, techo - 10f,
                    ancho * 0.17f, 14f);
                g.FillRectangle(fachadaMediaBrush, x + ancho * 0.72f, techo - 4f,
                    ancho * 0.12f, 8f);
            }
        }

        private void DibujarPuentesLejanos(Graphics g)
        {
            // Tableros macizos partidos en vanos; vigas inferiores y soportes caídos dan escala.
            for (int indice = 0, x = -420; x < AnchoNivel + 500f; indice++, x += 920)
            {
                float y = 326f + (indice % 3) * 17f;
                g.FillRectangle(arquitecturaMediaBrush, x, y, 112f, 17f);
                g.FillRectangle(fachadaMediaBrush, x, y, 112f, 4f);
                g.FillRectangle(arquitecturaMediaBrush, x + 150f, y + 3f, 108f, 18f);
                g.FillRectangle(fachadaMediaBrush, x + 150f, y + 3f, 108f, 4f);
                g.FillRectangle(arquitecturaMediaBrush, x + 292f, y + 1f, 60f, 16f);
                g.FillRectangle(fachadaMediaBrush, x + 292f, y + 1f, 60f, 4f);
                g.FillRectangle(sombraBrush, x + 12f, y + 17f, 87f, 8f);
                g.FillRectangle(sombraBrush, x + 163f, y + 21f, 78f, 8f);
                g.FillRectangle(fachadaMediaBrush, x + 22f, y + 17f, 11f, 38f);
                g.FillRectangle(arquitecturaMediaBrush, x + 318f, y + 17f, 12f, 46f);
                // Viga doblada y tramo suspendido bajo el extremo roto.
                g.FillRectangle(fachadaMediaBrush, x + 104f, y + 12f, 32f, 9f);
                g.FillRectangle(arquitecturaMediaBrush, x + 128f, y + 20f, 9f, 25f);
                g.FillRectangle(arquitecturaMediaBrush, x + 312f, y + 61f, 10f, 28f);
                g.FillRectangle(fachadaMediaBrush, x + 303f, y + 85f, 30f, 8f);
                if (indice % 2 == 0)
                {
                    g.FillRectangle(arquitecturaMediaBrush, x + 184f, y + 21f, 9f, 34f);
                    g.FillRectangle(fachadaMediaBrush, x + 178f, y + 53f, 20f, 7f);
                }
            }
        }

        private void DibujarArmazonAbandonado(Graphics g)
        {
            // Campus urbano del Sector 01: grupos compactos de fachadas, shafts y alas técnicas.
            for (int indice = 0, x = -220; x < AnchoNivel + 560f; indice++)
            {
                float cima = 112f + (indice * 29 % 58);
                float ancho = 490f + (indice * 47 % 130);
                switch (indice % 3)
                {
                    case 0:
                        DibujarComplejoServidores(g, x, cima, ancho, indice);
                        break;
                    case 1:
                        DibujarComplejoRefrigeracion(g, x, cima, ancho, indice);
                        break;
                    default:
                        DibujarTorreContencion(g, x, cima, ancho, indice);
                        break;
                }

                // Ala técnica a distinta altura; deja un vacío abierto antes del siguiente bloque.
                float alaX = x + ancho * 0.64f;
                float alaTop = cima + 84f + (indice % 2) * 32f;
                float alaW = ancho * 0.32f;
                float alaH = 432f - alaTop;
                DibujarFachadaUrbana(g, alaX, alaTop, alaW, alaH,
                    Math.Max(3, (int)(alaH / 43f)), 3, indice + 7, fachadaMediaBrush, false);

                // Haces y cableado suspendido entre cubiertas, sin formar una superficie jugable.
                float cableX = x + ancho * (0.34f + (indice % 3) * 0.12f);
                float largo = 82f + (indice * 19 % 82);
                g.DrawBezier(grietaPen,
                    new PointF(cableX, cima + 6f),
                    new PointF(cableX - 34f, cima + 42f),
                    new PointF(cableX + 26f, cima + largo - 18f),
                    new PointF(cableX + 8f, cima + largo));
                if (indice % 2 == 0)
                {
                    float segundoCableX = x + ancho * 0.83f;
                    g.DrawBezier(tenuePen,
                        new PointF(segundoCableX, cima + 18f),
                        new PointF(segundoCableX + 25f, cima + 52f),
                        new PointF(segundoCableX - 21f, cima + 92f),
                        new PointF(segundoCableX + 4f, cima + 134f));
                }

                // Pórtico desconectado: tramo corto de viga con dos tirantes asimétricos.
                if (indice % 3 != 1)
                {
                    float puenteX = x + ancho * 0.76f;
                    float puenteY = cima + 168f + (indice % 2) * 24f;
                    g.DrawLine(fachadaPen, puenteX, puenteY, puenteX + 72f, puenteY + 3f);
                    g.DrawLine(grietaPen, puenteX + 4f, puenteY + 4f, puenteX + 4f, puenteY + 22f);
                    g.DrawLine(grietaPen, puenteX + 61f, puenteY + 3f, puenteX + 61f, puenteY + 31f);
                    g.DrawLine(grietaPen, puenteX + 61f, puenteY + 31f, puenteX + 74f, puenteY + 38f);
                    g.DrawLine(fachadaPen, puenteX + 72f, puenteY + 3f,
                        puenteX + 86f, puenteY + 9f);
                }

                x += indice % 3 == 0 ? 690 : indice % 3 == 1 ? 735 : 660;
            }
        }

        private void DibujarComplejoServidores(Graphics g, float x, float cima, float ancho, int indice)
        {
            float baseY = 432f;
            float torreW = ancho * 0.48f;
            float torreX = x + ancho * (indice % 2 == 0 ? 0.07f : 0.18f);
            float alto = 274f + (indice % 3) * 31f;
            float techo = baseY - alto;

            DibujarFachadaUrbana(g, torreX, techo, torreW, alto, 6, 4,
                indice, fachadaMediaBrush, false);

            // Bloque de racks expuestos y montantes de servidor a través de una fachada rota.
            float vanoX = torreX + torreW * 0.55f;
            float vanoY = techo + 54f;
            float vanoW = torreW * 0.34f;
            float vanoH = alto * 0.68f;
            g.FillRectangle(fachadaOscuraBrush, vanoX, vanoY, vanoW, vanoH);
            g.DrawRectangle(fachadaPen, vanoX, vanoY, vanoW, vanoH);
            for (int rack = 0; rack < 5; rack++)
            {
                float rackY = vanoY + 10f + rack * (vanoH - 22f) / 5f;
                g.FillRectangle(sombraBrush, vanoX + 5f, rackY, vanoW - 10f, 17f);
                g.DrawLine(grietaPen, vanoX + 8f, rackY + 5f, vanoX + vanoW - 8f, rackY + 5f);
                g.DrawLine(grietaPen, vanoX + 8f, rackY + 11f, vanoX + vanoW - 8f, rackY + 11f);
                if ((rack + indice) % 3 == 0)
                    g.FillRectangle(residuoCianTenueBrush, vanoX + 8f, rackY + 14f, 8f, 2f);
            }

            // Núcleo vertical de refrigeración y antena rota sobre la torre de datos.
            float shaftX = torreX + torreW - 29f;
            g.FillRectangle(edificioLejanoBrush, shaftX, techo - 35f, 22f, 35f);
            g.DrawLine(grietaPen, shaftX + 11f, techo - 35f, shaftX + 11f, techo - 62f);
            g.DrawLine(grietaPen, shaftX + 1f, techo - 54f, shaftX + 21f, techo - 54f);
            g.FillRectangle(fachadaOscuraBrush, torreX + 12f, baseY - 46f, torreW * 0.34f, 27f);
            g.DrawRectangle(grietaPen, torreX + 12f, baseY - 46f, torreW * 0.34f, 27f);
            g.DrawString("SECTOR 01", identificadorFont, residuoCianTenueBrush,
                torreX + 17f, baseY - 41f);
        }

        private void DibujarComplejoRefrigeracion(Graphics g, float x, float cima, float ancho, int indice)
        {
            float baseY = 432f;
            float shaftW = 88f + (indice % 2) * 18f;
            float shaftH = 246f + (indice % 3) * 25f;
            float shaft1X = x + ancho * 0.08f;
            float shaft2X = x + ancho * 0.76f;

            // Torres de refrigeración rectilíneas con registros de ventilación por nivel.
            DibujarFachadaUrbana(g, shaft1X, baseY - shaftH, shaftW, shaftH,
                6, 2, indice + 2, fachadaMediaBrush, false);
            DibujarFachadaUrbana(g, shaft2X, baseY - shaftH + 42f, shaftW, shaftH - 42f,
                5, 2, indice + 5, fachadaMediaBrush, false);

            for (int rejilla = 0; rejilla < 7; rejilla++)
            {
                float y = baseY - shaftH + 48f + rejilla * 31f;
                g.DrawLine(grietaPen, shaft1X + 12f, y, shaft1X + shaftW - 12f, y);
                g.DrawLine(grietaPen, shaft2X + 12f, y + 7f, shaft2X + shaftW - 12f, y + 7f);
            }

            // Sala de turbina cuadrada, encajada entre los dos conductos verticales.
            float maquinaX = x + ancho * 0.34f;
            float maquinaY = 278f + (indice % 2) * 24f;
            float maquinaW = ancho * 0.34f;
            g.FillRectangle(fachadaMediaBrush, maquinaX, maquinaY, maquinaW, 142f);
            g.FillRectangle(fachadaOscuraBrush, maquinaX + 10f, maquinaY + 12f,
                maquinaW - 20f, 93f);
            g.DrawRectangle(fachadaPen, maquinaX, maquinaY, maquinaW, 142f);
            float fanX = maquinaX + (maquinaW - 70f) * 0.5f;
            g.DrawEllipse(tenuePen, fanX, maquinaY + 22f, 70f, 70f);
            g.DrawEllipse(grietaPen, fanX + 9f, maquinaY + 31f, 52f, 52f);
            g.DrawLine(grietaPen, fanX + 35f, maquinaY + 22f,
                fanX + 35f, maquinaY + 92f);
            g.DrawLine(grietaPen, fanX, maquinaY + 57f,
                fanX + 70f, maquinaY + 57f);
            g.FillRectangle(residuoVioletaTenueBrush, maquinaX + 14f,
                maquinaY + 120f, 17f, 2f);
            g.FillRectangle(residuoCianTenueBrush, maquinaX + 39f,
                maquinaY + 120f, 27f, 2f);

            // Tubo cuadrado de presión conecta turbina y shafts; los codos son verticales.
            g.FillRectangle(edificioLejanoBrush, maquinaX + 18f, maquinaY - 23f,
                maquinaW - 36f, 14f);
            g.DrawLine(grietaPen, maquinaX + 18f, maquinaY - 23f,
                maquinaX + maquinaW - 18f, maquinaY - 23f);
            g.DrawLine(grietaPen, shaft1X + shaftW * 0.5f, maquinaY - 9f,
                shaft1X + shaftW * 0.5f, maquinaY + 16f);
            g.DrawLine(grietaPen, shaft2X + shaftW * 0.5f, maquinaY - 9f,
                shaft2X + shaftW * 0.5f, maquinaY + 16f);
        }

        private void DibujarTorreContencion(Graphics g, float x, float cima, float ancho, int indice)
        {
            float baseY = 432f;
            float torreX = x + ancho * (indice % 2 == 0 ? 0.12f : 0.20f);
            float torreW = ancho * 0.66f;
            float torreH = 284f + (indice % 2) * 22f;
            float torreTop = baseY - torreH;

            DibujarFachadaUrbana(g, torreX, torreTop, torreW, torreH,
                6, 4, indice + 4, fachadaMediaBrush, false);

            // Cámara de observación de varios pisos con marcos reforzados y cristal roto.
            float ventanaX = torreX + torreW * 0.20f;
            float ventanaY = torreTop + 57f;
            float ventanaW = torreW * 0.54f;
            float ventanaH = torreH * 0.57f;
            g.FillRectangle(fachadaOscuraBrush, ventanaX, ventanaY, ventanaW, ventanaH);
            g.DrawRectangle(tenuePen, ventanaX, ventanaY, ventanaW, ventanaH);
            g.DrawLine(fachadaPen, ventanaX + ventanaW * 0.33f, ventanaY,
                ventanaX + ventanaW * 0.33f, ventanaY + ventanaH);
            g.DrawLine(fachadaPen, ventanaX + ventanaW * 0.66f, ventanaY,
                ventanaX + ventanaW * 0.66f, ventanaY + ventanaH);
            for (int piso = 1; piso < 4; piso++)
            {
                float y = ventanaY + ventanaH * piso / 4f;
                g.DrawLine(fachadaPen, ventanaX, y, ventanaX + ventanaW, y);
            }
            g.DrawLine(grietaPen, ventanaX + ventanaW * 0.18f, ventanaY + ventanaH * 0.15f,
                ventanaX + ventanaW * 0.43f, ventanaY + ventanaH * 0.48f);
            g.DrawLine(grietaPen, ventanaX + ventanaW * 0.43f, ventanaY + ventanaH * 0.48f,
                ventanaX + ventanaW * 0.31f, ventanaY + ventanaH * 0.75f);
            g.DrawLine(grietaPen, ventanaX + ventanaW * 0.43f, ventanaY + ventanaH * 0.48f,
                ventanaX + ventanaW * 0.79f, ventanaY + ventanaH * 0.66f);

            // Consola de seguridad en un lateral de la torre.
            float consolaX = torreX + torreW - 44f;
            g.FillRectangle(fachadaOscuraBrush, consolaX, torreTop + 92f, 34f, 72f);
            g.DrawRectangle(grietaPen, consolaX, torreTop + 92f, 34f, 72f);
            float estado = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 0.78f + indice);
            if (estado > 0.2f)
                g.FillRectangle(estado > 0.78f ? residuoVioletaBrush : residuoVioletaTenueBrush,
                    consolaX + 6f, torreTop + 102f, 12f, 3f);
        }

        private void DibujarCamaraContencion(Graphics g)
        {
            // Ventana de observación vertical: identidad del antiguo sector de investigación.
            const float x = 2220f;
            const float y = 214f;
            g.FillRectangle(sombraBrush, x, y, 248f, 212f);
            g.FillRectangle(arquitecturaMediaBrush, x + 12f, y + 10f, 224f, 192f);
            g.DrawRectangle(tenuePen, x, y, 248f, 212f);
            g.DrawRectangle(grietaPen, x + 18f, y + 18f, 212f, 176f);
            g.FillEllipse(ruinaLejanaBrush, x + 57f, y + 40f, 134f, 118f);
            g.DrawEllipse(tenuePen, x + 57f, y + 40f, 134f, 118f);
            g.DrawEllipse(grietaPen, x + 66f, y + 49f, 116f, 100f);
            g.DrawLine(grietaPen, x + 106f, y + 68f, x + 91f, y + 102f);
            g.DrawLine(grietaPen, x + 91f, y + 102f, x + 123f, y + 119f);
            g.DrawLine(grietaPen, x + 123f, y + 119f, x + 147f, y + 83f);
            g.DrawLine(grietaPen, x + 147f, y + 83f, x + 170f, y + 112f);

            float estadoAlerta = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 0.8f + 2.4f);
            if (estadoAlerta > 0.18f)
                g.FillRectangle(estadoAlerta > 0.78f ? residuoVioletaBrush : residuoVioletaTenueBrush,
                    x + 26f, y + 24f, 18f, 3f);
            g.FillRectangle(ruinaLejanaBrush, x + 198f, y + 28f, 22f, 66f);
            g.DrawRectangle(grietaPen, x + 198f, y + 28f, 22f, 66f);
            g.FillRectangle(residuoCianTenueBrush, x + 204f, y + 40f, 10f, 2f);
            g.FillRectangle(residuoCianTenueBrush, x + 204f, y + 51f, 10f, 2f);
            g.FillRectangle(residuoCianTenueBrush, x + 204f, y + 62f, 10f, 2f);
            g.DrawLine(grietaPen, x + 28f, y + 177f, x + 77f, y + 177f);
            g.DrawLine(grietaPen, x + 171f, y + 177f, x + 219f, y + 177f);
        }

        private void DibujarConducciones(Graphics g)
        {
            for (int x = -240; x < AnchoNivel + 640f; x += 640)
            {
                // Ductos anchos del plano cercano: segmentos separados y codos visibles.
                g.FillRectangle(arquitecturaMediaBrush, x, 106f, 210f, 16f);
                g.FillRectangle(arquitecturaMediaBrush, x + 252f, 106f, 258f, 16f);
                g.DrawLine(grietaPen, x, 106f, x + 210f, 106f);
                g.DrawLine(grietaPen, x + 252f, 106f, x + 510f, 106f);
                g.DrawLine(tenuePen, x + 8f, 119f, x + 201f, 119f);
                g.DrawLine(tenuePen, x + 260f, 119f, x + 502f, 119f);
                g.FillRectangle(sombraBrush, x + 88f, 101f, 30f, 26f);
                g.DrawRectangle(grietaPen, x + 88f, 101f, 30f, 26f);
                g.FillRectangle(sombraBrush, x + 365f, 101f, 30f, 26f);
                g.DrawRectangle(grietaPen, x + 365f, 101f, 30f, 26f);

                g.DrawLine(tenuePen, x, 116f, x + 210f, 116f);
                g.DrawLine(tenuePen, x + 252f, 116f, x + 510f, 116f);
                g.DrawLine(grietaPen, x + 208f, 108f, x + 208f, 124f);
                g.DrawLine(grietaPen, x + 510f, 108f, x + 510f, 124f);
                g.DrawBezier(grietaPen,
                    new PointF(x + 120f, 116f), new PointF(x + 140f, 148f),
                    new PointF(x + 175f, 151f), new PointF(x + 194f, 183f));
                g.DrawBezier(tenuePen,
                    new PointF(x + 430f, 122f), new PointF(x + 424f, 155f),
                    new PointF(x + 398f, 166f), new PointF(x + 400f, 211f));
                g.DrawLine(grietaPen, x + 112f, 127f, x + 112f, 145f);
                g.DrawLine(grietaPen, x + 402f, 126f, x + 402f, 148f);

                // Un pulso recorre lentamente la conducción sin afectar al espacio jugable.
                float avance = (float)((tiempoAnimacion * 34f + (x + 240) * 0.37f) % 462f);
                float pulsoX = x + 30f + avance;
                g.FillRectangle(residuoCianTenueBrush, pulsoX - 5f, 113f, 11f, 6f);
                g.FillRectangle(residuoCianBrush, pulsoX - 1f, 114f, 3f, 3f);
            }
        }

        private void DibujarResiduosDigitales(Graphics g)
        {
            // Luciérnagas débiles en torno a la fractura, con deriva determinista y lenta.
            for (int indice = 0; indice < luciernagaX.Length; indice++)
            {
                double fase = tiempoAnimacion * (0.13f + (indice % 4) * 0.018f) + indice * 1.73f;
                float deriva = luciernagaDeriva[indice];
                float x = luciernagaX[indice] + (float)Math.Sin(fase) * deriva
                    + (float)Math.Sin(fase * 0.43f + indice) * 2f;
                float y = luciernagaY[indice] + (float)Math.Sin(fase * 0.77f + indice * 0.61f) * deriva;
                float intensidad = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 0.42f + indice * 1.37f);
                float tamano = 1.7f + (indice % 3) * 0.55f;

                Brush luz = indice % 9 == 0
                    ? (intensidad > 0.82f ? residuoBlancoBrush : residuoCianTenueBrush)
                    : indice % 5 == 1
                        ? (intensidad > 0.78f ? residuoVioletaBrush : residuoVioletaTenueBrush)
                        : indice % 4 == 2
                            ? (intensidad > 0.80f ? residuoAzulBrush : residuoCianTenueBrush)
                            : (intensidad > 0.80f ? residuoCianBrush : residuoCianTenueBrush);
                g.FillEllipse(luz, x, y, tamano, tamano);
            }

            DibujarFragmentosDimensionales(g);

            // Tres destellos breves y desfasados; el umbral alto los hace poco frecuentes.
            for (int indice = 0; indice < 3; indice++)
            {
                float pulso = (float)Math.Sin(tiempoAnimacion * 0.72f + indice * 2.31f);
                if (pulso > 0.985f)
                {
                    float x = 704f + indice * 127f + (float)Math.Sin(tiempoAnimacion * 0.31f + indice) * 5f;
                    float y = 146f + indice * 71f;
                    g.DrawLine(nucleoGrietaPen, x, y, x + 6f, y - 3f);
                    g.FillEllipse(residuoBlancoBrush, x + 4f, y - 4f, 2f, 2f);
                }
            }
        }

        private void DibujarFragmentosDimensionales(Graphics g)
        {
            for (int indice = 0; indice < fragmentosDimensionales.Length; indice++)
            {
                float fase = (float)tiempoAnimacion;
                float angulo = fase * (0.075f + indice * 0.009f) + indice * 1.17f;
                float coseno = (float)Math.Cos(angulo);
                float seno = (float)Math.Sin(angulo);
                float centroX = fragmentoResiduoX[indice] + (float)Math.Sin(fase * 0.16f + indice) * 4f;
                float centroY = fragmentoResiduoY[indice] + (float)Math.Sin(fase * 0.13f + indice * 0.8f) * 5f;
                float tamano = fragmentoResiduoTamano[indice];
                float[] forma = formaFragmentoResiduo[indice];
                PointF[] puntos = fragmentosDimensionales[indice];

                for (int vertice = 0; vertice < puntos.Length; vertice++)
                {
                    float localX = forma[vertice * 2] * tamano;
                    float localY = forma[vertice * 2 + 1] * tamano;
                    puntos[vertice] = new PointF(
                        centroX + localX * coseno - localY * seno,
                        centroY + localX * seno + localY * coseno);
                }

                g.FillPolygon(indice % 2 == 0 ? residuoVioletaTenueBrush : residuoAzulBrush, puntos);
                g.DrawPolygon(indice % 2 == 0 ? fragmentoVioletaPen : fragmentoCianPen, puntos);
            }
        }

        private void DibujarEscombrosSuspendidos(Graphics g)
        {
            // Restos pequeños de maquinaria flotan junto a la grieta en el plano lejano.
            for (int indice = 0; indice < escombrosSuspendidos.Length; indice++)
            {
                float fase = (float)tiempoAnimacion;
                float angulo = fase * (0.045f + indice * 0.008f) + indice * 1.41f;
                float coseno = (float)Math.Cos(angulo);
                float seno = (float)Math.Sin(angulo);
                float centroX = escombroX[indice] + (float)Math.Sin(fase * 0.17f + indice * 1.6f) * 4f;
                float centroY = escombroY[indice] + (float)Math.Sin(fase * 0.14f + indice * 0.9f) * 6f;
                float escala = escombroTamano[indice] * (0.96f + 0.04f * (float)Math.Sin(fase * 0.32f + indice));
                float[] forma = formaEscombro[indice];
                PointF[] puntos = escombrosSuspendidos[indice];

                for (int vertice = 0; vertice < puntos.Length; vertice++)
                {
                    float localX = forma[vertice * 2] * escala;
                    float localY = forma[vertice * 2 + 1] * escala;
                    puntos[vertice] = new PointF(
                        centroX + localX * coseno - localY * seno,
                        centroY + localX * seno + localY * coseno);
                }

                g.FillPolygon(indice % 2 == 0 ? residuoAzulBrush : residuoVioletaTenueBrush, puntos);
                g.DrawPolygon(indice % 2 == 0 ? fragmentoCianPen : fragmentoVioletaPen, puntos);
                g.DrawLine(nucleoGrietaPen, puntos[0], puntos[2]);
            }
        }

        private void DibujarDistorsionesEspaciales(Graphics g)
        {
            // Pliegues de refracción separados e irregulares; no forman anillos cerrados.
            float onda = (float)Math.Sin(tiempoAnimacion * 0.38f)
                * (3f + respuestaVisualGrieta * 4f);
            float contrafase = (float)Math.Sin(tiempoAnimacion * 0.31f + 1.7f)
                * (2f + respuestaVisualGrieta * 3f);

            g.DrawBezier(nucleoGrietaPen,
                629f, 138f + onda, 650f, 117f + contrafase,
                675f, 151f + onda, 698f, 125f + contrafase);
            g.DrawBezier(tenuePen,
                708f, 137f + onda, 721f, 126f + contrafase,
                738f, 151f + onda, 751f, 132f + contrafase);

            g.DrawBezier(tenuePen,
                912f, 211f + contrafase, 931f, 188f + onda,
                954f, 223f + contrafase, 978f, 199f + onda);
            g.DrawBezier(nucleoGrietaPen,
                957f, 310f + onda, 976f, 287f + contrafase,
                1004f, 324f + onda, 1028f, 301f + contrafase);

            // Cortes cortos dan la impresión de líneas de fachada desviadas por la lente.
            g.DrawLine(tenuePen, 613f, 178f + onda, 654f, 178f + onda);
            g.DrawLine(nucleoGrietaPen, 659f, 181f + contrafase, 689f, 181f + contrafase);
            g.DrawLine(nucleoGrietaPen, 929f, 265f + onda, 960f, 265f + onda);
            g.DrawLine(tenuePen, 967f, 269f + contrafase, 1001f, 269f + contrafase);

            if (respuestaVisualGrieta > 0.02f)
            {
                int intensidad = (int)(respuestaVisualGrieta * 92f);
                distorsionTransicionPen.Color = Color.FromArgb(intensidad, 102, 220, 255);
                g.DrawBezier(distorsionTransicionPen,
                    667f, 216f + onda, 686f, 195f + contrafase,
                    706f, 229f + onda, 725f, 207f + contrafase);
                g.DrawBezier(distorsionTransicionPen,
                    895f, 335f + contrafase, 916f, 313f + onda,
                    941f, 348f + contrafase, 963f, 326f + onda);
            }
        }

        private void DibujarFiltracionesDimension(Graphics g)
        {
            // Tres apariciones de borde alfa suave, con fases distintas y cerca de la abertura.
            for (int indice = 0; indice < filtracionX.Length; indice++)
            {
                float fase = (float)tiempoAnimacion * 0.48f + indice * 2.17f;
                float vida = Math.Max(0f, (float)Math.Sin(fase));
                int alpha = (int)(vida * (88f + respuestaVisualGrieta * 30f));
                if (alpha < 3)
                    continue;

                float centroX = filtracionX[indice] + (float)Math.Sin(fase * 0.53f) * 3f;
                float centroY = filtracionY[indice] + (float)Math.Sin(fase * 0.71f + indice) * 4f;
                float ancho = filtracionTamano[indice] * (0.65f + vida * 0.55f);
                PointF[] puntos = formasFiltracion[indice];
                puntos[0] = new PointF(centroX - ancho * 0.72f, centroY - ancho * 0.18f);
                puntos[1] = new PointF(centroX - ancho * 0.04f, centroY - ancho * 0.62f);
                puntos[2] = new PointF(centroX + ancho * 0.80f, centroY + ancho * 0.12f);
                puntos[3] = new PointF(centroX + ancho * 0.08f, centroY + ancho * 0.46f);

                bool violeta = indice == 1;
                SolidBrush relleno = violeta ? filtracionVioletaBrush : filtracionCianBrush;
                Pen borde = violeta ? filtracionVioletaPen : filtracionCianPen;
                relleno.Color = Color.FromArgb(alpha, violeta ? 157 : 0, violeta ? 93 : 196, 242);
                borde.Color = Color.FromArgb(Math.Min(150, alpha + 24), violeta ? 180 : 88,
                    violeta ? 112 : 214, 255);
                g.FillPolygon(relleno, puntos);
                g.DrawPolygon(borde, puntos);
            }
        }

        private void DibujarHitoResonador(Graphics g)
        {
            // Gran anillo de comunicaciones roto, alto y lejano: funciona como silueta de referencia.
            const float centroX = 660f;
            const float centroY = 282f;
            g.DrawArc(tenuePen, centroX - 142f, centroY - 104f, 284f, 208f, 198f, 104f);
            g.DrawArc(tenuePen, centroX - 142f, centroY - 104f, 284f, 208f, 18f, 62f);
            g.DrawArc(grietaPen, centroX - 112f, centroY - 82f, 224f, 164f, 42f, 104f);
            g.DrawLine(tenuePen, centroX - 118f, centroY + 20f, centroX - 178f, 408f);
            g.DrawLine(tenuePen, centroX + 118f, centroY + 20f, centroX + 174f, 408f);
            g.DrawLine(grietaPen, centroX - 178f, 408f, centroX + 174f, 408f);
            g.DrawLine(grietaPen, centroX, centroY - 100f, centroX, 171f);
            g.FillRectangle(arquitecturaMediaBrush, centroX - 8f, 337f, 16f, 71f);

            float pulso = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 0.72f);
            Brush nucleo = pulso > 0.62f ? residuoCianBrush : residuoCianTenueBrush;
            g.FillEllipse(nucleo, centroX - 7f, centroY - 7f, 14f, 14f);
            g.FillRectangle(residuoVioletaTenueBrush, centroX - 2f, centroY - 112f, 4f, 18f);
        }

        private void DibujarSuelo(Graphics g)
        {
            foreach (RectangleF plataforma in PlataformasActivas)
            {
                if (plataforma.Top >= SueloY)
                {
                    g.FillRectangle(sueloBrush, plataforma);
                    g.FillRectangle(pisoInteriorBrush, plataforma.Left,
                        plataforma.Top + 4f, plataforma.Width, 14f);
                    g.FillRectangle(cianBrush, plataforma.Left, plataforma.Top,
                        plataforma.Width, 3f);

                    for (float x = plataforma.Left + 24f; x < plataforma.Right; x += 128f)
                    {
                        g.DrawLine(tenuePen, x, plataforma.Top + 4f, x, plataforma.Top + 18f);
                        g.FillRectangle(detalleBrush, x + 8f, plataforma.Top + 8f, 3f, 2f);
                        g.DrawLine(grietaPen, x + 7f, SueloY + 22f,
                            x - 38f, AltoVista);
                        g.DrawLine(tenuePen, x + 2f, SueloY + 25f,
                            x + 28f, SueloY + 25f);
                    }
                }
                else
                {
                    g.FillRectangle(arquitecturaMediaBrush, plataforma);
                    g.FillRectangle(metalBrush, plataforma.Left + 1f,
                        plataforma.Top + 4f, plataforma.Width - 2f, plataforma.Height - 5f);
                    g.FillRectangle(cianBrush, plataforma.Left, plataforma.Top,
                        plataforma.Width, 3f);
                    g.DrawLine(cianPen, plataforma.Left + 1f, plataforma.Top + 0.5f,
                        plataforma.Right - 1f, plataforma.Top + 0.5f);
                    g.FillRectangle(violetaBrush, plataforma.Left + 10f,
                        plataforma.Bottom - 2f, plataforma.Width - 20f, 1f);
                    g.FillRectangle(residuoCianTenueBrush, plataforma.Left + 8f,
                        plataforma.Bottom, plataforma.Width - 16f, 3f);

                    for (float x = plataforma.Left + 24f; x < plataforma.Right - 8f; x += 64f)
                    {
                        g.FillRectangle(sueloBrush, x, plataforma.Top + 9f, 20f, 5f);
                        g.DrawLine(tenuePen, x + 3f, plataforma.Top + 7f,
                            x + 18f, plataforma.Top + 7f);
                    }

                    g.DrawLine(tenuePen, plataforma.Left + 4f, plataforma.Bottom - 1f,
                        plataforma.Right - 4f, plataforma.Bottom - 1f);
                }
            }
        }

        private void DibujarPuerta(Graphics g)
        {
            float pulso = 0.5f + 0.5f * (float)Math.Sin(tiempoAnimacion * 1.1f);
            g.FillRectangle(residuoCianTenueBrush, puertaFinal.X - 5f, puertaFinal.Y - 5f,
                puertaFinal.Width + 10f, puertaFinal.Height + 10f);
            g.FillRectangle(panelBrush, puertaFinal);
            g.FillRectangle(obsidianaBrush, puertaFinal.X + 10f, puertaFinal.Y + 18f,
                puertaFinal.Width - 20f, puertaFinal.Height - 20f);
            g.DrawRectangle(cianPen, puertaFinal.X, puertaFinal.Y,
                puertaFinal.Width, puertaFinal.Height);
            g.DrawRectangle(tenuePen, puertaFinal.X + 8f, puertaFinal.Y + 8f,
                puertaFinal.Width - 16f, puertaFinal.Height - 8f);
            g.FillRectangle(pulso > 0.65f ? residuoCianBrush : residuoCianTenueBrush,
                puertaFinal.Left + 15f, puertaFinal.Top + 27f, puertaFinal.Width - 30f, 3f);
            g.FillRectangle(residuoVioletaTenueBrush, puertaFinal.Left + 15f,
                puertaFinal.Top + 38f, puertaFinal.Width - 30f, 2f);
            g.DrawLine(grietaPen, puertaFinal.Left + 18f, puertaFinal.Top + 63f,
                puertaFinal.Left + 35f, puertaFinal.Top + 80f);
            g.DrawLine(grietaPen, puertaFinal.Right - 18f, puertaFinal.Top + 63f,
                puertaFinal.Right - 35f, puertaFinal.Top + 80f);
            g.FillRectangle(residuoVioletaTenueBrush, puertaFinal.Left - 8f,
                puertaFinal.Top + 20f, 5f, 20f);
            g.FillRectangle(pulso > 0.6f ? residuoCianBrush : residuoCianTenueBrush,
                puertaFinal.Right + 3f, puertaFinal.Top + 20f, 5f, 20f);
            g.FillRectangle(violetaBrush, puertaFinal.Left + 8f,
                puertaFinal.Bottom - 20f, 6f, 12f);
            g.FillRectangle(cianBrush, puertaFinal.Right - 14f,
                puertaFinal.Bottom - 20f, 6f, 12f);
            g.FillRectangle(panelBrush, puertaFinal.Left + 7f, puertaFinal.Top - 25f,
                puertaFinal.Width - 14f, 18f);
            g.DrawString("01 // SALIDA", identificadorFont, blancoBrush,
                puertaFinal.Left + 5f, puertaFinal.Top - 23f);

            // Tres marcas discretas sobre el último tramo orientan hacia la compuerta.
            for (float marcaX = puertaFinal.Left - 112f; marcaX < puertaFinal.Left - 8f; marcaX += 36f)
            {
                float y = SueloY - 12f;
                g.DrawLine(tenuePen, marcaX, y, marcaX + 10f, y + 5f);
                g.DrawLine(tenuePen, marcaX + 10f, y + 5f, marcaX, y + 10f);
            }
        }

        private void DibujarJugador(Graphics g)
        {
            float x = jugadorX;
            float y = jugadorY;
            bool caminando = enSuelo && izquierdaPulsada != derechaPulsada;
            bool ascendiendo = !enSuelo && velocidadY < 0f;
            bool descendiendo = !enSuelo && velocidadY >= 0f;
            float fase = (float)(tiempoAnimacion * 13f);
            float paso = caminando ? (float)Math.Sin(fase) : 0f;
            float balanceoReposo = caminando || !enSuelo
                ? 0f
                : (float)Math.Sin(tiempoAnimacion * 2.4f) * 1.5f;
            float desplazamientoY = balanceoReposo;

            if (caminando)
                desplazamientoY += Math.Abs(paso) * 1.1f;

            bool poseFrontal = mezclaFrontal >= 0.5f;
            float escalaPoseX = poseFrontal
                ? Math.Max(0.5f, mezclaFrontal)
                : Math.Max(0.5f, 1f - mezclaFrontal);

            if (!poseFrontal && !mirandoDerecha)
                escalaPoseX = -escalaPoseX;

            g.DrawString("ECHO-0", identificadorFont, cianBrush, x - 14, y - 24 + desplazamientoY);
            g.FillEllipse(sombraBrush, x + 5f, y + 70f, 32f, 8f);

            var estado = g.Save();
            g.TranslateTransform(x + AnchoJugador / 2f, y + desplazamientoY);
            g.ScaleTransform(escalaPoseX, 1f, System.Drawing.Drawing2D.MatrixOrder.Prepend);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (poseFrontal)
            {
                DibujarPoseFrontal(g);
                g.Restore(estado);
                DibujarDestelloJugador(g, x, y);
                return;
            }

            float movimientoEstela = caminando
                ? paso * 2.5f
                : !enSuelo
                    ? Math.Sign(velocidadY) * 2f
                    : (float)Math.Sin(tiempoAnimacion * 2.4f) * 1.2f;

            float brazoLejanoX;
            float brazoLejanoY;
            float brazoCercanoX;
            float brazoCercanoY;
            float piernaLejanaX;
            float piernaLejanaY;
            float piernaCercanaX;
            float piernaCercanaY;

            if (!enSuelo && ascendiendo)
            {
                brazoLejanoX = -10f; brazoLejanoY = 32f;
                brazoCercanoX = 14f; brazoCercanoY = 24f;
                piernaLejanaX = -9f; piernaLejanaY = 64f;
                piernaCercanaX = 7f; piernaCercanaY = 68f;
            }
            else if (descendiendo)
            {
                brazoLejanoX = -12f; brazoLejanoY = 36f;
                brazoCercanoX = 15f; brazoCercanoY = 43f;
                piernaLejanaX = -10f; piernaLejanaY = 72f;
                piernaCercanaX = 12f; piernaCercanaY = 75f;
            }
            else if (caminando)
            {
                brazoLejanoX = -9f + paso * 3f; brazoLejanoY = 48f - Math.Abs(paso);
                brazoCercanoX = 12f - paso * 4f; brazoCercanoY = 46f + Math.Abs(paso);
                piernaLejanaX = -2f - paso * 5f; piernaLejanaY = 75f - Math.Abs(paso) * 2f;
                piernaCercanaX = 3f + paso * 5f; piernaCercanaY = 75f - Math.Abs(paso) * 2f;
            }
            else
            {
                brazoLejanoX = -8f; brazoLejanoY = 48f;
                brazoCercanoX = 12f; brazoCercanoY = 49f;
                piernaLejanaX = -2f; piernaLejanaY = 75f;
                piernaCercanaX = 4f; piernaCercanaY = 75f;
            }

            // Manto digital posterior: se mueve detrás del cuerpo y se invierte con el perfil.
            estelaForma[0] = new PointF(-5f, 31f);
            estelaForma[1] = new PointF(-18f, 37f + movimientoEstela);
            estelaForma[2] = new PointF(-15f, 60f + movimientoEstela * 1.4f);
            estelaForma[3] = new PointF(-4f, 53f);
            g.FillPolygon(obsidianaBrush, estelaForma);
            g.DrawPolygon(articulacionPen, estelaForma);
            g.DrawLine(filoVioletaPen, -7f, 37f, -15f, 55f + movimientoEstela);
            g.DrawLine(filoCianPen, -9f, 42f, -14f, 49f + movimientoEstela * 0.5f);

            // Pierna lejana y brazo trasero quedan detrás del torso para dar profundidad lateral.
            g.DrawLine(siluetaPen, -1f, 51f, piernaLejanaX, piernaLejanaY);
            g.DrawLine(extremidadPen, -1f, 51f, piernaLejanaX, piernaLejanaY);
            g.DrawLine(articulacionPen, -1f, 59f, piernaLejanaX, 64f);
            g.DrawLine(filoVioletaPen, piernaLejanaX - 2f, piernaLejanaY - 5f,
                piernaLejanaX + 3f, piernaLejanaY - 5f);

            g.DrawLine(siluetaPen, -2f, 34f, brazoLejanoX, brazoLejanoY);
            g.DrawLine(extremidadPen, -2f, 34f, brazoLejanoX, brazoLejanoY);
            g.DrawEllipse(articulacionPen, brazoLejanoX - 2f, brazoLejanoY - 2f, 4f, 4f);

            // Torso estrecho, inclinado hacia delante y con un reactor lateral visible.
            torsoForma[0] = new PointF(-5f, 32f);
            torsoForma[1] = new PointF(3f, 30f);
            torsoForma[2] = new PointF(9f, 34f);
            torsoForma[3] = new PointF(11f, 43f);
            torsoForma[4] = new PointF(7f, 54f);
            torsoForma[5] = new PointF(2f, 57f);
            torsoForma[6] = new PointF(-6f, 53f);
            torsoForma[7] = new PointF(-7f, 40f);
            g.FillPolygon(obsidianaBrush, torsoForma);
            torsoInteriorForma[0] = new PointF(-3f, 34f);
            torsoInteriorForma[1] = new PointF(3f, 33f);
            torsoInteriorForma[2] = new PointF(7f, 36f);
            torsoInteriorForma[3] = new PointF(8f, 43f);
            torsoInteriorForma[4] = new PointF(5f, 51f);
            torsoInteriorForma[5] = new PointF(1f, 53f);
            torsoInteriorForma[6] = new PointF(-4f, 50f);
            torsoInteriorForma[7] = new PointF(-5f, 40f);
            g.FillPolygon(armaduraBrush, torsoInteriorForma);
            g.DrawPolygon(articulacionPen, torsoForma);
            g.DrawLine(filoCianPen, 3f, 36f, 7f, 38f);
            g.FillEllipse(cianLuzBrush, 5f, 39f, 4f, 4f);
            g.DrawLine(visorInteriorPen, 6f, 44f, 3f, 49f);
            g.DrawLine(filoVioletaPen, -4f, 49f, 4f, 52f);

            // Pierna y brazo cercanos se adelantan; su paso es opuesto y estilizado.
            g.DrawLine(siluetaPen, 4f, 52f, piernaCercanaX, piernaCercanaY);
            g.DrawLine(extremidadPen, 4f, 52f, piernaCercanaX, piernaCercanaY);
            g.DrawEllipse(articulacionPen, piernaCercanaX - 2f, 61f, 5f, 5f);
            g.DrawLine(filoCianPen, piernaCercanaX - 1f, piernaCercanaY - 4f,
                piernaCercanaX + 5f, piernaCercanaY - 4f);

            g.DrawLine(siluetaPen, 5f, 35f, brazoCercanoX, brazoCercanoY);
            g.DrawLine(extremidadPen, 5f, 35f, brazoCercanoX, brazoCercanoY);
            g.DrawEllipse(articulacionPen, brazoCercanoX - 2f, brazoCercanoY - 2f, 5f, 5f);
            g.DrawLine(filoVioletaPen, 9f, 41f, brazoCercanoX - 1f, brazoCercanoY - 2f);

            // Casco lateral de planos afilados, con una quilla posterior asimétrica.
            mascaraForma[0] = new PointF(-9f, 8f);
            mascaraForma[1] = new PointF(-6f, 4f);
            mascaraForma[2] = new PointF(1f, 2f);
            mascaraForma[3] = new PointF(7f, 5f);
            mascaraForma[4] = new PointF(11f, 9f);
            mascaraForma[5] = new PointF(14f, 15f);
            mascaraForma[6] = new PointF(13f, 20f);
            mascaraForma[7] = new PointF(10f, 24f);
            mascaraForma[8] = new PointF(5f, 28f);
            mascaraForma[9] = new PointF(-1f, 30f);
            mascaraForma[10] = new PointF(-7f, 27f);
            mascaraForma[11] = new PointF(-11f, 22f);
            mascaraForma[12] = new PointF(-13f, 15f);
            g.FillPolygon(obsidianaBrush, mascaraForma);
            g.DrawPolygon(articulacionPen, mascaraForma);
            mascaraInteriorForma[0] = new PointF(-7f, 9f);
            mascaraInteriorForma[1] = new PointF(-3f, 7f);
            mascaraInteriorForma[2] = new PointF(5f, 8f);
            mascaraInteriorForma[3] = new PointF(10f, 12f);
            mascaraInteriorForma[4] = new PointF(11f, 16f);
            mascaraInteriorForma[5] = new PointF(4f, 14f);
            mascaraInteriorForma[6] = new PointF(-8f, 17f);
            g.FillPolygon(armaduraBrush, mascaraInteriorForma);

            // Visor en chevrón, situado en el frente del perfil y sin rasgos humanos.
            g.DrawLine(filoVioletaPen, -7f, 11f, -5f, 19f);
            g.DrawLine(filoCianPen, 3f, 14f, 8f, 12f);
            g.DrawLine(filoCianPen, 8f, 12f, 13f, 15f);
            g.DrawLine(visorInteriorPen, 4f, 14f, 8f, 13f);
            g.DrawLine(visorInteriorPen, 9f, 13f, 12f, 15f);
            g.DrawLine(filoVioletaPen, 1f, 20f, 7f, 22f);
            g.DrawLine(articulacionPen, 2f, 26f, -2f, 28f);

            g.Restore(estado);
            DibujarDestelloJugador(g, x, y);
        }

        private void DibujarDestelloJugador(Graphics g, float x, float y)
        {
            if (invulnerabilidadRestante <= 0f)
                return;

            float transcurrido = DuracionInvulnerabilidad - invulnerabilidadRestante;
            float pulso = 0.55f + 0.45f * (float)Math.Sin(transcurrido * 38f);
            if (transcurrido > 0.18f && pulso < 0.42f)
                return;

            int alfa = (int)(95f + pulso * 130f);
            destelloCombatePen.Color = Color.FromArgb(alfa, 190, 245, 255);
            g.DrawRectangle(destelloCombatePen, x - 2f, y - 3f,
                AnchoJugador + 4f, AltoJugador + 5f);
        }

        private void DibujarPoseFrontal(Graphics g)
        {
            float balanceo = (float)Math.Sin(tiempoAnimacion * 2.4f);

            // Dos faldones cortos se abren detrás de la silueta frontal.
            estelaForma[0] = new PointF(-6f, 33f);
            estelaForma[1] = new PointF(-17f, 39f + balanceo * 1.2f);
            estelaForma[2] = new PointF(-14f, 60f + balanceo * 1.5f);
            estelaForma[3] = new PointF(-6f, 54f);
            g.FillPolygon(obsidianaBrush, estelaForma);
            g.DrawPolygon(articulacionPen, estelaForma);
            g.DrawLine(filoVioletaPen, -8f, 38f, -14f, 55f + balanceo);

            estelaForma[0] = new PointF(6f, 33f);
            estelaForma[1] = new PointF(16f, 40f - balanceo);
            estelaForma[2] = new PointF(13f, 57f - balanceo * 1.3f);
            estelaForma[3] = new PointF(6f, 53f);
            g.FillPolygon(armaduraBrush, estelaForma);
            g.DrawPolygon(articulacionPen, estelaForma);
            g.DrawLine(filoCianPen, 8f, 39f, 13f, 52f - balanceo);

            // Piernas separadas para una lectura clara incluso a escala pequeña.
            g.DrawLine(siluetaPen, -5f, 52f, -7f, 72f);
            g.DrawLine(extremidadPen, -5f, 52f, -7f, 72f);
            g.DrawLine(siluetaPen, 5f, 52f, 7f, 72f);
            g.DrawLine(extremidadPen, 5f, 52f, 7f, 72f);
            g.DrawEllipse(articulacionPen, -9f, 59f, 5f, 5f);
            g.DrawEllipse(articulacionPen, 4f, 59f, 5f, 5f);
            g.DrawLine(filoCianPen, -11f, 75f, -3f, 75f);
            g.DrawLine(filoCianPen, 3f, 75f, 11f, 75f);

            // Brazos visibles a ambos lados, con hombros y codos marcados.
            g.DrawLine(siluetaPen, -8f, 34f, -13f, 48f + balanceo * 0.4f);
            g.DrawLine(extremidadPen, -8f, 34f, -13f, 48f + balanceo * 0.4f);
            g.DrawLine(siluetaPen, 8f, 34f, 13f, 48f - balanceo * 0.4f);
            g.DrawLine(extremidadPen, 8f, 34f, 13f, 48f - balanceo * 0.4f);
            g.DrawEllipse(articulacionPen, -11f, 35f, 6f, 6f);
            g.DrawEllipse(articulacionPen, 5f, 35f, 6f, 6f);
            g.DrawLine(filoVioletaPen, -13f, 44f, -11f, 48f + balanceo * 0.4f);
            g.DrawLine(filoCianPen, 13f, 44f, 11f, 48f - balanceo * 0.4f);

            // Pecho blindado simétrico alrededor del reactor cian.
            torsoForma[0] = new PointF(-9f, 33f);
            torsoForma[1] = new PointF(-7f, 30f);
            torsoForma[2] = new PointF(7f, 30f);
            torsoForma[3] = new PointF(9f, 33f);
            torsoForma[4] = new PointF(8f, 49f);
            torsoForma[5] = new PointF(5f, 56f);
            torsoForma[6] = new PointF(-5f, 56f);
            torsoForma[7] = new PointF(-8f, 49f);
            g.FillPolygon(obsidianaBrush, torsoForma);
            torsoInteriorForma[0] = new PointF(-6f, 34f);
            torsoInteriorForma[1] = new PointF(-4f, 32f);
            torsoInteriorForma[2] = new PointF(4f, 32f);
            torsoInteriorForma[3] = new PointF(6f, 34f);
            torsoInteriorForma[4] = new PointF(5f, 48f);
            torsoInteriorForma[5] = new PointF(3f, 52f);
            torsoInteriorForma[6] = new PointF(-3f, 52f);
            torsoInteriorForma[7] = new PointF(-5f, 48f);
            g.FillPolygon(armaduraBrush, torsoInteriorForma);
            g.DrawPolygon(articulacionPen, torsoForma);
            g.DrawLine(filoCianPen, -5f, 37f, -2f, 35f);
            g.DrawLine(filoCianPen, 2f, 35f, 5f, 37f);
            g.FillEllipse(cianLuzBrush, -3f, 40f, 6f, 6f);
            g.DrawLine(visorInteriorPen, 0f, 46f, 0f, 51f);
            g.DrawLine(filoVioletaPen, -5f, 50f, 5f, 50f);

            // Máscara frontal geométrica con dos ranuras cian y una cresta central.
            mascaraForma[0] = new PointF(-9f, 8f);
            mascaraForma[1] = new PointF(-7f, 4f);
            mascaraForma[2] = new PointF(-3f, 2f);
            mascaraForma[3] = new PointF(3f, 2f);
            mascaraForma[4] = new PointF(7f, 4f);
            mascaraForma[5] = new PointF(9f, 8f);
            mascaraForma[6] = new PointF(11f, 15f);
            mascaraForma[7] = new PointF(9f, 22f);
            mascaraForma[8] = new PointF(5f, 27f);
            mascaraForma[9] = new PointF(0f, 30f);
            mascaraForma[10] = new PointF(-5f, 27f);
            mascaraForma[11] = new PointF(-9f, 22f);
            mascaraForma[12] = new PointF(-11f, 15f);
            g.FillPolygon(obsidianaBrush, mascaraForma);
            g.DrawPolygon(articulacionPen, mascaraForma);
            mascaraInteriorForma[0] = new PointF(-7f, 9f);
            mascaraInteriorForma[1] = new PointF(-4f, 7f);
            mascaraInteriorForma[2] = new PointF(4f, 7f);
            mascaraInteriorForma[3] = new PointF(7f, 9f);
            mascaraInteriorForma[4] = new PointF(8f, 15f);
            mascaraInteriorForma[5] = new PointF(0f, 17f);
            mascaraInteriorForma[6] = new PointF(-8f, 15f);
            g.FillPolygon(armaduraBrush, mascaraInteriorForma);
            g.DrawLine(filoVioletaPen, -4f, 7f, 0f, 4f);
            g.DrawLine(filoVioletaPen, 0f, 4f, 4f, 7f);
            g.DrawLine(filoCianPen, -7f, 13f, -2f, 15f);
            g.DrawLine(visorInteriorPen, -6f, 13.5f, -3f, 14.5f);
            g.DrawLine(filoCianPen, 2f, 15f, 7f, 13f);
            g.DrawLine(visorInteriorPen, 3f, 14.5f, 6f, 13.5f);
            g.DrawLine(filoVioletaPen, 0f, 17f, 0f, 22f);
            g.DrawLine(articulacionPen, -4f, 24f, 0f, 27f);
        }

        private void DibujarEnemigos(Graphics g)
        {
            foreach (Enemigo enemigo in EnemigosActivos)
            {
                if (!enemigo.Vivo)
                {
                    if (enemigo.TiempoDesintegracion > 0f)
                        DibujarDesintegracionEnemigo(g, enemigo);
                    continue;
                }

                if (enemigo.Tipo == TipoEnemigo.Centinela)
                    DibujarCentinela(g, enemigo);
                else
                    DibujarAnomalia(g, enemigo);

                if (enemigo.TiempoDestello > 0f)
                {
                    float intensidad = enemigo.TiempoDestello / 0.14f;
                    destelloCombatePen.Color = Color.FromArgb(
                        (int)(90f + intensidad * 165f), 205, 245, 255);
                    g.DrawRectangle(destelloCombatePen, enemigo.X - 2f, enemigo.Y - 2f,
                        enemigo.Ancho + 4f, enemigo.Alto + 4f);
                }

                DibujarEstadoEnemigo(g, enemigo);
            }
        }

        private void DibujarDesintegracionEnemigo(Graphics g, Enemigo enemigo)
        {
            float progreso = 1f - enemigo.TiempoDesintegracion / 0.52f;
            float centroX = enemigo.X + enemigo.Ancho * 0.5f;
            float centroY = enemigo.Y + enemigo.Alto * 0.5f;

            for (int i = 0; i < 6; i++)
            {
                float angulo = i * (MathF.PI / 3f) + enemigo.Fase * 1.8f;
                float distancia = 4f + progreso * (15f + (i % 3) * 4f);
                float x = centroX + MathF.Cos(angulo) * distancia;
                float y = centroY + MathF.Sin(angulo) * distancia - progreso * 7f;
                float tamano = (4f + (i % 2) * 2f) * (1f - progreso * 0.35f);
                Brush particula = i % 3 == 0 ? residuoBlancoBrush
                    : i % 2 == 0 ? residuoCianBrush : residuoVioletaBrush;
                g.FillRectangle(particula, x - tamano * 0.5f, y - tamano * 0.5f,
                    tamano, tamano);
            }
        }

        private void DibujarCentinela(Graphics g, Enemigo enemigo)
        {
            float x = enemigo.X;
            float y = enemigo.Y;
            float ojoX = enemigo.Direccion >= 0f ? x + 28f : x + 12f;

            g.FillEllipse(sombraBrush, x + 2f, y + 44f, 42f, 8f);
            g.DrawLine(articulacionPen, x + 14f, y + 36f, x + 11f, y + 48f);
            g.DrawLine(articulacionPen, x + 32f, y + 36f, x + 35f, y + 48f);
            g.DrawLine(filoCianPen, x + 7f, y + 49f, x + 16f, y + 49f);
            g.DrawLine(filoCianPen, x + 30f, y + 49f, x + 39f, y + 49f);

            g.FillRectangle(obsidianaBrush, x + 7f, y + 13f, 32f, 27f);
            g.FillRectangle(metalBrush, x + 10f, y + 16f, 26f, 20f);
            g.DrawRectangle(articulacionPen, x + 7f, y + 13f, 32f, 27f);
            g.DrawLine(tenuePen, x + 12f, y + 28f, x + 34f, y + 28f);
            g.FillRectangle(panelBrush, x + 9f, y + 2f, 28f, 18f);
            g.DrawRectangle(articulacionPen, x + 9f, y + 2f, 28f, 18f);
            g.DrawLine(enemigo.EnAlerta ? violetaPen : filoCianPen,
                ojoX, y + 9f, ojoX + enemigo.Direccion * 5f, y + 9f);
            g.DrawLine(grietaPen, x + 22f, y - 3f, x + 22f, y + 2f);
            g.FillEllipse(enemigo.EnAlerta ? cianLuzBrush : violetaBrush,
                x + 20f, y - 6f, 5f, 5f);
            if (enemigo.EnAlerta)
                g.DrawEllipse(violetaPen, x + 17f, y - 9f, 11f, 11f);
            g.DrawLine(articulacionPen, x + 8f, y + 20f, x + 2f, y + 31f);
            g.DrawLine(articulacionPen, x + 38f, y + 20f, x + 44f, y + 31f);
        }

        private void DibujarAnomalia(Graphics g, Enemigo enemigo)
        {
            float centroX = enemigo.X + enemigo.Ancho * 0.5f;
            float centroY = enemigo.Y + enemigo.Alto * 0.5f;
            PointF[] forma = enemigo.Forma;
            forma[0] = new PointF(-14f, -5f);
            forma[1] = new PointF(-7f, -19f);
            forma[2] = new PointF(3f, -11f);
            forma[3] = new PointF(16f, -15f);
            forma[4] = new PointF(18f, 1f);
            forma[5] = new PointF(8f, 7f);
            forma[6] = new PointF(12f, 17f);
            forma[7] = new PointF(-8f, 13f);

            var estado = g.Save();
            g.TranslateTransform(centroX, centroY);
            g.RotateTransform((float)Math.Sin(enemigo.Fase * 0.7f) * 12f);
            g.FillEllipse(residuoVioletaBrush, -21f, -21f, 42f, 42f);
            g.FillPolygon(realidadFragmentoBrush, forma);
            g.DrawPolygon(realidadVioletaPen, forma);
            g.FillEllipse(cianLuzBrush, -4f, -4f, 8f, 8f);
            g.DrawLine(realidadBlancoPen, -9f, -2f, 1f, -12f);
            g.DrawLine(realidadCianPen, 5f, 5f, 13f, 1f);
            g.Restore(estado);
        }

        private void DibujarEstadoEnemigo(Graphics g, Enemigo enemigo)
        {
            string nombre = enemigo.Tipo == TipoEnemigo.Centinela ? "CENTINELA" : "ANOMALIA";
            Brush color = enemigo.Tipo == TipoEnemigo.Centinela ? cianBrush : violetaBrush;
            float centroX = enemigo.X + enemigo.Ancho * 0.5f;
            float yTexto = enemigo.Y - 28f;
            float anchoBarra = 48f;

            g.DrawString(nombre, identificadorFont, color, centroX - 36f, yTexto);
            g.FillRectangle(obsidianaBrush, centroX - anchoBarra * 0.5f, enemigo.Y - 13f,
                anchoBarra, 5f);
            float relleno = anchoBarra * enemigo.Integridad / enemigo.IntegridadMaxima;
            if (relleno > 0f)
                g.FillRectangle(color, centroX - anchoBarra * 0.5f, enemigo.Y - 13f,
                    relleno, 5f);
            g.DrawRectangle(tenuePen, centroX - anchoBarra * 0.5f, enemigo.Y - 13f,
                anchoBarra, 5f);
        }

        private void DibujarAtaqueJugador(Graphics g)
        {
            if (ataqueActivoRestante <= 0f)
                return;

            RectangleF zona = ObtenerZonaAtaque();
            float direccion = ataqueMirandoDerecha ? 1f : -1f;
            float progreso = 1f - ataqueActivoRestante / DuracionAtaque;
            float inicioX = ataqueMirandoDerecha ? zona.Left : zona.Right;
            float finX = inicioX + direccion * zona.Width * (0.68f + progreso * 0.32f);
            float inicioY = jugadorY + 47f;
            float controlUnoX = inicioX + direccion * zona.Width * 0.28f;
            float controlDosX = inicioX + direccion * zona.Width * 0.72f;
            float controlUnoY = inicioY + 9f - progreso * 4f;
            float controlDosY = inicioY - 21f - progreso * 7f;
            float finY = inicioY - 13f + progreso * 5f;

            g.DrawBezier(ataqueHaloPen, inicioX, inicioY,
                controlUnoX, controlUnoY, controlDosX, controlDosY, finX, finY);
            g.DrawBezier(ataqueCianPen, inicioX, inicioY,
                controlUnoX, controlUnoY, controlDosX, controlDosY, finX, finY);
        }

        private void DibujarInterfaz(Graphics g)
        {
            g.DrawString("NOCTRYX // SYSTEM COLLAPSE", tituloFont, cianBrush, 28, 22);
            g.DrawString(demoCompletada
                    ? "DEMO COMPLETADA"
                    : mapaActual == MapaActivo.CiudadEnColapso
                        ? "MAPA 1 // CIUDAD EN COLAPSO"
                        : "MAPA 2 // REALIDAD HOSTIL",
                hudFont, blancoBrush, 30, 48);
            g.DrawString("A / ← IZQUIERDA   D / → DERECHA   ESPACIO SALTAR   J ATACAR   E INTERACTUAR   ESC PAUSA",
                hudFont, violetaBrush, 30, AltoVista - 25);
            g.DrawString(derrotado
                    ? "ESTADO: DERROTADO"
                    : demoCompletada
                        ? "SEÑAL RECUPERADA"
                        : transicionMapaEnCurso
                            ? "ESTADO: CRUCE DIMENSIONAL"
                        : enSuelo ? "ESTADO: EN SUELO" : "ESTADO: EN AIRE",
                identificadorFont, blancoBrush, AnchoVista - 180, 26);

            g.DrawString("INTEGRIDAD", hudFont, blancoBrush, 30, 70);
            g.FillRectangle(sombraBrush, 30f, 88f, 204f, 13f);
            float anchoBarra = 200f * integridadJugador / IntegridadMaximaJugador;
            if (anchoBarra > 0f)
                g.FillRectangle(cianBrush, 32f, 90f, anchoBarra, 9f);
            g.DrawRectangle(tenuePen, 30f, 88f, 204f, 13f);
            g.DrawString($"{integridadJugador}/{IntegridadMaximaJugador}",
                hudFont, blancoBrush, 242f, 87f);
            g.DrawString(mapaActual == MapaActivo.CiudadEnColapso
                    ? "OBJETIVO // CRUZA LA GRIETA (E) O ALCANZA LA SALIDA"
                    : "OBJETIVO // RECUPERA LA SEÑAL (E)",
                hudFont, blancoBrush, 30f, 108f);
            if (invulnerabilidadRestante > 0f && !derrotado)
                g.DrawString("INVULNERABLE", identificadorFont, cianLuzBrush, 325f, 88f);

            bool registroDisponible = ecoEnZonaRegistro &&
                ((mapaActual == MapaActivo.CiudadEnColapso && !registroCiudadLeido) ||
                 (mapaActual == MapaActivo.RealidadHostil && !registroRealidadLeido));
            if (registroDisponible && !derrotado && !demoCompletada)
            {
                float avisoX = Math.Clamp(jugadorX - camaraX + 38f, 8f, AnchoVista - 200f);
                float avisoY = Math.Clamp(jugadorY - 18f, 112f, AltoVista - 30f);
                g.FillRectangle(sombraBrush, avisoX - 4f, avisoY - 2f, 190f, 18f);
                g.DrawString("E — LEER REGISTRO", hudFont, cianLuzBrush, avisoX, avisoY);
            }

            if (!registroDisponible && ecoEnZonaGrieta && !derrotado && !demoCompletada &&
                mapaActual == MapaActivo.CiudadEnColapso &&
                estadoTransicionGrieta == EstadoTransicionGrieta.Inactiva)
            {
                float avisoX = Math.Clamp(jugadorX - camaraX + 48f, 8f, AnchoVista - 150f);
                float avisoY = Math.Clamp(jugadorY + 10f, 58f, AltoVista - 30f);
                g.FillRectangle(sombraBrush, avisoX - 4f, avisoY - 2f, 142f, 18f);
                g.DrawString("E", hudFont, cianBrush, avisoX, avisoY);
                g.DrawString("— INTERACTUAR", hudFont, blancoBrush, avisoX + 10f, avisoY);
            }

            if (!registroDisponible && ecoEnZonaObjetivo && !derrotado && !demoCompletada && !transicionMapaEnCurso)
            {
                float avisoX = Math.Clamp(jugadorX - camaraX + 38f, 8f, AnchoVista - 150f);
                float avisoY = Math.Clamp(jugadorY - 18f, 112f, AltoVista - 30f);
                g.FillRectangle(sombraBrush, avisoX - 4f, avisoY - 2f, 142f, 18f);
                g.DrawString("E — RECUPERAR SEÑAL", hudFont, blancoBrush, avisoX, avisoY);
            }

            if (registroNarrativoVisible != 0 && tiempoRegistroVisible > 0f &&
                !derrotado && !demoCompletada)
            {
                RectangleF panelRegistro = new(170f, 140f, 940f, 130f);
                g.FillRectangle(panelBrush, panelRegistro);
                g.DrawRectangle(registroNarrativoVisible == 1 ? cianPen : violetaPen,
                    panelRegistro.X, panelRegistro.Y, panelRegistro.Width, panelRegistro.Height);
                string tituloRegistro = registroNarrativoVisible == 1
                    ? "REGISTRO 01 // CIUDAD EN COLAPSO"
                    : "REGISTRO 02 // REALIDAD HOSTIL";
                string textoRegistro = registroNarrativoVisible == 1
                    ? TextoRegistroCiudad : TextoRegistroRealidad;
                g.DrawString(tituloRegistro, hudFont,
                    registroNarrativoVisible == 1 ? cianLuzBrush : violetaBrush,
                    panelRegistro.X + 22f, panelRegistro.Y + 14f);
                g.DrawString(textoRegistro, controlesMenuFont, blancoBrush,
                    new RectangleF(panelRegistro.X + 22f, panelRegistro.Y + 44f,
                        panelRegistro.Width - 44f, panelRegistro.Height - 54f));
            }

            if (estadoInterfaz == EstadoInterfaz.Pausa)
            {
                DibujarPanelPausa(g);
                return;
            }

            if (derrotado)
            {
                RectangleF panelDerrota = new(350f, 225f, 580f, 330f);
                g.FillRectangle(sombraBrush, panelDerrota);
                g.DrawRectangle(violetaPen, panelDerrota.X, panelDerrota.Y,
                    panelDerrota.Width, panelDerrota.Height);
                g.DrawString("SISTEMA COMPROMETIDO", finalFont, violetaBrush,
                    panelDerrota.X + 25f, panelDerrota.Y + 30f);
                DibujarBotonInterfaz(g, botonesResultado[0], "REINTENTAR  (R)", seleccionResultado == 0);
                DibujarBotonInterfaz(g, botonesResultado[1], "VOLVER AL MENÚ", seleccionResultado == 1);
            }
            else if (demoCompletada)
            {
                RectangleF panelFinal = new(250f, 95f, 780f, 560f);
                g.FillRectangle(panelBrush, panelFinal);
                g.DrawRectangle(cianPen, panelFinal.X, panelFinal.Y,
                    panelFinal.Width, panelFinal.Height);
                DibujarTextoCentrado(g, "SEÑAL RECUPERADA", finalFont, cianBrush, 125f);
                DibujarTextoCentrado(g, "TRANSMISIÓN FRAGMENTADA", opcionMenuFont, violetaBrush, 202f);
                DibujarTextoCentrado(g, "ECHO... ¿puedes escucharme?", controlesMenuFont, blancoBrush, 244f);
                DibujarTextoCentrado(g, "No confíes en lo que recuerdas.", controlesMenuFont, blancoBrush, 275f);
                DibujarTextoCentrado(g, "El colapso no fue un accidente.", controlesMenuFont, blancoBrush, 306f);
                DibujarTextoCentrado(g, "Fuiste tú quien abrió la primera puerta.", controlesMenuFont, blancoBrush, 337f);
                DibujarTextoCentrado(g, "NOCTRYX: SYSTEM COLLAPSE", subtituloMenuFont, cianLuzBrush, 385f);
                DibujarTextoCentrado(g, "FIN DE LA DEMO", opcionMenuFont, blancoBrush, 419f);
                DibujarBotonInterfaz(g, botonesVictoria[0], "REINICIAR  (R)", seleccionResultado == 0);
                DibujarBotonInterfaz(g, botonesVictoria[1], "VOLVER AL MENÚ", seleccionResultado == 1);
            }
        }
    }
}
