# AGENTS.md

Guidance for AI coding agents and contributors working in this repository.

## What this repository is

An **unofficial** C# (.NET) binding for the official Noetix Bumi robot SDK (`noetix_sdk_bumi`, SDK 3.1.0).
It is not affiliated with Noetix Robotics. See [README.md](README.md) for the architecture.

```text
Noetix.Sdk.Bumi (C#, src/)  ->  P/Invoke  ->  libnoetix_bumi_c.so / libnoetix_bumi_low_c.so (C++, native/)  ->  official SDK
```

## Safety rules (highest priority)

This code can move a 17 kg humanoid robot.

- **Never run anything that sends motion commands** (`PublishCmd`, `LowController`, dances, standing up) unless the user
  explicitly asks for it in that conversation and confirms the robot is on a gantry with the area cleared.
- Default to **read-only** work on a robot: status subscriptions, listing services, reading devices.
- **Never connect to or probe the motion-control board (192.168.55.102).** Only the user compute board (192.168.55.101)
  is open to users.
- Do not stop, disable or restart robot services (for example `noetix-video-capture`) without telling the user first.
- Never use `LowController` for experiments; it is exported only for parity with the official SDK.
- Do not remove validation in `noetix_high_publish_cmd` (finite values within [-1, 1], known action).

## Secrets and personal data

- Never write passwords, tokens, keys, default robot credentials or personal data into code, docs, tests or commits.
  Refer to "the account in the official documentation" instead.
- Do not commit machine-specific paths, IP addresses of private networks (other than the official fixed robot addresses),
  or logs captured from a robot without reviewing them.

## Official SDK and licensing

- The official SDK (`libnoetix_sdk.a`, CycloneDDS libraries, headers) is **never committed or redistributed** from here.
  `.gitignore` excludes `*.so`, `*.so.*`, `*.a`, `third_party/` and `noetix_sdk_bumi/`; keep it that way.
- Built native libraries embed the official static library, so they are not published either.
- This repository is MIT-licensed. Keep the copyright header style used in `native/` sources.

## Naming and API conventions

- **Mirror the official SDK**: class names `HighController`, `LowController`, `MediaController`; method names are the
  official snake_case names converted to PascalCase (`publish_cmd` -> `PublishCmd`).
- Enum **numeric values must equal the official ones** (they are passed to native code). `ControlCmd` order is fixed.
- Keep official semantics: methods returning `bool` in the official API return `bool` (for example `Init()`); do not turn
  them into exceptions. Native failures elsewhere surface as `NoetixSdkException`.
- Callbacks run on SDK threads: keep them short, never let exceptions escape into native code, and never keep pointers
  passed to a callback after it returns.

## Changing the native ABI

Any new or changed function touches all of these; the contract tests fail if they drift:

1. `native/include/noetix_bumi_c.h` or `noetix_bumi_low_c.h` (and `static_assert` struct sizes)
2. the matching `native/src/*.cpp`
3. `src/Noetix.Sdk.Bumi/Native/NativeMethods.cs` (or `NativeLowMethods.cs`) and `Types/`
4. the public controller in `src/Noetix.Sdk.Bumi/Controllers/`
5. `NOETIX_BUMI_C_API_VERSION` if the change is not backwards compatible
6. `docs/verification.md` (what is and is not verified)

Constraints discovered the hard way:

- `HighController` and `LowController` **cannot be linked into the same library** (duplicate global symbols), hence two
  native libraries. A process uses only one controller.
- The official static library holds **GCC 11 LTO bytecode**: build the native layer with GCC 11 (Ubuntu 22.04).
- `Environment.SetEnvironmentVariable` does not reach native code on Linux; use `NoetixSdk.SetDdsConfig`.
- Native libraries are 64-bit only (aarch64, x86_64).

## Build and test

Run from the repository root.

```bash
dotnet test Noetix.Sdk.Bumi.slnx                      # contract tests; also works on Windows, loads no native code

# Native layer (Linux, GCC 11), needs a checkout of the official SDK
cmake -S native -B native/build -DNOETIX_SDK_ROOT=/path/to/noetix_sdk_bumi
cmake --build native/build -j

# Native smoke tests (Linux, no robot needed)
NOETIX_BUMI_NATIVE_TESTS=1 LD_LIBRARY_PATH=native/build dotnet test
```

- `TreatWarningsAsErrors` is on; keep the build warning-free.
- Tests must not require a robot. Anything that needs real hardware belongs in `samples/` or a manual checklist in
  `docs/`, never in the default test run.
- Do not claim something works on a robot or on aarch64 unless it was actually run there; record results in
  `docs/verification.md`.

## Documentation

- Language: README, code comments and `docs/verification.md` are English. `docs/edu-pro-research.md` is Chinese.
- Mark claims in docs as **official** (stated by Noetix docs/source), **third-party**, or **inference** (needs
  verification), as `docs/edu-pro-research.md` does.
- Keep `README.md` and `docs/` in sync with behaviour changes (for example blocking `Init()`).

## Git conventions

- English commit messages in [Conventional Commits](https://www.conventionalcommits.org/) style
  (`feat:`, `fix:`, `docs:`, `test:`, `chore:`, optionally with a scope such as `feat(native):`); subject in the
  imperative, no trailing period, short body explaining why when useful.
- Split work into logical commits (license/config, native, C# types, controllers, tests, docs).
- Do **not** add `Co-authored-by` trailers for AI tools; commits are authored by the maintainer.
- Never rewrite published history or force-push unless the maintainer asks.
- Do not commit `bin/`, `obj/`, `native/build/` or any native binaries.

## Maintainer

Gil Zhang <gil.zhang.dev@outlook.com>
