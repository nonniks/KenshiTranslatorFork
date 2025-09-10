using KenshiTranslator.Helper;
using System.Reflection;

namespace KenshiTranslator;

static class Program
{

    [STAThread]
    static void Main()
    {
        /*var re = new ReverseEngineer();
        //string startPath = "C:/AlternativProgramFiles/Steam/steamapps/workshop/content/233860/";
        string startPath = "C:/AlternativProgramFiles/Steam/steamapps/common/Kenshi/mods/";
        if (!Directory.Exists(startPath))
        {
            Console.WriteLine($"Folder not found: {startPath}");
            return;
        }

        var folders = Directory.GetDirectories(startPath);

        foreach (var folder in folders)
        {
            var modFiles = Directory.GetFiles(folder, "*.mod");
            foreach (var file in modFiles)
            {
                try
                {
                    Console.WriteLine($"Processing: {file}");

                    // Load mod
                    re.LoadModFile(file);

                    // Save as .resaved
                    string resavedPath = Path.Combine(
                        Path.GetDirectoryName(file),
                        Path.GetFileNameWithoutExtension(file) + ".resaved"
                    );
                    re.SaveModFile(resavedPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {file}: {ex.Message}");
                }
            }
        }*/

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }    
}