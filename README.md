# YANESEmu

YANESEmu is an early NES emulator written in C#. It is a learning project and currently runs a small subset of NES software. It is not yet a general purpose emulator.

## Requirements

- .NET 10 SDK
- An iNES NROM ROM image (`.nes`)
- A terminal that supports ANSI true-color output to see the live frame in the terminal

The emulator has no external runtime packages. It runs on macOS, Windows, and Linux. When the CPU halts, it writes a portable `frame.bmp` to the current working directory.

## Run

From the repository directory, pass the path to a ROM:

```sh
dotnet run -- "/path/to/game.nes"
```

Use `--trace` after the ROM path to print the supported CPU instruction trace:

```sh
dotnet run -- "/path/to/game.nes" --trace
```

The current build expects iNES mapper 0 (NROM), with one or two 16 KiB PRG banks and zero or one 8 KiB CHR bank. CHR RAM is used when the ROM has no CHR bank. Other mappers and NES 2.0 images are rejected with a diagnostic. No ROM is included in this repository.

## Current features

### CPU and bus

- 6502 execution for a limited set of official instructions, including common loads, stores, arithmetic, logic, shifts, branches, stack operations, and register transfers.
- CPU RAM at `$0000–$07FF`, mirrored through `$1FFF`.
- PPU register window at `$2000–$3FFF`, mirrored every eight bytes.
- NROM PRG mapped at `$8000–$FFFF`, including 16 KiB bank mirroring.
- Reset vector loading and basic NMI entry.
- Optional instruction trace with `--trace`.

The CPU is incomplete. Encountering an unsupported opcode stops execution and reports the address. Cycle timing is approximate and is not yet validated against a full 6502 test suite.

### PPU

- CPU access to PPU registers `$2000–$2007`, including register mirroring.
- VRAM address and scroll write latches, buffered `$2007` reads, and configurable VRAM increment.
- CHR ROM/RAM, two-screen nametable RAM with horizontal or vertical NROM mirroring, and palette RAM mirrors.
- PPU cycle advancement at three dots per CPU cycle, basic vblank status, and NMI request.
- Basic 256×240 background rendering from nametable, attribute, pattern, and palette data.
- ANSI color frame output in interactive terminals and a `frame.bmp` snapshot when execution halts.

Sprites, APU audio, controller input, full scrolling/timing accuracy, and a graphical window are not implemented yet. The terminal image is a downsampled preview.

## Project layout

- `Emulator.cs`: shared CPU bus, machine state, and run loop.
- `Cpu.cs`: CPU instruction execution and cycle accounting.
- `Ppu.cs`: PPU registers, memory, timing, and background rendering.
- `Cartridge.cs`: iNES NROM loading and reset initialization.
- `Program.cs`: command-line startup and emulator orchestration.
- `TraceLogger.cs`: optional instruction trace formatting.
- `FramePresenter.cs`: terminal display and BMP output.
- `tests/YANESEmu.Tests`: dependency-free automated core checks.

The emulator publishes instruction, frame, and diagnostic events. Logging and display code subscribe from the application layer so core code remains usable in tests without writing to the console.

## Build and checks

```sh
dotnet build
dotnet run --project tests/YANESEmu.Tests
```

The test runner checks CPU operations, RAM mirroring, PPU buffered reads and palette mirrors, vblank NMI, NROM and CHR RAM loading, and the core's frame and diagnostic events. These are foundation checks, not a complete hardware conformance suite.

## Updating this README

When adding or changing emulator features, update the relevant feature list, limitations, supported ROM requirements, command examples, and test instructions here in the same change. Describe only behavior that is implemented and verified.
