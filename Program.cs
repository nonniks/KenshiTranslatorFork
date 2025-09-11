using KenshiTranslator.Helper;
using System.Reflection;

namespace KenshiTranslator;

static class Program
{

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }    
}