# UsbScope

> See every USB port on your Windows machine, what's plugged in, at what speed, and (for USB-C) what the cable and PD contract actually are.

A Windows tray + CLI utility. Tells you in plain English what each USB port on the host is, what's connected, the negotiated speed, and — on USB-C — the cable e-marker info, PD negotiation, and charging bottleneck. Inspired by macOS [WhatCable](https://www.whatcable.uk/), but scoped to *all* USB ports, not only Type-C.

**Status:** Phase 1 — userspace-only MVP. No driver yet.

## Why this exists

On macOS, WhatCable reads USB-C cable VDOs and PD PDOs directly from IOKit. On Windows that data flows through UCSI (USB Type-C Connector System Software Interface) between the embedded controller and `UcmUcsiCx.sys`, with no public userspace API. Linux has `/sys/class/typec/`; Windows doesn't.

UsbScope explores how much of that value can be delivered without a custom driver — and broadens the scope to USB-A / Mini-B / Micro-B as well, because "is my USB 3.0 disk negotiating at USB 2.0?" is a real diagnostic question we already have the data to answer.

## Coverage by phase

| Source                  | Phase 1 (current)                      | Phase 2 (driver)         | Phase 3 (UCSI 2.0+)        |
|-------------------------|----------------------------------------|--------------------------|----------------------------|
| Physical ports          | SMBIOS Type 8 (`Win32_PortConnector`)  | —                        | —                          |
| Connected device list   | `Win32_PnPEntity` + cfgmgr32           | —                        | —                          |
| Negotiated speed        | hub IOCTL (`USB_NODE_CONNECTION_INFO`) | —                        | —                          |
| Vendor/product name     | embedded `usb.ids`                     | —                        | —                          |
| USB-C Alt Mode adverts  | Billboard descriptor (planned)         | —                        | —                          |
| Cable e-marker / VDOs   | —                                      | —                        | UCSI `GET_PD_MESSAGE`      |
| PD contract / PDOs      | vendor WMI when published              | UCSI `GET_PDOS`          | —                          |
| Charging power          | battery WMI (`BatteryStatus`)          | UCSI per-port            | —                          |
| Vendor extras           | Dell `DCIM_*`, Lenovo `Lenovo_*`       | —                        | —                          |

**Note on Billboard vs. e-marker:** USB-IF Billboard descriptors expose *Alternate Mode advertisements* on a device (e.g. a USB-C dock saying "I support DisplayPort Alt Mode") — they do **not** carry cable e-marker / VDO data. The cable's e-marker info lives on the cable's PD chip and is only reachable via UCSI Discover Identity (Phase 3).

Phase 2 is a signed KMDF filter on the UCM-UCSI ACPI device exposing IOCTLs to userspace (EV signing + eventual WHQL). Phase 3 needs Windows 11 22H2 Sept Update+ on hardware that implements UCSI 2.0 — practically ~70% of modern Win 11 laptops, none of the older Intel / pre-Phoenix AMD ones.

## Architecture

```
UsbScope.sln
├── src/
│   ├── UsbScope.Core/                    cross-platform models, provider interfaces, usb.ids DB
│   ├── UsbScope.Providers.Windows/       SMBIOS / WMI / cfgmgr32 / CsWin32 interop
│   ├── UsbScope.Tray/                    WPF tray app
│   └── UsbScope.Cli/                     `usbscope.exe --json`
└── tests/
    ├── UsbScope.Core.Tests/
    └── UsbScope.Providers.Windows.Tests/
```

Providers are pluggable behind `IPortProvider`, `IPowerProvider`, `IVendorProvider`. A future `UcsiProvider` slots in for Phase 2 with no UI changes. The aggregator dedupes ports by `PortId` (stable, derived from the SMBIOS designator when available) so multiple providers can contribute data to the same port.

## Requirements

- Windows 11 23H2 or later
- x64 or ARM64
- .NET 10 Desktop Runtime (bundled in single-file publish)

## Build

```powershell
dotnet build
dotnet run --project src/UsbScope.Tray
dotnet run --project src/UsbScope.Cli -- --json
```

## License

MIT. Open source from day one.

The embedded `usb.ids` database is © its contributors, dual-licensed under GPL-2.0-or-later and 3-clause BSD; UsbScope redistributes under the BSD-3 option. See `src/UsbScope.Core/Data/README.md`.
