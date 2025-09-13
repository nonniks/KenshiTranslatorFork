Kenshi Translator

A modern .NET 9 tool for translating .mod files for the game Kenshi.
It simplifies the manual process by extracting text into an editable dictionary (.dict), translating it with external providers, and applying the translations back into the .mod.

📥 Download

Grab the latest release from the Releases
 page.

Unzip the archive.

Run KenshiTranslator.exe.

Note:

The build requires the .NET 9 Desktop Runtime (x64)

✨ Features

Translate Kenshi .mod files (supports v16 & v17).

Clean, simple Windows UI.

Can copy a mod Folder from the Workshop Folder to the Game Directory with just a click (it translates only mods that are in the Game's Directory)

Always creates a backup (.backup) before applying translations.

Dictionary (.dict) format allows manual editing & resuming translations.

Open source and extensible.

⚙️ How It Works

Extract – The tool reads your .mod file and extracts all translatable strings into a dictionary file (.dict).

Translate – The extracted dictionary can be auto-translated with providers (Google Translate, etc.) or manually edited.

Apply – The dictionary is merged back into the .mod, replacing the original strings with their translated versions.

Backup – Your original .mod is preserved as mod.backup in case you want to restore it.

This workflow ensures you can iterate on translations safely without ever losing your original data.

🛠 Build from Source

Prerequisites: .NET 9 SDK 
.

# Clone the repo
git clone https://github.com/Kakrain/KenshiTranslator.git
cd KenshiTranslator

# Restore & build
dotnet restore
dotnet build

# Run the application
dotnet run --project KenshiTranslator