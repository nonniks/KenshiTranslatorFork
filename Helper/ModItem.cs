using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiTranslator.Helper
{
    public class ModItem
    {
        public string Name { get; set; }
        public string Language { get; set; } = "detecting...";
        public bool InGameDir { get; set; }
        public bool Selected { get; set; }
        public long workshopId { get; set; }
        private static Dictionary<int, Image> iconCache = new();

        public static Image gameDirIcon = ResourceLoader.LoadImage("KenshiTranslator.icons.kenshiicon.png");
        public static Image workshopIcon = ResourceLoader.LoadImage("KenshiTranslator.icons.steamicon.png");
        public static Image selectedIcon = ResourceLoader.LoadImage("KenshiTranslator.icons.selectedicon.png");
        public ModItem(string name)
        {
            InGameDir = false;
            Selected = false;
            workshopId = -1;
            Name = name;
        }
        public Image CreateCompositeIcon()
        {
            int key = (Convert.ToInt32(InGameDir) * 100) +
                      (Convert.ToInt32(workshopId != -1) * 10) +
                      Convert.ToInt32(Selected);

            if (iconCache.TryGetValue(key, out var cached))
                return cached;

            // ✅ PROPER SOLUTION: Use using statements for temporary bitmaps
            using (Bitmap blank = new Bitmap(16, 16))
            using (Bitmap tempBmp = new Bitmap(48, 16))
            {
                using (Graphics g = Graphics.FromImage(tempBmp))
                {
                    g.DrawImage(InGameDir ? gameDirIcon : blank, 0, 0);
                    g.DrawImage(workshopId != -1 ? workshopIcon : blank, 16, 0);
                    g.DrawImage(Selected ? selectedIcon : blank, 32, 0);
                }

                // Create a new image to store in cache (clone the temporary bitmap)
                Image finalImage = (Image)tempBmp.Clone();
                iconCache[key] = finalImage;
                return finalImage;
            }
        }
        public static void DisposeIconCache()
        {
            foreach (var image in iconCache.Values)
            {
                image.Dispose();
            }
            iconCache.Clear();
        }
        public string getBackupFilePath()
        {
            return Path.Combine(MainForm.gamedirModsPath, Path.GetFileNameWithoutExtension(Name), Path.GetFileNameWithoutExtension(Name)+".backup");
        }
        public string getDictFilePath()
        {
           return Path.Combine(MainForm.gamedirModsPath, Path.GetFileNameWithoutExtension(Name), Path.GetFileNameWithoutExtension(Name) + ".dict");
        }
        public string getModFilePath()
        {
            if (InGameDir)
            {
                return Path.Combine(MainForm.gamedirModsPath, Path.GetFileNameWithoutExtension(Name), Name);
            }
            if (workshopId != -1)
            {
                return Path.Combine(MainForm.workshopModsPath, workshopId.ToString(), Name);
            }
            Debug.WriteLine($"Error getting mod file path for {Name}");
            return null;
        }
    }
}
