using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        private string gamedirModsPath;
        private string workshopModsPath;
        private Dictionary<string, ModItem> mergedMods = new Dictionary<string, ModItem>();
        List<string> gameDirMods = new List<string>();
        List<string> selectedMods = new List<string>();
        List<string> workshopMods = new List<string>();

        private ReverseEngineer re = new ReverseEngineer();
        private Button openGameDirButton;
        private Button openSteamLinkButton;
        private Button copyToGameDirButton;
        private Button TranslateModButton;

        public MainForm()
        {
            Text = "Kenshi Translator";
            Width = 800;
            Height = 500;
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(layout);
            modsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,        
                FullRowSelect = true
            };
            modsListView.Columns.Add("Mod Name", -2, HorizontalAlignment.Left);
            layout.Controls.Add(modsListView, 0, 0);
            modsListView.SelectedIndexChanged += SelectedIndexChanged;


            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true
            };
            layout.Controls.Add(buttonPanel, 1, 0);

            openGameDirButton = new Button { Text = "Open Game Directory", AutoSize = true, Enabled = false };
            openGameDirButton.Click += OpenGameDirButton_Click;
            buttonPanel.Controls.Add(openGameDirButton);

            openSteamLinkButton = new Button { Text = "Open Steam Link", AutoSize = true, Enabled = false };
            openSteamLinkButton.Click += OpenSteamLinkButton_Click;
            buttonPanel.Controls.Add(openSteamLinkButton);

            copyToGameDirButton = new Button { Text = "Copy to GameDir", AutoSize = true, Enabled = false };
            copyToGameDirButton.Click += CopyToGameDirButton_Click;
            buttonPanel.Controls.Add(copyToGameDirButton);

            TranslateModButton = new Button { Text = "Translate Mod", AutoSize = true, Enabled = false };
            TranslateModButton.Click += TranslateModButton_Click;
            buttonPanel.Controls.Add(TranslateModButton);

            steamInstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null);
            if (string.IsNullOrEmpty(steamInstallPath))
            {
                steamInstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);
            }
            workshopModsPath = Path.Combine(steamInstallPath, "steamapps/workshop/content/233860");
            gamedirModsPath = Path.Combine(steamInstallPath, "steamapps/common/Kenshi/mods");

            LoadGameDirMods();
            LoadSelectedMods();
            LoadWorkshopMods();
            modIcons = new ImageList();
            modIcons.ImageSize = new Size(48, 16);
            modsListView.SmallImageList = modIcons;
            PopulateModsListView();
            modsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

        }
        private void SelectedIndexChanged(object sender, EventArgs e)
        {
            if (modsListView.SelectedItems.Count != 1)
            {
                openGameDirButton.Enabled = false;
                openSteamLinkButton.Enabled = false;
                copyToGameDirButton.Enabled = false;
                TranslateModButton.Enabled = false;
                return;
            }

            string modName = modsListView.SelectedItems[0].Text;
            if (mergedMods.TryGetValue(modName, out var mod))
            {
                openGameDirButton.Enabled = mod.InGameDir;
                copyToGameDirButton.Enabled = !mod.InGameDir && (mod.workshopId != -1);
                openSteamLinkButton.Enabled = (mod.workshopId != -1);
                TranslateModButton.Enabled = mod.InGameDir;
            }
        }
        private void OpenGameDirButton_Click(object sender, EventArgs e)
        {
            string modName = modsListView.SelectedItems[0].Text;
            string modpath = Path.Combine(gamedirModsPath, modName.Substring(0,modName.Length-4)).Replace("/","\\");
            if (Directory.Exists(modpath))
            {
                Process.Start("explorer.exe", modpath);
            }
            else
            {
                MessageBox.Show(modpath+ " not found!");
            }
        }
        private void TranslateModButton_Click(object sender, EventArgs e)
        {
            string modName = modsListView.SelectedItems[0].Text;
            string modPath = Path.Combine(gamedirModsPath, modName.Substring(0, modName.Length - 4), modName).Replace("/", "\\");
            string backupPath = Path.Combine(gamedirModsPath, modName.Substring(0, modName.Length - 4), modName.Substring(0, modName.Length - 4)+".backup").Replace("/", "\\");

            if (!File.Exists(modPath))
            {
                MessageBox.Show(modPath + " not found!");
                return;
            }
            re.LoadModFile(modPath);
            if (!File.Exists(backupPath))
            {
                File.Copy(modPath, backupPath);
                Console.WriteLine($"Backup created at {backupPath}");
            }
            re.ApplyToStrings(s => "meep");
            re.SaveModFile(modPath);
            Console.WriteLine($"Mod saved back to {modPath}");
        }
        private void OpenSteamLinkButton_Click(object sender, EventArgs e)
        {
            string modName = modsListView.SelectedItems[0].Text;
            var mod = mergedMods.ContainsKey(modName) ? mergedMods[modName] : null;
            if (mod != null && mod.workshopId != -1)
            {
                string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.workshopId}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("This mod is not from the Steam Workshop.");
            }
        }
        private void CopyToGameDirButton_Click(object sender, EventArgs e)
        {
            if (modsListView.SelectedItems.Count != 1)
                return;
            string modName = modsListView.SelectedItems[0].Text;
            if (!mergedMods.TryGetValue(modName, out var mod)) 
                return;
            if (mod.workshopId == -1) 
                return;

            string workshopFolder = Path.Combine(workshopModsPath, mod.workshopId.ToString());

            string gameDirFolder = Path.Combine(gamedirModsPath, modName.Substring(0, modName.Length - 4));

            if (Directory.Exists(gameDirFolder))
            {
                MessageBox.Show("Mod already exists in GameDir!");
                return;
            }

            // Copy directory recursively
            CopyDirectory(workshopFolder, gameDirFolder);

            // Mark as installed
            mod.InGameDir = true;

            // Update icon
            if (modIcons.Images.ContainsKey(mod.Name))
            {
                modIcons.Images.RemoveByKey(mod.Name);
            }
            modIcons.Images.Add(mod.Name,mod.CreateCompositeIcon());
            SelectedIndexChanged(null, null);
            var item = modsListView.SelectedItems[0];
            item.ImageKey = mod.Name + "_temp";
            item.ImageKey = mod.Name;
            MessageBox.Show($"{mod.Name} copied to GameDir!");
        }
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }
        private void PopulateModsListView()
        {
            modsListView.Items.Clear();
            foreach (var mod in selectedMods)
            {
                if (!mergedMods.ContainsKey(mod))
                    mergedMods[mod] = new ModItem(mod);
                mergedMods[mod].Selected = true;
            }

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
