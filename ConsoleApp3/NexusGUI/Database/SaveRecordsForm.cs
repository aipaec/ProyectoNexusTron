using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace NexusGUI.Database;

/// <summary>Gestor visual de partidas guardadas de NOCTRYX.</summary>
public sealed class SaveRecordsForm : Form
{
    private sealed record Partida(int Id, string Nombre, string Mapa, int Integridad,
        float? PosicionX, float? PosicionY, DateTime FechaGuardado)
    {
        public bool Cargable => PosicionX.HasValue && PosicionY.HasValue;
    }

    private static readonly string connectionString = new SqlConnectionStringBuilder
    {
        DataSource = Environment.GetEnvironmentVariable("NOCTRYX_LOCALDB_SERVER")
            ?? @"(localdb)\MSSQLLocalDB",
        InitialCatalog = "NOCTRYX_H4",
        IntegratedSecurity = true,
        Encrypt = true,
        TrustServerCertificate = true,
        ConnectTimeout = 4
    }.ConnectionString;

    private readonly Func<GameProgressSnapshot> capturarProgreso;
    private readonly Func<GameProgressSnapshot, string?> restaurarProgreso;
    private readonly Font fuenteLista = new("Consolas", 10f, FontStyle.Regular);
    private readonly Font fuenteNombreCampo = new("Segoe UI", 10f);
    private readonly Font fuenteEstadoLabel = new("Consolas", 9f);
    private readonly Font fuenteFormulario = new("Segoe UI", 9f);
    private readonly Font fuenteBoton = new("Consolas", 9f, FontStyle.Bold);
    private readonly ListBox listaPartidas = new()
    {
        Dock = DockStyle.Fill,
        DrawMode = DrawMode.OwnerDrawFixed,
        ItemHeight = 82,
        IntegralHeight = false,
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(7, 11, 23),
        ForeColor = Color.FromArgb(222, 236, 247),
        SelectionMode = SelectionMode.One
    };
    private readonly TextBox nombrePartida = new()
    {
        Width = 290,
        MaxLength = 60,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.FromArgb(15, 23, 40),
        ForeColor = Color.FromArgb(225, 240, 250),
    };
    private readonly Label estado = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(142, 170, 192)
    };
    private readonly Button botonGuardar = new() { Text = "GUARDAR PARTIDA", Enabled = false };
    private readonly Button botonCargar = new() { Text = "CARGAR", Enabled = false };
    private readonly Button botonEliminar = new() { Text = "ELIMINAR", Enabled = false };
    private readonly Button botonNueva = new() { Text = "NUEVA", Enabled = false };
    private readonly Button botonActualizar = new() { Text = "ACTUALIZAR" };
    private readonly SolidBrush tarjetaBrush = new(Color.FromArgb(20, 30, 49));
    private readonly SolidBrush tarjetaSeleccionadaBrush = new(Color.FromArgb(29, 54, 78));
    private readonly SolidBrush acentoCianBrush = new(Color.FromArgb(0, 225, 255));
    private readonly SolidBrush textoPrincipalBrush = new(Color.FromArgb(230, 241, 250));
    private readonly SolidBrush textoTenueBrush = new(Color.FromArgb(147, 174, 195));
    private readonly SolidBrush violetaBrush = new(Color.FromArgb(190, 85, 255));
    private readonly Pen bordeCianPen = new(Color.FromArgb(170, 0, 225, 255), 1.4f);
    private readonly Pen separadorPen = new(Color.FromArgb(60, 98, 132), 1f);
    private readonly Font tituloFont = new("Segoe UI", 21f, FontStyle.Bold);
    private readonly Font subtituloFont = new("Consolas", 9f, FontStyle.Bold);
    private readonly Font nombreTarjetaFont = new("Segoe UI", 11f, FontStyle.Bold);
    private readonly Font detalleTarjetaFont = new("Consolas", 9f, FontStyle.Regular);
    private int? selectedId;

    public SaveRecordsForm(Func<GameProgressSnapshot> captureProgress,
        Func<GameProgressSnapshot, string?> restoreProgress)
    {
        capturarProgreso = captureProgress ?? throw new ArgumentNullException(nameof(captureProgress));
        restaurarProgreso = restoreProgress ?? throw new ArgumentNullException(nameof(restoreProgress));

        Text = "NOCTRYX // PARTIDAS GUARDADAS";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 620);
        MinimumSize = new Size(760, 520);
        BackColor = Color.FromArgb(7, 10, 22);
        ForeColor = Color.FromArgb(225, 240, 250);
        Font = fuenteFormulario;
        KeyPreview = true;
        listaPartidas.Font = fuenteLista;
        nombrePartida.Font = fuenteNombreCampo;
        estado.Font = fuenteEstadoLabel;

        ConstruirInterfaz();
        listaPartidas.DrawItem += DibujarPartida;
        listaPartidas.SelectedIndexChanged += (_, _) => ActualizarSeleccion();
        botonGuardar.Click += (_, _) => GuardarPartida();
        botonCargar.Click += (_, _) => CargarPartidaSeleccionada();
        botonEliminar.Click += (_, _) => EliminarPartida();
        botonNueva.Click += (_, _) => PrepararNuevaPartida();
        botonActualizar.Click += (_, _) => CargarPartidas(selectedId);
        nombrePartida.TextChanged += (_, _) => ActualizarEstadoBotones();
        KeyDown += ManejarTeclas;
        Shown += (_, _) => CargarPartidas(null);
    }

    private void ConstruirInterfaz()
    {
        var cabecera = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(28, 17, 24, 10) };
        cabecera.Paint += (_, e) =>
        {
            e.Graphics.DrawLine(bordeCianPen, 28, cabecera.Height - 2, cabecera.Width - 28, cabecera.Height - 2);
            e.Graphics.DrawLine(separadorPen, 28, cabecera.Height - 5, 212, cabecera.Height - 5);
        };
        var titulo = new Label
        {
            Text = "PARTIDAS GUARDADAS",
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 225, 255),
            Font = tituloFont,
            Location = new Point(26, 10)
        };
        var subtitulo = new Label
        {
            Text = "NOCTRYX // ARCHIVO DE MEMORIA LOCAL",
            AutoSize = true,
            ForeColor = Color.FromArgb(185, 127, 255),
            Font = subtituloFont,
            Location = new Point(30, 53)
        };
        cabecera.Controls.Add(titulo);
        cabecera.Controls.Add(subtitulo);

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 154, Padding = new Padding(24, 10, 24, 14) };
        pie.Paint += (_, e) => e.Graphics.DrawLine(separadorPen, 24, 0, pie.Width - 24, 0);
        var etiquetaNombre = new Label
        {
            Text = "IDENTIFICACIÓN DE PARTIDA",
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 225, 255),
            Font = subtituloFont,
            Location = new Point(25, 16)
        };
        nombrePartida.Location = new Point(25, 38);
        var filaBotones = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 7, 0, 0)
        };
        EstilizarBoton(botonGuardar, true);
        EstilizarBoton(botonCargar);
        EstilizarBoton(botonNueva);
        EstilizarBoton(botonEliminar);
        EstilizarBoton(botonActualizar);
        filaBotones.Controls.AddRange([botonGuardar, botonCargar, botonNueva, botonEliminar, botonActualizar]);
        estado.Location = new Point(25, 91);
        pie.Controls.Add(etiquetaNombre);
        pie.Controls.Add(nombrePartida);
        pie.Controls.Add(filaBotones);
        pie.Controls.Add(estado);

        var marcoLista = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 12, 22, 10) };
        var etiquetaLista = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "MEMORIAS DISPONIBLES",
            ForeColor = Color.FromArgb(150, 184, 208),
            Font = subtituloFont,
            TextAlign = ContentAlignment.MiddleLeft
        };
        marcoLista.Controls.Add(listaPartidas);
        marcoLista.Controls.Add(etiquetaLista);
        Controls.Add(marcoLista);
        Controls.Add(pie);
        Controls.Add(cabecera);
    }

    private void EstilizarBoton(Button boton, bool primario = false)
    {
        boton.AutoSize = true;
        boton.Height = 32;
        boton.FlatStyle = FlatStyle.Flat;
        boton.FlatAppearance.BorderColor = primario ? Color.FromArgb(0, 205, 235) : Color.FromArgb(83, 103, 138);
        boton.FlatAppearance.MouseOverBackColor = Color.FromArgb(31, 53, 78);
        boton.BackColor = primario ? Color.FromArgb(10, 48, 67) : Color.FromArgb(16, 23, 40);
        boton.ForeColor = primario ? Color.FromArgb(135, 246, 255) : Color.FromArgb(214, 229, 242);
        boton.Font = fuenteBoton;
        boton.Margin = new Padding(0, 0, 9, 0);
        boton.Cursor = Cursors.Hand;
    }

    private void DibujarPartida(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= listaPartidas.Items.Count)
            return;

        var partida = (Partida)listaPartidas.Items[e.Index]!;
        Rectangle rect = Rectangle.Inflate(e.Bounds, -3, -3);
        bool seleccionada = (e.State & DrawItemState.Selected) != 0;
        e.Graphics.FillRectangle(seleccionada ? tarjetaSeleccionadaBrush : tarjetaBrush, rect);
        if (seleccionada)
            e.Graphics.DrawRectangle(bordeCianPen, rect);
        else
            e.Graphics.DrawLine(separadorPen, rect.Left + 12, rect.Bottom - 1, rect.Right - 12, rect.Bottom - 1);

        float x = rect.Left + 17;
        float y = rect.Top + 11;
        e.Graphics.FillRectangle(acentoCianBrush, x, y + 2, 3, 42);
        e.Graphics.DrawString(partida.Nombre, nombreTarjetaFont, textoPrincipalBrush, x + 13, y);
        string mapa = partida.Mapa == "CIUDAD EN COLAPSO" ? "MAPA 01  //  CIUDAD EN COLAPSO" : "MAPA 02  //  REALIDAD HOSTIL";
        e.Graphics.DrawString(mapa, detalleTarjetaFont, violetaBrush, x + 13, y + 24);
        string posicion = partida.Cargable ? $"X {partida.PosicionX:0}  /  Y {partida.PosicionY:0}" : "POSICIÓN NO DISPONIBLE";
        string derecha = $"INTEGRIDAD  {partida.Integridad,3}%     {partida.FechaGuardado.ToLocalTime():yyyy-MM-dd HH:mm}";
        SizeF medidaDerecha = e.Graphics.MeasureString(derecha, detalleTarjetaFont);
        e.Graphics.DrawString(derecha, detalleTarjetaFont, textoPrincipalBrush,
            Math.Max(x + 250, rect.Right - medidaDerecha.Width - 15), y + 2);
        e.Graphics.DrawString(posicion, detalleTarjetaFont, textoTenueBrush,
            Math.Max(x + 250, rect.Right - 190), y + 27);
        e.DrawFocusRectangle();
    }

    private void CargarPartidas(int? idASeleccionar)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            using var adapter = new SqlDataAdapter(
                "SELECT RegistroId, Nombre, Mapa, Integridad, PosicionX, PosicionY, FechaGuardado FROM dbo.RegistrosPartida ORDER BY FechaGuardado DESC;",
                connection);
            var datos = new DataTable();
            adapter.Fill(datos);
            listaPartidas.BeginUpdate();
            listaPartidas.Items.Clear();
            foreach (DataRow fila in datos.Rows)
            {
                listaPartidas.Items.Add(new Partida(
                    Convert.ToInt32(fila["RegistroId"]),
                    Convert.ToString(fila["Nombre"]) ?? "PARTIDA SIN NOMBRE",
                    Convert.ToString(fila["Mapa"]) ?? "",
                    Convert.ToInt32(fila["Integridad"]),
                    fila["PosicionX"] == DBNull.Value ? null : Convert.ToSingle(fila["PosicionX"]),
                    fila["PosicionY"] == DBNull.Value ? null : Convert.ToSingle(fila["PosicionY"]),
                    Convert.ToDateTime(fila["FechaGuardado"])));
            }
            listaPartidas.EndUpdate();
            selectedId = null;
            if (idASeleccionar.HasValue)
            {
                for (int i = 0; i < listaPartidas.Items.Count; i++)
                {
                    if (((Partida)listaPartidas.Items[i]!).Id != idASeleccionar.Value)
                        continue;
                    listaPartidas.SelectedIndex = i;
                    break;
                }
            }
            estado.Text = listaPartidas.Items.Count == 0
                ? "No hay partidas guardadas. Escribe una identificación y guarda la partida actual."
                : $"{listaPartidas.Items.Count} memoria(s) disponibles. Selecciona una para cargar, actualizar o eliminar.";
            ActualizarSeleccion();
        }
        catch (SqlException ex)
        {
            Debug.WriteLine($"NOCTRYX save list: {ex}");
            estado.Text = "No se pudo acceder al archivo de partidas.";
            MessageBox.Show(this,
                "No se pudieron consultar las partidas. Verifica que LocalDB esté instalado y que NOCTRYX_H4 se haya creado con el script de instalación.",
                "Archivo de partidas no disponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ActualizarSeleccion()
    {
        if (listaPartidas.SelectedItem is Partida partida)
        {
            selectedId = partida.Id;
            nombrePartida.Text = partida.Nombre;
            botonCargar.Enabled = partida.Cargable;
            botonEliminar.Enabled = true;
        }
        else
        {
            selectedId = null;
            botonCargar.Enabled = false;
            botonEliminar.Enabled = false;
        }
        ActualizarEstadoBotones();
        listaPartidas.Invalidate();
    }

    private void ActualizarEstadoBotones()
    {
        botonGuardar.Enabled = nombrePartida.Text.Trim().Length is > 0 and <= 60;
        botonNueva.Enabled = listaPartidas.Items.Count > 0;
    }

    private void PrepararNuevaPartida()
    {
        selectedId = null;
        listaPartidas.ClearSelected();
        nombrePartida.Clear();
        nombrePartida.Focus();
        estado.Text = "Escribe un identificador; se guardará el estado actual de ECHO-0.";
        ActualizarSeleccion();
    }

    private void GuardarPartida()
    {
        string nombre = nombrePartida.Text.Trim();
        if (nombre.Length is 0 or > 60)
        {
            MessageBox.Show(this, "Escribe una identificación de hasta 60 caracteres.",
                "Nombre de partida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            nombrePartida.Focus();
            return;
        }

        GameProgressSnapshot snapshot;
        try
        {
            snapshot = capturarProgreso();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "No se puede guardar ahora",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                if (selectedId.HasValue)
                {
                    command.CommandText = "UPDATE dbo.RegistrosPartida SET Nombre=@nombre, Mapa=@mapa, Integridad=@integridad, PosicionX=@x, PosicionY=@y, FechaGuardado=SYSUTCDATETIME() WHERE RegistroId=@id;";
                    command.Parameters.Add("@id", SqlDbType.Int).Value = selectedId.Value;
                }
                else
                {
                    command.CommandText = "INSERT INTO dbo.RegistrosPartida (Nombre, Mapa, Integridad, PosicionX, PosicionY) VALUES (@nombre, @mapa, @integridad, @x, @y); SELECT CONVERT(int, SCOPE_IDENTITY());";
                }
                command.Parameters.Add("@nombre", SqlDbType.NVarChar, 60).Value = nombre;
                command.Parameters.Add("@mapa", SqlDbType.NVarChar, 30).Value = snapshot.Map;
                command.Parameters.Add("@integridad", SqlDbType.Int).Value = snapshot.Integrity;
                command.Parameters.Add("@x", SqlDbType.Real).Value = snapshot.PositionX;
                command.Parameters.Add("@y", SqlDbType.Real).Value = snapshot.PositionY;
                object? result = command.ExecuteScalar();
                if (!selectedId.HasValue && result is not null && result != DBNull.Value)
                    selectedId = Convert.ToInt32(result);
                transaction.Commit();
            }
            catch
            {
                try { transaction.Rollback(); } catch (SqlException) { }
                throw;
            }

            int? idGuardado = selectedId;
            estado.Text = "Partida guardada: mapa, posición e integridad de ECHO-0 actualizados.";
            CargarPartidas(idGuardado);
        }
        catch (SqlException ex)
        {
            Debug.WriteLine($"NOCTRYX save: {ex}");
            estado.Text = "No se guardó la partida.";
            MessageBox.Show(this, "No se pudo guardar. La partida anterior permanece intacta; revisa la conexión a LocalDB e inténtalo otra vez.",
                "Guardado no completado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CargarPartidaSeleccionada()
    {
        if (listaPartidas.SelectedItem is not Partida partida || !partida.Cargable)
            return;

        var snapshot = new GameProgressSnapshot(partida.Mapa, partida.PosicionX!.Value,
            partida.PosicionY!.Value, partida.Integridad);
        string? error = restaurarProgreso(snapshot);
        if (error is not null)
        {
            MessageBox.Show(this, error, "No se pudo cargar la partida",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void EliminarPartida()
    {
        if (listaPartidas.SelectedItem is not Partida partida)
            return;
        if (MessageBox.Show(this, $"¿Eliminar la partida «{partida.Nombre}»? Esta acción no se puede deshacer.",
                "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                using var command = new SqlCommand(
                    "DELETE FROM dbo.RegistrosPartida WHERE RegistroId=@id;", connection, transaction);
                command.Parameters.Add("@id", SqlDbType.Int).Value = partida.Id;
                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch
            {
                try { transaction.Rollback(); } catch (SqlException) { }
                throw;
            }

            estado.Text = "Partida eliminada.";
            nombrePartida.Clear();
            CargarPartidas(null);
        }
        catch (SqlException ex)
        {
            Debug.WriteLine($"NOCTRYX delete save: {ex}");
            estado.Text = "No se pudo eliminar la partida.";
            MessageBox.Show(this, "No se pudo eliminar la partida. Comprueba la conexión e inténtalo de nuevo.",
                "Eliminación no completada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ManejarTeclas(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (botonCargar.Enabled && listaPartidas.Focused)
                CargarPartidaSeleccionada();
            else if (botonGuardar.Enabled && nombrePartida.Focused)
                GuardarPartida();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete && botonEliminar.Enabled)
        {
            EliminarPartida();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            base.Dispose(disposing);
            tarjetaBrush.Dispose();
            tarjetaSeleccionadaBrush.Dispose();
            acentoCianBrush.Dispose();
            textoPrincipalBrush.Dispose();
            textoTenueBrush.Dispose();
            violetaBrush.Dispose();
            bordeCianPen.Dispose();
            separadorPen.Dispose();
            tituloFont.Dispose();
            subtituloFont.Dispose();
            nombreTarjetaFont.Dispose();
            detalleTarjetaFont.Dispose();
            fuenteLista.Dispose();
            fuenteNombreCampo.Dispose();
            fuenteEstadoLabel.Dispose();
            fuenteFormulario.Dispose();
            fuenteBoton.Dispose();
            return;
        }
        base.Dispose(disposing);
    }
}
