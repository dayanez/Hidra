<img src="icon.png" align="right" />

# Hidra

Hidra is a lightweight, brand-agnostic Windows input remapping tool. It remaps input from
keyboards, mice, and controllers - regardless of manufacturer - to virtual output devices, so a
key on one device can drive behavior on a completely different device.

Hidra is a fork of [Universal Control Remapper (UCR)](https://github.com/Snoothy/UCR), originally
created by evilC and Snoothy (HidWizards), being modernized onto .NET 8 and rebranded as its own
project. See the [LICENSE](LICENSE) file for the original MIT license and copyright notice, which
this fork retains as required.

<img src="Screenshot.png" align="center" />

## Status

This is an active, in-progress fork - not a finished product. Currently working and tested:

- Keyboard/mouse capture (via the [Interception](https://github.com/oblitum/Interception) driver)
- Virtual Xbox360/DualShock4 controller output (via [ViGEm](https://github.com/nefarius/ViGEmBus))
- The core remapping engine, plugin system, and WPF UI

Not yet ported: DirectInput/XInput physical controller input, vJoy virtual output, and a handful
of niche device providers (MIDI, Tobii eye tracker, TitanOne, DS4Windows). `Hidra.Tests` is not
yet building either.

## Features

- Remap any number of inputs to any number of outputs on emulated output devices, with full
  analog support
- Cross-device mappings: a binding's inputs can come from entirely different physical devices
- Profiles and nesting allow for easy configuration
- Endless remapping potential through plugin extension support
- Remapping and device order persists through reboots and unplugging of devices
- Uses no injection, making it compatible with games using anti-tampering technologies

## Device support

Hidra supports input and output devices through provider plugins (forked from
[IOWrapper](https://github.com/evilC/IOWrapper) as `Hidra.IOWrapper`, vendored directly in this
repo).

### Supported input

- Keyboard (using [Interception](https://github.com/oblitum/Interception))
- Mouse (using [Interception](https://github.com/oblitum/Interception))

### Supported output

- Xbox 360 controller (using [ViGEm](https://github.com/nefarius/ViGEmBus))
- DualShock 4 controller (using [ViGEm](https://github.com/nefarius/ViGEmBus))
- Keyboard (using [Interception](https://github.com/oblitum/Interception))
- Mouse (using [Interception](https://github.com/oblitum/Interception))

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or Visual Studio 2022
17.8+ with the ".NET desktop development" workload).

```
dotnet build Hidra.sln
dotnet run --project Hidra\Hidra.csproj
```

To actually capture input or emit virtual controller output, you'll also need the
[Interception driver](https://github.com/oblitum/Interception) (requires a reboot after install)
and the [ViGEm Bus driver](https://github.com/nefarius/ViGEmBus/releases) installed.

## License

Hidra is Open Source software and is released under the [MIT license](LICENSE), as is the
upstream UCR/IOWrapper code it's built on.
