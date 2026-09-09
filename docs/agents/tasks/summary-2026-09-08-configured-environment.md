# Consistent framework environment selection

## Objective / acceptance

Use the host's resolved environment consistently for startup and offline framework settings, with isolated configuration lifetimes and compatible trusted-host handling.

## What changed

Added `FwConfig.setDefaultOverrideName` as a lifetime-scoped initialization input. Startup provides the builder's resolved environment before normal or CLI work. Unconfigured callers use trimmed ASPNETCORE_ENVIRONMENT, then DOTNET_ENVIRONMENT. Changing the selection invalidates that scope's host cache.

## Changed contracts

Existing methods keep their signatures. The built-in host now determines environment precedence, so conflicting environment variables can select different overrides than previously. Settings documentation and the dated changelog describe this behavior. No schema changes.

## Commands used / verification

- `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~ConfiguredEnvironmentTests|FullyQualifiedName~FwConfigTests|FullyQualifiedName~FwDependencyTests|FullyQualifiedName~DevCliTests' --verbosity quiet`: 32 passed.
- Tests cover explicit selection, trimming, both fallback variables, empty fallback, nested scope restoration, host override behavior and rejected untrusted hosts. Existing configuration, dependency and CLI checks are included. A production-entry check runs Program.Main in CLI help mode with conflicting host environment variables and verifies the resulting framework environment; it exits before DB or scaffolding work.

## Risks / follow-ups

Set the explicit environment during initialization before concurrent requests. Do not change this initialization input while dependent FW instances are in use. No live application or shared database was started.
