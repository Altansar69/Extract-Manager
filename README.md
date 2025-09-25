# Extract Manager  

![Release](https://img.shields.io/github/v/release/Altansar69/Extract-Manager?style=flat-square) 
![Downloads](https://img.shields.io/github/downloads/Altansar69/Extract-Manager/total?style=flat-square) 
![.NET Runtime](https://img.shields.io/badge/.NET-9.0-blue?style=flat-square) 
![License](https://img.shields.io/github/license/Altansar69/Extract-Manager?style=flat-square)

A simple and lightweight WPF application to streamline the process of extracting archive files, especially those that are password-protected.  

![Main window](https://i.imgur.com/0yVwcL7.png)  
![Password management](https://i.imgur.com/iKGewmH.png)  
![Extraction log](https://i.imgur.com/1ajj9aT.png)  

---

## ✨ Features  

- **Password Management** – Add, remove, and save commonly used passwords.  
- **Silent Extraction** – Right-click archives in Windows Explorer and extract automatically using your saved passwords.  
- **Interactive Extraction** – Use the UI to select an archive and follow the detailed log output.  
- **Automatic 7-Zip Handling** – Downloads and configures `7za.exe` if missing.  
- **Windows Shell Integration** – Register/unregister a context menu item for common archive formats.  
- **Import/Export Passwords** – Securely export/import encrypted password lists.  
- **Live Logging** – Real-time status messages inside the application.  
- **Customizable UI** – Dark-red theme with persistent window size/position.  

---

## 📦 Installation  

1. Download the latest **ExtractManager.exe** from the [Releases](../../releases) page.  
2. Place it in a folder of your choice and run it.  
3. On first launch, the app may download the `7za.exe` binary from 7-Zip (required for extraction).  
4. To enable right-click extraction, go to **Settings → Register Context Menu** (administrator rights may be required).  

---

## 🚀 Usage  

### Managing Passwords  
- Open the **Passwords** tab.  
- Add a password → press **Enter** or click *Add Password*.  
- Remove a password → select it and click *Remove Selected*.  

### Silent Extraction (Context Menu)  
- Make sure the context menu is registered in **Settings**.  
- Right-click an archive file (`.zip`, `.rar`, `.7z`, `.tar`, `.gz`).  
- Choose **Extract with Extract Manager**.  
- Files are extracted to the same directory; logs are written to `debug.log` (if enabled).  

### Interactive Extraction  
- Click **Select Archive to Extract**.  
- Pick an archive.  
- Choose output path and monitor progress via the log window.  

---

## ❓ FAQ  

- **Why administrator privileges?**  
  Only required for registering/unregistering the Windows context menu.  

- **Where are passwords/settings stored?**  
  In `passwords.json` and `appsettings.json`, next to `ExtractManager.exe`.  

- **Which archive formats are supported in the context menu?**  
  `.zip`, `.rar`, `.7z`, `.tar`, `.gz`.  

- **Is Import/Export safe?**  
  Yes, passwords are encrypted before export.  

- **Why 7-Zip and SharpCompress?**  
  SharpCompress is used to bootstrap extraction if 7-Zip is not available. 7-Zip is faster, so it’s preferred once installed.  

---

## ⚙️ Requirements  

- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)  
  *(A self-contained build is also available that does not require .NET runtime.)*  

---

## 📜 Credits  

- **Author:** afraidium  
- **Third-Party Libraries:**  
  - [7-Zip](https://www.7-zip.org/)  
  - [SharpCompress](https://github.com/adamhathcock/sharpcompress)  

---

## 🔑 Example Passwords  

Two example passwords you can import:  
cs.rin.ru
online-fix.me

Encrypted import string:  
YhoHRRdKXQgDE0IQGw9bXA9fQwQUBFxVFwkHEm8=

---

## 📅 Update Log  

**09/15/2025**  
- Fixed silent extraction issues (context menu).  
- Combined UI and silent extraction into a unified Extract Service.  
- Added x86/x64 detection and automatic 7za binary download.  
- Added builds for x86 and x64.  
- Added popup indicator for silent extractions.  
- Changed packaging to avoid single-file issues.  

**09/14/2025**  
- Updated versioning scheme.  
- Improved password import/export (deduplication, case-insensitive, merging).  
- Fixed window state not saving/restoring in multi-monitor setups.  

---

## 📥 Download  

👉 [Latest Release on GitHub](../../releases)  
