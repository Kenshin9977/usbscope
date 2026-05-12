# UsbScope

> See every USB port and device on your Windows machine. On hardware that exposes USB-PD, see what the cable and PD contract actually are too.

A Windows tray + CLI utility. Tells you in plain English what each USB port on the host is, what's connected, the negotiated speed, and — on supported hardware — the cable e-marker, PD negotiation, and Alt Mode advertisements. Inspired by macOS [WhatCable](https://www.whatcable.uk/), but broader: it covers USB-A and Mini/Micro-B too because "is my USB 3.0 disk negotiating at USB 2.0?" is a real diagnostic question on every PC.

**Status:** Phase 1, 1.5 and 3 land. Phase 2 driver is scaffolded; the IOCTL surface is defined but the UCM-UCSI filter logic is the next iteration.

## What it shows (and where the data comes from)

| Field                          | Source on Windows                        | Where it works                 |
|--------------------------------|------------------------------------------|--------------------------------|
| Physical ports list            | SMBIOS Type 8 (`Win32_PortConnector`)   | Every PC with a BIOS           |
| Connected devices              | `Win32_PnPEntity` + cfgmgr32             | Every Windows machine          |
| Negotiated speed (LS/FS/HS/SS) | Hub IOCTL `GET_NODE_CONNECTION_INFO_EX` | Every Windows machine          |
| Vendor + product names         | Embedded `usb.ids` (Linux DB)            | Every Windows machine          |
| USB-C Alt Mode adverts         | BOS Billboard descriptor (hub IOCTL)     | Every Windows machine *if* the connected device advertises Billboard |
| PD contract (negotiated PDO)   | UCSI via Phase 2 driver                  | Only UCSI-capable hardware     |
| Cable e-marker / VDOs          | UCSI Discover Identity via Phase 2 driver| Only UCSI-capable hardware     |
| Per-port partner type, role    | UCSI                                     | Only UCSI-capable hardware     |
| Charging power (host-wide)     | `BatteryStatus` WMI                      | Every Windows laptop with battery |
| Vendor extras (Dell/Lenovo)    | `DCIM_*` / `Lenovo_*` WMI                | When those WMI namespaces exist|

## Hardware compatibility (honest)

USB-PD / UCSI data is only as good as the hardware. Microsoft mandates UCSI on **Windows 11–certified laptops that ship with USB-C**, which makes laptops the primary target for the full feature set:

| Category                                              | UCSI typically exposed? | Coverage |
|-------------------------------------------------------|-------------------------|----------|
| Windows 11 laptops with USB-C (Surface, ThinkPad, XPS, Framework, …) | ✅ Microsoft cert mandates it | ~85–90% of recent Win11 laptops |
| Intel desktop boards Z690/Z790 with integrated TB4    | ✅ Most                 | High-end Intel desktops        |
| AM5 boards with explicit USB4 / TB (X670E with WiFi/TB) | ✅ Some, model-dependent | Enthusiast AM5                |
| AM5 entry/mid (B650, A620)                            | ❌ Usually no           | Most consumer AM5              |
| AM4 (X570 / B550 / X470 / B450 / A520 / A320)         | ❌ Almost never         | Huge install base — not covered for Phase 2/3 |
| Intel H/B-series desktop (H610, B660, B760)           | ❌ Rarely               | Budget Intel desktops          |
| Pre-2019 hardware                                     | ❌ UCSI didn't exist    | Older PCs                      |

**Phase 1 and 1.5 work on 100% of Windows 10 / 11 machines** — that's the floor. Phase 2 and 3 ride on top when the hardware cooperates. If your USB-C ports come through a programmable PD controller exposed via ACPI (most laptops, some desktops), you get the full picture. If they're "data + 5 V" passive Type-C receptacles on a budget motherboard, you get Phase 1 / 1.5 only — which is still the majority of the useful diagnostic value for non-charging use cases.

### Quick check on your machine

Open Device Manager → "Universal Serial Bus controllers" or "USB Connector Managers". If you see a **USB Connector Manager** entry that's *running* (not stopped), Phase 2/3 will work once the driver lands. If those entries are missing or in Stopped state, your hardware doesn't expose UCSI — Phase 1 / 1.5 are still useful.

## Architecture

```
UsbScope.sln
├── src/
│   ├── UsbScope.Core/                    cross-platform models, provider interfaces,
│   │                                     usb.ids DB, USB-PD VDO decoder, Billboard parser
│   ├── UsbScope.Providers.Windows/       SMBIOS / WMI / cfgmgr32 / hub IOCTL
│   ├── UsbScope.Providers.Ucsi/          IOCTL client for the kernel driver
│   ├── UsbScope.Driver/                  KMDF kernel driver scaffold (build separately)
│   ├── UsbScope.Tray/                    WPF tray app
│   └── UsbScope.Cli/                     `usbscope.exe --json`
└── tests/
    ├── UsbScope.Core.Tests/              models, usb.ids, VDO decoder, Billboard parser
    └── UsbScope.Providers.Windows.Tests/ snapshot aggregator with fakes
```

Providers are pluggable behind `IPortProvider`, `IPowerProvider`, `IVendorProvider`. The aggregator dedupes ports by `PortId` (stable, from SMBIOS designator when available) so multiple providers can contribute data to the same port. `UcsiPortProvider` skips itself silently when the driver isn't installed.

## Requirements

- Windows 11 23H2 or later for Phase 2/3; Windows 10 1809+ for Phase 1/1.5
- x64 or ARM64
- .NET 10 Desktop Runtime (bundled in single-file publish)
- For Phase 2/3 only: hardware with UCSI-exposed USB-C ports (see compat matrix above)

## Build

```powershell
dotnet build
dotnet run --project src/UsbScope.Tray
dotnet run --project src/UsbScope.Cli -- --json
```

Building the kernel driver (Phase 2) needs the WDK, the WDK VS extension, and MSVC Spectre-mitigated libs — see [`src/UsbScope.Driver/README.md`](src/UsbScope.Driver/README.md).

## License

MIT. Open source from day one.

The embedded `usb.ids` database is © its contributors, dual-licensed under GPL-2.0-or-later and 3-clause BSD; UsbScope redistributes under the BSD-3 option. See `src/UsbScope.Core/Data/README.md`.
