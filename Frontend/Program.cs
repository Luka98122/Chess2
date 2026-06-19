using System;
using System.Windows.Forms;

namespace Frontend
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ChessForm());
        }
    }
}