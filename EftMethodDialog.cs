using System.Drawing;
using System.Windows.Forms;

namespace FikaServerSetupWizard
{
    public enum EftInstallMethod { None, BSG, Steam }

    public class EftMethodDialog : Form
    {
        public EftInstallMethod ChosenMethod { get; private set; } = EftInstallMethod.None;

        public EftMethodDialog()
        {
            Text            = "EFT Installationsmethode";
            Size            = new Size(520, 380);
            MinimumSize     = new Size(520, 380);
            MaximumSize     = new Size(520, 380);
            BackColor       = Theme.Bg0;
            ForeColor       = Theme.Tx0;
            Font            = Theme.Bd;
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;

            BuildUI();
        }

        void BuildUI()
        {
            // Header
            var pnlHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 68,
                BackColor = Theme.Bg1,
            };
            pnlHeader.Paint += (_, e) =>
            {
                using var p = new Pen(Theme.Line);
                e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1,
                    pnlHeader.Width, pnlHeader.Height - 1);
            };
            pnlHeader.Controls.Add(new Label
            {
                Text      = "EFT INSTALLATIONSMETHODE WAEHLEN",
                Font      = Theme.H1,
                ForeColor = Theme.Gold,
                AutoSize  = true,
                Location  = new Point(16, 10),
            });
            pnlHeader.Controls.Add(new Label
            {
                Text      = "Welche Methode soll bei  >>  ALLE INSTALLIEREN  fuer EFT verwendet werden?",
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(18, 42),
            });
            Controls.Add(pnlHeader);

            // Body
            var body = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Theme.Bg0,
                Padding   = new Padding(16),
            };

            // Card: BSG
            var cardBSG = MakeCard(
                "BSG LAUNCHER",
                "Gekauft auf escapefromtarkov.com  (empfohlen)",
                () => Pick(EftInstallMethod.BSG));
            cardBSG.Location = new Point(16, 16);
            body.Controls.Add(cardBSG);

            // Card: Steam
            var cardSteam = MakeCard(
                "STEAM  (App-ID 3932890)",
                "Gekauft im Steam-Store",
                () => Pick(EftInstallMethod.Steam));
            cardSteam.Location = new Point(16, 16 + 80 + 12);
            body.Controls.Add(cardSteam);

            // Divider
            var divider = new Panel
            {
                Location  = new Point(16, 16 + 80 + 12 + 80 + 16),
                Size      = new Size(460, 1),
                BackColor = Theme.Line,
            };
            body.Controls.Add(divider);

            // Cancel
            var btnCancel = new Button
            {
                Text      = "ABBRECHEN",
                Font      = Theme.Nav,
                ForeColor = Theme.Tx1,
                BackColor = Theme.Bg2,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(140, 34),
                Location  = new Point(16, 16 + 80 + 12 + 80 + 32),
                Cursor    = Cursors.Hand,
            };
            btnCancel.FlatAppearance.BorderColor = Theme.Line;
            btnCancel.FlatAppearance.BorderSize  = 1;
            btnCancel.Click += (_, _) =>
            {
                ChosenMethod = EftInstallMethod.None;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            body.Controls.Add(btnCancel);

            Controls.Add(body);
        }

        Panel MakeCard(string title, string subtitle, Action onClick)
        {
            var card = new Panel
            {
                Size      = new Size(460, 72),
                BackColor = Theme.Bg2,
                Cursor    = Cursors.Hand,
            };
            card.Paint += (_, e) =>
            {
                using var p = new Pen(Theme.Line);
                e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            var lblTitle = new Label
            {
                Text      = title,
                Font      = Theme.H2,
                ForeColor = Theme.Gold,
                AutoSize  = true,
                Location  = new Point(16, 12),
            };
            var lblSub = new Label
            {
                Text      = subtitle,
                Font      = Theme.Sm,
                ForeColor = Theme.Tx1,
                AutoSize  = true,
                Location  = new Point(18, 40),
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblSub);

            // Arrow indicator
            var arrow = new Label
            {
                Text      = "▶",
                Font      = Theme.H2,
                ForeColor = Theme.Tx2,
                Size      = new Size(28, 72),
                Location  = new Point(card.Width - 34, 0),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            card.Controls.Add(arrow);

            void SetHover(bool on)
            {
                card.BackColor     = on ? Theme.BgActive : Theme.Bg2;
                lblTitle.ForeColor = on ? Color.White    : Theme.Gold;
                arrow.ForeColor    = on ? Theme.Gold     : Theme.Tx2;
            }

            foreach (Control c in new Control[] { card, lblTitle, lblSub, arrow })
            {
                c.MouseEnter += (_, _) => SetHover(true);
                c.MouseLeave += (_, _) => SetHover(false);
                c.Click      += (_, _) => onClick();
            }

            return card;
        }

        void Pick(EftInstallMethod method)
        {
            ChosenMethod = method;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}