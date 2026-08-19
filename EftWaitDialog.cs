using System;
using System.Drawing;
using System.Windows.Forms;

namespace FikaServerSetupWizard
{
    public class EftWaitDialog : Form
    {
        public EftWaitDialog(string title, string message)
        {
            Text            = title;
            Size            = new Size(560, 340);
            MinimumSize     = new Size(560, 340);
            MaximumSize     = new Size(560, 340);
            BackColor       = Theme.Bg0;
            ForeColor       = Theme.Tx0;
            Font            = Theme.Bd;
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            TopMost         = true;

            BuildUI(title, message);
        }

        void BuildUI(string title, string message)
        {
            // Colored top bar
            var topBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 6,
                BackColor = Theme.Amber,
            };
            Controls.Add(topBar);

            // Warning icon row
            var iconRow = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 68,
                BackColor = Theme.Bg1,
            };

            iconRow.Controls.Add(new Label
            {
                Text      = "⚠",
                Font      = new Font("Segoe UI", 26f, FontStyle.Bold),
                ForeColor = Theme.Amber,
                Size      = new Size(52, 68),
                Location  = new Point(16, 0),
                TextAlign = ContentAlignment.MiddleCenter,
            });

            iconRow.Controls.Add(new Label
            {
                Text      = title.ToUpperInvariant(),
                Font      = Theme.H1,
                ForeColor = Theme.Gold,
                AutoSize  = true,
                Location  = new Point(76, 10),
            });

            iconRow.Paint += (_, e) =>
            {
                using var p = new Pen(Theme.Line);
                e.Graphics.DrawLine(p, 0, iconRow.Height - 1,
                    iconRow.Width, iconRow.Height - 1);
            };
            Controls.Add(iconRow);

            // Message body
            var body = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Theme.Bg0,
                Padding   = new Padding(20, 16, 20, 16),
            };

            var msgBox = new RichTextBox
            {
                Text        = message,
                Font        = Theme.Bd,
                ForeColor   = Theme.Tx0,
                BackColor   = Theme.Bg0,
                ReadOnly    = true,
                BorderStyle = BorderStyle.None,
                ScrollBars  = RichTextBoxScrollBars.None,
                Location    = new Point(20, 12),
                Size        = new Size(500, 160),
                WordWrap    = true,
            };
            body.Controls.Add(msgBox);

            // Divider
            body.Controls.Add(new Panel
            {
                Location  = new Point(20, 178),
                Size      = new Size(500, 1),
                BackColor = Theme.Line,
            });

            // OK Button
            var btnOk = new Button
            {
                Text      = "OK",
                Font      = Theme.H2,
                ForeColor = Theme.Bg0,
                BackColor = Theme.Gold,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(160, 38),
                Location  = new Point(20, 190),
                Cursor    = Cursors.Hand,
            };
            btnOk.FlatAppearance.BorderSize  = 0;
            btnOk.Click += (_, _) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            body.Controls.Add(btnOk);

            // Hint label 
            body.Controls.Add(new Label
            {
                Text      = Translations.T("eft_wait_hint"),
                Font      = Theme.Sm,
                ForeColor = Theme.Tx2,
                AutoSize  = true,
                Location  = new Point(192, 202),
            });

            Controls.Add(body);
        }
    }
}