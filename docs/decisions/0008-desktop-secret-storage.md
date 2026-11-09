# 0008. Key storage in the Avalonia desktop

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

Extends ADR 0006 (Avalonia, item 4: keys in system stores, in memory without them). Task 040.

## Context
Avalonia has no built-in secure store (in MAUI it is `SecureStorage`). Three `ISecretStore` implementations are needed for Linux, Windows and macOS, plus a fallback: the key only in process memory if there is no system store.

Checked with proof-of-concept code (`poc/backlog/040-avalonia-secret-store/`) on Linux 2026-10-28, gnome-keyring 46 in an isolated D-Bus session:
- `org.freedesktop.secrets` (Secret Service) starts by D-Bus activation; a client on Tmds.DBus 0.95.1 (MIT) creates, finds, reads and deletes a secret through the `session` collection (checked: `read matches=True`, empty after deletion).
- The `default` collection (alias `login`) is not created in a container: the daemon asks for a password through `org.gnome.keyring.SystemPrompter`, which needs a display. On a real desktop the collection already exists, but this could not be checked that way. If `ReadAlias("default")` returns `/`, Secret Service is considered unavailable.
- Collections in the "locked" state or a request that needs a prompt (`prompt != /`) are not used in the check: they are unavailable without a UI.

## Decision
1. **Linux — Secret Service** through D-Bus, the `Tmds.DBus` 0.95.1 library (MIT, published 2026-09-04; version 0.95.2 came out 2026-10-27 and is not taken until two weeks pass). The collection is the `default` alias. Availability: connecting to the session, `OpenSession("plain")`, `ReadAlias("default")` is not `/`, a write without a prompt. Otherwise the key is in memory.
2. **Windows — DPAPI** (`System.Security.Cryptography.ProtectedData` 10.0.12, MIT, `DataProtectionScope.CurrentUser`). The file `secrets/<name>.bin` in the data folder (`DesktopAppPaths`), the file holds only encrypted bytes. The file name is checked: only Latin letters, digits, `_`, `-`, `.`.
3. **macOS — Keychain** through the `/usr/bin/security` utility: writing with `add-generic-password -U` with the password in hex (`-X`) passed through the stdin of the interactive `security -i` mode, so that the key does not get into the process arguments; reading with `find-generic-password -w`. **Not verified** (no macOS machine, see task 040).
4. **Fallback — process memory.** `DesktopSecretStore` chooses the backend on the first availability check (`IsPersistentAsync`, the result is cached). If there is no backend or it is unavailable, the values live in memory until the app is closed.
5. **Interface for the key page.** Presentation gets `ISecretPersistence` with `Task<bool> IsPersistentAsync(CancellationToken)`. An asynchronous method instead of the property from the plan: the Secret Service check is a D-Bus call and cannot be done in a constructor or on the UI thread. MAUI implements `true` (SecureStorage always exists).

The key is never written to disk in plain text: neither into DPAPI files, nor into the log, nor into process arguments (see item 3).

## Alternatives
- **libsecret through P/Invoke:** one C library, but needs glib and our own pointer wrapping; the check would reach the same service, only harder.
- **Calling `secret-tool` as a process:** no library needed, but nothing works on a machine without `libsecret-tools`, and the password passes through the arguments or the stdin of an external program.
- **KWallet directly:** covers KDE, but Secret Service works there too (KWallet provides it), and a separate backend is extra code.
- **Encrypting a file with a master password on Linux:** works without the service, but the password must be asked on every start; the owner chose memory as the fallback (ADR 0006).

## Consequences
- Easier: one `ISecretStore` for all desktop OSes, the key does not appear in files, the key page knows whether the key will be kept.
- Harder: on Linux without gnome-keyring or KWallet the key must be entered after every start; on macOS and in part of the Windows scenario the check is only by documentation ("not verified" statuses in task 040); Secret Service does not start in CI, the Linux integration test is skipped.
- Dependencies: `Tmds.DBus` (only the Avalonia head), `System.Security.Cryptography.ProtectedData` (can be removed if the Windows build uses only MAUI).
