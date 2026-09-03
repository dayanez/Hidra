# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `Button to Action` plugin: maps a keyboard or mouse button to running a program, opening a
  URL, sending a synthetic key chord, or running a system command (lock, volume, media keys).
  Ported from a standalone prototype (`InputAutomationEngine`) into Hidra's own plugin system,
  so it shares Hidra's existing input capture and profile switching instead of running its own
  separate keyboard hook and process monitor. The action runs on a thread pool thread, not the
  input provider's capture thread, so a slow action (e.g. a process launch) can't add latency to
  every other keystroke.
- System tray icon. Closing the main window now hides it to the tray instead of exiting Hidra,
  since the point of a remapper is to keep running in the background; a genuine exit is available
  from the tray's right-click menu, which also has a "Start with Windows" toggle. Relaunching the
  exe while the window is hidden brings it back rather than doing nothing (this needed a fix in
  the existing single-instance IPC too, since it looked up the running window via
  `Process.MainWindowHandle`, which is always zero for a hidden window; it now uses `FindWindow`
  by title instead, which finds the window whether it's visible or not).

### Changed

- Forked from Universal Control Remapper (UCR) and rebranded as Hidra
- Ported the core app, plugin system, and the Interception/ViGEm providers to .NET 8
- Vendored the IOWrapper driver layer directly into this repo as `Hidra.IOWrapper`
- Added `Core_RawInputHook`, a driver-free keyboard/mouse input provider (user-space hook plus
  Raw Input for mouse movement), as a replacement for the kernel-driver-based Interception
  provider

### Removed

- The Interception input provider and its vendored wrapper DLL, now that `Core_RawInputHook`
  covers keyboard and mouse capture without a kernel driver
- The niche device providers inherited from UCR that were never ported to .NET 8 and were not
  part of the buildable solution (MIDI, Tobii eye tracker, TitanOne, DS4Windows, SpaceMouse,
  vJoy, SharpDX-based DirectInput/XInput), along with the shared libraries and the standalone
  `IOWrapper.sln`/`TestApp` that only those providers used. They remain available in git history
  if a future port needs them.
- The standalone `InputAutomationEngine` prototype console app, now that its functionality lives
  in Hidra as the `Button to Action` plugin
- The `Core_ViGEm` provider (virtual Xbox 360/DualShock 4 controller output) and its
  now-unreferenced `ProviderLogger` dependency. Hidra is now scoped to keyboard and mouse
  remapping only; it does not support game controllers as input or output. This is a deliberate
  narrowing of scope, not an unported gap, so it is not expected to come back. The code is still
  in git history for anyone forking Hidra back into a general HID remapper.

Everything below this point is inherited history from the upstream UCR project, prior to the
Hidra fork.

## [0.9.0] - 2020-01-02

### Added

- Added Shadow devices
- Added option to clear bindings
- Added Filters for plugins
- `Button to Filter`: Allows toggling of a filter using a button
- `Axis to Filter`: Allows toggling of a filter using an axis
- Added device cache allowing configuration of disconnected devices
- Added per input blocking for supported providers
- Provider Report now contains `ErrorMessage` property. If the provider is not live, this should contain a string indicating why
- [Tobii Provider] If IsLive is false, now reports reason in ErrorMessage
- [SpaceMouse provider] IsLive is always true, as HID is always present
- [MIDI provider] IsLive is always true, as MIDI is always present
- [TitanOne Provider] IsLive now reflects connected status of device
- [TitanOne Provider] Reports 0 devices if IsLive is false
- [vJoy Provider] IsLive now reflects whether driver is installed
- [Interception Provider] IsLive is false if no devices are found, assumes driver is not installed

### Fixed

- Child profiles now properly inherit all parent devices. Fixes crash on activating profile
- IOWrapper device list is now refreshed every time devices are queried

### Changed

- Updated to IOWrapper v0.11.2
- [Interception Provider] Blockable property of BindingDescriptor now indicates if input is blockable or not. This is controlled by whether BlockingEnabled in the settings file is true or not
- [XInput Provider] Only show Xinput devices in ProviderReport that are currently connected
- [DS4WindowsApi Provider] Only show DS4 devices in ProviderReport that are currently connected
- [SpaceMouse Provider] Only show SpaceMouse devices in ProviderReport that are currently connected
- [Tobii Provider] IsLive now reflects state of driver
- [Tobii Provider] Only show Tobii devices in ProviderReport that are currently connected
- [ViGEm Provider] Do not show devices if Bus Driver not installed

### Removed

- [Interception Provider] BlockingControlledByUi setting removed

## [0.8.0] - 2019-07-27

### Added
- Material design
- Quick access button to add new profile
- Profile overview in the main window
- Added save button to profile window
- Added Anti-Deadzone helper
- Added Anti-Deadzone option to AxisToAxis plugin
- Added ButtonToEvent plugin
- Added input validation for plugin values

### Changed
- Updated to IOWrapper v0.10.5
- `Button to Axis` parameters changed to two axis values and option for initialization
- Redesigned main window dashboard
- Redesigned profile window
- Show active profile in process bar
- Replaced menu with toolbar in main window
- Replaced dialog windows with proper dialogs
- Replaced menu with toolbar in profile window
- UCR Unblocker now uses the current directory as default
- Sensitivity to Axis Merger plugin added
- Improved circular deadzone calculation

### Removed
- Removed states

### Fixed
- Sum Mode in Axis Merger plugin no longer overflows
- Unblocking no longer crashes if the UCR path has spaces
- Bind Mode button now only responds to mouse down and not mouse up (Fixes binding Space bar re-triggering Bind Mode on release)

## [0.7.0] - 2019-01-03

### Changed

- `Button to Axis` parameters changed to two axis values and option for initialization
- Plugin updates are now of type short instead of long. Some operations are performed using int, to avoid wrap-around or crashes.
- Subscription and Bind Mode callbacks are now executed as Tasks and are an Action<short> rather than dynamic
- Default blocking to true while UCR GUI does not support selecting block

## [0.6.0] - 2018-12-03

### Added

- Added bind mode for inputs

### Changed
  - Updated to IOWrapper v0.9.11

## [0.5.2] - 2018-10-28
### Fixed

- Values other than 0 or 50 for DeadZone should now work properly - Maximum deflection should now be achievable again

## [0.5.1] - 2018-10-28
### Added
- Interception Provider: Duplicate devices now have #2, #3 etc at end of name to differentiate them  
- You no longer put a Provider into Bind Mode, you put a Device into Bind Mode (Bind Mode is still not implemented on the front end)  
- "Provider Libraries" to simplify writing of new providers
- Rewritten DirectInput and XInput providers using new Provider Libraries  
- Unit tests for Provider Libraries

### Fixed
- Interception Provider: Should no longer crash on startup when there are multiple identical devices.
- Interception Provider: Keyboard keys are no longer inverted (Press is now press, release is now release)  
- Interception Provider: When both X and Y movement was received (ie diagonal movement), Interception would only process X and ignore Y.
- Fixed the MVVM for the main window

### Removed
- IOWrapper: All code from ProviderInterface that was not related to the interface itself was removed (ie Old helper libraries removed)

## [0.5.0] - 2018-10-08
### Added
- `Buttons to Axis` plugin
- Invert output to `Button to Axis`
- `Button to Buttons (Long Press)`
- Reset button for `Delta To Axis` relative mode
- DLL blocking detecting and unblocking
- Appveyor build server setup

### Fixed
- Allow `+` and `-` to be typed into plugin decimal inputs 
- nuke restore task
- nuke versioning task

### Removed
- AxisToAxisCumulative, AxisToDelta, ButtonToButtonsLongPress and DeltaToAxis plugins were removed from the UCR repo and moved to the UCR-Plugins repo
- Cake build script

## [0.4.0] - 2018-09-03
### Added
- Inputs and outputs now support live preview when a profile is active
- Added circular dead zone feature
- `Button to Axis` has absolute value to support Xbox triggers
- `Axes to Axes`: Maps a stick to another stick, with optional circular dead zone which acts on both axes
- `Axis to Axis (Cumulative)`: Cumulatively move the output axis with the value of the input axis
- `Axis to Delta`: Joystick type axis to Mouse type axis
- `Delta to Axis`: Mouse type axis to Joystick type axis
- IOWrapper: Complete Delta Axis implementation for mouse support
- IOWrapper: Interception devices now show `K:` or `M:` prefix to aid in identification
- IOWrapper: Interception support for horizontal wheel

### Changed
- Updated to IOWrapper v0.5.7

### Fixed
- Fixed wrapping errors for invert, dead zone and sensitivity settings
- IOWrapper: Interception provider, fixes to device enumeration - should now find PS/2 and built-in devices
- IOWrapper: Interception mouse wheel fix
- IOWrapper: ViGEm DS4 DPad fixed
- IOWrapper: ViGEm Xbox triggers fixed
