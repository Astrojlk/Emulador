using System;
using System.Threading;
using System.Windows.Forms;

namespace MainForm
{
    internal static class Program
    {
        /// <summary>
        /// Ponto de entrada principal para o aplicativo.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Console.WriteLine("Iniciando...");

            // Aguarda 2 segundos antes de abrir o Windows Form
            Thread.Sleep(2000);

            // Abre o Windows Form
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            // Exibe a mensagem de conclusão no console
            Console.WriteLine("Iniciado");
        }
    }
}
