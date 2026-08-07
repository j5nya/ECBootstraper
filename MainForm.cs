using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EchoBootstrapper
{
    internal class MainForm : Form
    {
        private const int WindowWidth = 520;
        private const int WindowHeight = 320;
        private const int LogoSize = 96;
        private const int SideMargin = 52;

        private readonly string _protocolArgument;
        private readonly bool _launchMode;

        private readonly Installer _installer = new Installer();
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        private PictureBox _logo;
        private Label _status;
        private ProgressStrip _bar;
        private Label _close;

        public bool Preview { get; set; }

        public MainForm(string protocolArgument)
        {
            _protocolArgument = protocolArgument;
            _launchMode = !string.IsNullOrEmpty(protocolArgument);
            BuildUi();
        }

        private void BuildUi()
        {
            Text = Config.ProductName;
            ClientSize = new Size(WindowWidth, WindowHeight);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            ShowInTaskbar = true;
            Font = new Font("Segoe UI", 9F);
            Icon = TryLoadIcon();

            Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(205, 205, 205)))
                    e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            };

            _logo = new PictureBox
            {
                Size = new Size(LogoSize, LogoSize),
                Location = new Point((WindowWidth - LogoSize) / 2, 56),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = TryLoadLogo(),
            };
            Controls.Add(_logo);

            _status = new Label
            {
                Text = "Starting " + Config.DisplayName + " ...",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(60, 60, 60),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,

                Size = new Size(WindowWidth - 40, 42),
                Location = new Point(20, 182),
            };
            Controls.Add(_status);

            _bar = new ProgressStrip
            {
                Size = new Size(WindowWidth - SideMargin * 2, 16),
                Location = new Point(SideMargin, 236),
            };
            Controls.Add(_bar);

            _close = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = false,
                Size = new Size(28, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(WindowWidth - 32, 6),
                Cursor = Cursors.Hand,
            };
            _close.MouseEnter += (s, e) => _close.ForeColor = Color.FromArgb(70, 70, 70);
            _close.MouseLeave += (s, e) => _close.ForeColor = Color.FromArgb(150, 150, 150);
            _close.Click += (s, e) => Close();
            Controls.Add(_close);

            MouseDown += StartDrag;
            _logo.MouseDown += StartDrag;
            _status.MouseDown += StartDrag;

            FormClosing += (s, e) => _cancel.Cancel();
            Shown += async (s, e) => await RunAsync().ConfigureAwait(true);
        }

        private async Task RunAsync()
        {
            try
            {
                if (Preview)
                {
                    await Task.Delay(Timeout.Infinite, _cancel.Token).ConfigureAwait(true);
                    return;
                }

                var progress = new Progress<Status>(step =>
                {
                    if (!string.IsNullOrEmpty(step.Text)) _status.Text = step.Text;
                });

                var outdated = await _installer.IsOutdatedAsync(_cancel.Token).ConfigureAwait(true);

                var manifest = await _installer.FetchManifestAsync(_cancel.Token).ConfigureAwait(true);

                var options = new InstallOptions
                {
                    DesktopShortcut = true,
                    RegisterProtocol = true,
                };

                await _installer.InstallAsync(manifest, options, progress, _cancel.Token).ConfigureAwait(true);

                if (_launchMode)
                    await _installer.LaunchFromProtocolAsync(_protocolArgument, progress, _cancel.Token)
                        .ConfigureAwait(true);

                if (outdated)
                {
                    // Message first, page second. Opening the browser straight away puts
                    // a window over this one before the line can be read, and then the
                    // player has no idea why the site turned up.
                    _status.Text = "A newer launcher is out - opening the download page...";
                    _bar.Freeze();
                    await Task.Delay(3000, _cancel.Token).ConfigureAwait(true);
                    Installer.OpenDownloadPage();
                    _status.Text = "A newer launcher is out - please download it again from the site.";
                    return;
                }

                await Task.Delay(_launchMode ? 1200 : 2000, _cancel.Token).ConfigureAwait(true);
                Close();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void Fail(Exception ex)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => Fail(ex))); return; }
            _status.ForeColor = Color.FromArgb(190, 40, 40);
            _status.Text = ex.Message;
            _bar.Freeze();
        }

        private static Image TryLoadLogo()
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png"))
                {
                    if (stream == null) return null;

                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        memory.Position = 0;
                        return Image.FromStream(memory);
                    }
                }
            }
            catch { return null; }
        }

        private static Icon TryLoadIcon()
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                {
                    if (stream == null) return null;

                    return new Icon(stream, SystemInformation.IconSize);
                }
            }
            catch { return null; }
        }

        private const int WmNcLButtonDown = 0xA1;
        private const int HtCaption = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private void StartDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
        }
    }
}
