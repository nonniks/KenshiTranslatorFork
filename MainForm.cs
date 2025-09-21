using KenshiTranslator.Helper;
using KenshiTranslator.Translator;
using NTextCat;
using System.Collections;
using System.Diagnostics;
using System.Text;
//TODO: keep an eye open for dictionary related bugs.
class ListViewColumnSorter : IComparer
{
    public int Column { get; set; } = 0;
    public SortOrder Order { get; set; } = SortOrder.Ascending;

    public int Compare(object? x, object? y)
    {
        if (x is not ListViewItem itemX || y is not ListViewItem itemY)
            return 0;

        string textX = itemX.SubItems[Column].Text;
        string textY = itemY.SubItems[Column].Text;

        int result = string.Compare(textX, textY, StringComparison.CurrentCultureIgnoreCase);

        return Order == SortOrder.Ascending ? result : -result;
    }
}
namespace KenshiTranslator
{
    public class MainForm : Form
    {
        private ListView modsListView;
        private ImageList modIcons = new ImageList();
        private Dictionary<string, ModItem> mergedMods = new Dictionary<string, ModItem>();
        List<string> gameDirMods = new List<string>();
        List<string> selectedMods = new List<string>();
        List<string> workshopMods = new List<string>();
        private Dictionary<string, ListViewItem> modItemsLookup = new();
        private Dictionary<string, string> languageCache = new();
        private RankedLanguageIdentifier? identifier;

        private ProgressBar progressBar;
        private Label progressLabel;
        private TextBox searchTextBox;
        private TranslationLogForm? logForm;
        private DateTime translationStartTime;
        private int translationTotalItems;
        private ModManager modM = new ModManager(new ReverseEngineer());
        private Button openGameDirButton;
        private Button openSteamLinkButton;
        private Button copyToGameDirButton;
        private readonly object reLockRE = new object();
        private ComboBox providerCombo;
        private TextBox customApiTextBox;
        private Button testApiButton;
        private Label apiStatusLabel;
        private ComboBox fromLangCombo;
        private ComboBox toLangCombo;
        private Button TranslateModButton;
        private string lastSelectedFromLang = "en";
        private string lastSelectedToLang = "en";
        private Button CreateDictionaryButton;
        private Button ShowLogButton;
        private Dictionary<string, string>? _supportedLanguages;
        private TranslatorInterface _activeTranslator=GTranslate_Translator.Instance;
        private CustomApiTranslator? _customApiTranslator;
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

            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 25
            };

            searchTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Search mods by name..."
            };
            searchTextBox.TextChanged += SearchTextBox_TextChanged;
            searchTextBox.KeyDown += SearchTextBox_KeyDown;
            searchPanel.Controls.Add(searchTextBox);

            var clearButton = new Button
            {
                Text = "✕",
                Width = 25,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 8, FontStyle.Bold)
            };
            clearButton.Click += (s, e) => {
                searchTextBox.Text = "";
                searchTextBox.Focus();
            };
            searchPanel.Controls.Add(clearButton);

            layout.Controls.Add(searchPanel, 0, 1);
            layout.SetColumnSpan(searchPanel, 2);

            modsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,        
                FullRowSelect = true
            };
            modsListView.Columns.Add("Mod Name", -2, HorizontalAlignment.Left);
            modsListView.Columns.Add("Language", 100);
            modsListView.Columns.Add("Translation Progress", 120, HorizontalAlignment.Left);
            layout.Controls.Add(modsListView, 0, 2);
            modsListView.SelectedIndexChanged += SelectedIndexChanged;
            modsListView.ColumnClick += ModsListView_ColumnClick!;
            modsListView.ListViewItemSorter = new ListViewColumnSorter();

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true
            };
            layout.Controls.Add(buttonPanel, 1, 2);

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
            providerCombo.Items.AddRange(new string[] { "Aggregate", "Bing", "Google", "Google2", "Microsoft", "Yandex", "Google Cloud V3", "Custom API" });
            providerCombo.SelectedIndex = 0;
            _activeTranslator = GTranslate_Translator.Instance;
            providerCombo.SelectedIndexChanged += (s,e)=>providerCombo_SelectedIndexChanged(s,e);
            buttonPanel.Controls.Add(providerCombo);

            customApiTextBox = new TextBox {
                Dock = DockStyle.Top,
                PlaceholderText = "Enter DeepL key (ends with :fx) or Google key (starts with AIza)",
                Visible = false
            };
            customApiTextBox.TextChanged += async (s, e) => {
                apiStatusLabel.Text = "";
                if (providerCombo.SelectedItem?.ToString() == "Custom API" && !string.IsNullOrWhiteSpace(customApiTextBox.Text))
                {
                    try
                    {
                        _customApiTranslator?.Dispose();
                        _customApiTranslator = new CustomApiTranslator(customApiTextBox.Text);
                        _activeTranslator = _customApiTranslator;
                        testApiButton.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error setting custom API: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        testApiButton.Enabled = false;
                    }
                }
                else
                {
                    testApiButton.Enabled = false;
                }
            };
            buttonPanel.Controls.Add(customApiTextBox);

            testApiButton = new Button {
                Text = "Test API",
                AutoSize = true,
                Enabled = false,
                Visible = false
            };
            testApiButton.Click += async (s, e) => await TestApiButton_Click();
            buttonPanel.Controls.Add(testApiButton);

            apiStatusLabel = new Label {
                Dock = DockStyle.Top,
                Height = 20,
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            buttonPanel.Controls.Add(apiStatusLabel);


            fromLangCombo = new ComboBox();
            toLangCombo = new ComboBox();
            fromLangCombo.Width = 120;
            toLangCombo.Width = 120;

            buttonPanel.Controls.Add(fromLangCombo);
            buttonPanel.Controls.Add(toLangCombo);

            fromLangCombo.SelectedValue = lastSelectedFromLang;
            toLangCombo.SelectedValue = lastSelectedToLang;   

            CreateDictionaryButton = new Button { Text = "Create Dictionary", AutoSize = true, Enabled = false };
            CreateDictionaryButton.Click += async (s, e) => await CreateDictionaryButton_Click();
            buttonPanel.Controls.Add(CreateDictionaryButton);

            TranslateModButton = new Button { Text = "Translate Mod", AutoSize = true, Enabled = false };
            TranslateModButton.Click += async (s, e) => await TranslateModButton_Click();
            buttonPanel.Controls.Add(TranslateModButton);

            ShowLogButton = new Button { Text = "Show Log", AutoSize = true, Enabled = false };
            ShowLogButton.Click += ShowLogButton_Click;
            buttonPanel.Controls.Add(ShowLogButton);



            this.FormClosing += (s, e) => SaveLanguageCache();
            this.FormClosing += (s, e) => ModItem.DisposeIconCache();
            this.FormClosing += (s, e) => _customApiTranslator?.Dispose();
            _ = InitializeAsync();
        }
        private void ModsListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var sorter = (ListViewColumnSorter)modsListView.ListViewItemSorter!;

            if (sorter.Column == e.Column)
            {
                // Toggle sort order
                sorter.Order = sorter.Order == SortOrder.Ascending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                sorter.Column = e.Column;
                sorter.Order = SortOrder.Ascending;
            }

            modsListView.Sort();
        }
        private async void providerCombo_SelectedIndexChanged(object? sender, EventArgs? e)
        {
            if (providerCombo.SelectedItem == null) return;
            string provider = providerCombo.SelectedItem.ToString()!;

            // Save current language selection
            if (fromLangCombo.SelectedValue != null)
                lastSelectedFromLang = fromLangCombo.SelectedValue.ToString()!;
            if (toLangCombo.SelectedValue != null)
                lastSelectedToLang = toLangCombo.SelectedValue.ToString()!;

            if (provider == "Google Cloud V3")
            {
                customApiTextBox.Visible = true;
                testApiButton.Visible = true;
                apiStatusLabel.Visible = true;
                apiStatusLabel.Text = "";

                // Show file dialog to select Service Account JSON
                using var openFileDialog = new OpenFileDialog
                {
                    Title = "Select Google Cloud Service Account JSON file",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    customApiTextBox.Text = openFileDialog.FileName;
                    customApiTextBox.ReadOnly = true; // Make it read-only since it's selected via dialog

                    try
                    {
                        _customApiTranslator?.Dispose();
                        _customApiTranslator = new CustomApiTranslator(openFileDialog.FileName);
                        _activeTranslator = _customApiTranslator;
                        testApiButton.Enabled = true;
                        apiStatusLabel.Text = "✅ Service Account JSON loaded";
                        apiStatusLabel.ForeColor = Color.Green;
                    }
                    catch (Exception ex)
                    {
                        apiStatusLabel.Text = $"❌ Error: {ex.Message}";
                        apiStatusLabel.ForeColor = Color.Red;
                        testApiButton.Enabled = false;
                    }
                }
                else
                {
                    // User cancelled file selection, revert to previous provider
                    providerCombo.SelectedIndex = 0; // Aggregate
                    return;
                }
            }
            else if (provider == "Custom API")
            {
                customApiTextBox.Visible = true;
                testApiButton.Visible = true;
                apiStatusLabel.Visible = true;
                apiStatusLabel.Text = "";
                customApiTextBox.ReadOnly = false; // Allow manual input for custom API

                // Create custom API translator if API endpoint is provided
                if (!string.IsNullOrWhiteSpace(customApiTextBox.Text))
                {
                    _customApiTranslator?.Dispose();
                    _customApiTranslator = new CustomApiTranslator(customApiTextBox.Text);
                    _activeTranslator = _customApiTranslator;
                    testApiButton.Enabled = true;
                }
                else
                {
                    testApiButton.Enabled = false;
                }
                _supportedLanguages = await _activeTranslator.GetSupportedLanguagesAsync();
            }
            else
            {
                customApiTextBox.Visible = false;
                testApiButton.Visible = false;
                apiStatusLabel.Visible = false;
                _customApiTranslator?.Dispose();
                _customApiTranslator = null;
                _activeTranslator = GTranslate_Translator.Instance;
                ((GTranslate_Translator)_activeTranslator).setTranslator(provider);
                _supportedLanguages = await _activeTranslator.GetSupportedLanguagesAsync();
            }

            fromLangCombo.DataSource = _supportedLanguages.Select(lang => new ComboItem(lang.Key, lang.Value)).ToList();
            fromLangCombo.DisplayMember = "Name";
            fromLangCombo.ValueMember = "Code";

            toLangCombo.DataSource = _supportedLanguages.Select(lang => new ComboItem(lang.Key, lang.Value)).ToList();
            toLangCombo.DisplayMember = "Name";
            toLangCombo.ValueMember = "Code";

            // Restore previously selected languages if available
            if (fromLangCombo.Items.Count > 0)
            {
                if (_supportedLanguages.ContainsKey(lastSelectedFromLang))
                    fromLangCombo.SelectedValue = lastSelectedFromLang;
                else
                    fromLangCombo.SelectedValue = "en";
            }
            if (toLangCombo.Items.Count > 0)
            {
                if (_supportedLanguages.ContainsKey(lastSelectedToLang))
                    toLangCombo.SelectedValue = lastSelectedToLang;
                else
                    toLangCombo.SelectedValue = "en";
            }
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

                gameDirMods = await Task.Run(() => modM.LoadGameDirMods());
                selectedMods = await Task.Run(() => modM.LoadSelectedMods());
                workshopMods = await Task.Run(() => modM.LoadWorkshopMods());

                // Continue with UI setup on the main thread
                this.Invoke((MethodInvoker)delegate {
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
        private void SelectedIndexChanged(object? sender, EventArgs? e)
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
                openGameDirButton.Enabled = mod.InGameDir || (mod.WorkshopId != -1);
                copyToGameDirButton.Enabled = !mod.InGameDir && (mod.WorkshopId != -1);
                openSteamLinkButton.Enabled = (mod.WorkshopId != -1);
                CreateDictionaryButton.Enabled = mod.InGameDir;
                TranslateModButton.Enabled = File.Exists(mod.getDictFilePath());
            }
        }
        private void OpenGameDirButton_Click(object? sender, EventArgs e)
        {
            string modName = modsListView.SelectedItems[0].Text;
            string? modpath = Path.GetDirectoryName(((ModItem)modsListView.SelectedItems[0].Tag!)?.getModFilePath()!);
            if (modpath!= null && Directory.Exists(modpath))
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

            string modPath = mod.getModFilePath()!;
            if (!File.Exists(modPath))
            {
                MessageBox.Show("Mod file not found!");
                return;
            }

            // Ensure dictionary exists
            string dictFile = mod.getDictFilePath();
            modM.LoadModFile(modPath);
            var td = new TranslationDictionary(modM.GetReverseEngineer());
            if (!File.Exists(dictFile))
                td.ExportToDictFile(dictFile);

            progressBar.Maximum = 100;
            progressBar.Value = 0;

            // Initialize translation tracking
            translationStartTime = DateTime.Now;
            translationTotalItems = File.ReadAllText(dictFile).Split(new[] { "|_END_|" }, StringSplitOptions.None).Length;

            // Initialize log window if not exists
            if (logForm == null || logForm.IsDisposed)
            {
                logForm = new TranslationLogForm();
            }
            logForm.Reset();
            ShowLogButton.Enabled = true;

            DateTime lastUIUpdate = DateTime.MinValue;
            var progress = new Progress<int>(percent =>
            {
                // Throttle UI updates to prevent freezing (max once per 500ms)
                var now = DateTime.Now;
                if ((now - lastUIUpdate).TotalMilliseconds < 500 && percent < 100)
                    return;

                lastUIUpdate = now;
                progressBar.Value = percent;

                // Calculate time remaining
                var elapsed = now - translationStartTime;
                var estimatedTotal = elapsed.TotalSeconds > 0 && percent > 0 ?
                    TimeSpan.FromSeconds(elapsed.TotalSeconds * 100 / percent) :
                    TimeSpan.Zero;
                var remaining = estimatedTotal > elapsed ? estimatedTotal - elapsed : TimeSpan.Zero;

                var timeText = remaining.TotalSeconds > 0 ?
                    $" | ETA: {remaining:mm\\:ss}" : "";

                progressLabel.Text = $"Translating {modName}... {percent}%{timeText}";
            });

            string sourceLang = fromLangCombo.SelectedValue?.ToString() ?? "auto";
            string targetLang = toLangCombo.SelectedValue?.ToString() ?? "en";
            int failureCount = 0;
            int successCount = 0;
            const int failureThreshold = 10;
            // Start async translation with resume support
            try
            {
                // Check if we can use batch translation (Google V3)
                Func<List<string>, string, string, Task<List<string>>>? batchTranslateFunc = null;
                if (_activeTranslator is CustomApiTranslator customApi &&
                    customApi.CurrentApiType == ApiType.GoogleCloudV3)
                {
                    batchTranslateFunc = customApi.TranslateBatchV3Async;
                    Debug.WriteLine("[MainForm] Using Google V3 batch translation - much faster!");
                }

                successCount = await TranslationDictionary.ApplyTranslationsAsync(dictFile, async (original) =>
                {
                    try
                    {
                        if (failureCount >= failureThreshold)
                            return "";
                        var translated = await _activeTranslator.TranslateAsync(original, sourceLang, targetLang).ConfigureAwait(false);
                        return translated;

                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        if (failureCount >= failureThreshold)
                            throw new InvalidOperationException($"Too many consecutive translation failures. The provider {_activeTranslator.Name} may not be working.{ex.Message}");
                            return "";
                    }
                }, progress, batchTranslateFunc != null ? 100 : 200,
                (original, translated, success) => logForm?.LogTranslation(original, translated, success),
                (original, error) => logForm?.LogError(original, error),
                batchTranslateFunc).ConfigureAwait(false);
            }
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
            MessageBox.Show($"{modName}: Dictionary generated!");
            updateTranslationProgress(modName);
            UpdateDetectedLanguage(modName, await DetectModLanguagesAsync(mod));
            TranslateModButton.Enabled = File.Exists(mod.getDictFilePath());
        }

        private void ShowLogButton_Click(object? sender, EventArgs e)
        {
            if (logForm == null || logForm.IsDisposed)
            {
                logForm = new TranslationLogForm();
            }

            if (logForm.Visible)
            {
                logForm.BringToFront();
            }
            else
            {
                logForm.Show(this);
            }
        }

        private async Task TestApiButton_Click()
        {
            if (_customApiTranslator == null)
            {
                apiStatusLabel.Text = "❌ No API configured";
                apiStatusLabel.ForeColor = Color.Red;
                return;
            }

            testApiButton.Enabled = false;
            apiStatusLabel.Text = "🔄 Testing API...";
            apiStatusLabel.ForeColor = Color.Orange;

            try
            {
                // Test with a simple translation
                string testText = "Hello, world!";
                string testResult = await _customApiTranslator.TranslateAsync(testText, "EN", "RU");

                if (!string.IsNullOrEmpty(testResult) && testResult != testText)
                {
                    apiStatusLabel.Text = $"✅ API OK (Test: {testResult})";
                    apiStatusLabel.ForeColor = Color.Green;
                }
                else
                {
                    apiStatusLabel.Text = "⚠️ API returned empty/unchanged result";
                    apiStatusLabel.ForeColor = Color.Orange;
                }
            }
            catch (Exception ex)
            {
                apiStatusLabel.Text = $"❌ API Error: {ex.Message}";
                apiStatusLabel.ForeColor = Color.Red;
            }
            finally
            {
                testApiButton.Enabled = true;
            }
        }

        private async Task TranslateModButton_Click()
        {
            if (modsListView.SelectedItems.Count == 0)
                return;
            var selectedItem = modsListView.SelectedItems[0];
            string modName = selectedItem.Text;

            if (!mergedMods.TryGetValue(modName, out var mod))
                return;
            string modPath = mod.getModFilePath()!;
            string dictFile = mod.getDictFilePath();
            lock (reLockRE)
            {
                modM.LoadModFile(modPath);
                var td = new TranslationDictionary(modM.GetReverseEngineer());
                td.ImportFromDictFile(dictFile);
                
                int progress = TranslationDictionary.GetTranslationProgress(dictFile);
                if (progress < 95)
                {
                    var result = MessageBox.Show(
                        $"Dictionary of {modName} is only {progress}% complete.\n\nDo you want to apply the partial translation anyway?",
                        "Partial Translation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.No)
                        return;
                }
                if (!File.Exists(mod.getBackupFilePath()))
                    File.Copy(modPath, mod.getBackupFilePath());
                modM.GetReverseEngineer().SaveModFile(modPath);
                MessageBox.Show($"Translation of {modName} is finished!");
            }
            UpdateDetectedLanguage(modName, await DetectModLanguagesAsync(mod));
            updateTranslationProgress(modName);

            return;
        }
        private void OpenSteamLinkButton_Click(object? sender, EventArgs e)
        {
            string modName = modsListView.SelectedItems[0].Text;
            var mod = mergedMods.ContainsKey(modName) ? mergedMods[modName] : null;
            if (mod != null && mod.WorkshopId != -1)
            {
                string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.WorkshopId}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("This mod is not from the Steam Workshop.");
            }
        }
        private void CopyToGameDirButton_Click(object? sender, EventArgs e)
        {
            if (modsListView.SelectedItems.Count != 1)
                return;
            string modName = modsListView.SelectedItems[0].Text;
            if (!mergedMods.TryGetValue(modName, out var mod)) 
                return;
            if (mod.WorkshopId == -1) 
                return;

            // modName.Substring(0, modName.Length - 4)
            string workshopFolder = Path.Combine(ModManager.workshopModsPath!, mod.WorkshopId.ToString());
            
            string gameDirFolder = Path.Combine(ModManager.gamedirModsPath!, Path.GetFileNameWithoutExtension(modName));

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
            return (lang == "eng|___") ? Color.Green : Color.Red; 
        }
        private string getTranslationProgress(ModItem mod)
        {
            int progress = File.Exists(mod.getDictFilePath()) ? TranslationDictionary.GetTranslationProgress(mod.getDictFilePath()) : File.Exists(mod.getBackupFilePath()) ? 100 : 0;
            return (progress== 100) ? "Translated" : progress > 0 ? $"{progress:F0}%" :"Not translated";
        }
        private void updateTranslationProgress(string modName)
        {
            var item = modsListView.Items.Cast<ListViewItem>().FirstOrDefault(i => ((ModItem)i.Tag!).Name == modName);
            if (item != null)
            {
                var mod = (ModItem)item.Tag!;
                string progressText = getTranslationProgress(mod);
                item.SubItems[2].Text = progressText;
                modsListView.Refresh();
            }
        }
        private void UpdateDetectedLanguage(string modName, string detectedLanguage)
        {
            languageCache[modName] = detectedLanguage;
            var item = modsListView.Items.Cast<ListViewItem>().FirstOrDefault(i => ((ModItem)i.Tag!).Name == modName);
            if (item != null)
            {
                item.SubItems[1].Text = detectedLanguage;
                item.SubItems[1].ForeColor = colorLanguage(detectedLanguage);
                modsListView.Invalidate(item.Bounds);
            }
        }
        private void PopulateModsListView(string searchFilter = "")
        {
            modsListView.Items.Clear();
            foreach (var mod in modM.LoadSelectedMods())
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
                string? folderPart = Path.GetDirectoryName(folder_mod!);
                if (folderPart == null) continue;
                string filePart = Path.GetFileName(folder_mod);
                if (!mergedMods.ContainsKey(filePart))
                    mergedMods[filePart] = new ModItem(filePart);
                mergedMods[filePart].WorkshopId = Convert.ToInt64(folderPart);
            }

            // Filter mods based on search text
            var filteredMods = string.IsNullOrWhiteSpace(searchFilter)
                ? mergedMods.Values
                : mergedMods.Values.Where(mod => mod.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));

            foreach (var mod in filteredMods)
            {
                // Create composite icon for this mod
                Image icon = mod.CreateCompositeIcon();
                if (!modIcons.Images.ContainsKey(mod.Name))
                    modIcons.Images.Add(mod.Name, icon);
                string progressText = getTranslationProgress(mod);
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

        private void SearchTextBox_TextChanged(object? sender, EventArgs e)
        {
            string searchText = searchTextBox.Text;
            PopulateModsListView(searchText);

            // Update status
            int totalMods = mergedMods.Count;
            int visibleMods = modsListView.Items.Count;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                progressLabel.Text = $"Found {visibleMods}/{totalMods} mods matching '{searchText}'";
            }
            else
            {
                progressLabel.Text = "Ready";
            }
        }

        private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                searchTextBox.Text = "";
                e.Handled = true;
            }
        }

        private string detectLanguageFor(string s)
        {
            var candidates = identifier!.Identify(s).OrderBy(c => c.Item2).ToList();
            var best = candidates[0];
            if (best.Item2 > 3950)
                return "___";
            return best.Item1.Iso639_3;

        }
        private async Task<string> DetectModLanguagesAsync(ModItem mod)
        {
            try
            {
                return await Task.Run(() =>
                {
                    modM.LoadModFile(mod.getModFilePath()!);
                    var lang_tuple = modM.GetReverseEngineer().getModSummary();
                    var alpha_mostCertain = detectLanguageFor(lang_tuple.Item1);
                    var sign_mostCertain = detectLanguageFor(lang_tuple.Item2);
                    return $"{alpha_mostCertain}|{sign_mostCertain}";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting language for {mod.Name}: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }
        private async Task DetectAllLanguagesAsync()
        {
            var modsToDetect = modsListView.Items
                .Cast<ListViewItem>()
                .Select(item => (ModItem)item.Tag!)
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
                string detected = await DetectModLanguagesAsync(mod);

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
        private void SafeInvoke(Action action)
        {
            if (this.IsHandleCreated)
                this.Invoke(action);
            else
                action();
        }
    }
}
