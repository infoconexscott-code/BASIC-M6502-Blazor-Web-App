# BASIC M6502 Blazor Web App

A **Blazor WebAssembly (PWA)** that brings **Microsoft’s 6502 BASIC** to the browser.  
Written in **C#** for **.NET 10**, running fully on **WebAssembly** — no plugins.

![Screenshot](docs/screenshot.png)

---

## Features

- 100% client-side Blazor WebAssembly
- Executes the authentic **Microsoft 6502 BASIC** ROM
- Interactive console (keyboard I/O mapped to emulated memory)
- SAVE / LOAD via IndexedDB (or placeholder, depending on build)
- Installable PWA (works offline)
- Clean, modular design:
  - `Basic6502.Core` → CPU + memory bus + I/O interfaces
  - `Basic6502.Web`  → Blazor UI + PWA bits

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- A modern browser (Chrome/Edge/Firefox/Safari)

### Run (Development)
```bash
dotnet run --project Basic6502.Web
```

Open the app, wait for `READY.` then try:

```basic
10 PRINT "HELLO"
20 GOTO 10
RUN
```

### Publish (AOT for speed)

```bash
dotnet publish Basic6502.Web -c Release -p:BlazorWebAssemblyAOT=true
```

Serve the static output in:

```
Basic6502.Web/bin/Release/net10.0/publish/wwwroot
```

> **ROM**: place your assembled Microsoft BASIC ROM as `Basic6502.Web/wwwroot/basic.bin`.

---

## Project Structure

```
BASIC-M6502-Blazor-Web-App/
├─ Basic6502.Core/      # 6502 CPU, memory bus, console bridge
├─ Basic6502.Web/       # Blazor WebAssembly PWA front-end
├─ README.md
└─ LICENSE (MIT)
```

---

## Tech Stack

| Layer     | Choice                        |
| --------- | ----------------------------- |
| Language  | C# (.NET 10)                  |
| Front-end | Blazor WebAssembly            |
| Runtime   | WebAssembly (AOT recommended) |
| Storage   | IndexedDB                     |
| Packaging | PWA                           |
| Emulator  | Custom 6502 core in C#        |

---

## License

MIT for this project.
Microsoft’s 6502 BASIC source was released under MIT (include their license text if you redistribute the ROM/source).

---

## Roadmap

* Full 6502 opcode coverage
* SAVE/LOAD UX, sample programs
* TRACE mode and step debugger
* Multiple target shims (KIM-1 / PET / Apple) via vector table

