// GNS Clock - powered by Tech House - v3
// Italic digits, gradient fill (2 colors) + edge color, presets, screensaver mode
// Right-click for menu. Drag to move. Drag edges to resize. Double-click = 12/24h.
// Screensaver mode: fullscreen clock; press any key or click to exit.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GNSClock
{
    public class ClockForm : Form
    {
        private Timer timer;
        private ContextMenuStrip menu;
        private NotifyIcon tray;

        private bool is24 = false;
        private bool showDate = true;
        private bool transparentBg = false;
        private bool gradientFill = true;
        private Color bgColor = Color.FromArgb(18, 18, 24);
        private Color fillColor = Color.FromArgb(0, 229, 255);    // gradient top
        private Color fillColor2 = Color.FromArgb(41, 121, 255);  // gradient bottom
        private Color edgeColor = Color.FromArgb(8, 34, 84);      // outline
        private string imagePath = "";

        // screensaver mode state
        private bool ssMode = false;
        private bool ssAuto = false;       // entered automatically by idle timer
        private int ssTimeout = 0;         // seconds of idle before auto screensaver; 0 = off
        private Rectangle savedBounds;
        private bool savedTrans;
        private ToolStripMenuItem ssTimeMenu;
        private System.Collections.Generic.List<Form> ssCovers = new System.Collections.Generic.List<Form>();
        private StopwatchForm swForm = null;

        // flip animation state per card (HH, MM, SS)
        private string[] flipCur = new string[] { "", "", "" };
        private string[] flipOld = new string[] { "", "", "" };
        private int[] flipT = new int[] { 0, 0, 0 };
        private const int FlipMs = 300;

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private ToolStripMenuItem mi24, miDate, miTop, miTransparent, miStartup, miGradient, miSs;

        // near-black transparency key: edge halo blends to a soft dark shadow instead of pink
        private static readonly Color KeyColor = Color.FromArgb(1, 1, 2);
        private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "GNSClock";
        private string settingsFile;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public ClockForm()
        {
            settingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GNSClock", "settings.ini");

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            MinimumSize = new Size(150, 60);
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = bgColor;
            Text = "GNS Clock";

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Bounds = new Rectangle(wa.Right - 400, wa.Top + 20, 380, 150);

            BuildMenu();
            LoadSettings();
            ApplyBackground();

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "GNS Clock - Tech House (double-click: hide/show)";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Visible = !Visible; };

            timer = new Timer();
            timer.Interval = 200;
            timer.Tick += delegate { CheckIdle(); Invalidate(); };
            timer.Start();

            MouseDown += OnDragMouseDown;
            MouseDoubleClick += delegate { if (!ssMode) { is24 = !is24; Invalidate(); } };
            KeyDown += delegate { if (ssMode) ExitScreensaver(); };
            FormClosing += delegate { SaveSettings(); tray.Visible = false; };
        }

        private void OnDragMouseDown(object sender, MouseEventArgs e)
        {
            if (ssMode) { ExitScreensaver(); return; }
            if (e.Button == MouseButtons.Left && e.Clicks == 1)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private int IdleSeconds()
        {
            LASTINPUTINFO li = new LASTINPUTINFO();
            li.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref li)) return 0;
            uint idleMs = (uint)Environment.TickCount - li.dwTime;
            return (int)(idleMs / 1000);
        }

        private void CheckIdle()
        {
            if (ssTimeout <= 0) return;
            int idle = IdleSeconds();
            if (!ssMode && idle >= ssTimeout)
            {
                ssAuto = true;
                EnterScreensaver();
            }
            else if (ssMode && ssAuto && idle < 1)
            {
                ExitScreensaver(); // any mouse/keyboard activity wakes the desktop
            }
        }

        private void EnterScreensaver()
        {
            if (ssMode) return;
            ssMode = true;
            savedBounds = Bounds;
            savedTrans = transparentBg;
            transparentBg = false;
            TransparencyKey = Color.Empty;
            if (BackgroundImage != null) { BackgroundImage.Dispose(); BackgroundImage = null; }
            BackColor = Color.Black;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = true;

            // jet-black covers on all other monitors
            foreach (Screen sc in Screen.AllScreens)
            {
                if (sc.Bounds == Screen.PrimaryScreen.Bounds) continue;
                Form cover = new Form();
                cover.FormBorderStyle = FormBorderStyle.None;
                cover.StartPosition = FormStartPosition.Manual;
                cover.BackColor = Color.Black;
                cover.ShowInTaskbar = false;
                cover.TopMost = true;
                cover.Bounds = sc.Bounds;
                cover.Cursor = Cursors.Default;
                cover.MouseDown += delegate { ExitScreensaver(); };
                cover.KeyDown += delegate { ExitScreensaver(); };
                cover.Show();
                ssCovers.Add(cover);
            }

            flipCur[0] = ""; flipCur[1] = ""; flipCur[2] = "";
            flipT[0] = 0; flipT[1] = 0; flipT[2] = 0;
            timer.Interval = 33; // smooth flip animation
            Cursor.Hide();
            Focus();
            Invalidate();
        }

        private void ExitScreensaver()
        {
            if (!ssMode) return;
            ssMode = false;
            ssAuto = false;
            foreach (Form cover in ssCovers) { try { cover.Close(); } catch { } }
            ssCovers.Clear();
            timer.Interval = 200;
            transparentBg = savedTrans;
            Bounds = savedBounds;
            ApplyBackground();
            Cursor.Show();
            SyncChecks();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            if (m.Msg == WM_NCHITTEST && !ssMode)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1) // HTCLIENT
                {
                    int lp = unchecked((int)(long)m.LParam);
                    int sx = (short)(lp & 0xFFFF);
                    int sy = (short)((lp >> 16) & 0xFFFF);
                    Point p = PointToClient(new Point(sx, sy));
                    int grip = 10;
                    bool left = p.X < grip, right = p.X > ClientSize.Width - grip;
                    bool top = p.Y < grip, bottom = p.Y > ClientSize.Height - grip;
                    int ht = 0;
                    if (top && left) ht = 13; else if (top && right) ht = 14;
                    else if (bottom && left) ht = 16; else if (bottom && right) ht = 17;
                    else if (left) ht = 10; else if (right) ht = 11;
                    else if (top) ht = 12; else if (bottom) ht = 15;
                    if (ht != 0) m.Result = (IntPtr)ht;
                }
                return;
            }
            base.WndProc(ref m);
        }

        private ToolStripMenuItem AddCheck(string text, EventHandler onClick)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(text);
            mi.Click += onClick;
            menu.Items.Add(mi);
            return mi;
        }

        private ToolStripMenuItem Preset(string name, Color f1, Color f2, Color ed)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(name);
            Color a = f1, b = f2, c = ed;
            mi.Click += delegate { fillColor = a; fillColor2 = b; edgeColor = c; Invalidate(); };
            return mi;
        }

        private ToolStripMenuItem SsOption(string name, int seconds)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(name);
            mi.Tag = seconds;
            int sec = seconds;
            mi.Click += delegate { ssTimeout = sec; SyncChecks(); };
            return mi;
        }

        private Color PickColor(Color current)
        {
            ColorDialog cd = new ColorDialog();
            cd.Color = current;
            cd.FullOpen = true;
            if (cd.ShowDialog() == DialogResult.OK) return cd.Color;
            return current;
        }

        private void BuildMenu()
        {
            menu = new ContextMenuStrip();

            mi24 = AddCheck("24-hour format", delegate { is24 = !is24; SyncChecks(); Invalidate(); });
            miDate = AddCheck("Show date", delegate { showDate = !showDate; SyncChecks(); Invalidate(); });
            miTop = AddCheck("Always on top (Topmost)", delegate { TopMost = !TopMost; SyncChecks(); });
            miSs = AddCheck("Screensaver now (fullscreen)", delegate
            {
                if (ssMode) ExitScreensaver(); else EnterScreensaver();
                SyncChecks();
            });

            ssTimeMenu = new ToolStripMenuItem("Auto screensaver after");
            ssTimeMenu.DropDownItems.Add(SsOption("Off", 0));
            ssTimeMenu.DropDownItems.Add(SsOption("10 seconds", 10));
            ssTimeMenu.DropDownItems.Add(SsOption("20 seconds", 20));
            ssTimeMenu.DropDownItems.Add(SsOption("30 seconds", 30));
            ssTimeMenu.DropDownItems.Add(SsOption("1 minute", 60));
            ssTimeMenu.DropDownItems.Add(SsOption("5 minutes", 300));
            ssTimeMenu.DropDownItems.Add(SsOption("10 minutes", 600));
            menu.Items.Add(ssTimeMenu);

            ToolStripMenuItem miSw = new ToolStripMenuItem("Stopwatch (study timer)");
            miSw.Click += delegate
            {
                if (swForm == null || swForm.IsDisposed)
                {
                    swForm = new StopwatchForm();
                    swForm.Show();
                }
                else swForm.Activate();
            };
            menu.Items.Add(miSw);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem bgMenu = new ToolStripMenuItem("Background");

            miTransparent = new ToolStripMenuItem("Transparent");
            miTransparent.Click += delegate { transparentBg = !transparentBg; ApplyBackground(); SyncChecks(); };
            bgMenu.DropDownItems.Add(miTransparent);

            ToolStripMenuItem miColor = new ToolStripMenuItem("Choose color...");
            miColor.Click += delegate
            {
                bgColor = PickColor(bgColor);
                transparentBg = false; imagePath = "";
                ApplyBackground(); SyncChecks();
            };
            bgMenu.DropDownItems.Add(miColor);

            ToolStripMenuItem miImage = new ToolStripMenuItem("Choose image...");
            miImage.Click += delegate
            {
                OpenFileDialog od = new OpenFileDialog();
                od.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif";
                if (od.ShowDialog() == DialogResult.OK)
                {
                    imagePath = od.FileName; transparentBg = false;
                    ApplyBackground(); SyncChecks();
                }
            };
            bgMenu.DropDownItems.Add(miImage);

            ToolStripMenuItem miNoImage = new ToolStripMenuItem("Remove image");
            miNoImage.Click += delegate { imagePath = ""; ApplyBackground(); };
            bgMenu.DropDownItems.Add(miNoImage);

            menu.Items.Add(bgMenu);

            // ---- Text colors: gradient fill + edge + presets ----
            ToolStripMenuItem txtMenu = new ToolStripMenuItem("Text colors");

            miGradient = new ToolStripMenuItem("Gradient fill");
            miGradient.Click += delegate { gradientFill = !gradientFill; SyncChecks(); Invalidate(); };
            txtMenu.DropDownItems.Add(miGradient);

            ToolStripMenuItem miFill = new ToolStripMenuItem("Fill color 1 (top)...");
            miFill.Click += delegate { fillColor = PickColor(fillColor); Invalidate(); };
            txtMenu.DropDownItems.Add(miFill);

            ToolStripMenuItem miFill2 = new ToolStripMenuItem("Fill color 2 (bottom)...");
            miFill2.Click += delegate { fillColor2 = PickColor(fillColor2); Invalidate(); };
            txtMenu.DropDownItems.Add(miFill2);

            ToolStripMenuItem miEdge = new ToolStripMenuItem("Edge color...");
            miEdge.Click += delegate { edgeColor = PickColor(edgeColor); Invalidate(); };
            txtMenu.DropDownItems.Add(miEdge);

            txtMenu.DropDownItems.Add(new ToolStripSeparator());
            txtMenu.DropDownItems.Add(Preset("Neon Cyan > Blue / Navy edge",
                Color.FromArgb(0, 229, 255), Color.FromArgb(41, 121, 255), Color.FromArgb(8, 34, 84)));
            txtMenu.DropDownItems.Add(Preset("Gold > Amber / Black edge",
                Color.FromArgb(255, 224, 130), Color.FromArgb(255, 143, 0), Color.FromArgb(33, 22, 0)));
            txtMenu.DropDownItems.Add(Preset("White > Silver / Crimson edge",
                Color.FromArgb(255, 255, 255), Color.FromArgb(176, 190, 197), Color.FromArgb(183, 28, 28)));
            txtMenu.DropDownItems.Add(Preset("Pink > Violet / Purple edge",
                Color.FromArgb(255, 128, 213), Color.FromArgb(170, 80, 255), Color.FromArgb(64, 0, 96)));
            txtMenu.DropDownItems.Add(Preset("Lime > Green / Forest edge",
                Color.FromArgb(190, 255, 80), Color.FromArgb(0, 200, 83), Color.FromArgb(8, 60, 20)));
            txtMenu.DropDownItems.Add(Preset("Orange > Red / Deep Blue edge",
                Color.FromArgb(255, 183, 77), Color.FromArgb(244, 67, 54), Color.FromArgb(13, 30, 80)));

            menu.Items.Add(txtMenu);

            menu.Items.Add(new ToolStripSeparator());
            miStartup = AddCheck("Start with Windows", delegate { ToggleStartup(); SyncChecks(); });
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miExit = new ToolStripMenuItem("Exit");
            miExit.Click += delegate { Close(); };
            menu.Items.Add(miExit);

            menu.Opening += delegate { SyncChecks(); };
            ContextMenuStrip = menu;
        }

        private void SyncChecks()
        {
            mi24.Checked = is24;
            miDate.Checked = showDate;
            miTop.Checked = TopMost;
            miTransparent.Checked = transparentBg;
            miStartup.Checked = IsStartupEnabled();
            miGradient.Checked = gradientFill;
            miSs.Checked = ssMode;
            if (ssTimeMenu != null)
            {
                foreach (object o in ssTimeMenu.DropDownItems)
                {
                    ToolStripMenuItem mi = o as ToolStripMenuItem;
                    if (mi != null && mi.Tag is int) mi.Checked = ((int)mi.Tag == ssTimeout);
                }
            }
        }

        private void ApplyBackground()
        {
            if (BackgroundImage != null) { BackgroundImage.Dispose(); BackgroundImage = null; }
            if (transparentBg)
            {
                BackColor = KeyColor;
                TransparencyKey = KeyColor;
            }
            else
            {
                TransparencyKey = Color.Empty;
                BackColor = bgColor;
                if (imagePath.Length > 0 && File.Exists(imagePath))
                {
                    try
                    {
                        using (FileStream fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                        {
                            Image tmp = Image.FromStream(fs);
                            BackgroundImage = new Bitmap(tmp);
                            tmp.Dispose();
                        }
                        BackgroundImageLayout = ImageLayout.Stretch;
                    }
                    catch { imagePath = ""; }
                }
            }
            Invalidate();
        }

        private void ToggleStartup()
        {
            try
            {
                RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (IsStartupEnabled()) k.DeleteValue(AppName, false);
                else k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                k.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not update startup setting: " + ex.Message);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                object v = (k == null) ? null : k.GetValue(AppName);
                if (k != null) k.Close();
                return v != null;
            }
            catch { return false; }
        }

        private static FontFamily GetFamily(string name)
        {
            try { return new FontFamily(name); }
            catch { return FontFamily.GenericMonospace; }
        }

        private GraphicsPath MakePath(string text, FontFamily fam, FontStyle style, out RectangleF bounds)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddString(text, fam, (int)style, 100f, new PointF(0, 0), StringFormat.GenericDefault);
            bounds = p.GetBounds();
            if (bounds.Width < 1) bounds = new RectangleF(0, 0, 1, 1);
            return p;
        }

        private void MoveScale(GraphicsPath p, RectangleF b, float scale, float x, float y)
        {
            using (Matrix m = new Matrix(scale, 0, 0, scale, x - b.X * scale, y - b.Y * scale))
                p.Transform(m);
        }

        private void PaintPath(Graphics g, GraphicsPath p, float penW)
        {
            RectangleF pb = p.GetBounds();
            if (pb.Width < 1 || pb.Height < 1) return;
            pb.Inflate(penW + 2, penW + 2);
            if (ssMode)
            {
                // screensaver: pure white digits on jet black
                using (SolidBrush wb = new SolidBrush(Color.White))
                    g.FillPath(wb, p);
                return;
            }
            using (Pen pen = new Pen(edgeColor, penW))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawPath(pen, p);
            }
            if (gradientFill)
            {
                using (LinearGradientBrush br = new LinearGradientBrush(pb, fillColor, fillColor2, LinearGradientMode.Vertical))
                    g.FillPath(br, p);
            }
            else
            {
                using (SolidBrush br = new SolidBrush(fillColor))
                    g.FillPath(br, p);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (ssMode) { PaintFlipClock(g); return; }

            DateTime now = DateTime.Now;
            string timeStr = now.ToString(is24 ? "HH:mm:ss" : "hh:mm:ss");
            string ampm = is24 ? "" : now.ToString("tt");
            string dateStr = now.ToString("ddd, dd MMM yyyy");

            float w = ClientSize.Width - 24;
            float h = ClientSize.Height - 18;
            if (w < 20 || h < 20) return;
            if (ssMode) { w *= 0.82f; h *= 0.7f; }
            float timeH = showDate ? h * 0.62f : h * 0.90f;
            float availTimeW = (ampm.Length > 0) ? w * 0.84f : w;

            FontFamily famTime = GetFamily("Consolas");
            FontFamily famUi = GetFamily("Segoe UI");
            FontStyle st = FontStyle.Bold | FontStyle.Italic;

            RectangleF tb, ab = RectangleF.Empty, db = RectangleF.Empty;
            GraphicsPath tp = MakePath(timeStr, famTime, st, out tb);
            GraphicsPath ap = null, dp = null;
            try
            {
                float s = Math.Min(availTimeW / tb.Width, timeH / tb.Height);
                float tw = tb.Width * s, th = tb.Height * s;

                float aw = 0, ah = 0;
                if (ampm.Length > 0)
                {
                    ap = MakePath(ampm, famUi, st, out ab);
                    float sa = s * 0.32f;
                    aw = ab.Width * sa; ah = ab.Height * sa;
                }

                float dw = 0, dh = 0, sd = 0;
                if (showDate)
                {
                    dp = MakePath(dateStr, famUi, FontStyle.Italic, out db);
                    sd = Math.Min((w * 0.75f) / db.Width, (h * 0.20f) / db.Height);
                    dw = db.Width * sd; dh = db.Height * sd;
                }

                float totalW = tw + (aw > 0 ? aw + 8 : 0);
                float contentH = th + (showDate ? dh + 6 : 0);
                float x = (ClientSize.Width - totalW) / 2f;
                float y = (ClientSize.Height - contentH) / 2f;

                MoveScale(tp, tb, s, x, y);
                PaintPath(g, tp, Math.Max(1.5f, th * 0.045f));

                if (ap != null)
                {
                    float sa = s * 0.32f;
                    MoveScale(ap, ab, sa, x + tw + 8, y + th - ah);
                    PaintPath(g, ap, Math.Max(1f, ah * 0.06f));
                }

                if (dp != null)
                {
                    MoveScale(dp, db, sd, (ClientSize.Width - dw) / 2f, y + th + 6);
                    PaintPath(g, dp, Math.Max(1f, dh * 0.05f));
                }
            }
            finally
            {
                tp.Dispose();
                if (ap != null) ap.Dispose();
                if (dp != null) dp.Dispose();
            }
        }

        private static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d2 = rad * 2;
            p.AddArc(r.X, r.Y, d2, d2, 180, 90);
            p.AddArc(r.Right - d2, r.Y, d2, d2, 270, 90);
            p.AddArc(r.Right - d2, r.Bottom - d2, d2, d2, 0, 90);
            p.AddArc(r.X, r.Bottom - d2, d2, d2, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Fliqlo-style flip-clock cards for screensaver mode
        private void PaintFlipClock(Graphics g)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            DateTime now = DateTime.Now;
            int hr = now.Hour;
            string ampm = "";
            if (!is24)
            {
                ampm = hr >= 12 ? "PM" : "AM";
                hr = hr % 12; if (hr == 0) hr = 12;
            }
            string[] parts = new string[] { hr.ToString("00"), now.Minute.ToString("00"), now.Second.ToString("00") };

            float W = ClientSize.Width, H = ClientSize.Height;
            float cardH = H * 0.55f;
            float cardW = cardH * 1.15f;
            float gap = cardW * 0.14f;
            float totalW = cardW * 3 + gap * 2;
            if (totalW > W * 0.92f)
            {
                float k = (W * 0.92f) / totalW;
                cardW *= k; cardH *= k; gap *= k; totalW *= k;
            }
            float x0 = (W - totalW) / 2f;
            float y0 = (H - cardH) / 2f - (showDate ? H * 0.035f : 0);

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;

            Color digitC = Color.FromArgb(216, 216, 216);

            float fs;
            using (Font probe = new Font("Segoe UI", 100f, FontStyle.Bold))
            {
                SizeF m = g.MeasureString("88", probe);
                fs = 100f * Math.Min((cardW * 0.88f) / m.Width, (cardH * 0.96f) / m.Height);
            }

            using (Font dfont = new Font("Segoe UI", fs, FontStyle.Bold))
            using (SolidBrush db = new SolidBrush(digitC))
            {
                for (int i = 0; i < 3; i++)
                {
                    // detect digit change -> start flip
                    if (flipCur[i].Length == 0) flipCur[i] = parts[i];
                    else if (parts[i] != flipCur[i])
                    {
                        flipOld[i] = flipCur[i];
                        flipCur[i] = parts[i];
                        flipT[i] = Environment.TickCount;
                    }
                    float p = 1f;
                    if (flipT[i] != 0)
                    {
                        p = (Environment.TickCount - flipT[i]) / (float)FlipMs;
                        if (p >= 1f) { p = 1f; flipT[i] = 0; }
                    }

                    RectangleF rc = new RectangleF(x0 + i * (cardW + gap), y0, cardW, cardH);
                    DrawFlipCard(g, rc, flipCur[i], flipOld[i], p, dfont, db, sf, cardW, cardH);
                }

                if (ampm.Length > 0)
                {
                    using (Font af = new Font("Segoe UI", cardH * 0.075f, FontStyle.Bold))
                    using (SolidBrush ab = new SolidBrush(Color.FromArgb(120, 120, 120)))
                        g.DrawString(ampm, af, ab, x0 + cardW * 0.08f, y0 + cardH - cardH * 0.16f);
                }

                if (showDate)
                {
                    using (Font df2 = new Font("Segoe UI", cardH * 0.085f, FontStyle.Regular))
                    using (SolidBrush db2 = new SolidBrush(Color.FromArgb(130, 130, 130)))
                        g.DrawString(now.ToString("ddd, dd MMM yyyy"), df2, db2,
                            new RectangleF(0, y0 + cardH + H * 0.03f, W, cardH * 0.15f), sf);
                }
            }
            sf.Dispose();
        }

        // One flip card: top half flap folds down over the hinge to reveal the new digit
        private void DrawFlipCard(Graphics g, RectangleF rc, string cur, string old, float p,
            Font font, Brush brush, StringFormat sf, float cardW, float cardH)
        {
            using (GraphicsPath rr = RoundRect(rc, cardW * 0.09f))
            using (LinearGradientBrush cb = new LinearGradientBrush(rc,
                Color.FromArgb(40, 40, 44), Color.FromArgb(24, 24, 26), LinearGradientMode.Vertical))
                g.FillPath(cb, rr);

            float hy = rc.Y + rc.Height / 2f;
            RectangleF topR = new RectangleF(rc.X, rc.Y, rc.Width, rc.Height / 2f);
            RectangleF botR = new RectangleF(rc.X, hy, rc.Width, rc.Height / 2f);

            // static top: new digit (revealed as the flap falls)
            g.SetClip(topR);
            g.DrawString(cur, font, brush, rc, sf);
            g.ResetClip();

            // static bottom: old digit until the flap lands
            g.SetClip(botR);
            g.DrawString((p < 1f && old.Length > 0) ? old : cur, font, brush, rc, sf);
            g.ResetClip();

            // moving flap
            if (p < 1f && old.Length > 0)
            {
                Color flapBg = Color.FromArgb(36, 36, 40);
                if (p < 0.5f)
                {
                    // phase 1: old top half folds down toward hinge
                    float sy = 1f - p * 2f; if (sy < 0.03f) sy = 0.03f;
                    g.SetClip(topR);
                    g.TranslateTransform(0, hy);
                    g.ScaleTransform(1f, sy);
                    g.TranslateTransform(0, -hy);
                    using (SolidBrush fb = new SolidBrush(flapBg)) g.FillRectangle(fb, topR);
                    g.DrawString(old, font, brush, rc, sf);
                    g.ResetTransform();
                    g.ResetClip();
                }
                else
                {
                    // phase 2: new bottom half unfolds from hinge
                    float sy = p * 2f - 1f; if (sy < 0.03f) sy = 0.03f;
                    g.SetClip(botR);
                    g.TranslateTransform(0, hy);
                    g.ScaleTransform(1f, sy);
                    g.TranslateTransform(0, -hy);
                    using (SolidBrush fb = new SolidBrush(flapBg)) g.FillRectangle(fb, botR);
                    g.DrawString(cur, font, brush, rc, sf);
                    g.ResetTransform();
                    g.ResetClip();
                }
            }

            // hinge line across the middle
            float hw = Math.Max(2f, cardH * 0.014f);
            using (Pen hp = new Pen(Color.Black, hw))
                g.DrawLine(hp, rc.X, hy, rc.Right, hy);
            using (Pen hl = new Pen(Color.FromArgb(28, 255, 255, 255), 1f))
                g.DrawLine(hl, rc.X, hy + hw, rc.Right, hy + hw);
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
                Rectangle b = ssMode ? savedBounds : Bounds;
                string[] lines = new string[]
                {
                    "is24=" + (is24 ? "1" : "0"),
                    "date=" + (showDate ? "1" : "0"),
                    "top=" + (TopMost ? "1" : "0"),
                    "trans=" + ((ssMode ? savedTrans : transparentBg) ? "1" : "0"),
                    "grad=" + (gradientFill ? "1" : "0"),
                    "sstime=" + ssTimeout,
                    "bg=" + bgColor.ToArgb(),
                    "fg=" + fillColor.ToArgb(),
                    "fg2=" + fillColor2.ToArgb(),
                    "edge=" + edgeColor.ToArgb(),
                    "img=" + imagePath,
                    "x=" + b.X, "y=" + b.Y, "w=" + b.Width, "h=" + b.Height
                };
                File.WriteAllLines(settingsFile, lines);
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsFile)) return;
                string[] lines = File.ReadAllLines(settingsFile);
                int x = Left, y = Top, wd = Width, ht = Height;
                foreach (string line in lines)
                {
                    int i = line.IndexOf('=');
                    if (i < 1) continue;
                    string k = line.Substring(0, i);
                    string v = line.Substring(i + 1);
                    switch (k)
                    {
                        case "is24": is24 = (v == "1"); break;
                        case "date": showDate = (v == "1"); break;
                        case "top": TopMost = (v == "1"); break;
                        case "trans": transparentBg = (v == "1"); break;
                        case "grad": gradientFill = (v == "1"); break;
                        case "sstime": ssTimeout = int.Parse(v); break;
                        case "bg": bgColor = Color.FromArgb(int.Parse(v)); break;
                        case "fg": fillColor = Color.FromArgb(int.Parse(v)); break;
                        case "fg2": fillColor2 = Color.FromArgb(int.Parse(v)); break;
                        case "edge": edgeColor = Color.FromArgb(int.Parse(v)); break;
                        case "img": imagePath = v; break;
                        case "x": x = int.Parse(v); break;
                        case "y": y = int.Parse(v); break;
                        case "w": wd = int.Parse(v); break;
                        case "h": ht = int.Parse(v); break;
                    }
                }
                Bounds = new Rectangle(x, y, wd, ht);
            }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ClockForm());
        }
    }

    // ---- Stopwatch window: helps students time question solving ----
    // Space = start/pause, L = lap, R = reset. Right-click for options.
    // Shapes: Rectangle (resize freely), Circle (ring stopwatch; scroll wheel = resize)
    public class StopwatchForm : Form
    {
        private System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        private Timer timer;
        private Label lbl;
        private Button btnStart, btnLap, btnReset;
        private ListBox lapList;
        private FlowLayoutPanel btnPanel;
        private int lapCount = 0;
        private TimeSpan lastLap = TimeSpan.Zero;

        private bool sTransparent = false;
        private Color sBg = Color.FromArgb(14, 14, 20);
        private string sImg = "";
        private ToolStripMenuItem cTop, cTrans;
        private ToolStripMenuItem shRect, shCircle;
        private static readonly Color SwKey = Color.FromArgb(1, 1, 2);

        // shape: 0 = rectangle, 1 = square, 2 = circle
        private int shapeMode = 0;
        private bool resizing = false;
        private RectangleF cBtnStart, cBtnLap, cBtnReset;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public StopwatchForm()
        {
            Text = "Stopwatch - GNS Clock";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(560, 280);
            MinimumSize = new Size(200, 190);
            TopMost = true;
            DoubleBuffered = true;
            BackColor = sBg;

            lbl = new Label();
            lbl.Dock = DockStyle.Fill;
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.ForeColor = Color.FromArgb(0, 229, 255);
            lbl.BackColor = Color.Transparent;
            lbl.Font = new Font("Consolas", 42f, FontStyle.Bold);
            lbl.Text = "00:00:00.0";
            Controls.Add(lbl);

            lapList = new ListBox();
            lapList.Dock = DockStyle.Right;
            lapList.Width = 185;
            lapList.BackColor = Color.FromArgb(24, 24, 32);
            lapList.ForeColor = Color.White;
            lapList.BorderStyle = BorderStyle.None;
            lapList.Font = new Font("Consolas", 10f);
            Controls.Add(lapList);

            btnPanel = new FlowLayoutPanel();
            btnPanel.Dock = DockStyle.Bottom;
            btnPanel.Height = 56;
            btnPanel.FlowDirection = FlowDirection.LeftToRight;
            btnPanel.Padding = new Padding(10, 8, 10, 8);
            btnPanel.BackColor = Color.FromArgb(24, 24, 32);

            btnStart = MakeButton("Start");
            btnStart.Click += delegate { ToggleRun(); };
            btnPanel.Controls.Add(btnStart);

            btnLap = MakeButton("Lap");
            btnLap.Click += delegate { AddLap(); };
            btnPanel.Controls.Add(btnLap);

            btnReset = MakeButton("Reset");
            btnReset.Click += delegate { DoReset(); };
            btnPanel.Controls.Add(btnReset);

            Controls.Add(btnPanel);

            BuildContextMenu();

            timer = new Timer();
            timer.Interval = 100;
            timer.Tick += delegate
            {
                UpdateLabel();
                if (shapeMode == 2) Invalidate();
            };
            timer.Start();

            Resize += delegate { OnShapeResize(); };

            MouseDown += OnSwMouseDown;
            MouseWheel += delegate(object s, MouseEventArgs e)
            {
                if (shapeMode != 2) return;
                int d = Math.Min(Math.Max(Width + (e.Delta > 0 ? 24 : -24), 220), 800);
                resizing = true;
                Size = new Size(d, d);
                resizing = false;
                UpdateCircleRegion();
                Invalidate();
            };

            // Space = start/pause, L = lap, R = reset
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space) { ToggleRun(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.L) AddLap();
                else if (e.KeyCode == Keys.R) DoReset();
            };
        }

        private void ToggleRun()
        {
            if (sw.IsRunning) { sw.Stop(); btnStart.Text = "Start"; }
            else { sw.Start(); btnStart.Text = "Pause"; }
            if (shapeMode == 2) Invalidate();
        }

        private void DoReset()
        {
            sw.Reset();
            btnStart.Text = "Start";
            lapCount = 0;
            lastLap = TimeSpan.Zero;
            lapList.Items.Clear();
            UpdateLabel();
            if (shapeMode == 2) Invalidate();
        }

        private void OnSwMouseDown(object sender, MouseEventArgs e)
        {
            if (shapeMode != 2) return;
            if (e.Button == MouseButtons.Left)
            {
                if (cBtnStart.Contains(e.Location)) { ToggleRun(); return; }
                if (cBtnLap.Contains(e.Location)) { AddLap(); Invalidate(); return; }
                if (cBtnReset.Contains(e.Location)) { DoReset(); return; }
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0); // drag window
            }
        }

        private void OnShapeResize()
        {
            if (resizing) return;
            if (shapeMode == 2)
            {
                UpdateCircleRegion();
                Invalidate();
                return;
            }
            float aw = Math.Max(100f, ClientSize.Width - (lapList.Visible ? lapList.Width : 0));
            float size = Math.Max(14f, Math.Min(aw / 9f, (ClientSize.Height - 56) / 2.2f));
            lbl.Font = new Font("Consolas", size, FontStyle.Bold);
        }

        private void UpdateCircleRegion()
        {
            int d = Math.Min(ClientSize.Width, ClientSize.Height);
            GraphicsPath gp = new GraphicsPath();
            gp.AddEllipse(0, 0, d, d);
            Region = new Region(gp);
        }

        private void SetShape(int mode)
        {
            shapeMode = mode;
            bool circle = (mode == 2);

            lbl.Visible = !circle;
            lapList.Visible = !circle;
            btnPanel.Visible = !circle;

            if (circle)
            {
                FormBorderStyle = FormBorderStyle.None;
                int d = Math.Max(280, Math.Min(Width, Height));
                resizing = true;
                Size = new Size(d, d);
                resizing = false;
                UpdateCircleRegion();
            }
            else
            {
                Region = null;
                FormBorderStyle = FormBorderStyle.Sizable;
                OnShapeResize();
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (shapeMode != 2) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            int d = Math.Min(ClientSize.Width, ClientSize.Height);
            float cx = d / 2f, cy = d / 2f;
            Color accent = lbl.ForeColor;

            // dial fill
            RectangleF dial = new RectangleF(1, 1, d - 2, d - 2);
            if (BackgroundImage != null)
            {
                g.DrawImage(BackgroundImage, dial);
            }
            else
            {
                Color topC = Color.FromArgb(sBg.A,
                    Math.Min(255, sBg.R + 24), Math.Min(255, sBg.G + 24), Math.Min(255, sBg.B + 30));
                using (LinearGradientBrush br = new LinearGradientBrush(dial, topC, sBg, LinearGradientMode.Vertical))
                    g.FillEllipse(br, dial);
            }

            // ring track + seconds progress arc
            float ringW = d * 0.035f;
            RectangleF ring = new RectangleF(ringW, ringW, d - ringW * 2, d - ringW * 2);
            using (Pen track = new Pen(Color.FromArgb(48, 255, 255, 255), ringW))
                g.DrawEllipse(track, ring);
            float frac = (float)((sw.Elapsed.TotalSeconds % 60.0) / 60.0);
            if (frac > 0.002f)
            {
                using (Pen prog = new Pen(accent, ringW))
                {
                    prog.StartCap = LineCap.Round;
                    prog.EndCap = LineCap.Round;
                    g.DrawArc(prog, ring, -90f, 360f * frac);
                }
            }

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;

            // time in the center
            using (Font tf = new Font("Consolas", d * 0.095f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(accent))
                g.DrawString(Fmt(sw.Elapsed), tf, tb, new RectangleF(0, cy - d * 0.18f, d, d * 0.22f), sf);

            // last lap under the time
            if (lapCount > 0 && lapList.Items.Count > 0)
            {
                using (Font lf = new Font("Segoe UI", d * 0.035f, FontStyle.Italic))
                using (SolidBrush lb = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    g.DrawString(lapList.Items[0].ToString().Trim(), lf, lb,
                        new RectangleF(0, cy + d * 0.04f, d, d * 0.10f), sf);
            }

            // round buttons: Lap | Start/Pause | Reset
            float r = d * 0.072f;
            float by = cy + d * 0.30f;
            cBtnLap = new RectangleF(cx - d * 0.22f - r, by - r, r * 2, r * 2);
            cBtnStart = new RectangleF(cx - r, by - r, r * 2, r * 2);
            cBtnReset = new RectangleF(cx + d * 0.22f - r, by - r, r * 2, r * 2);
            DrawRoundBtn(g, cBtnLap, "L", accent, sf);
            DrawRoundBtn(g, cBtnStart, sw.IsRunning ? "II" : "►", accent, sf);
            DrawRoundBtn(g, cBtnReset, "R", accent, sf);

            sf.Dispose();
        }

        private void DrawRoundBtn(Graphics g, RectangleF rc, string glyph, Color accent, StringFormat sf)
        {
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(210, 42, 42, 60)))
                g.FillEllipse(fill, rc);
            using (Pen border = new Pen(accent, Math.Max(1.5f, rc.Width * 0.045f)))
                g.DrawEllipse(border, rc);
            using (Font bf = new Font("Segoe UI", rc.Height * 0.32f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
                g.DrawString(glyph, bf, tb, rc, sf);
        }

        private void AddLap()
        {
            if (sw.Elapsed == TimeSpan.Zero) return;
            TimeSpan t = sw.Elapsed;
            TimeSpan split = t - lastLap;
            lastLap = t;
            lapCount++;
            lapList.Items.Insert(0, string.Format("Lap {0,2}  +{1}", lapCount, Fmt(split)));
            lapList.Items.Insert(1, string.Format("        = {0}", Fmt(t)));
        }

        private void BuildContextMenu()
        {
            ContextMenuStrip cm = new ContextMenuStrip();

            cTop = new ToolStripMenuItem("Always on top (Topmost)");
            cTop.Click += delegate { TopMost = !TopMost; cTop.Checked = TopMost; };
            cm.Items.Add(cTop);

            ToolStripMenuItem shapeMenu = new ToolStripMenuItem("Shape");
            shRect = new ToolStripMenuItem("Rectangle");
            shRect.Click += delegate { SetShape(0); };
            shapeMenu.DropDownItems.Add(shRect);
            shCircle = new ToolStripMenuItem("Circle (ring dial)");
            shCircle.Click += delegate { SetShape(2); };
            shapeMenu.DropDownItems.Add(shCircle);
            cm.Items.Add(shapeMenu);

            cm.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem bgMenu = new ToolStripMenuItem("Background");

            cTrans = new ToolStripMenuItem("Transparent");
            cTrans.Click += delegate { sTransparent = !sTransparent; ApplyBg(); };
            bgMenu.DropDownItems.Add(cTrans);

            ToolStripMenuItem cColor = new ToolStripMenuItem("Choose color...");
            cColor.Click += delegate
            {
                ColorDialog cd = new ColorDialog();
                cd.Color = sBg;
                cd.FullOpen = true;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    sBg = cd.Color; sTransparent = false; sImg = "";
                    ApplyBg();
                }
            };
            bgMenu.DropDownItems.Add(cColor);

            ToolStripMenuItem cImage = new ToolStripMenuItem("Choose image...");
            cImage.Click += delegate
            {
                OpenFileDialog od = new OpenFileDialog();
                od.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif";
                if (od.ShowDialog() == DialogResult.OK)
                {
                    sImg = od.FileName; sTransparent = false;
                    ApplyBg();
                }
            };
            bgMenu.DropDownItems.Add(cImage);

            ToolStripMenuItem cNoImg = new ToolStripMenuItem("Remove image");
            cNoImg.Click += delegate { sImg = ""; ApplyBg(); };
            bgMenu.DropDownItems.Add(cNoImg);

            cm.Items.Add(bgMenu);

            ToolStripMenuItem cTextColor = new ToolStripMenuItem("Timer text color...");
            cTextColor.Click += delegate
            {
                ColorDialog cd = new ColorDialog();
                cd.Color = lbl.ForeColor;
                cd.FullOpen = true;
                if (cd.ShowDialog() == DialogResult.OK) lbl.ForeColor = cd.Color;
            };
            cm.Items.Add(cTextColor);

            cm.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem cClose = new ToolStripMenuItem("Close stopwatch");
            cClose.Click += delegate { Close(); };
            cm.Items.Add(cClose);

            cm.Opening += delegate
            {
                cTop.Checked = TopMost;
                cTrans.Checked = sTransparent;
                shRect.Checked = (shapeMode == 0);
                shCircle.Checked = (shapeMode == 2);
            };
            ContextMenuStrip = cm;
            lbl.ContextMenuStrip = cm;
        }

        private void ApplyBg()
        {
            if (BackgroundImage != null) { BackgroundImage.Dispose(); BackgroundImage = null; }
            if (sTransparent)
            {
                BackColor = SwKey;
                TransparencyKey = SwKey;
            }
            else
            {
                TransparencyKey = Color.Empty;
                BackColor = sBg;
                if (sImg.Length > 0 && File.Exists(sImg))
                {
                    try
                    {
                        using (FileStream fs = new FileStream(sImg, FileMode.Open, FileAccess.Read))
                        {
                            Image tmp = Image.FromStream(fs);
                            BackgroundImage = new Bitmap(tmp);
                            tmp.Dispose();
                        }
                        BackgroundImageLayout = ImageLayout.Stretch;
                    }
                    catch { sImg = ""; }
                }
            }
        }

        private Button MakeButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = 95;
            b.Height = 38;
            b.FlatStyle = FlatStyle.Flat;
            b.ForeColor = Color.White;
            b.BackColor = Color.FromArgb(40, 40, 56);
            b.FlatAppearance.BorderColor = Color.FromArgb(0, 229, 255);
            return b;
        }

        private string Fmt(TimeSpan t)
        {
            return string.Format("{0:00}:{1:00}:{2:00}.{3:0}",
                (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds / 100);
        }

        private void UpdateLabel()
        {
            lbl.Text = Fmt(sw.Elapsed);
        }
    }
}
