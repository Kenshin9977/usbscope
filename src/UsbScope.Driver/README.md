# UsbScope kernel driver (Phase 2 scaffold)

A KMDF software-only driver that exposes a control device (`\\.\UsbScope`) and a small IOCTL surface to its userspace client. Currently a skeleton: the IOCTLs reply but the data they return is stubbed. The next iteration attaches it as an upper filter on the UCM-UCSI ACPI client device to populate `IOCTL_USBSCOPE_GET_UCSI_STATE` from real intercepted UCSI traffic.

## What's here

| File                 | Role                                                                 |
|----------------------|----------------------------------------------------------------------|
| `Public.h`           | IOCTL contract shared with the userspace client                       |
| `Driver.c`           | DriverEntry, EvtDriverDeviceAdd, EvtIoDeviceControl                  |
| `UsbScope.Driver.vcxproj` | KMDF project file, targets x64 + ARM64, Spectre-mitigated        |

## Prerequisites

The driver targets the **10.0.22621.0** SDK/WDK (pinned in the `.vcxproj`). Run
each `winget` line from an **elevated** PowerShell.

1. **Visual Studio 2022** — Community, Professional or Enterprise. **Not Build
   Tools**: the WDK build integration ships as a VS extension, and Build Tools
   does not support extensions (`NoApplicableSKUsException`). Install with the
   C++ workload, the 22621 SDK, and the Spectre-mitigated libs (without the last,
   the link fails with `LNK1104: cannot open file 'libcmt.lib'`):
   ```powershell
   winget install --id Microsoft.VisualStudio.2022.Community --override `
     "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.NativeDesktop --includeRecommended --add Microsoft.VisualStudio.Component.Windows11SDK.22621 --add Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre"
   ```
2. **Windows Driver Kit (WDK) 22621**:
   ```powershell
   winget install Microsoft.WindowsWDK.10.0.22621
   ```
3. **Wire the WDK into Visual Studio.** The winget WDK does not integrate itself
   with a current VS: its standalone `WDK.vsix` version-gates against VS 17.14+
   (`the extension version is lower than the version requested`), and it leaves
   the `WDKContentRoot` registry value empty (without it the compile fails with
   `C1083: 'ntddk.h' not found`). Both are fixed by copying the extension's
   MSBuild targets into VS and setting the root — run once, elevated:
   ```powershell
   $vsix = "${env:ProgramFiles(x86)}\Windows Kits\10\Vsix\VS2022\10.0.22621.0\WDK.vsix"
   $vs   = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
   $tmp  = "$env:TEMP\wdkvsix"; Remove-Item $tmp -Recurse -Force -EA SilentlyContinue
   Add-Type -AssemblyName System.IO.Compression.FileSystem
   [IO.Compression.ZipFile]::ExtractToDirectory($vsix, $tmp)
   Copy-Item (Join-Path $tmp '$MSBuild\*') "$vs\MSBuild" -Recurse -Force
   'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots',
   'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots' | ForEach-Object {
     New-ItemProperty $_ -Name WDKContentRoot -Value "${env:ProgramFiles(x86)}\Windows Kits\10\" -PropertyType String -Force | Out-Null }
   ```
   > CI / headless alternative: the [EWDK](https://learn.microsoft.com/windows-hardware/drivers/develop/using-the-enterprise-wdk)
   > is a self-contained build environment (no VS, no VSIX, no registry). Mount it,
   > run `LaunchBuildEnv.cmd`, then the same `msbuild` line below.

## Build (developer flow)

```powershell
cd src\UsbScope.Driver
msbuild UsbScope.Driver.vcxproj /p:Configuration=Release /p:Platform=x64
```

Output is `x64\Release\UsbScope.Driver.sys` — **unsigned**. Build-time signing is
off in the project on purpose (recent `signtool` requires `/fd`, which the WDK's
older signing targets don't pass, and test-cert generation depends on cert-store
state — both make it environment-fragile and pointless for a CI compile-check).
Sign explicitly for local install (next section) or for distribution (below).

## Install (dev / test signing)

The build is unsigned, so x64 Windows won't load it as-is. Test-sign it, trust
the cert, and turn on test signing — all one-time except the signing itself:

```powershell
# 1. a self-signed code-signing cert (once)
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=UsbScope Test" `
          -CertStoreLocation Cert:\CurrentUser\My -FriendlyName "UsbScope Test Cert"
# 2. sign the driver — /fd is required by current signtool
$signtool = (Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" |
             Sort-Object FullName | Select-Object -Last 1).FullName
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint x64\Release\UsbScope.Driver.sys
# 3. trust it so kernel signature checks pass (once)
Export-Certificate -Cert $cert -FilePath "$env:TEMP\UsbScopeTest.cer" | Out-Null
Import-Certificate -FilePath "$env:TEMP\UsbScopeTest.cer" -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
Import-Certificate -FilePath "$env:TEMP\UsbScopeTest.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
# 4. enable test signing, then REBOOT (a "Test Mode" watermark appears)
bcdedit /set testsigning on
```

The driver installs as a kernel service, no INF needed (it has no PnP device to bind to):

```powershell
sc.exe create UsbScope binPath= "C:\path\to\UsbScope.sys" type= kernel start= demand
sc.exe start UsbScope
```

Verify it loaded:

```powershell
sc.exe query UsbScope
Get-CimInstance Win32_SystemDriver -Filter "Name='UsbScope'"
```

Userspace then opens `\\.\UsbScope` via `CreateFile`.

## Uninstall

```powershell
sc.exe stop UsbScope
sc.exe delete UsbScope
```

## Production signing (for distribution outside dev machines)

1. Acquire an **EV code signing certificate** (Certum SimplySign EV ≈ €140/yr, DigiCert ≈ €400/yr — both work). Standard non-EV certs **do not** work for kernel-mode on Windows 10 1607+.
2. Sign `UsbScope.sys` with the EV cert.
3. Submit to the **Microsoft Hardware Dev Center / Partner Center** for attestation signing. Microsoft re-signs with its own cert. Free, automated, takes hours not days.
4. Distribute the re-signed `.sys`. Users no longer need test signing mode.

WHQL certification (HLK testing, Windows Update distribution) is a separate, heavier process — only needed for very wide rollout.

## Architecture roadmap

This driver is intentionally minimal in its first cut. The expansion path:

1. **Current (scaffold)**: control device + stub IOCTL handlers.
2. **Filter attach**: register as an upper filter on UCM-UCSI ACPI client devices via INF (`AddReg ... UpperFilters`). Intercept I/O, snoop UCSI command responses (`GET_CONNECTOR_STATUS`, `GET_PDOS`, `GET_CABLE_PROPERTY`), cache in a per-connector struct.
3. **State exposure**: `IOCTL_USBSCOPE_GET_UCSI_STATE` returns the live cache.
4. **Discover Identity** (Phase 3): driver issues its own `GET_PD_MESSAGE` commands for the SOP' Discover Identity response and returns the raw VDOs to userspace, which decodes them via the `VdoDecoder` in `UsbScope.Core`.

The control-device-only design means the driver is useful even on machines whose UCM-UCSI stack we haven't filtered (the GET_UCSI_STATE returns zero connectors, GET_DISCOVER_IDENTITY returns "no response"). Userspace handles those cases gracefully.
