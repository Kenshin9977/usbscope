# WhatCable for Windows

> Tells you what your USB-C cable actually is, what's negotiated, and what's bottlenecking charging — on Windows.

A Windows tray utility inspired by [WhatCable](https://www.whatcable.uk/) (macOS). Decodes USB-C cable e-marker data, PD negotiation, and charging bottlenecks in plain English.

**Status:** Phase 1 — userspace-only MVP. No driver yet.

## Why this exists

macOS exposes USB-C cable VDOs and PD PDOs directly to userspace via IOKit, which is how WhatCable works. Windows doesn't have an equivalent: the data flows through UCSI (USB Type-C Connector System Software Interface) between the embedded controller and `UcmUcsiCx.sys`, but no public userspace API surfaces it. Linux has `/sys/class/typec/`; Windows doesn't.

This project explores how much of WhatCable's value can be delivered on Windows without a custom driver, and lays the groundwork for a signed UCSI filter driver if Phase 1 falls short.

## Phasing

### Phase 1 — Userspace MVP (current)
Pure .NET 8, no custom driver. Pulls:
- USB-C port topology via SetupAPI / WMI (`Win32_USBHub`, `Win32_USBController`)
- Negotiated speeds per device
- Charging power via battery WMI classes (`BatteryStatus`, `MSBatteryClass`)
- Vendor-specific PD info where available (Dell `DCIM_*`, Lenovo `Lenovo_*` WMI namespaces)
- Charge-source bottleneck inference

Goal: deliver ~60% of WhatCable's value with zero driver work. Validation milestone before committing to Phase 2.

### Phase 2 — UCSI access via signed KMDF filter driver
Attach a filter to the UCM-UCSI ACPI device, expose IOCTLs to userspace. Surfaces PDOs, `GET_CABLE_PROPERTY`, `GET_CONNECTOR_STATUS`. Requires EV code signing and eventual WHQL.

### Phase 3 — Discover Identity / e-marker VDO decode
Requires UCSI 2.0+ (Windows 11 22H2 Sept Update+). Realistic coverage: ~70% of recent Windows 11 hardware. Intel 11th gen and pre-Phoenix AMD will not work.

## Architecture

```
WhatCable.sln
├── src/
│   ├── WhatCable.Core/      class library — models, provider interfaces
│   ├── WhatCable.Tray/      WPF tray app
│   └── WhatCable.Cli/       console app, `whatcable.exe --json`
└── tests/
    └── WhatCable.Core.Tests/
```

Providers are pluggable behind `IPortProvider`, `IPowerProvider`, `IVendorProvider` so a `UcsiProvider` can slot in for Phase 2 without rewriting the UI.

## Requirements

- Windows 11 23H2 or later
- x64 or ARM64
- .NET 8 Desktop Runtime (bundled in single-file publish)

## Build

```powershell
dotnet build
dotnet run --project src/WhatCable.Tray
dotnet run --project src/WhatCable.Cli -- --json
```

## License

MIT. Open source from day one.
