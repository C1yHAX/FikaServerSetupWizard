using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FikaServerSetupWizard
{
    public class MainForm : Form
    {
        // Constants────
        const int NAV_W    = 200;
        const int HDR_H    = 56;
        const int STATUS_H = 22;
        const int LOG_H    = 200;

        // Borderless window────
        // The frame is gone, so moving and resizing are handed back to Windows
        // via WM_NCLBUTTONDOWN. That keeps Aero snap, the resize cursors and
        // drag-to-restore working exactly as on a normal window.
        const int GRIP     = 6;    // thickness of the resize strips
        const int CORNER   = 18;   // diagonal-resize zone at the strip ends
        const int CAP_H    = 32;   // caption button height

        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int WM_GETMINMAXINFO = 0x0024;

        const int HTCAPTION     = 2;
        const int HTLEFT        = 10, HTRIGHT       = 11;
        const int HTTOP         = 12, HTTOPLEFT     = 13, HTTOPRIGHT    = 14;
        const int HTBOTTOM      = 15, HTBOTTOMLEFT  = 16, HTBOTTOMRIGHT = 17;

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(
            IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct MinMaxInfo
        {
            public Point Reserved, MaxSize, MaxPosition,
                         MinTrackSize, MaxTrackSize;
        }

        Label? _btnMax;

        // State────────
        readonly AppConfig        _config = new();
        readonly OperationContext _ctx;
        readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 50 };

        // Layout───────
        SplitContainer _mainSplit   = null!;
        SplitContainer _rightSplit  = null!;
        Panel          _contentArea = null!;

        // Controls─────
        RichTextBox _logBox       = null!;
        Label       _statusBarLbl = null!;

        // Content-Panel Textboxes
        TextBox? _tbSptDir;
        TextBox? _tbApiKey;
        TextBox? _tbWaApiKey;
        TextBox? _tbHeadlessDir;

        // Settings-Panel Textboxes
        TextBox? _tbSetSptDir, _tbSetEftDir, _tbSetApiKey;

        // Firewall Port-Status Labels
        readonly Dictionary<string, Label> _fwLabels = new();

        // Step metadata
        readonly (string id, string key)[] _steps =
        {
            ("Steam",    "n_Steam"),
            ("EFT",      "n_EFT"),
            ("SPT",      "n_SPT"),
            ("Fika",     "n_Fika"),
            ("Headless", "n_Headless"),
            ("Docker",   "n_Docker"),
            ("Firewall", "n_Firewall"),
            ("WebApp",   "n_WebApp"),
        };

        readonly Dictionary<string, Label> _badges = new();
        readonly Dictionary<string, Panel> _panels  = new();

        // Per-section status line inside each component panel
        readonly Dictionary<string, Label> _statusBoxes = new();

        string _activePanelId = "Steam";

        // Queues
        readonly ConcurrentQueue<(string text, string lvl)>             _logQ    = new();
        readonly ConcurrentQueue<(string id, int state)>                _badgeQ  = new();
        readonly ConcurrentQueue<(string port, string proto, string s)> _fwQ     = new();
        readonly ConcurrentQueue<(string id, int state, string msg)>    _statusQ = new();

        // CONSTRUCTOR
        public MainForm()
        {
            _ctx = new OperationContext
            {
                Config       = _config,
                Log          = (msg, lv)        => _logQ.Enqueue((msg, lv)),
                NotifyStatus = (id, state, msg) =>
                {
                    _badgeQ.Enqueue((id, state));
                    _statusQ.Enqueue((id, state, msg));
                    _logQ.Enqueue(($"[{id.ToUpper()}] {msg}",
                        state == 2 ? "O" : state == 3 ? "E" : "S"));
                },
                SetBadge     = (id, state)      => _badgeQ.Enqueue((id, state)),
                FWPort       = (port, proto, st) => _fwQ.Enqueue((port, proto, st)),
                UpdateApiKey = key  => SafeInvoke(() =>
                {
                    _config.ApiKey = key;
                    if (_tbApiKey    != null) _tbApiKey.Text    = key;
                    if (_tbWaApiKey  != null) _tbWaApiKey.Text  = key;
                    if (_tbSetApiKey != null) _tbSetApiKey.Text = key;
                }),
                UpdateSptDir = path => SafeInvoke(() =>
                {
                    _config.SptDir = path;
                    if (_tbSptDir    != null) _tbSptDir.Text    = path;
                    if (_tbSetSptDir != null) _tbSetSptDir.Text = path;
                }),
                UpdateEftDir = path => SafeInvoke(() =>
                {
                    _config.EftDir = path;
                    if (_tbSetEftDir != null) _tbSetEftDir.Text = path;
                }),
                GetApiKey = () => _config.ApiKey,
            };

            // ShowBlockingOkDialog
            _ctx.ShowBlockingOkDialog = (title, message) =>
            {
                bool result = false;
                Invoke(new Action(() =>
                {
                    var dlg = new EftWaitDialog(title, message);
                    result  = dlg.ShowDialog(this) == DialogResult.OK;
                }));
                return result;
            };

            InitForm();
            BuildUI();

            _uiTimer.Tick += OnUiTick;
            _uiTimer.Start();

            Shown += (_, _) =>
            {
                try { _mainSplit.SplitterDistance  = NAV_W; } catch { }
                try { _rightSplit.SplitterDistance = Math.Max(60, _rightSplit.Height - LOG_H); } catch { }

                _logQ.Enqueue((Translations.T("log_started"),   "O"));
                _logQ.Enqueue((Translations.T("log_autocheck"), "S"));
                RunBg(() => Task.Run(() => Installer.OpCheckAll(_ctx)));
            };
        }

        // FORM INIT
        void InitForm()
        {
            Text           = "FIKA-SERVER SETUP UTILITY  //  v1.0.1";
            Size           = new Size(1440, 860);
            MinimumSize    = new Size(1024, 680);
            BackColor      = Theme.Bg0;
            ForeColor      = Theme.Tx0;
            Font           = Theme.Bd;
            StartPosition  = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;

            try
            {
                string iconPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            }
            catch { }
        }

        // A borderless window maximises over the whole monitor, taskbar
        // included, unless the working area is reported back to Windows.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
                var scr = Screen.FromHandle(Handle);

                mmi.MaxPosition = new Point(
                    scr.WorkingArea.Left - scr.Bounds.Left,
                    scr.WorkingArea.Top  - scr.Bounds.Top);
                mmi.MaxSize = new Point(
                    scr.WorkingArea.Width, scr.WorkingArea.Height);
                mmi.MinTrackSize = new Point(
                    MinimumSize.Width, MinimumSize.Height);

                Marshal.StructureToPtr(mmi, m.LParam, false);
                return;
            }
            base.WndProc(ref m);
        }

        // Keeps the glyph right when the state changes by other means
        // (Win+Up, Aero snap, double-clicking the header).
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateMaxGlyph();
        }

        // BUILD UI
        void BuildUI()
        {
            SuspendLayout();

            // Docking is resolved in insertion order, so the resize strips have
            // to go in before the header/status bar to end up on the outside.
            Controls.Add(BuildGrip(DockStyle.Top));
            Controls.Add(BuildGrip(DockStyle.Bottom));
            Controls.Add(BuildGrip(DockStyle.Left));
            Controls.Add(BuildGrip(DockStyle.Right));

            Controls.Add(BuildHeader());
            Controls.Add(BuildStatusBar());

            _mainSplit = new SplitContainer
            {
                Dock            = DockStyle.Fill,
                Panel1MinSize   = 180,
                Panel2MinSize   = 100,
                FixedPanel      = FixedPanel.Panel1,
                IsSplitterFixed = true,
                SplitterWidth   = 1,
                BackColor       = Theme.Line,
            };
            _mainSplit.Panel1.BackColor = Theme.Bg1;
            _mainSplit.Panel2.BackColor = Theme.Bg0;

            _rightSplit = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Orientation   = Orientation.Horizontal,
                SplitterWidth = 1,
                Panel1MinSize = 80,
                Panel2MinSize = 120,
                BackColor     = Theme.Line,
            };
            _rightSplit.Panel1.BackColor = Theme.Bg0;
            _rightSplit.Panel2.BackColor = Theme.Bg1;

            _mainSplit.Panel1.Controls.Add(BuildSidebar());

            _contentArea = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg0 };
            BuildAllContentPanels();
            _rightSplit.Panel1.Controls.Add(_contentArea);
            _rightSplit.Panel2.Controls.Add(BuildLogPanel());
            _mainSplit.Panel2.Controls.Add(_rightSplit);
            Controls.Add(_mainSplit);

            ResumeLayout();
            ShowPanel(_activePanelId);
        }

        // LANGUAGE SWITCH
        void SwitchLanguage(string lang)
        {
            if (Translations.Lang == lang) return;
            Translations.Lang = lang;

            string savedRtf   = "";
            string savedPanel = _activePanelId;
            try { savedRtf = _logBox?.Rtf ?? ""; } catch { }

            _uiTimer.Stop();
            SuspendLayout();

            Controls.Clear();
            _panels.Clear();
            _badges.Clear();
            _statusBoxes.Clear();
            _fwLabels.Clear();
            _tbSptDir      = null;
            _tbApiKey      = null;
            _tbWaApiKey    = null;
            _tbHeadlessDir = null;
            _tbSetSptDir  = null;
            _tbSetEftDir  = null;
            _tbSetApiKey  = null;
            _logBox       = null!;
            _statusBarLbl = null!;
            _contentArea  = null!;
            _mainSplit    = null!;
            _rightSplit   = null!;

            BuildUI();

            try { _mainSplit.SplitterDistance = NAV_W; } catch { }
            ResumeLayout(true);
            try { _mainSplit.SplitterDistance  = NAV_W; } catch { }
            try { _rightSplit.SplitterDistance = Math.Max(60, _rightSplit.Height - LOG_H); } catch { }

            try { if (!string.IsNullOrEmpty(savedRtf)) _logBox.Rtf = savedRtf; } catch { }
            ShowPanel(savedPanel);

            Refresh();
            _uiTimer.Start();
        }

        // BORDERLESS WINDOW  –  resize strips
        Panel BuildGrip(DockStyle side)
        {
            var g = new Panel
            {
                Dock      = side,
                Width     = GRIP,
                Height    = GRIP,
                BackColor = Theme.Bg1,
            };

            // Top and bottom strips span the full width, so they own the corners.
            int HitAt(int x) => side switch
            {
                DockStyle.Left  => HTLEFT,
                DockStyle.Right => HTRIGHT,
                DockStyle.Top   => x < CORNER             ? HTTOPLEFT
                                 : x > g.Width - CORNER   ? HTTOPRIGHT
                                                          : HTTOP,
                _               => x < CORNER             ? HTBOTTOMLEFT
                                 : x > g.Width - CORNER   ? HTBOTTOMRIGHT
                                                          : HTBOTTOM,
            };

            g.MouseMove += (_, e) =>
            {
                if (WindowState == FormWindowState.Maximized)
                {
                    g.Cursor = Cursors.Default;
                    return;
                }
                g.Cursor = HitAt(e.X) switch
                {
                    HTLEFT or HTRIGHT                 => Cursors.SizeWE,
                    HTTOP  or HTBOTTOM                => Cursors.SizeNS,
                    HTTOPLEFT or HTBOTTOMRIGHT        => Cursors.SizeNWSE,
                    _                                 => Cursors.SizeNESW,
                };
            };

            g.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                if (WindowState == FormWindowState.Maximized) return;
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN,
                    (IntPtr)HitAt(e.X), IntPtr.Zero);
            };

            return g;
        }

        // Lets a control act as the title bar: drag to move (Windows handles
        // snapping and drag-to-restore), double-click to toggle maximise.
        void MakeDraggable(Control c)
        {
            c.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN,
                    (IntPtr)HTCAPTION, IntPtr.Zero);
            };
            c.DoubleClick += (_, _) => ToggleMaximise();
        }

        void ToggleMaximise()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            UpdateMaxGlyph();
        }

        void UpdateMaxGlyph()
        {
            if (_btnMax != null)
                _btnMax.Text = WindowState == FormWindowState.Maximized
                    ? ""   // restore
                    : "";  // maximise
        }

        // Caption buttons – flat, themed, Windows-style glyphs.
        Label MakeCaptionBtn(string glyph, Color hoverBg, Color hoverFg,
            Action onClick)
        {
            var b = new Label
            {
                Text      = glyph,
                Font      = new Font("Segoe MDL2 Assets", 8.5f),
                ForeColor = Theme.Tx0,
                BackColor = Theme.Bg1,
                AutoSize  = false,
                Size      = new Size(46, CAP_H),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor    = Cursors.Hand,
            };
            b.MouseEnter += (_, _) =>
            {
                b.BackColor = hoverBg;
                b.ForeColor = hoverFg;
            };
            b.MouseLeave += (_, _) =>
            {
                b.BackColor = Theme.Bg1;
                b.ForeColor = Theme.Tx0;
            };
            b.Click += (_, _) => onClick();
            return b;
        }

        // HEADER
        Panel BuildHeader()
        {
            var pnl = new Panel
                { Dock = DockStyle.Top, Height = HDR_H, BackColor = Theme.Bg1 };

            var title = new Label
            {
                Text      = "FIKA-SERVER SETUP UTILITY",
                Font      = Theme.H1,
                ForeColor = Theme.Gold,
                AutoSize  = true,
                Location  = new Point(16, 8),
            };
            var sub = new Label
            {
                Text      = Translations.T("h_sub"),
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(18, 34),
            };
            pnl.Controls.Add(title);
            pnl.Controls.Add(sub);

            // The header replaces the removed title bar.
            MakeDraggable(pnl);
            MakeDraggable(title);
            MakeDraggable(sub);

            var btnClose = MakeCaptionBtn("", Theme.Red, Color.White,
                Close);
            _btnMax      = MakeCaptionBtn("", Theme.Bg3, Theme.GoldL,
                ToggleMaximise);
            var btnMin   = MakeCaptionBtn("", Theme.Bg3, Theme.GoldL,
                () => WindowState = FormWindowState.Minimized);

            pnl.Controls.Add(btnClose);
            pnl.Controls.Add(_btnMax);
            pnl.Controls.Add(btnMin);
            UpdateMaxGlyph();

            // Anchoring would lock in the placeholder width the panel still has
            // at this point, so lay the buttons out whenever the header resizes.
            void LayoutCaptionBtns()
            {
                btnClose.Location = new Point(pnl.Width - 46,  0);
                _btnMax!.Location = new Point(pnl.Width - 92,  0);
                btnMin.Location   = new Point(pnl.Width - 138, 0);
            }
            pnl.Resize += (_, _) => LayoutCaptionBtns();
            LayoutCaptionBtns();

            pnl.Paint += (_, e) =>
            {
                using var p = new Pen(Theme.Line);
                e.Graphics.DrawLine(p, 0, HDR_H - 1, pnl.Width, HDR_H - 1);
            };
            return pnl;
        }

        // STATUS BAR
        Panel BuildStatusBar()
        {
            var pnl = new Panel
                { Dock = DockStyle.Bottom, Height = STATUS_H, BackColor = Theme.Bg2 };
            _statusBarLbl = new Label
            {
                Text      = Translations.T("sb_ready"),
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(8, 4),
            };
            pnl.Controls.Add(_statusBarLbl);
            pnl.Paint += (_, e) =>
            {
                using var p = new Pen(Theme.Line);
                e.Graphics.DrawLine(p, 0, 0, pnl.Width, 0);
            };
            return pnl;
        }

        // SIDEBAR
        Panel BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg1 };
            int y = 12;

            sidebar.Controls.Add(new Label
            {
                Text      = Translations.T("s_install"),
                Font      = Theme.Cap,
                ForeColor = Theme.Tx2,
                AutoSize  = true,
                Location  = new Point(14, y),
            });
            y += 18;

            for (int i = 0; i < _steps.Length; i++)
            {
                var item = BuildNavItem(i + 1, _steps[i].id,
                    Translations.T(_steps[i].key));
                item.Location = new Point(0, y);
                sidebar.Controls.Add(item);
                y += 36;
            }

            y += 6;

            // >> ALLE INSTALLIEREN
            var btnAll = MakeSidebarButton(Translations.T("s_all"), Theme.Gold);
            btnAll.Location = new Point(8, y);
            btnAll.Width    = NAV_W - 16;
            btnAll.Click   += async (_, _) =>
            {
                var dlg = new EftMethodDialog();
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    _logQ.Enqueue(("Installation abgebrochen.", "W"));
                    return;
                }

                _config.EftMethod = dlg.ChosenMethod == EftInstallMethod.BSG
                    ? "BSG" : "Steam";

                _logQ.Enqueue(($"EFT-Methode: {_config.EftMethod}", "S"));

                btnAll.Enabled = false;
                try   { await Task.Run(() => Installer.OpAll(_ctx)); }
                finally { SafeInvoke(() => btnAll.Enabled = true); }
            };
            sidebar.Controls.Add(btnAll);
            y += 34;

            // >> STATUS PRÜFEN
            var btnCheck = MakeSidebarButton(Translations.T("s_check"), Theme.GoldD);
            btnCheck.Location = new Point(8, y);
            btnCheck.Width    = NAV_W - 16;
            btnCheck.Click   += async (_, _) =>
            {
                btnCheck.Enabled = false;
                try   { await Task.Run(() => Installer.OpCheckAll(_ctx)); }
                finally { SafeInvoke(() => btnCheck.Enabled = true); }
            };
            sidebar.Controls.Add(btnCheck);
            y += 34;

            sidebar.Controls.Add(new Panel
            {
                Location  = new Point(8, y + 4),
                Size      = new Size(NAV_W - 16, 1),
                BackColor = Theme.Line,
            });
            y += 14;

            var navSettings = BuildNavItem(0, "Settings",
                Translations.T("s_settings"), hideNum: true);
            navSettings.Location = new Point(0, y);
            sidebar.Controls.Add(navSettings);
            y += 36;

            sidebar.Controls.Add(new Panel
            {
                Location  = new Point(8, y + 6),
                Size      = new Size(NAV_W - 16, 1),
                BackColor = Theme.Line,
            });
            y += 18;

            sidebar.Controls.Add(new Label
            {
                Text      = Translations.T("lang_label"),
                Font      = Theme.Cap,
                ForeColor = Theme.Tx2,
                AutoSize  = true,
                Location  = new Point(14, y),
            });
            y += 16;

            var btnDE = MakeLangBtn("DE");
            var btnEN = MakeLangBtn("EN");
            btnDE.Location = new Point(8,  y);
            btnEN.Location = new Point(56, y);

            void RefreshLangBtns()
            {
                btnDE.BackColor = Translations.Lang == "DE" ? Theme.Gold : Theme.Bg3;
                btnDE.ForeColor = Translations.Lang == "DE" ? Theme.Bg0  : Theme.Tx1;
                btnEN.BackColor = Translations.Lang == "EN" ? Theme.Gold : Theme.Bg3;
                btnEN.ForeColor = Translations.Lang == "EN" ? Theme.Bg0  : Theme.Tx1;
            }
            RefreshLangBtns();

            btnDE.Click += (_, _) => SwitchLanguage("DE");
            btnEN.Click += (_, _) => SwitchLanguage("EN");

            sidebar.Controls.Add(btnDE);
            sidebar.Controls.Add(btnEN);

            return sidebar;
        }

        Button MakeLangBtn(string text)
        {
            var btn = new Button
            {
                Text      = text,
                Size      = new Size(40, 24),
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.Nav,
                Cursor    = Cursors.Hand,
            };
            btn.FlatAppearance.BorderColor = Theme.Line;
            btn.FlatAppearance.BorderSize  = 1;
            return btn;
        }

        Panel BuildNavItem(int number, string id, string label,
            bool hideNum = false)
        {
            var row = new Panel
            {
                Size      = new Size(NAV_W, 36),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
            };

            if (!hideNum)
            {
                row.Controls.Add(new Label
                {
                    Text      = number.ToString("D2"),
                    Font      = Theme.Mn2,
                    ForeColor = Theme.Tx2,
                    Size      = new Size(30, 36),
                    Location  = new Point(12, 0),
                    TextAlign = ContentAlignment.MiddleLeft,
                });
            }

            var lblName = new Label
            {
                Text      = label,
                Font      = Theme.Nav,
                ForeColor = Theme.Tx0,
                Size      = new Size(hideNum ? NAV_W - 46 : NAV_W - 80, 36),
                Location  = new Point(hideNum ? 14 : 44, 0),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var badge = new Label
            {
                Text      = "●",
                Font      = Theme.Cap,
                ForeColor = Theme.Tx2,
                Size      = new Size(20, 36),
                Location  = new Point(NAV_W - 24, 0),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            if (!hideNum) _badges[id] = badge;

            row.Controls.Add(lblName);
            row.Controls.Add(badge);

            row.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Line);
                e.Graphics.DrawLine(pen, 0, row.Height - 1,
                    row.Width, row.Height - 1);
                if (_activePanelId == id)
                {
                    using var accent = new Pen(Theme.Gold, 2);
                    e.Graphics.DrawLine(accent, 1, 2, 1, row.Height - 2);
                }
            };

            void SetHover(bool on)
            {
                bool active = _activePanelId == id;
                row.BackColor     = (on || active) ? Theme.BgActive : Color.Transparent;
                lblName.ForeColor = (on || active) ? Theme.Gold     : Theme.Tx0;
            }

            foreach (Control c in new Control[] { row, lblName, badge })
            {
                c.MouseEnter += (_, _) => SetHover(true);
                c.MouseLeave += (_, _) => SetHover(false);
                c.Click      += (_, _) => { ShowPanel(id); row.Invalidate(); };
            }

            return row;
        }

        // CONTENT PANELS
        void BuildAllContentPanels()
        {
            BuildSteamPanel();
            BuildEftPanel();
            BuildSptPanel();
            BuildFikaPanel();
            BuildHeadlessPanel();
            BuildDockerPanel();
            BuildFirewallPanel();
            BuildWebAppPanel();
            BuildSettingsPanel();
        }

        void RegisterPanel(string id, Panel pnl)
        {
            pnl.Dock    = DockStyle.Fill;
            pnl.Visible = false;
            // The status boxes cost 60px per section – scroll rather than clip
            // the buttons when the window is near its minimum size.
            pnl.AutoScroll = true;
            _panels[id] = pnl;
            _contentArea.Controls.Add(pnl);
        }

        Panel ContentShell(string heading, string description = "")
        {
            var pnl = new Panel { BackColor = Theme.Bg0 };

            pnl.Controls.Add(new Label
            {
                Text      = heading,
                Font      = Theme.H1,
                ForeColor = Theme.Gold,
                AutoSize  = true,
                Location  = new Point(24, 20),
            });
            pnl.Controls.Add(new Panel
            {
                Location  = new Point(24, 50),
                Size      = new Size(700, 1),
                BackColor = Theme.Line,
            });

            if (!string.IsNullOrEmpty(description))
            {
                // Fixed box rather than AutoSize: the descriptions now carry
                // full paths and were getting clipped at the ascenders.
                pnl.Controls.Add(new Label
                {
                    Text      = description,
                    Font      = Theme.Bd,
                    ForeColor = Theme.Tx1,
                    AutoSize  = false,
                    Location  = new Point(24, 56),
                    Size      = new Size(720, 36),
                    TextAlign = ContentAlignment.TopLeft,
                });
            }

            return pnl;
        }

        // Per-section status panel – mirrors the sidebar badge, but with the
        // full message so each section explains its own state.
        void AddStatusBox(Panel pnl, string id, ref int y)
        {
            var box = new Panel
            {
                Location  = new Point(24, y),
                Size      = new Size(700, 48),
                BackColor = Theme.Bg2,
            };
            box.Paint += (_, e) =>
            {
                using var p = new Pen(Theme.Line);
                e.Graphics.DrawRectangle(p, 0, 0, box.Width - 1, box.Height - 1);
            };

            box.Controls.Add(new Label
            {
                Text      = Translations.T("st_hdr"),
                Font      = Theme.Cap,
                ForeColor = Theme.Tx2,
                AutoSize  = true,
                Location  = new Point(10, 7),
            });

            var val = new Label
            {
                Text      = Translations.T("st_pending"),
                Font      = Theme.Mn2,
                ForeColor = Theme.Tx1,
                AutoSize  = false,
                Location  = new Point(10, 24),
                Size      = new Size(680, 18),
                TextAlign = ContentAlignment.TopLeft,
            };
            box.Controls.Add(val);
            _statusBoxes[id] = val;

            pnl.Controls.Add(box);
            y += 60;
        }

        // 01 STEAM
        void BuildSteamPanel()
        {
            var pnl = ContentShell("STEAM", Translations.T("st_desc"));
            int y = 100;

            AddStatusBox(pnl, "Steam", ref y);
            AddInfoRow(pnl, "Standard-Pfad:",
                @"C:\Program Files (x86)\Steam", ref y);
            y += 8;
            AddSectionLabel(pnl, Translations.T("sec_action"), ref y);

            AddActionBtn(pnl, Translations.T("btn_chk_steam"), ref y, async () =>
            {
                _logQ.Enqueue(("Checking Steam...", "S"));
                await Task.Delay(100);
                bool found =
                    File.Exists(@"C:\Program Files (x86)\Steam\steam.exe")
                 || File.Exists(@"C:\Program Files\Steam\steam.exe");
                _badgeQ.Enqueue(("Steam", found ? 2 : 4));
                _logQ.Enqueue((found
                    ? "[OK]  Steam found."
                    : "[!!]  Steam not found.", found ? "O" : "W"));
            });

            AddActionBtn(pnl, Translations.T("btn_inst_steam"), ref y, () =>
                RunBg(() => Task.Run(() => Installer.OpSteam(_ctx))));

            RegisterPanel("Steam", pnl);
        }

        // 02 EFT
        void BuildEftPanel()
        {
            var pnl = ContentShell("ESCAPE FROM TARKOV");
            int y = 70;

            AddStatusBox(pnl, "EFT", ref y);
            AddSectionLabel(pnl, Translations.T("e_sec"), ref y);

            pnl.Controls.Add(new Label
            {
                Text      = Translations.T("e_note"),
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 24;

            AddInstallCard(pnl,
                Translations.T("e_bsg_t"),
                Translations.T("e_bsg_s"),
                Translations.T("btn_install"),
                ref y,
                () =>
                {
                    _config.EftMethod = "BSG";
                    RunBg(() => Task.Run(() => Installer.OpEFTBSG(_ctx)));
                });

            AddInstallCard(pnl,
                Translations.T("e_st_t"),
                Translations.T("e_st_s"),
                Translations.T("btn_vsteam"),
                ref y,
                () =>
                {
                    _config.EftMethod = "Steam";
                    RunBg(() => Task.Run(() => Installer.OpEFTSteam(_ctx)));
                });

            RegisterPanel("EFT", pnl);
        }

        void AddInstallCard(Panel parent, string title, string subtitle,
            string btnText, ref int y, Action onClick)
        {
            var card = new Panel
            {
                Location  = new Point(24, y),
                Size      = new Size(520, 52),
                BackColor = Theme.Bg2,
            };
            card.Paint += (_, e) =>
            {
                using var p = new Pen(Theme.Line);
                e.Graphics.DrawRectangle(p, 0, 0,
                    card.Width - 1, card.Height - 1);
            };
            card.Controls.Add(new Label
            {
                Text      = title,
                Font      = Theme.H3,
                ForeColor = Theme.Gold,
                AutoSize  = true,
                Location  = new Point(12, 8),
            });
            card.Controls.Add(new Label
            {
                Text      = subtitle,
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(14, 28),
            });

            var btn = MakeButton(btnText);
            btn.Size     = new Size(140, 28);
            btn.Location = new Point(card.Width - 152, 12);
            btn.Click   += async (_, _) =>
            {
                btn.Enabled = false;
                try   { await Task.Run(onClick); }
                finally { SafeInvoke(() => btn.Enabled = true); }
            };
            card.Controls.Add(btn);
            parent.Controls.Add(card);
            y += 62;
        }

        // 03 SPT
        void BuildSptPanel()
        {
            var pnl = ContentShell("SPT SERVER", Translations.T("spt_desc"));
            int y = 108;

            pnl.Controls.Add(new Label
            {
                Text      = Translations.T("spt_note"),
                Font      = Theme.Sm,
                ForeColor = Theme.Amber,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 22;
            AddStatusBox(pnl, "SPT", ref y);
            AddSectionLabel(pnl, Translations.T("sec_action"), ref y);

            AddLabel(pnl, "Pfad:", ref y);
            _tbSptDir = AddTextBox(pnl, _config.SptDir, ref y);
            _tbSptDir.TextChanged += (_, _) => _config.SptDir = _tbSptDir.Text;

            AddActionBtn(pnl, Translations.T("btn_browse"), ref y, () =>
            {
                using var dlg = new FolderBrowserDialog
                    { Description = Translations.T("fbr_spt") };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var picked = Installer.ResolveSptDir(dlg.SelectedPath)
                                 ?? dlg.SelectedPath;
                    _config.SptDir = picked;
                    SafeInvoke(() =>
                    {
                        if (_tbSptDir    != null) _tbSptDir.Text    = picked;
                        if (_tbSetSptDir != null) _tbSetSptDir.Text = picked;
                    });
                }
            });

            AddActionBtn(pnl, Translations.T("btn_chk_spt"), ref y, () =>
            {
                var resolved = Installer.ResolveSptDir(_config.SptDir);
                bool ok      = resolved != null;

                if (ok && resolved != _config.SptDir)
                {
                    _config.SptDir = resolved!;
                    SafeInvoke(() =>
                    {
                        if (_tbSptDir    != null) _tbSptDir.Text    = resolved!;
                        if (_tbSetSptDir != null) _tbSetSptDir.Text = resolved!;
                    });
                }

                _logQ.Enqueue((ok
                    ? $"[OK]  SPT Server found: {resolved}"
                    : "[!!]  SPT Server not found.", ok ? "O" : "W"));
                _badgeQ.Enqueue(("SPT", ok ? 2 : 4));
            });

            AddActionBtn(pnl, Translations.T("btn_inst_spt"), ref y, () =>
                RunBg(() => Task.Run(() => Installer.OpSPT(_ctx))));

            RegisterPanel("SPT", pnl);
        }

        // 04 FIKA
        void BuildFikaPanel()
        {
            var pnl = ContentShell("FIKA", Translations.T("fika_desc"));
            int y = 108;

            pnl.Controls.Add(new Label
            {
                Text      = Translations.T("fika_note"),
                Font      = Theme.Sm,
                ForeColor = Theme.Amber,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 22;
            AddStatusBox(pnl, "Fika", ref y);
            AddSectionLabel(pnl, Translations.T("sec_action"), ref y);

            AddLabel(pnl, "API-Key:", ref y);
            _tbApiKey = AddTextBox(pnl, _config.ApiKey, ref y);
            _tbApiKey.TextChanged += (_, _) =>
            {
                _config.ApiKey = _tbApiKey.Text;
                if (_tbSetApiKey != null
                    && _tbSetApiKey.Text != _tbApiKey.Text)
                    _tbSetApiKey.Text = _tbApiKey.Text;
            };

            AddActionBtn(pnl, Translations.T("btn_inst_fika"), ref y, () =>
                RunBg(() => Task.Run(() => Installer.OpFika(_ctx))));

            RegisterPanel("Fika", pnl);
        }

        // 05 HEADLESS
        void BuildHeadlessPanel()
        {
            var pnl = ContentShell("HEADLESS CLIENT",
                Translations.T("hl_desc"));
            int y = 96;

            foreach (var key in new[] { "hl_note", "hl_note2" })
            {
                pnl.Controls.Add(new Label
                {
                    Text      = Translations.T(key),
                    Font      = Theme.Sm,
                    ForeColor = Theme.Amber,
                    AutoSize  = true,
                    Location  = new Point(24, y),
                });
                y += 18;
            }
            y += 6;

            AddStatusBox(pnl, "Headless", ref y);
            AddSectionLabel(pnl, Translations.T("hl_dir_h"), ref y);

            _tbHeadlessDir = AddTextBox(pnl,
                Installer.HeadlessDirFor(_ctx), ref y);
            _tbHeadlessDir.TextChanged += (_, _) =>
                _config.HeadlessDir = _tbHeadlessDir.Text;

            AddActionBtn(pnl, Translations.T("btn_browse"), ref y, () =>
            {
                using var dlg = new FolderBrowserDialog
                    { Description = Translations.T("fbr_hl") };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                _config.HeadlessDir = dlg.SelectedPath;
                SafeInvoke(() =>
                {
                    if (_tbHeadlessDir != null)
                        _tbHeadlessDir.Text = dlg.SelectedPath;
                });
            });

            AddSectionLabel(pnl, Translations.T("sec_action"), ref y);

            AddActionBtn(pnl, Translations.T("btn_inst_hl"), ref y, () =>
                RunBg(() => Task.Run(() => Installer.OpHeadless(_ctx))));

            RegisterPanel("Headless", pnl);
        }

        // 06 DOCKER + WSL2
        void BuildDockerPanel()
        {
            var pnl = ContentShell("DOCKER + WSL2",
                Translations.T("dk_desc"));
            int y = 108;

            pnl.Controls.Add(new Label
            {
                Text      = Translations.T("dk_note"),
                Font      = Theme.Sm,
                ForeColor = Theme.Amber,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 22;
            AddStatusBox(pnl, "Docker", ref y);
            AddSectionLabel(pnl, Translations.T("sec_action"), ref y);

            AddActionBtn(pnl, Translations.T("btn_chk_docker"), ref y,
                async () =>
                {
                    _logQ.Enqueue(("Checking Docker...", "S"));
                    bool ok = false;
                    try
                    {
                        var psi = new ProcessStartInfo("docker", "--version")
                        {
                            RedirectStandardOutput = true,
                            UseShellExecute        = false,
                            CreateNoWindow         = true,
                        };
                        using var proc = Process.Start(psi);
                        string output =
                            proc?.StandardOutput.ReadToEnd() ?? "";
                        proc?.WaitForExit();
                        ok = proc?.ExitCode == 0
                          && !string.IsNullOrWhiteSpace(output);
                        _logQ.Enqueue((ok
                            ? $"[OK]  {output.Trim()}"
                            : "[!!]  Docker not running.", ok ? "O" : "W"));
                    }
                    catch
                    {
                        _logQ.Enqueue(("[!!]  Docker not installed.", "W"));
                    }
                    _badgeQ.Enqueue(("Docker", ok ? 2 : 4));
                    await Task.CompletedTask;
                });

            AddActionBtn(pnl, Translations.T("btn_inst_docker"), ref y, () =>
                RunBg(() => Task.Run(() => Installer.OpDocker(_ctx))));

            AddActionBtn(pnl, Translations.T("btn_wsl2"), ref y, () =>
            {
                _logQ.Enqueue(("Opening WSL2 installation guide...", "S"));
                OpenUrl("https://learn.microsoft.com/windows/wsl/install");
            });

            RegisterPanel("Docker", pnl);
        }

        // 07 FIREWALL
        void BuildFirewallPanel()
        {
            var pnl = ContentShell("FIREWALL");
            int y = 70;

            AddStatusBox(pnl, "Firewall", ref y);
            AddSectionLabel(pnl, Translations.T("fw_sec"), ref y);

            var ports = new[]
            {
                ("6969",  "TCP", Translations.T("fw_p1")),
                ("6969",  "UDP", Translations.T("fw_p2")),
                ("25565", "UDP", Translations.T("fw_p3")),
                ("8080",  "TCP", Translations.T("fw_p4")),
                ("5000",  "TCP", Translations.T("fw_p5")),
            };

            foreach (var (port, proto, desc) in ports)
            {
                var row = new Panel
                {
                    Location  = new Point(24, y),
                    Size      = new Size(500, 28),
                    BackColor = Theme.Bg2,
                };
                row.Paint += (_, e) =>
                {
                    using var p = new Pen(Theme.Line);
                    e.Graphics.DrawLine(p, 0, row.Height - 1,
                        row.Width, row.Height - 1);
                };

                var statusLbl = new Label
                {
                    Text      = "●",
                    Font      = Theme.Cap,
                    ForeColor = Theme.Tx2,
                    Size      = new Size(16, 28),
                    Location  = new Point(4, 0),
                    TextAlign = ContentAlignment.MiddleCenter,
                };
                _fwLabels[$"{port}/{proto}"] = statusLbl;

                row.Controls.Add(statusLbl);
                row.Controls.Add(new Label { Text = port,  Font = Theme.Mn2, ForeColor = Theme.Gold, Size = new Size(54, 28),  Location = new Point(22,  0), TextAlign = ContentAlignment.MiddleLeft });
                row.Controls.Add(new Label { Text = proto, Font = Theme.Cap, ForeColor = Theme.Tx1,  Size = new Size(36, 28),  Location = new Point(78,  0), TextAlign = ContentAlignment.MiddleLeft });
                row.Controls.Add(new Label { Text = desc,  Font = Theme.Sm,  ForeColor = Theme.Tx0,  Size = new Size(250, 28), Location = new Point(118, 0), TextAlign = ContentAlignment.MiddleLeft });

                pnl.Controls.Add(row);
                y += 28;
            }

            y += 14;
            AddActionBtn(pnl, Translations.T("btn_ports"), ref y, () =>
                RunBg(() => Task.Run(() => Installer.OpFirewall(_ctx))));

            RegisterPanel("Firewall", pnl);
        }

        // 08 WEBAPP
        void BuildWebAppPanel()
        {
            var pnl = ContentShell("FIKAWEBAPP");
            int y = 70;

            AddStatusBox(pnl, "WebApp", ref y);
            AddSectionLabel(pnl, Translations.T("wa_sec"), ref y);

            pnl.Controls.Add(new Label
            {
                Text      = Translations.T("wa_desc"),
                Font      = Theme.Bd,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 24;

            AddLabel(pnl, Translations.T("wa_api"), ref y);
            _tbWaApiKey = AddTextBox(pnl, _config.ApiKey, ref y);
            _tbWaApiKey.TextChanged += (_, _) =>
            {
                _config.ApiKey = _tbWaApiKey.Text;
                if (_tbApiKey    != null
                    && _tbApiKey.Text    != _tbWaApiKey.Text)
                    _tbApiKey.Text    = _tbWaApiKey.Text;
                if (_tbSetApiKey != null
                    && _tbSetApiKey.Text != _tbWaApiKey.Text)
                    _tbSetApiKey.Text = _tbWaApiKey.Text;
            };

            pnl.Controls.Add(new Label
            {
                Text      = Translations.T("wa_note"),
                Font      = Theme.Sm,
                ForeColor = Theme.Amber,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 24;

            AddActionBtn(pnl, Translations.T("btn_inst_wa"), ref y, () =>
                RunBg(() => Task.Run(() => Installer.OpWebApp(_ctx))));

            AddActionBtn(pnl, Translations.T("btn_open_wa"), ref y, () =>
                OpenUrl("http://localhost:8080"));

            RegisterPanel("WebApp", pnl);
        }

        // SETTINGS
        void BuildSettingsPanel()
        {
            var pnl = ContentShell(Translations.T("set_title"));
            int y = 70;

            AddSectionLabel(pnl, Translations.T("set_spt_h"), ref y);
            pnl.Controls.Add(new Label
            {
                Text      = Translations.T("set_spt_d"),
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 18;

            _tbSetSptDir = AddTextBox(pnl, _config.SptDir, ref y);
            _tbSetSptDir.TextChanged += (_, _) =>
            {
                _config.SptDir = _tbSetSptDir.Text;
                if (_tbSptDir != null
                    && _tbSptDir.Text != _tbSetSptDir.Text)
                    _tbSptDir.Text = _tbSetSptDir.Text;
            };

            AddActionBtn(pnl, Translations.T("btn_browse"), ref y, () =>
            {
                using var dlg = new FolderBrowserDialog
                    { Description = Translations.T("fbr_spt") };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    // Picking the game folder is the obvious mistake on 4.1+ –
                    // step into SPT_Runtime for the user when we find it there.
                    var picked = Installer.ResolveSptDir(dlg.SelectedPath)
                                 ?? dlg.SelectedPath;
                    _config.SptDir = picked;
                    SafeInvoke(() =>
                    {
                        if (_tbSetSptDir != null) _tbSetSptDir.Text = picked;
                        if (_tbSptDir    != null) _tbSptDir.Text    = picked;
                    });
                }
            });

            y += 6;
            AddSectionLabel(pnl, Translations.T("set_eft_h"), ref y);
            pnl.Controls.Add(new Label
            {
                Text      = Translations.T("set_eft_d"),
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 18;

            _tbSetEftDir = AddTextBox(pnl, _config.EftDir, ref y);
            _tbSetEftDir.TextChanged += (_, _) =>
                _config.EftDir = _tbSetEftDir.Text;

            AddActionBtn(pnl, Translations.T("btn_browse"), ref y, () =>
            {
                using var dlg = new FolderBrowserDialog
                    { Description = Translations.T("fbr_eft") };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _config.EftDir = dlg.SelectedPath;
                    SafeInvoke(() =>
                    {
                        if (_tbSetEftDir != null)
                            _tbSetEftDir.Text = dlg.SelectedPath;
                    });
                }
            });

            y += 6;
            AddSectionLabel(pnl, Translations.T("set_api_h"), ref y);
            _tbSetApiKey = AddTextBox(pnl, _config.ApiKey, ref y);
            _tbSetApiKey.TextChanged += (_, _) =>
            {
                _config.ApiKey = _tbSetApiKey.Text;
                if (_tbApiKey   != null
                    && _tbApiKey.Text   != _tbSetApiKey.Text)
                    _tbApiKey.Text   = _tbSetApiKey.Text;
                if (_tbWaApiKey != null
                    && _tbWaApiKey.Text != _tbSetApiKey.Text)
                    _tbWaApiKey.Text = _tbSetApiKey.Text;
            };

            y += 8;

            var btnSave = MakeButton(Translations.T("btn_save"));
            btnSave.Location = new Point(24, y);
            btnSave.Width    = 150;
            btnSave.Click   += (_, _) =>
                _logQ.Enqueue(("Settings saved.", "O"));
            pnl.Controls.Add(btnSave);

            var btnRecheck = MakeButton(Translations.T("btn_recheck"));
            btnRecheck.Location = new Point(184, y);
            btnRecheck.Width    = 190;
            btnRecheck.Click   += async (_, _) =>
            {
                btnRecheck.Enabled = false;
                try   { await Task.Run(() => Installer.OpCheckAll(_ctx)); }
                finally { SafeInvoke(() => btnRecheck.Enabled = true); }
            };
            pnl.Controls.Add(btnRecheck);

            RegisterPanel("Settings", pnl);
        }

        // LOG PANEL
        Panel BuildLogPanel()
        {
            var pnl = new Panel
                { Dock = DockStyle.Fill, BackColor = Theme.Bg1 };

            var hdr = new Panel
                { Dock = DockStyle.Top, Height = 26, BackColor = Theme.Bg2 };
            hdr.Controls.Add(new Label
            {
                Text      = Translations.T("log_hdr"),
                Font      = Theme.Cap,
                ForeColor = Theme.Tx2,
                AutoSize  = true,
                Location  = new Point(8, 6),
            });

            var btnClear = new Button
            {
                Text      = Translations.T("btn_clear"),
                Font      = Theme.Cap,
                ForeColor = Theme.Tx1,
                FlatStyle = FlatStyle.Flat,
                Dock      = DockStyle.Right,
                Width     = 64,
                BackColor = Color.Transparent,
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (_, _) => _logBox?.Clear();
            hdr.Controls.Add(btnClear);
            hdr.Paint += (_, e) =>
            {
                using var p = new Pen(Theme.Line);
                e.Graphics.DrawLine(p, 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            };

            _logBox = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                BackColor   = Theme.Bg1,
                ForeColor   = Theme.Tx0,
                Font        = Theme.Mn2,
                BorderStyle = BorderStyle.None,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                WordWrap    = false,
            };

            pnl.Controls.Add(_logBox);
            pnl.Controls.Add(hdr);
            return pnl;
        }

        // NAVIGATION
        void ShowPanel(string id)
        {
            if (!_panels.ContainsKey(id)) return;
            _activePanelId = id;
            foreach (var kv in _panels)
                kv.Value.Visible = kv.Key == id;
        }

        // UI HELPERS
        Button MakeButton(string text)
        {
            var btn = new Button
            {
                Text      = text,
                ForeColor = Theme.Gold,
                BackColor = Theme.Bg3,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.H3,
                Height    = 30,
                Cursor    = Cursors.Hand,
            };
            btn.FlatAppearance.BorderColor = Theme.Line;
            btn.FlatAppearance.BorderSize  = 1;
            return btn;
        }

        Button MakeSidebarButton(string text, Color fg)
        {
            var btn = new Button
            {
                Text      = text,
                ForeColor = fg,
                BackColor = Theme.Bg2,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.Nav,
                Height    = 28,
                Cursor    = Cursors.Hand,
            };
            btn.FlatAppearance.BorderColor = Theme.Line;
            btn.FlatAppearance.BorderSize  = 1;
            return btn;
        }

        void AddSectionLabel(Panel pnl, string text, ref int y)
        {
            pnl.Controls.Add(new Panel
            {
                Location  = new Point(24, y),
                Size      = new Size(500, 1),
                BackColor = Theme.Line,
            });
            y += 4;
            pnl.Controls.Add(new Label
            {
                Text      = text,
                Font      = Theme.Cap,
                ForeColor = Theme.Tx2,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 18;
        }

        void AddLabel(Panel pnl, string text, ref int y)
        {
            pnl.Controls.Add(new Label
            {
                Text      = text,
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            y += 18;
        }

        TextBox AddTextBox(Panel pnl, string value, ref int y)
        {
            var tb = new TextBox
            {
                Text        = value,
                Location    = new Point(24, y),
                Size        = new Size(440, 24),
                BackColor   = Theme.Bg3,
                ForeColor   = Theme.Tx0,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = Theme.Mn2,
            };
            pnl.Controls.Add(tb);
            y += 32;
            return tb;
        }

        void AddInfoRow(Panel pnl, string label, string value, ref int y)
        {
            pnl.Controls.Add(new Label
            {
                Text      = label,
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(24, y),
            });
            pnl.Controls.Add(new Label
            {
                Text      = value,
                Font      = Theme.Mn2,
                ForeColor = Theme.Tx0,
                AutoSize  = true,
                Location  = new Point(140, y),
            });
            y += 24;
        }

        void AddActionBtn(Panel pnl, string text, ref int y, Action onClick)
        {
            var btn = MakeButton(text);
            btn.Location = new Point(24, y);
            btn.Width    = 220;
            btn.Click   += async (_, _) =>
            {
                btn.Enabled = false;
                try   { await Task.Run(onClick); }
                finally { SafeInvoke(() => btn.Enabled = true); }
            };
            pnl.Controls.Add(btn);
            y += 38;
        }

        // TIMER/QUEUES
        void OnUiTick(object? s, EventArgs e)
        {
            while (_logQ.TryDequeue(out var entry))
                AppendLog(entry.text, entry.lvl);

            while (_badgeQ.TryDequeue(out var b))
                ApplyBadge(b.id, b.state);

            while (_statusQ.TryDequeue(out var st))
                ApplyStatusBox(st.id, st.state, st.msg);

            while (_fwQ.TryDequeue(out var fw))
            {
                bool ok = fw.s == "v";
                AppendLog(
                    $"[FW]  Port {fw.port}/{fw.proto}: {(ok ? "OK" : "Not open")}",
                    ok ? "O" : "W");
                if (_fwLabels.TryGetValue($"{fw.port}/{fw.proto}", out var lbl))
                    lbl.ForeColor = ok ? Theme.GreenL : Theme.RedL;
            }
        }

        void AppendLog(string text, string lvl)
        {
            if (_logBox == null || _logBox.IsDisposed) return;
            var ts    = $"[{DateTime.Now:HH:mm:ss}] ";
            var color = lvl switch
            {
                "O" => Theme.GreenL,
                "E" => Theme.RedL,
                "W" => Theme.AmberL,
                _   => Theme.Tx1,
            };
            _logBox.SuspendLayout();
            _logBox.SelectionStart  = _logBox.TextLength;
            _logBox.SelectionLength = 0;
            _logBox.SelectionColor  = Theme.Tx2;
            _logBox.AppendText(ts);
            _logBox.SelectionColor  = color;
            _logBox.AppendText(text + "\n");
            _logBox.ScrollToCaret();
            _logBox.ResumeLayout();
        }

        void ApplyBadge(string id, int state)
        {
            if (!_badges.TryGetValue(id, out var lbl)) return;
            lbl.ForeColor = StateColor(state);
        }

        void ApplyStatusBox(string id, int state, string msg)
        {
            if (!_statusBoxes.TryGetValue(id, out var lbl)) return;
            if (lbl.IsDisposed) return;
            lbl.Text      = msg;
            lbl.ForeColor = StateColor(state);
        }

        static Color StateColor(int state) => state switch
        {
            1 => Theme.Gold,
            2 => Theme.GreenL,
            3 => Theme.RedL,
            4 => Theme.AmberL,
            _ => Theme.Tx2,
        };

        // UTILITIES
        void SafeInvoke(Action a)
        {
            if (InvokeRequired) Invoke(a);
            else a();
        }

        void RunBg(Func<Task> task) => Task.Run(task);

        static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url)
                    { UseShellExecute = true });
            }
            catch { }
        }
    }
}