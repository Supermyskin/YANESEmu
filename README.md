# YANESEmu

YANESEmu is an early NES emulator written in C# with a cross-platform desktop window built on Avalonia. It is a learning project and currently runs a small subset of NES software. It is not yet a general purpose emulator.

## Requirements

- .NET 10 SDK
- An iNES NROM ROM image (`.nes`)

The desktop app targets macOS, Windows, and Linux. Avalonia provides the application window and pixel-scaled display; SoundFlow/MiniAudio provides cross-platform audio output. The emulator remains early-stage; CPU and PPU accuracy, audio, and cartridge support are incomplete.

## Run

From the repository directory, start the app and choose **File → Open ROM…**:

```sh
dotnet run
```

On macOS, **About**, **File**, **Controls**, and **Audio** appear in the system menu bar; **File** provides ROM opening, reset, and pause/resume. macOS supplies its standard Quit command in the application menu. On Windows and Linux, the same controls appear in the window menu. **⌘O** opens a ROM and **⌘R** resets it on macOS. The window scales the 256×240 framebuffer with nearest-neighbor filtering. ROM loading errors are shown in red in the status area. Closing the window waits for the emulation worker to stop before the app exits.

The File menu also includes **Step Instruction** (pause first) and **Stop**. Reset waits for the current CPU worker to stop before reinitializing the machine. A deliberate Stop preserves the current machine state; use Reset to restart the ROM.

To print a CPU instruction trace to the terminal while using the window, start the app with `--trace`, then open a ROM:

```sh
dotnet run -- --trace
```

The current build expects iNES mapper 0 (NROM), with one or two 16 KiB PRG banks and zero or one 8 KiB CHR bank. CHR RAM is used when the ROM has no CHR bank. Other mappers and NES 2.0 images are rejected with a diagnostic. No ROM is included in this repository.

## Current features

### CPU and bus

- All 151 official NMOS 6502 opcodes and common stable undocumented instructions. The optional nestest comparison checks the complete CPU reference trace, including register, flag, and instruction-cycle state.
- CPU RAM at `$0000–$07FF`, mirrored through `$1FFF`.
- PPU register window at `$2000–$3FFF`, mirrored every eight bytes.
- NROM PRG mapped at `$8000–$FFFF`, including 16 KiB bank mirroring.
- Reset vector loading, NMI and maskable IRQ entry, and BRK handling.
- CPU runs on a background task so the desktop window remains responsive; closing waits for that task to stop.
- Pause waits for the current instruction batch to reach a safe boundary; single-step runs one instruction while paused or stopped.
- The core run loop accepts a cancellation token so callers can stop continuous execution.
- ROM diagnostics and file access errors are shown in red in the app status area.

The core matches the nestest CPU reference log. Timing advances per instruction, so bus-level timing details and interrupt sampling subtleties are not covered by that comparison. Common unofficial NOP, LAX, SAX, SBC, ANC, ALR, ARR, AXS, SLO, RLA, SRE, RRA, DCP, and ISC operations are implemented; unofficial KIL opcodes stop with a diagnostic. Other unsupported opcodes stop execution and report the mnemonic, byte, and address. CPU conformance alone does not guarantee a game will run because PPU, full APU, and mapper support remain incomplete.

### PPU

- CPU access to PPU registers `$2000–$2007`, including register mirroring.
- VRAM address and scroll write latches, buffered `$2007` reads, and configurable VRAM increment.
- CHR ROM/RAM, two-screen nametable RAM with horizontal or vertical NROM mirroring, and palette RAM mirrors.
- PPU cycle advancement at three dots per CPU cycle, basic vblank status, and NMI request.
- 256×240 background rendering from nametable, attribute, pattern, palette, and scroll data.
- 8×8 and 8×16 OAM sprites with transparency, flips, palettes, background priority, sprite-zero hit, and the eight-sprites-per-scanline limit.
- 256×240 framebuffer displayed in the Avalonia desktop window.

### Controller input

- Player-one keyboard input is connected to the NES serial controller port at `$4016`.
- Default bindings are Z/X for A/B, Right Shift for Select, Enter for Start, and the arrow keys for directions.
- Choose **Controls → Configure Controller…** to change bindings. Settings are saved in the user's application-data folder and restored at startup.

### Audio

- Pulse, triangle, and noise channels generate samples from CPU cycle timing.
- SoundFlow/MiniAudio outputs sound through the system's default audio device.
- Choose **Audio → Audio Settings…** to set master volume or mute. Preferences are saved in the user's application-data folder.
- The DMC sample channel and some cycle-accurate APU details are not implemented.

Mapper support beyond NROM and cycle-accurate PPU fetch/evaluation timing are not implemented. Background and sprite rendering currently produce visible game graphics, but timing-sensitive games may render incorrectly or fail to run.

## Project layout

- `Emulator.cs`: shared CPU bus, machine state, and run loop.
- `Cpu.cs`: CPU instruction execution and cycle accounting.
- `Ppu.cs`: PPU registers, memory, timing, and background rendering.
- `Cartridge.cs`: iNES NROM loading and reset initialization.
- `Apu.cs`, `AudioSampleBuffer.cs`, `AudioOutput.cs`: NES audio synthesis, sample buffering, and desktop playback.
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

The test runner checks CPU operations and arithmetic flags, all official opcode dispatch and base cycle counts, indexed page-cross timing, branch timing, common undocumented-opcode behavior, zero-page wrapping, control-flow returns, reset registers and exact vectors, cancellable run-loop behavior, IRQ/NMI/BRK vectors and stack status, RAM mirroring, PPU buffered reads and palette mirrors, 8×8/8×16 sprite rendering, sprite-zero hit and overflow, NROM and CHR RAM loading, and the core's frame and diagnostic events. To compare the complete CPU trace against nestest, download `nestest.nes` and `nestest.log` from the [NES test ROM collection](https://github.com/christopherpow/nes-test-roms/tree/master/other), set `YANESEMU_NESTEST_ROM` and `YANESEMU_NESTEST_LOG` to their paths, and run the test project. The ROM is not included. The desktop app uses Avalonia packages restored during the build.

## Updating this README

When adding or changing emulator features, update the relevant feature list, limitations, supported ROM requirements, command examples, and test instructions here in the same change. Describe only behavior that is implemented and verified.
