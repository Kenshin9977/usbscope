# USB IDs database

`usb.ids.gz` is the [Linux USB ID Repository](http://www.linux-usb.org/usb.ids), a community-maintained mapping from USB Vendor IDs / Product IDs to vendor and product names.

- Source: http://www.linux-usb.org/usb.ids
- License: dual-licensed under GPL-2.0-or-later and 3-clause BSD; this project redistributes it under the BSD-3 option.
- Refresh: regenerate by re-downloading and gzipping. The file is stable over weeks; bumping every few months is enough.

The embedded resource is loaded lazily by `UsbScope.Core.UsbIds.UsbIdsDatabase` on first lookup.
