# 🎮 Fika-Server-Setup-Wizard

> All-in-One setup tool for running your own FIKA multiplayer server on SPT (Single Player Tushonka).

> **Updated for SPT 4.1** — the project formerly known as Single Player Tarkov now lives at
> [github.com/SP-Tushonka](https://github.com/SP-Tushonka). The old `ligma.waffle-lord.net`
> installer host is gone and the server moved into a `SPT_Runtime` subfolder; this tool
> handles both.

![License](https://img.shields.io/github/license/c0d1ngf1eber/Fika-Server-Setup-Wizard)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)

---

## 📖 About

Instead of reading multiple guides, manually downloading files and configuring everything yourself - this tool automates the entire process.

It detects existing installations, downloads the required components and configures them automatically.

**Designed for users with no technical background.**

---

## ✨ Features

- 🔍 **Automatic detection** of all existing installations (all drives, Steam libraries, common paths)
- 🚀 **One-click full setup** via "Install All"
- 🔧 **Individual installation** of each component separately
- 🟢 **Real-time status indicator** for every component
- 📋 **Live log** with timestamps for every action
- 🌐 **Bilingual UI** - German and English

---

## 📦 Components

| # | Component | Description |
|---|-----------|-------------|
| 01 | **Steam** | Downloads and starts the official Steam installer |
| 02 | **Escape from Tarkov** | Install via BSG Launcher or Steam |
| 03 | **SPT Server** | Installs the .NET 10 runtimes, then downloads and runs the official SPT installer |
| 04 | **Fika** | Installs Plugin + Server-Mod from official GitHub releases |
| 05 | **Headless Client** | Installs Fika.Headless plugin + FikaHeadlessManager |
| 06 | **Docker + WSL2** | Enables WSL2, installs kernel update and Docker Desktop |
| 07 | **Firewall** | Opens all required ports automatically |
| 08 | **FikaWebApp** | Pulls and starts the lacyway/fikawebapp Docker container |

---

## 📁 Install Layout (SPT 4.1+)

SPT 4.1 moved the server out of the install root. The wizard's **SPT path** always points at
the folder that holds `SPT.Server.exe` — if you pick the game folder, it steps into
`SPT_Runtime` for you.

```
<game folder>\
├─ EscapeFromTarkov.exe
├─ BepInEx\plugins\Fika\      ← Fika.Core.dll, Fika.Headless.dll
├─ FikaHeadlessManager.exe
└─ SPT_Runtime\               ← the wizard's "SPT path"
   ├─ SPT.Server.exe
   └─ user\mods\fika-server\  ← server mod + fika.jsonc
```

Note the asymmetry: `BepInEx` stays at the game root and is a *sibling* of `SPT_Runtime`,
not a child of it.

---

## 🔌 Firewall Ports

| Port | Protocol | Usage |
|------|----------|-------|
| 6969 | TCP + UDP | SPT Server |
| 25565 | UDP | Fika Peer-to-Peer |
| 8080 | TCP | FikaWebApp |
| 5000 | TCP | Container internal |

---

## 💻 Requirements

- Windows 10 or Windows 11 (64-bit)
- Administrator rights
- Internet connection
- .NET Desktop Runtime 10 and ASP.NET Core Runtime 10 — required by the SPT 4.x server;
  the wizard installs both automatically as part of the SPT step
- Docker Desktop — only needed for the FikaWebApp component

---

## 🔒 Downloads — Official Sources Only

All files are downloaded exclusively from official sources:

| Component | Source |
|-----------|--------|
| Steam | `cdn.akamai.steamstatic.com` |
| BSG Launcher | `launcher.escapefromtarkov.com` |
| SPT | `github.com/SP-Tushonka/installer` |
| .NET 10 runtimes | `aka.ms/dotnet` → `builds.dotnet.microsoft.com` |
| Fika | `github.com/project-fika` |
| FikaWebApp | Docker Hub — `lacyway/fikawebapp` |
| Docker | `desktop.docker.com` |
| WSL2 | `wslstorestorage.blob.core.windows.net` |

---

## 🚀 How to Use

1. Download the latest release from the Releases page
3. Accept the UAC prompt (Administrator rights required)
4. Select your language (German / English)
5. Click **"Install All"** or install each component individually

---

## 📸 Screenshots

Comming Soon

---

## ⚠️ Disclaimer

This is an **unofficial community tool** and has no affiliation with:
- Battlestate Games
- The SPT Project
- The FIKA Project

Use at your own risk.

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

Pull requests are welcome! For major changes, please open an issue first to discuss what you would like to change.

---

<div align="center">
Made with ❤️ by c0d1ngf1eber
</div>
