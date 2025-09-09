using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KenshiTranslator
{
    public class MainForm : Form
    {
        private ListBox modsList;

        public MainForm()
        {
            Text = "Kenshi Translator";
            Width = 600;
            Height = 400;

            modsList = new ListBox { Dock = DockStyle.Fill };
            Controls.Add(modsList);

            LoadMods();
        }

        private void LoadMods()
        {
            string steamInstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam","InstallPath",null);
            if (string.IsNullOrEmpty(steamInstallPath))
            {
                steamInstallPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam","InstallPath",null);
            }


            //var MainPath = "C:\\AlternativProgramFiles\\Steam\\steamapps\\common\\Kenshi\\data";
            //Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            string cfgPath = Path.Combine(steamInstallPath, "steamapps/common/Kenshi/data", "mods.cfg");

            if (!File.Exists(cfgPath))
            {
                MessageBox.Show("mods.cfg not found!");
                return;
            }

            foreach (var line in File.ReadAllLines(cfgPath))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    modsList.Items.Add(line.Trim());
            }
        }
    }
}
