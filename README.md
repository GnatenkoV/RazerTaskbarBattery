# Razer Taskbar Battery

A lightweight **C# .NET 9 WPF** application that shows Razer-device battery levels in the Windows notification area. It uses Windows HID (`hid.dll` and `setupapi.dll`) and optional Razer Synapse 3/4 log fallback. This project is mainly for a personal usage.

## Screenshots

### Settings

Choose the battery source and tray device, adjust refresh intervals, and control startup, percentage display, and the charging indicator.

![Settings window with battery monitoring, refresh schedule, appearance, and startup options](images/settings.png)

### Tray menu

Right-click the tray icon to view battery status, select a device, refresh readings, or open Settings and About. Double-click the tray icon to open Settings directly.

![Tray context menu and device-selection submenu](images/context-menu.png)

## Battery icon styles

The tray icon fills with the current charge level: **green at 41–100%**, **yellow at 21–40%**, and **red at 0–20%**. Each pair below shows the normal icon followed by its charging variant.

| 10% | 30% | 50% | 70% | 90% | Numeric | Unknown |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| ![10% battery, normal and charging](images/battery-10.png) | ![30% battery, normal and charging](images/battery-30.png) | ![50% battery, normal and charging](images/battery-50.png) | ![70% battery, normal and charging](images/battery-70.png) | ![90% battery, normal and charging](images/battery-90.png) | ![78% numeric battery, normal and charging](images/battery-numeric.png) | ![Unknown battery level](images/battery-unknown.png) |

Enable **Show battery percentage** in Settings for the numeric style. An unavailable reading displays a gray icon with a question mark. **Show charging indicator** controls the charging bolt; it appears at icon sizes of 24 pixels and above.

Charging status is queried directly over HID using the read-only [OpenRazer power commands](https://github.com/openrazer/openrazer/blob/master/driver/razerchromacommon.c). If the device does not provide a validated charging reply, Automatic mode can use Synapse's charging status instead. A known HID charging state takes precedence over Synapse logs.

## Build and publish

Requires Windows 10/11 (x64) and the .NET 9 SDK or Visual Studio with .NET desktop development workload. On your build machine:

```powershell
dotnet publish .\RazerTaskbarBattery\RazerTaskbarBattery.csproj -c Release -p:PublishProfile=SingleFile
```

You can also select the **SingleFile** publish profile from Visual Studio. The executable will be created at `RazerTaskbarBattery\publish\RazerTaskbarBattery.exe`. It is a self-contained x64 **single-file host** and does not require a separately installed .NET runtime. The OS provides the HID and system-tray APIs.

### Existing data and battery protocol

The migration preserves the previous HID reader, adaptive battery-query detection, OpenRazer device catalog, Synapse 3/4 parsers, settings store and dynamic battery icon drawing. `AppSettings` and the current settings JSON remain compatible. `Version.props` supplies the assembly version displayed in About.

## Diagnostics and limitations

Direct HID isn't universal: different Razer devices can use different report formats and read transaction IDs. Unknown vendor-1532 devices are tested only with known read-only battery-query variants, and responses must pass checksum and status validation. For unsupported devices, Synapse log fallback may work.

Diagnostics may contain Windows HID paths with identifying device details; review before sharing.
