# XUnity.AutoTranslator upstream provenance

- Upstream: `bbepis/XUnity.AutoTranslator`
- Version: `v5.6.1`
- Commit: `7f1f3b9e8fc7d93a97734773804ba9c8fdf57714`
- Modified file: `src/XUnity.AutoTranslator.Plugin.Core/PluginLoader.cs`
- License: MIT

The distributed compatibility DLL was verified as:

- AssemblyVersion: `5.6.1.0`
- AssemblyInformationalVersion: `5.6.1`
- SHA-256: `23ab4057483fc98dce4df390809e554e6ce1f0b3ac3fe87145eefd1972903611`
- Build configuration metadata: `Debug`

The modification replaces the generic IL2CPP `AddComponent<T>()` call with the
non-generic `AddComponent(Il2CppSystem.Type)` path and casts the result back to
`AutoTranslatorProxyBehaviour`.
