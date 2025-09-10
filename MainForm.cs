using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace KenshiTranslator
{
    public class ModItem
    {
        public string Name { get; set; }
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
            int key = (Convert.ToInt32(InGameDir) * 100) + (Convert.ToInt32(workshopId != -1) * 10) + Convert.ToInt32(Selected);
            if (iconCache.TryGetValue(key, out var cached))
                return cached;
            Bitmap blank = new Bitmap(16, 16);
            Bitmap bmp = new Bitmap(48, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                int x = 0;
                g.DrawImage(InGameDir ? gameDirIcon : blank, 0, 0);
                g.DrawImage(workshopId != -1 ? workshopIcon : blank, 16, 0);
                g.DrawImage(Selected ? selectedIcon : blank, 32, 0);         
            }
            iconCache[key] = bmp;
            return bmp;
        }   
    }
    public class MainForm : Form
    {
        private ListView modsListView;
        private ImageList modIcons;
        private string steamInstallPath;
        List<string> gameDirMods = new List<string>();
        List<string> selectedMods = new List<string>();
        List<string> workshopMods = new List<string>();

        public MainForm()
        {
            Text = "Kenshi Translator";
            Width = 600;
            Height = 400;
            modsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,        
                FullRowSelect = true
            };
            modsListView.Columns.Add("Mod Name", -2, HorizontalAlignment.Left);
            Controls.Add(modsListView);
            
           

            steamInstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null);
            if (string.IsNullOrEmpty(steamInstallPath))
            {
                steamInstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);
            }
            LoadGameDirMods();
            LoadSelectedMods();
            LoadWorkshopMods();
            modIcons = new ImageList();
            modIcons.ImageSize = new Size(48, 16);
            modsListView.SmallImageList = modIcons;
            PopulateModsListView();
            modsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

        }
        private void PopulateModsListView()
        {
            modsListView.Items.Clear();
            var mergedMods = new Dictionary<string, ModItem>();

            foreach (var mod in gameDirMods)
            {
                if (!mergedMods.ContainsKey(mod))
                    mergedMods[mod] = new ModItem(mod);
                mergedMods[mod].InGameDir = true;
            }

            foreach (var folder_mod in workshopMods)
            {
                string folderPart = Path.GetDirectoryName(folder_mod);
                string filePart = Path.GetFileName(folder_mod);
                if (!mergedMods.ContainsKey(filePart))
                    mergedMods[filePart] = new ModItem(filePart);
                mergedMods[filePart].workshopId = Convert.ToInt64(folderPart);
            }

            foreach (var mod in selectedMods)
            {
                if (!mergedMods.ContainsKey(mod))
                    mergedMods[mod] = new ModItem(mod);
                mergedMods[mod].Selected = true;
            }
            foreach (var mod in mergedMods.Values)
            {
                // Create composite icon for this mod
                Image icon = mod.CreateCompositeIcon();
                if (!modIcons.Images.ContainsKey(mod.Name))
                    modIcons.Images.Add(mod.Name, icon);

                // Add to ListView
                var item = new ListViewItem(mod.Name)
                {
                    ImageKey = mod.Name
                };
                modsListView.Items.Add(item);
            }

        }
        private void LoadGameDirMods()
        {
            string gamedirModsPath = Path.Combine(steamInstallPath, "steamapps/common/Kenshi/mods");
            if (!Directory.Exists(gamedirModsPath))
            {
                MessageBox.Show("gamedir folder not found!");
                return;
            }
            // Get all subdirectories in the mods folder
            foreach (var folder in Directory.GetDirectories(gamedirModsPath))
            {
                // Get all .mod files in the current folder
                var files = Directory.GetFiles(folder, "*.mod");
                foreach (var file in files)
                {
                    gameDirMods.Add(Path.GetFileName(file));        // Save to the variable
                }
            }
        }
        private void LoadWorkshopMods()
        {
            string workshopModsPath = Path.Combine(steamInstallPath, "steamapps/workshop/content/233860");
            if (!Directory.Exists(workshopModsPath))
            {
                MessageBox.Show("workshop folder not found!");
                return;
            }
            // Get all subdirectories in the mods folder
            foreach (var folder in Directory.GetDirectories(workshopModsPath))
            {
                // Get all .mod files in the current folder
                var files = Directory.GetFiles(folder, "*.mod");
                foreach (var file in files)
                {
                    string parentFolder = new DirectoryInfo(Path.GetDirectoryName(file)).Name;
                    string fileName = Path.GetFileName(file);
                    string relativeName = Path.Combine(parentFolder, fileName);
                    workshopMods.Add(relativeName);
                }
            }
        }

        private void LoadSelectedMods()
        {
            
            string cfgPath = Path.Combine(steamInstallPath, "steamapps/common/Kenshi/data", "mods.cfg");

            if (!File.Exists(cfgPath))
            {
                MessageBox.Show("mods.cfg not found!");
                return;
            }

            foreach (var line in File.ReadAllLines(cfgPath))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    selectedMods.Add(line.Trim());
            }
        }
    }
}
