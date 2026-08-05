using System;
using System.Linq;
using System.Windows.Forms;

namespace EchoBootstrapper
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var protocolArgument = args.FirstOrDefault(a =>
                a.StartsWith(Config.ProtocolScheme + ":", StringComparison.OrdinalIgnoreCase));

            var preview = args.Any(a => a.Equals("--preview", StringComparison.OrdinalIgnoreCase));

            Application.Run(new MainForm(args, protocolArgument) { Preview = preview });
        }
    }
}
