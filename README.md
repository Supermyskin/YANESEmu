# YANESEmu

YANESEmu is an early NES emulator written in C# with a cross-platform desktop window built on Avalonia. It is a learning project and currently runs a small subset of NES software. It is not yet a general purpose emulator.

## Requirements

- .NET 10 SDK
- An iNES NROM ROM image (`.nes`)

The desktop app targets macOS, Windows, and Linux. Avalonia provides the application window and pixel-scaled display. The emulator remains early-stage; CPU and PPU accuracy, audio, input, and cartridge support are incomplete.

## Run

From the repository directory, start the app and choose **File → Open ROM…**:

```sh
dotnet run
```

On macOS, **About** and **File** appear in the system menu bar; **File** provides ROM opening, reset, and pause/resume. macOS supplies its standard Quit command in the application menu. On Windows and Linux, the same controls appear in the window menu. **⌘O** opens a ROM and **⌘R** resets it on macOS. The window scales the 256×240 framebuffer with nearest-neighbor filtering. ROM loading errors are shown in red in the status area. Closing the window waits for the emulation worker to stop before the app exits.

The File menu also includes **Step Instruction** (pause first) and **Stop**. Reset waits for the current CPU worker to stop before reinitializing the machine. A deliberate Stop preserves the current machine state; use Reset to restart the ROM.

To print a CPU instruction trace to the terminal while using the window, start the app with `--trace`, then open a ROM:

```sh
dotnet run -- --trace
```

The current build expects iNES mapper 0 (NROM), with one or two 16 KiB PRG banks and zero or one 8 KiB CHR bank. CHR RAM is used when the ROM has no CHR bank. Other mappers and NES 2.0 images are rejected with a diagnostic. No ROM is included in this repository.

## Current features

### CPU and bus

- Execution paths for all 151 official NMOS 6502 opcodes, with conformance and timing validation in progress.
- CPU RAM at `$0000–$07FF`, mirrored through `$1FFF`.
- PPU register window at `$2000–$3FFF`, mirrored every eight bytes.
- NROM PRG mapped at `$8000–$FFFF`, including 16 KiB bank mirroring.
- Reset vector loading, NMI and maskable IRQ entry, and BRK handling.
- CPU runs on a background task so the desktop window remains responsive; closing waits for that task to stop.
- Pause waits for the current instruction batch to reach a safe boundary; single-step runs one instruction while paused or stopped.
- The core run loop accepts a cancellation token so callers can stop continuous execution.
- ROM diagnostics and file access errors are shown in red in the app status area.

Instruction behavior and cycle timing are still being validated. Common unofficial NOP forms are implemented with their operand reads and cycle costs, and unofficial KIL opcodes stop with a diagnostic. Other unsupported opcodes stop execution and report the mnemonic, byte, and address. The UI shell does not make unsupported games compatible.

### PPU

- CPU access to PPU registers `$2000–$2007`, including register mirroring.
- VRAM address and scroll write latches, buffered `$2007` reads, and configurable VRAM increment.
- CHR ROM/RAM, two-screen nametable RAM with horizontal or vertical NROM mirroring, and palette RAM mirrors.
- PPU cycle advancement at three dots per CPU cycle, basic vblank status, and NMI request.
- Basic 256×240 background rendering from nametable, attribute, pattern, and palette data.
- 256×240 framebuffer displayed in the Avalonia desktop window.

Sprites, APU audio, controller input, full scrolling/timing accuracy, and accurate PPU timing are not implemented yet. The window currently displays the existing basic background renderer output.

## Project layout

- `Emulator.cs`: shared CPU bus, machine state, and run loop.
- `Cpu.cs`: CPU instruction execution and cycle accounting.
- `Ppu.cs`: PPU registers, memory, timing, and background rendering.
- `Cartridge.cs`: iNES NROM loading and reset initialization.
- `Program.cs`, `YanesEmuApp.cs`, `MainWindow.cs`: Avalonia startup, desktop window, ROM controls, and framebuffer display.
- `TraceLogger.cs`: optional instruction trace formatting, enabled with `--trace`.
- `FramePresenter.cs`: legacy terminal display and BMP output utilities (not used by the desktop app).
- `tests/YANESEmu.Tests`: automated core checks.

The emulator publishes instruction, frame, and diagnostic events. The desktop app subscribes to diagnostics and displays them in the window; the emulator core does not write directly to the console.

## Build and checks

```sh
dotnet build
dotnet run --project tests/YANESEmu.Tests
```

The test runner checks CPU operations, all official opcode dispatch and base cycle counts, indexed page-cross timing, branch timing, common unofficial NOP behavior, cancellable run-loop behavior, IRQ/NMI/BRK vectors and stack status, RAM mirroring, PPU buffered reads and palette mirrors, NROM and CHR RAM loading, and the core's frame and diagnostic events. These are foundation checks, not a complete hardware conformance suite. The desktop app uses Avalonia packages restored during the build.

## Updating this README

When adding or changing emulator features, update the relevant feature list, limitations, supported ROM requirements, command examples, and test instructions here in the same change. Describe only behavior that is implemented and verified.
