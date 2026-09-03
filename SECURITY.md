# Security Policy

## What Hidra actually does, and why that matters here

Hidra remaps input at a low level: it captures raw keyboard and mouse events
and emits synthetic keyboard/mouse events back out, all driver-free via the
`Core_RawInputHook` provider. Those are the same primitives a keylogger would
use. The only difference is intent, and what the code does with the data
once it has it.

That means this project is held to a higher bar than a typical utility. A
change that looks like a small refactor to `Core_RawInputHook.cs` or
`SingleGlobalInstance.cs` deserves a closer read than the diff stat suggests,
specifically checking for:

- Any new network call, file write, or IPC that captured input could flow
  through, however indirect
- Logging of raw input beyond what's needed for the active remap (and never to
  a location outside the running process)
- Anything that touches the NTFS Zone.Identifier / Mark-of-the-Web stream
  outside of `UnblockFiles()`'s existing, narrowly-scoped use on Hidra's own
  install directory
- New mutex, named pipe, or shared-memory objects with broad ACLs
  (`WorldSid`, `Everyone`) that aren't already justified by existing
  single-instance/IPC code
- Obfuscated, minified, or otherwise hard-to-review code in a PR: a project
  like this should be trivially auditable end to end

None of this means the existing capabilities are unsafe; they're exactly
what a keyboard and mouse remapper needs. It means the project's actual
defense is "every line is readable and accounted for," so keeping that true
is a requirement, not a suggestion.

## Reporting a Vulnerability

If you find a security issue (a way for captured input to leave the machine,
a privilege-escalation path, a way for one profile/plugin to affect another
user's session, or anything in the categories above), please report it
privately rather than opening a public issue:

- Preferred: open a
  [GitHub Security Advisory](https://github.com/dayanez/Hidra/security/advisories/new)
  on this repository. This is private between you and the maintainers until a
  fix is ready.
- If that's not available to you, contact a maintainer directly through their
  GitHub profile rather than filing a public issue.

Please include what you found, the affected file(s)/version, and, if you
have one, a minimal way to reproduce it. This is a young, actively-changing
fork with no formal release cadence yet, so fixes land on `master` as soon as
they're ready rather than through a backport process.

## Responsible Use

Hidra is built for remapping your own input devices on your own machine:
accessibility, ergonomics, and cross-device control. Contributions intended
to facilitate unauthorized surveillance (capturing another user's input
without their knowledge), credential theft, or evading anti-cheat/DRM
protections in ways that violate another service's terms are out of scope
for this project and will not be merged.
