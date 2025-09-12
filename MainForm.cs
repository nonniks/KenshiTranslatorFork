using KenshiTranslator.Helper;
using KenshiTranslator.Translator;
using Microsoft.Win32;
using NTextCat;
using System.Diagnostics;
using System.Text;

namespace KenshiTranslator
{
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
        private Button CreateDictionaryButton;
        private Dictionary<string, string> _supportedLanguages;
        private TranslatorInterface _activeTranslator;
        public class ComboItem
        {
            public string Code { get; }
            public string Name { get; }

            public ComboItem(string code, string name)
            {
                Code = code;
                Name = name;
            }

            public override string ToString() => Name; // optional, makes debugging nicer
        }
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
            modsListView.Columns.Add("Translation Progress", 120, HorizontalAlignment.Left);
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
            providerCombo.Items.AddRange(new string[] { "Aggregate", "Bing", "Google", "Google2", "Microsoft", "Yandex" });
            providerCombo.SelectedIndex = 0;
            _activeTranslator = GTranslate_Translator.Instance;
            providerCombo.SelectedIndexChanged += (s,e)=>providerCombo_SelectedIndexChanged(s,e);
            buttonPanel.Controls.Add(providerCombo);


            fromLangCombo = new ComboBox();
            toLangCombo = new ComboBox();
            fromLangCombo.Width = 120;
            toLangCombo.Width = 120;

            buttonPanel.Controls.Add(fromLangCombo);
            buttonPanel.Controls.Add(toLangCombo);

            // Set defaults
            fromLangCombo.SelectedValue = "en";  // now this works
            toLangCombo.SelectedValue = "en";    // now this works

            CreateDictionaryButton = new Button { Text = "Create Dictionary", AutoSize = true, Enabled = false };
            CreateDictionaryButton.Click += async (s, e) => await CreateDictionaryButton_Click();
            buttonPanel.Controls.Add(CreateDictionaryButton);

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
            if (providerCombo.SelectedItem == null) return;
            string provider = providerCombo.SelectedItem.ToString();
            _activeTranslator = GTranslate_Translator.Instance;
            ((GTranslate_Translator)_activeTranslator).setTranslator(providerCombo.SelectedItem.ToString());
            _supportedLanguages = await _activeTranslator.GetSupportedLanguagesAsync();

            // Populate ComboBoxes
            fromLangCombo.DataSource = _supportedLanguages.Select(lang => new ComboItem(lang.Key, lang.Value)).ToList();
            fromLangCombo.DisplayMember = "Name";
            fromLangCombo.ValueMember = "Code";

            toLangCombo.DataSource = _supportedLanguages.Select(lang => new ComboItem(lang.Key, lang.Value)).ToList();
            toLangCombo.DisplayMember = "Name";
            toLangCombo.ValueMember = "Code";

            if (fromLangCombo.Items.Count > 0)
                fromLangCombo.SelectedValue = "en"; 
            if (toLangCombo.Items.Count > 0)
                toLangCombo.SelectedValue = "en";
        }
        public record LanguageInfo(string code, string name);
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
                //_ = LoadLibreTranslateLanguagesAsync();
                providerCombo_SelectedIndexChanged(null,null);
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
                CreateDictionaryButton.Enabled = false;
                TranslateModButton.Enabled = false;
                return;
            }

            string modName = modsListView.SelectedItems[0].Text;
            if (mergedMods.TryGetValue(modName, out var mod))
            {
                openGameDirButton.Enabled = mod.InGameDir || (mod.workshopId != -1);
                copyToGameDirButton.Enabled = !mod.InGameDir && (mod.workshopId != -1);
                openSteamLinkButton.Enabled = (mod.workshopId != -1);
                CreateDictionaryButton.Enabled = mod.InGameDir;
                TranslateModButton.Enabled = File.Exists(mod.getDictFilePath());
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
        private async Task CreateDictionaryButton_Click()
        {
            if (modsListView.SelectedItems.Count == 0)
                return;

            var selectedItem = modsListView.SelectedItems[0];
            string modName = selectedItem.Text;

            if (!mergedMods.TryGetValue(modName, out var mod))
                return;

            string modPath = mod.getModFilePath();
            if (!File.Exists(modPath))
            {
                MessageBox.Show("Mod file not found!");
                return;
            }

            // Ensure dictionary exists
            string dictFile = mod.getDictFilePath();
            var td = new TranslationDictionary(re);
            lock (reLockRE)
            {
                re.LoadModFile(modPath);
            }
            if (!File.Exists(dictFile))
                td.ExportToDictFile(dictFile);

            progressBar.Maximum = 100;
            progressBar.Value = 0;

            var progress = new Progress<int>(percent =>
            {
                progressBar.Value = percent;
                progressLabel.Text = $"Translating {modName}... {percent}%";
            });

            string sourceLang = fromLangCombo.SelectedItem?.ToString()?.Split(' ')[0] ?? "auto";
            string targetLang = toLangCombo.SelectedItem?.ToString()?.Split(' ')[0] ?? "en";
            int failureCount = 0;
            int successCount = 0;
            const int failureThreshold = 10;
            // Start async translation with resume support
            try
            {
                await TranslationDictionary.ApplyTranslationsAsync(dictFile, async (original) =>
                {
                    try
                    {
                        if (failureCount >= failureThreshold)
                            return null;
                        var translated = await _activeTranslator.TranslateAsync(original, sourceLang, targetLang);
                        successCount++;
                        return translated;

                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        if (failureCount >= failureThreshold)
                            throw new InvalidOperationException($"Too many consecutive translation failures. The provider {_activeTranslator.Name} may not be working.");
                            return null;
                    }
                }, progress);
            }// limit concurrent requests to prevent API issues
            catch (Exception ex)
            {
                progressLabel.Text = "Translation aborted.";
                MessageBox.Show($"Dictionary translation failed: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (successCount == 0)
            {
                progressLabel.Text = "No translations.";
                MessageBox.Show($"No translations were produced. Try a different provider (current: {_activeTranslator.Name}).",
                    "Translation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            progressLabel.Text = $"Dictionary complete: {modName}";
            progressBar.Value = 100;
            MessageBox.Show($"{modName} translation finished!");

            TranslateModButton.Enabled = File.Exists(mod.getDictFilePath());
        }
        private async Task TranslateModButton_Click()
        {
            if (modsListView.SelectedItems.Count == 0)
                return;

            var selectedItem = modsListView.SelectedItems[0];
            string modName = selectedItem.Text;

            if (!mergedMods.TryGetValue(modName, out var mod))
                return;
            string modPath = mod.getModFilePath();
            string dictFile = mod.getDictFilePath();
            lock (reLockRE)
            {
                re.LoadModFile(modPath);
                var td = new TranslationDictionary(re);
                td.ImportFromDictFile(dictFile);
                
                if (TranslationDictionary.GetTranslationProgress(dictFile) != 100)
                {
                    MessageBox.Show($"Dictionary of {modName} is not complete!");
                    return;
                }
                if (!File.Exists(mod.getBackupFilePath()))
                    File.Copy(modPath, mod.getBackupFilePath());
                re.SaveModFile(modPath);
                MessageBox.Show($"Translation of {modName} is finished!");
            }
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

                double progress = File.Exists(mod.getDictFilePath()) ? TranslationDictionary.GetTranslationProgress(mod.getDictFilePath()) : File.Exists(mod.getBackupFilePath()) ? 100 : 0;

                string progressText = progress == 100 ? "Translated" :
                                      progress > 0 ? $"{progress:F0}%" :
                                      "Not translated";

                // Add to ListView
                var item = new ListViewItem(new[] { mod.Name, mod.Language, progressText })
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
                    gameDirMods.Add(Path.GetFileName(file));
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
            foreach (var folder in Directory.GetDirectories(workshopModsPath))
            {
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
