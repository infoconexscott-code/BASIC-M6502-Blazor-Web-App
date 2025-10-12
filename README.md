# BASIC-M6502-Blazor-Web-App

A Blazor WebAssembly PWA that brings Microsoft's 6502 BASIC interpreter to life in the browser. Written in C#, targeting .NET 8, and running entirely on WebAssembly with no plugins required.

## Project structure

| Path | Description |
|------|-------------|
| `Basic6502.Core/` | Core emulator library that exposes the MOS 6502 CPU, a configurable memory bus, and console bridge for text I/O. |
| `tests/Basic6502.Core.UnitTests/` | Unit tests that validate isolated components such as the memory bus. |
| `tests/Basic6502.Core.IntegrationTests/` | Integration tests that execute 6502 programs against the console bridge to verify cross-component behavior. |
| `tests/Basic6502.EndToEndTests/` | High-level scenarios that simulate BASIC programs interacting with the bridge to exercise input and output flows. |
| `.github/workflows/` | Continuous integration and deployment workflows for building, testing, and publishing the project. |

## Core emulator

The `Basic6502.Core` library contains the full CPU, memory bus, and console bridge required to execute Microsoft 6502 BASIC. The CPU implementation covers the complete 6502 instruction set (including BCD arithmetic), and the bus supports mapping peripheral devices such as the console bridge into the address space. The console bridge exposes a trio of memory-mapped registers that the emulated program can use for text input and output, enabling straightforward integration with UI layers such as Blazor.

## Testing strategy

All test projects target .NET 8 and use xUnit. They are organized by scope to keep responsibilities clear:

- **Unit tests** verify small units of functionality such as memory read/write operations.
- **Integration tests** stitch together the CPU, bus, and console bridge to confirm realistic instruction execution.
- **End-to-end tests** queue input through the bridge and assert that emulated programs echo the expected output, mirroring runtime behavior.

To run the tests locally, execute the following commands once the .NET 8 SDK is installed:

```bash
dotnet test tests/Basic6502.Core.UnitTests/Basic6502.Core.UnitTests.csproj
dotnet test tests/Basic6502.Core.IntegrationTests/Basic6502.Core.IntegrationTests.csproj
dotnet test tests/Basic6502.EndToEndTests/Basic6502.EndToEndTests.csproj
```

## Continuous integration

The `build` workflow restores dependencies, builds the core library, and runs the unit, integration, and end-to-end test suites on every push and pull request. The pipeline targets the .NET 8 SDK to match the runtime used by the application.

## Continuous deployment

The `deploy` workflow can be triggered manually or on pushes to `main`. It publishes the `Basic6502.Core` library in Release mode and uploads the compiled artifacts, ensuring the emulator is ready for downstream packaging or hosting.

## Prerequisites

- .NET SDK 8.0 or newer (the repository includes a `global.json` that pins builds to SDK 8.0.100)
- A modern browser capable of running Blazor WebAssembly apps for the front-end experience

With the prerequisites in place you can build the emulator library, run the automated tests, and integrate the components into the Blazor front end that hosts Microsoft BASIC in the browser.
