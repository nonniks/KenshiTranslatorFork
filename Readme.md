\# Kenshi Translator



A modern .NET 8 tool for translating `.mod` files for the game Kenshi. It simplifies the manual process by extracting text into a editable dictionary and applying translations automatically.



\[!\[.NET 8](https://img.shields.io/badge/.NET-8.0-512bd4)](https://dotnet.microsoft.com/)

\[!\[License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)



\## Download



1\.  Grab the latest release from the \[Releases](https://github.com/YourUsername/KenshiTranslator/releases) page.

2\.  Unzip the file.

3\.  Run `KenshiTranslator.App.exe`.



> \*\*Note:\*\* Requires the \[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).



\## Features



\*   Translates Kenshi `.mod` files (supports v16 \& v17).

\*   Clean, simple WPF user interface.

\*   Non-destructive: makes a copy of your original mod. (as mod.backup)

\*   Open source and extensible.



\## Build from Source



Prerequisites: \[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).



```bash

\# Clone the repo

git clone https://github.com/Kakrain/KenshiTranslator.git

cd KenshiTranslator



\# Restore \& build

dotnet restore

dotnet build



\# Run the application

dotnet run --project KenshiTranslator.App

