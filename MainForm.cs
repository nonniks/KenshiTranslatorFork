using KenshiTranslator.Helper;
using KenshiTranslator.Translator;
using Microsoft.Win32;
using NTextCat;
using System.Diagnostics;
using System.Text;

namespace KenshiTranslator
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
        public string getModFilePath() {
            if (InGameDir)
            {
                return Path.Combine(MainForm.gamedirModsPath, Path.GetFileNameWithoutExtension(Name), Name);
            }
            if (workshopId != -1) {
                return Path.Combine(MainForm.workshopModsPath, workshopId.ToString(), Name);
            }
            Debug.WriteLine($"Error getting mod file path for {Name}");
            return null;
        }
    }
    public class MainForm : Form
    {
        private ListView modsListView;
        private ImageList modIcons;
        public static string steamInstallPath;
        public static string gamedirModsPath;
        public static string workshopModsPath;
        private Dictionary<string, ModItem> mergedMods = new Dictionary<string, ModItem>();
        List<string> gameDirMods = new List<string>();
        List<string> selectedMods = new List<string>();
        List<string> workshopMods = new List<string>();
        private Dictionary<string, ListViewItem> modItemsLookup = new();
        private Dictionary<string, string> languageCache = new();
        private RankedLanguageIdentifier identifier;

        private ProgressBar progressBar;
        private Label progressLabel;
        private ReverseEngineer re = new ReverseEngineer();
        private Button openGameDirButton;
        private Button openSteamLinkButton;
        private Button copyToGameDirButton;
        private readonly object reLockRE = new object();
        private ComboBox providerCombo;
        private ComboBox fromLangCombo;
        private ComboBox toLangCombo;
        private Button TranslateModButton;
        private ITranslator translator;
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


            progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 20,
                Minimum = 0,
                Maximum = 0,
                Value = 0
            };
            layout.Controls.Add(progressBar, 0, 0);
            layout.SetColumnSpan(progressBar, 2);

            progressLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Text = "Ready",
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(progressLabel, 1, 0);
            layout.SetColumnSpan(progressLabel, 2);

            modsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,        
                FullRowSelect = true
            };
            modsListView.Columns.Add("Mod Name", -2, HorizontalAlignment.Left);
            modsListView.Columns.Add("Language", 100);
            layout.Controls.Add(modsListView, 0,1);
            modsListView.SelectedIndexChanged += SelectedIndexChanged;


            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true
            };
            layout.Controls.Add(buttonPanel, 1, 1);

            openGameDirButton = new Button { Text = "Open Mod Directory", AutoSize = true, Enabled = false };
            openGameDirButton.Click += OpenGameDirButton_Click;
            buttonPanel.Controls.Add(openGameDirButton);

            openSteamLinkButton = new Button { Text = "Open Steam Link", AutoSize = true, Enabled = false };
            openSteamLinkButton.Click += OpenSteamLinkButton_Click;
            buttonPanel.Controls.Add(openSteamLinkButton);

            copyToGameDirButton = new Button { Text = "Copy to GameDir", AutoSize = true, Enabled = false };
            copyToGameDirButton.Click += CopyToGameDirButton_Click;
            buttonPanel.Controls.Add(copyToGameDirButton);

            providerCombo = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            //providerCombo.Items.AddRange(new string[] { "Google", "Libre" });
            providerCombo.Items.AddRange(new string[] { "Libre", "Fun","Google" });
            providerCombo.SelectedIndex = 0;
            translator = LibreTranslator.Instance;
            providerCombo.SelectedIndexChanged += (s,e)=>providerCombo_SelectedIndexChanged(s,e);
            buttonPanel.Controls.Add(providerCombo);


            fromLangCombo = new ComboBox();
            toLangCombo = new ComboBox();
            fromLangCombo.Width = 120;
            toLangCombo.Width = 120;
            //fromLangCombo.Top = providerCombo.Bottom + 10;
            //toLangCombo.Top = providerCombo.Bottom + 10;
            //fromLangCombo.Left = providerCombo.Left;
            //toLangCombo.Left = fromLangCombo.Right + 10;
            buttonPanel.Controls.Add(fromLangCombo);
            buttonPanel.Controls.Add(toLangCombo);


            TranslateModButton = new Button { Text = "Translate Mod", AutoSize = true, Enabled = false };
            TranslateModButton.Click += async (s, e) => await TranslateModButton_Click();
            buttonPanel.Controls.Add(TranslateModButton);

            steamInstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null);
            if (string.IsNullOrEmpty(steamInstallPath))
            {
                steamInstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);
            }
            workshopModsPath = Path.Combine(steamInstallPath, "steamapps/workshop/content/233860");
            gamedirModsPath = Path.Combine(steamInstallPath, "steamapps/common/Kenshi/mods");

            this.FormClosing += (s, e) => SaveLanguageCache();
            //this.FormClosing += (s, e) => ModItem.DisposeIconCache();
            _ = InitializeAsync();

        }
        private async void providerCombo_SelectedIndexChanged(object sender, EventArgs e)
        {

            string provider = providerCombo.SelectedItem.ToString();
            switch (providerCombo.SelectedItem.ToString())
            {
                case "Libre":
                    translator = LibreTranslator.Instance;
                    break;
                case "Fun":
                    translator = FunTranslator.Instance;
                    break;
                default:
                    translator = GoogleTranslator.Instance;
                    break;
            }

            // Populate language combos
            var languages = await translator.GetSupportedLanguagesAsync();

            fromLangCombo.DataSource = new BindingSource(languages.ToList(), null);
            fromLangCombo.DisplayMember = "Value";  // human name
            fromLangCombo.ValueMember = "Key";      // language code

            toLangCombo.DataSource = new BindingSource(languages.ToList(), null);
            toLangCombo.DisplayMember = "Value";
            toLangCombo.ValueMember = "Key";

            // Default selections
            fromLangCombo.SelectedValue = "en";   // English as default source
            toLangCombo.SelectedValue = "en";   // English as default target
        }
        private async Task LoadLibreTranslateLanguagesAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                var response = await client.GetStringAsync("https://libretranslate.com/languages");
                var list = System.Text.Json.JsonSerializer.Deserialize<List<LanguageInfo>>(response);

                this.Invoke(() =>
                {
                    fromLangCombo.Items.Clear();
                    toLangCombo.Items.Clear();
                    foreach (var lang in list)
                    {
                        fromLangCombo.Items.Add(new ComboItem(lang.code, lang.name));
                        toLangCombo.Items.Add(new ComboItem(lang.code, lang.name));
                    }
                    // Set defaults:
                    toLangCombo.SelectedIndex = fromLangCombo.Items
                        .Cast<ComboItem>()
                        .ToList().FindIndex(i => i.Code == "en");
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load LibreTranslate languages: " + ex.Message);
            }
        }
        public record LanguageInfo(string code, string name);
        public class ComboItem
        {
            public string Code { get; }
            public string Name { get; }
            public ComboItem(string code, string name) { Code = code; Name = name; }
            public override string ToString() => Name;
        }
        private async Task InitializeAsync()
        {
            try
            {
                // Show initial progress
                this.SafeInvoke(() => {
                    progressLabel.Text = "Loading mods...";
                    progressBar.Style = ProgressBarStyle.Marquee; // Use marquee style for indeterminate progress
                    progressLabel.Refresh();
                });

                // Load data asynchronously
                await Task.Run(() => LoadGameDirMods());
                await Task.Run(() => LoadSelectedMods());
                await Task.Run(() => LoadWorkshopMods());

                // Continue with UI setup on the main thread
                this.Invoke((MethodInvoker)delegate {
                    modIcons = new ImageList();
                    modIcons.ImageSize = new Size(48, 16);
                    modsListView.SmallImageList = modIcons;
                    InitLanguageDetector();
                    LoadLanguageCache();
                    PopulateModsListView();
                    modsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

                    progressBar.Style = ProgressBarStyle.Continuous; // Switch back to continuous
                    progressLabel.Text = "Ready";
                });

                // Start language detection
                _ = DetectAllLanguagesAsync();
                _ = LoadLibreTranslateLanguagesAsync();
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate {
                    MessageBox.Show($"Initialization failed: {ex.Message}");
                });
            }
        }
        private void LoadLanguageCache()
        {
            string path = "languages.txt";
            if (!File.Exists(path))
                return;

            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                    languageCache[parts[0]] = parts[1];
            }
        }
        private void SaveLanguageCache()
        {
            try
            {
                using var writer = new StreamWriter("languages.txt", false, Encoding.UTF8);
                foreach (var kvp in languageCache)
                {
                    writer.WriteLine($"{kvp.Key}={kvp.Value}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save language cache: " + ex.Message);
            }
        }
        private void InitLanguageDetector()
        {
            if (identifier == null)
                identifier = new RankedLanguageIdentifierFactory().Load("LanguageModels/Core14.profile.xml");
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
                openGameDirButton.Enabled = mod.InGameDir || (mod.workshopId != -1);
                copyToGameDirButton.Enabled = !mod.InGameDir && (mod.workshopId != -1);
                openSteamLinkButton.Enabled = (mod.workshopId != -1);
                TranslateModButton.Enabled = mod.InGameDir;
            }
        }
        private void OpenGameDirButton_Click(object sender, EventArgs e)
        {
            string modName = modsListView.SelectedItems[0].Text;
            string modpath = Path.GetDirectoryName(((ModItem)modsListView.SelectedItems[0].Tag).getModFilePath());
            if (Directory.Exists(modpath))
            {
                Process.Start("explorer.exe", modpath);
            }
            else
            {
                MessageBox.Show(modpath+ " not found!");
            }
        }
        private async Task TranslateModButton_Click()
        {
            if (modsListView.SelectedItems.Count == 0) return;

            // Choose provider
            //Translator.Provider = providerCombo.SelectedIndex == 0 ? TranslationProvider.Google : TranslationProvider.Libre;

            var selectedItem = modsListView.SelectedItems[0];
            string modName = selectedItem.Text;

            // Translate
            //string translated = await Translator.Translate(modName, "en");
            //translationBox.Text = translated;
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
        private Color colorLanguage(string lang)
        {
            return (lang == "eng") ? Color.Green : Color.Red; 
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
                var item = new ListViewItem(new[] { mod.Name, mod.Language })
                {
                    Tag = mod,
                    ImageKey = mod.Name
                };
                item.UseItemStyleForSubItems = false;
                if (languageCache.TryGetValue(mod.Name, out var cachedLang))
                {
                    item.SubItems[1].Text = cachedLang;
                    item.SubItems[1].ForeColor = colorLanguage(cachedLang);
                }
                else
                {
                    modItemsLookup[mod.Name] = item;
                    item.SubItems[1].Text = "detecting...";
                    item.SubItems[1].ForeColor = Color.Gray;
                }
                modsListView.Items.Add(item);
            }

        }
        private async Task DetectAllLanguagesAsync()
        {
            var modsToDetect = modsListView.Items
                .Cast<ListViewItem>()
                .Select(item => (ModItem)item.Tag)
                .Where(mod => !languageCache.ContainsKey(mod.Name))
                .ToList();

            // UI update must be on main thread
            this.Invoke((MethodInvoker)delegate {
                progressBar.Minimum = 0;
                progressBar.Maximum = modsToDetect.Count;
                progressBar.Value = 0;
                progressLabel.Text = "Starting detection...";
            });

            foreach (var mod in modsToDetect)
            {
                // Update progress label on UI thread
                this.Invoke((MethodInvoker)delegate {
                    progressLabel.Text = $"Detecting language for: {mod.Name}";
                });

                string detected = "Unknown";

                try
                {
                    detected = await Task.Run(() =>
                    {
                        lock (reLockRE)
                        {
                            re.LoadModFile(mod.getModFilePath());
                            var languages = identifier.Identify(re.getModSummary()).ToList();
                            var mostCertain = languages.FirstOrDefault();
                            return mostCertain != null ? mostCertain.Item1.Iso639_3 : "Unknown";
                        }
                    });
                }
                catch (Exception ex)
                {
                    detected = $"Error: {ex.Message}";
                    Debug.WriteLine($"Error detecting language for {mod.Name}: {ex.Message}");
                }

                // Update UI on main thread
                this.Invoke((MethodInvoker)delegate {
                    languageCache[mod.Name] = detected;

                    // Find and update the specific ListViewItem
                    foreach (ListViewItem item in modsListView.Items)
                    {
                        if (item.Text == mod.Name)
                        {
                            item.SubItems[1].Text = detected;
                            item.SubItems[1].ForeColor = colorLanguage(detected);
                            break;
                        }
                    }

                    progressBar.Value++;
                    progressLabel.Text = $"Processed: {progressBar.Value} of {progressBar.Maximum}";
                });

                // Small delay to keep UI responsive
                await Task.Delay(10);
            }

            // Final update on UI thread
            this.Invoke((MethodInvoker)delegate {
                progressLabel.Text = "Language detection complete!";
                progressBar.Value = progressBar.Maximum;
            });

            SaveLanguageCache();
        }
        private string DetectSingleModLanguage(ModItem mod)
        {
            lock (reLockRE)
            {
                string filePath = mod.getModFilePath();

                // ✅ ADD FILE EXISTENCE CHECK
                if (!File.Exists(filePath))
                {
                    return "FileNotFound";
                }

                try
                {
                    re.LoadModFile(filePath);
                    var summary = re.getModSummary();

                    // ✅ ADD NULL CHECK
                    if (string.IsNullOrEmpty(summary))
                    {
                        return "EmptyContent";
                    }

                    var languages = identifier.Identify(summary).ToList();
                    var mostCertain = languages.FirstOrDefault();
                    return mostCertain != null ? mostCertain.Item1.Iso639_3 : "Unknown";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing {filePath}: {ex.Message}");
                    return "Error";
                }
            }
        }

        private void SafeInvoke(Action action)
        {
            if (this.IsHandleCreated)
                this.Invoke(action);
            else
                action();
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
