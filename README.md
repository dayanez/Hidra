<img src="icon.png" align="right" width="96" />

# Hidra

[![Build](https://github.com/dayanez/Hidra/actions/workflows/build.yml/badge.svg)](https://github.com/dayanez/Hidra/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Hidra remaps your keyboard and mouse, entirely in software.** Turn any key or button into a
different key, a mouse action, or a triggered command, no drivers, no reboot, works with any
keyboard or mouse regardless of manufacturer.

```
Caps Lock  ->  Escape
Side mouse button  ->  Ctrl+Shift+Escape (open Task Manager)
F13  ->  Launch a program, open a URL, or mute the volume
```

That's the whole idea: a background utility that intercepts input before it reaches Windows and
sends out whatever you told it to instead.

## Why Hidra

Most remapping tools fall into one of two camps: OEM software tied to one brand of keyboard or
mouse, or general HID remappers that need a kernel driver to talk to game controllers. Hidra is
neither. It only does keyboard and mouse, on purpose, and that narrow scope is what lets it run
without installing anything at the driver level, on hardware from any manufacturer.

## Features

- **Remap keys and mouse input** to other keys, mouse buttons, or mouse movement, with full
  analog control (sensitivity, dead zones) over mouse axes
- **Trigger actions from a button**: launch a program, open a URL, send a key chord, or run a
  system command (lock, volume, media keys), no output device needed
- **Cross-device combos**: hold a keyboard key as a modifier that changes what a mouse button
  does, or vice versa
- **Profiles**: nest them, and auto-switch between them based on which application is focused
- **Runs in the background**: closing the window keeps Hidra remapping from the system tray, with
  an optional "start with Windows" toggle
- **No kernel driver, ever**: input capture and output are both done in user space; nothing to
  install, nothing that needs a reboot
- **No injection**: doesn't hook into other processes, so it stays compatible with games that use
  anti-tampering technologies

## Getting started

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or Visual Studio
2022 17.8+ with the ".NET desktop development" workload). There's nothing else to install.

```
dotnet build Hidra.sln
dotnet run --project Hidra\Hidra.csproj
```

Hidra isn't packaged as a standalone release yet, so building from source is currently the only
way to run it.

## Current status

This is an active, in-progress fork, not a finished product yet. Working today:

- Driver-free keyboard and mouse capture and output, via a single provider (`Core_RawInputHook`)
- The full remapping engine, plugin system, and WPF UI described above

Not working yet:

- `Hidra.Tests` doesn't build (still targets an old .NET Framework version from before this fork)

Out of scope, on purpose, not a gap to be filled: Hidra does not support game controllers,
gamepads, or joysticks, as input or output. The original project this forked from
([Universal Control Remapper](https://github.com/Snoothy/UCR)) supported those, along with MIDI,
eye trackers, and other niche devices; all of that was removed rather than ported, in favor of
doing one thing (keyboard and mouse) well. It's still in git history if a future fork wants it
back; see `CHANGELOG.md`.

## How it's built

Hidra is a .NET 8 WPF app. The remapping engine lives in `Hidra.Core`, built-in remap plugins in
`Hidra.Plugins`, and device access in `Hidra.IOWrapper` (a vendored, heavily trimmed fork of
[IOWrapper](https://github.com/evilC/IOWrapper)). See `AGENTS.md` for the full directory
breakdown, and `SECURITY.md` for why input-capture code here gets held to a higher bar than a
typical utility.

## Attribution and license

Hidra is a fork of [Universal Control Remapper (UCR)](https://github.com/Snoothy/UCR), originally
created by evilC and Snoothy (HidWizards). Hidra is open source under the [MIT license](LICENSE),
as is the upstream UCR/IOWrapper code it's built on; see the LICENSE file for the original
copyright notice, which this fork retains as required.
