# Architecture Map

A file-by-file map of this repo: what each project, folder, and file is for, so you can find where
to make a change without grepping around first. For build/test commands, security-sensitive areas,
and contributor conventions, see `AGENTS.md`; this file is purely "what lives where."

The five projects in `Hidra.sln`, in dependency order:

```
Hidra.IOWrapper  (vendored device I/O layer)
      ^
Hidra.Core       (engine: profiles, mappings, plugin contracts)
      ^
Hidra.Plugins    (built-in remap/filter plugins)
      ^
Hidra             (WPF app: Views/ViewModels)

Hidra.Tests       (NUnit tests, references Hidra.Core + Hidra.Plugins)
```

---

## Hidra.Core — the remapping engine

Everything here is UI-free: profiles, mappings, devices, and the plugin contract they all share.

### Attributes/
Declarative metadata a plugin uses to describe itself to the UI/reflection layer.

- `PluginAttribute.cs` — marks a class as a plugin; carries its display name/description/group.
- `PluginIoAttribute.cs` — shared base for `PluginInput`/`PluginOutput`.
- `PluginInput.cs` / `PluginOutput.cs` — declare one input/output binding slot a plugin exposes.
- `PluginGroupAttribue.cs` — groups a plugin's outputs under a named section.
- `PluginSettingsGroupAttribue.cs` — groups a plugin's settings under a named section.
- `PluginGuiAttribute.cs` — marks a property as GUI-editable (display name/order/group).

### Managers/
The runtime services `Context` composes; each owns one concern.

- `BindingManager.cs` — drives "bind mode" (listening for the next input to assign to a binding).
- `DevicesManager.cs` — enumerates devices from the I/O backend, caches them to disk.
- `PluginsManager.cs` — MEF-loads plugin DLLs into `Plugins`.
- `ProcessProfileSwitcher.cs` — polls the foreground process, auto-activates a matching profile.
- `ProfilesManager.cs` — creates/copies/finds profiles, including Guid-remapping on copy.
- `SubscriptionsManager.cs` — activates/deactivates a profile by wiring its bindings to the I/O backend.

### Models/
Persisted domain objects.

- `Profile.cs` — a device/mapping configuration profile, with child profiles and auto-switch rules.
- `Mapping.cs` — one input-binding-to-plugin-chain mapping within a profile.
- `Plugin.cs` — abstract base for all plugin types (I/O categories, GUI property matrix, filters, lifecycle).
- `Device.cs` — a physical/cached input or output device and its binding-menu lookup.
- `DeviceCache.cs` — on-disk snapshot of a device's identity + binding menu (for disconnected devices).
- `DeviceConfiguration.cs` — a device (+ optional shadow devices) as configured into a profile.
- `Filter.cs` — a named boolean condition plugins can gate on.
- `PluginProperty.cs` / `PluginPropertyGroup.cs` — reflection wrappers exposing `[PluginGui]` properties to the settings UI.
- `PropertyValidationResult.cs` — pass/fail + message result for a plugin property validator.

**Models/Binding/** — the binding value types:
`DeviceBinding.cs` (one input/output binding with live value + bind-mode state),
`DeviceBindingInfo.cs` (serializable key/axis identity),
`DeviceBindingNode.cs` (a node in a device's binding menu tree).

**Models/Subscription/** — the live (runtime, non-persisted) subscription graph built when a
profile activates: `SubscriptionState.cs` is the root; `DeviceSubscription.cs`,
`DeviceConfigurationSubscription.cs`, `InputSubscription.cs`, `OutputSubscription.cs`,
`MappingSubscription.cs`, `PluginSubscription.cs`, `FilterState.cs` build out from there.

### Utilities/
- `Logger.cs` — static facade over NLog (Trace/Debug/Info/Warn/Error/Fatal).
- `Functions.cs` — axis math helpers (invert, clamp, split, abs, percentage-to-range).
- `InputValidation.cs` — shared plugin-property validators (percentage, range, non-zero).
- `Constants.cs` — shared axis min/max value constants.
- **AxisHelpers/** — `DeadZoneHelper.cs` (linear), `CircularDeadZoneHelper.cs` (2D radial),
  `AntiDeadZoneHelper.cs` (boosts small values outward), `SensitivityHelper.cs` (linear/cubic curve).

### Properties/
`AssemblyInfo.cs` and `Annotations.cs` (JetBrains ReSharper attributes) — generated/vendored,
not hand-maintained.

---

## Hidra.Plugins — built-in remap and filter plugins

### Remapper/
- `ButtonToButton.cs` — map one button to another.
- `ButtonToEvent.cs` — remap a button to a fire-once event.
- `ButtonToButtonWithModifier.cs` — button-to-button gated by a cross-device modifier (chord).
- `ButtonToAction.cs` — button triggers a program/URL/key-chord/system command via `ActionExecutor`.
- `ButtonsToAxis.cs` — two buttons map to one axis's extremes.
- `ButtonToAxis.cs` — button maps to configurable released/pressed axis values.
- `AxisInitializer.cs` — writes an axis output from a fixed percentage on activation only.
- `AxisSplitter.cs` — splits one axis into two new axes.
- `AxisToButton.cs` — axis sign maps to two buttons.
- `AxisToAxis.cs` — passes an axis through, optionally inverted.
- `AxesToAxes.cs` — X/Y axis pair remap with per-axis invert, shared sensitivity/dead zone.
- `AxisMerger.cs` — combines two axes into one (average/greatest/sum) with dead zone/sensitivity.
- `AxisToAxisWithModifier.cs` — axis remap that switches sensitivity while a cross-device modifier
  is held ("sniper mode" / DPI preset).

### Filter/
- `ButtonToFilter.cs` — button sets/toggles a named filter's active state.
- `AxisToFilter.cs` — axis crossing a bound range sets/toggles a named filter's state.

### Utilities/
- `SendInputNative.cs` — P/Invoke declarations for `SendInput`/`LockWorkStation` (pure Win32).
- `ActionExecutor.cs` — runs `ButtonToAction`'s side effects off the capture thread.
- `VirtualKeyMap.cs` — bidirectional lookup between key names (e.g. "F13", "Ctrl") and Win32 virtual-key codes.

---

## Hidra — the WPF application

### Views/ + ViewModels/ (paired by feature)

| Area | Views | ViewModels |
|---|---|---|
| Main shell | `MainWindow.xaml(.cs)` | — |
| Dashboard (home screen) | — | `Dashboard/DashboardViewModel.cs`, `ProfileItem.cs`, `DeviceItem.cs` |
| Profile editor | `ProfileViews/ProfileWindow.xaml(.cs)` | `ProfileViewModels/ProfileViewModel.cs` |
| Mapping chain (mapping → plugin → filter → binding) | `Controls/MappingCardControl.xaml(.cs)`, `Controls/DeviceBindingControl.xaml(.cs)` | `ProfileViewModels/MappingViewModel.cs`, `PluginViewModel.cs`, `FilterViewModel.cs`, `DeviceBindingViewModel.cs` |
| Plugin property UI | `Controls/Plugin/*` (PluginControl, PluginPropertyControl, PluginPropertyListControl, template selectors) | `ProfileViewModels/Plugin*ViewModel.cs` (Toolbox, Group, Item, Simple, Property, PropertyGroup) |
| Device selection | `Controls/DeviceSelectControl.xaml(.cs)`, `Controls/DeviceAddRemoveControl.xaml(.cs)`, `Controls/ProfileDeviceListControl.xaml(.cs)` | `Controls/DeviceSelectControlViewModel.cs`, `DeviceAddRemoveControlViewModel.cs`, `Dashboard/ProfileDeviceListControlViewModel.cs`, `DeviceViewModels/DeviceViewModel.cs` |
| Dialogs (modal popups) | `Dialogs/*.xaml(.cs)` — one file per dialog (About, AddDevices, AddMappingPlugin, Alert, Bool, CreateProfile, Decision, Help, ManageDeviceConfiguration, String, Text) | `Dashboard/AddDevicesDialogViewModel.cs`, `CreateProfileDialogViewModel.cs`, `ManageDeviceConfigurationViewModel.cs`, `ProfileViewModels/AddMappingPluginDialogViewModel.cs`, `Dialogs/*ViewModel.cs` (Bool/Decision/Alert/String — trivial one-property VMs) |
| Custom title bar | `Controls/WindowBar.xaml(.cs)` | — |
| Enum dropdowns | `Controls/EnumControl.xaml(.cs)` | `EnumerationExtension.cs` (XAML markup extension) |
| Misc row/item VMs | — | `ComboboxItemViewModel.cs`, `ContextMenuItem.cs` |

### Utilities/
- `TrayIcon.cs` — system tray support (background-run when the main window is closed).
- `NativeMethods.cs` — Win32 P/Invoke (window lookup, message filtering).
- `SingleGlobalInstance.cs` — enforces a single running instance of the app.
- `ResourceLoader.cs`, `DataContextBindingProxy.cs` — WPF/XAML plumbing helpers.
- `Commands/RelayCommand.cs` — the `ICommand` implementation ViewModels bind buttons to.
- `Converter/InverseBooleanConverter.cs` — XAML value converter.
- `Validators/NotEmptyValidationRule.cs`, `DecimalBindingValidator.cs`, `NumberBindingValidator.cs` — XAML input validation rules.

### Properties/ (generated, not hand-maintained)
`AssemblyInfo.cs`, `Annotations.cs`, `Resources.Designer.cs` + `Resources.resx`.

### Other
`App.xaml(.cs)` (entry point; `UnblockFiles()` here is security-sensitive, see `SECURITY.md`),
`Hidra.csproj`, `Hidra.ico`, `Resources/` (icons), `SampleData/*.xaml` (design-time sample data
for the XAML designer, not shipped).

---

## Hidra.Tests — NUnit test suite

- **Factory/** `DeviceFactory.cs` — test helper constructing fake `Device`/device lists.
- **FactoryTests/** `DeviceFactoryTests.cs` — covers the factory above.
- **ManagerTests/** — one file per `Hidra.Core` manager: `BindingManagerTests.cs` (input-validity
  gating), `PluginsManagerTests.cs` (MEF discovery + cloning), `ProcessProfileSwitcherTests.cs`
  (executable matching), `DevicesManagerTests.cs` (device-tree transform).
- **ModelTests/** — `ContextTests.cs`, `AttributeTests.cs`, `SubscriptionTest.cs`,
  `PersistenceTests.cs` (save/load round-trips), `ProfileTests.cs` (add/remove/rename/copy,
  including Guid-remap regressions).
- **PluginTests/** — `FilterPluginTests.cs`, `RemapperPluginTests.cs` (behavioral tests for every
  Remapper plugin).
- **UtilityTests/** — `FunctionTests/FunctionTests.cs`, `HelperTests/*` (one file per AxisHelper).

---

## Hidra.IOWrapper — vendored device I/O layer

A trimmed, vendored fork of [IOWrapper](https://github.com/evilC/IOWrapper). Its own
`README.md`/`CHANGELOG.md` describe pre-fork history; treat them as historical record, not Hidra's
own docs (see `AGENTS.md`). Layout under `Source/`:

- `Core/` — `IOController.cs` (loads providers), `GenericMEFPluginLoader.cs`.
- `Core Providers/Core_RawInputHook/` — the one provider Hidra ships: driver-free keyboard/mouse
  capture and output. Security-sensitive; see `SECURITY.md`.
- `ProviderInterface/` — the contract providers implement.
- `DataObjects/` — DTOs shared between providers and `Core.Core`.
- `Provider Libraries/Subscription Handling/` — `SubscriptionHandler`, `EmptyEventDictionary`
  (shared subscription bookkeeping providers can reuse).

---

## Root-level files

| File | Purpose |
|---|---|
| `Hidra.sln` | The solution: all five projects above. |
| `Directory.Build.props` | Enables .NET analyzers for every project. |
| `.editorconfig` | Formatting/naming conventions for `dotnet format` and IDEs. |
| `.github/workflows/build.yml` | CI: build + run `Hidra.Tests` on every push/PR. |
| `.github/workflows/codeql.yml` | CodeQL security scanning. |
| `.github/workflows/release.yml` | Builds and publishes a release zip on tag push. |
| `README.md` | User-facing: what Hidra is, how to get it, how to build it. |
| `AGENTS.md` | Contributor/agent guide: build & test commands, security-sensitive areas, writing style, commit conventions. |
| `CONTRIBUTING.md` | Commit message conventions, contribution process. |
| `SECURITY.md` | Why input-capture code here gets extra scrutiny, and which files that covers. |
| `CODE_OF_CONDUCT.md` | Community conduct policy. |
| `CHANGELOG.md` | What changed release-over-release, including what was removed from the original fork. |
| `docs/images/` | Screenshots embedded in `README.md`. |

---

## Section-banner convention

Some larger files in `Hidra`, `Hidra.Core`, and `Hidra.Plugins` use a plain-comment banner to
separate logical groups of members:

```csharp
// ---------------------------------------------------------------------------
// Section Name
// ---------------------------------------------------------------------------
```

This was applied only where a file actually mixed several kinds of members (fields, constructors,
public API, private helpers, ...) with no prior separation. Small single-purpose files, and files
already organized with `#region` blocks, were left as-is rather than double-banner them. Follow the
same judgment when adding new code: a 20-line class doesn't need a banner; a 150-line one that
mixes fields, setup, and three unrelated behaviors probably does.
