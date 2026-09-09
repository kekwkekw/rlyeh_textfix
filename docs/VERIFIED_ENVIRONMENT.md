# Verified environment and golden binaries

The final clean-install test succeeded with the following environment:

- Game process: `rlyehshoujotaix_cl.exe`
- Unity: `6000.3.5f2`
- Windows x64
- BepInEx: `6.0.0-be.785`
- BepInEx commit: `6abdba47eeebe08552282e7a58ef0f4a9ab60b62`
- .NET runtime: `6.0.7`
- XUnity.AutoTranslator: `5.6.1`
- XUnity.ResourceRedirector: `2.1.0`
- RlyehTextFix: `1.1.0`

Verified golden binary hashes:

| File | Version | SHA-256 |
|---|---|---|
| `RlyehTextFix.dll` | 1.1.0.0 | `b3201de040ce0d64ea0198bcee7a54379699d8d6908b5a13ec4e1d95acc74444` |
| `XUnity.AutoTranslator.Plugin.Core.dll` | 5.6.1.0 | `23ab4057483fc98dce4df390809e554e6ce1f0b3ac3fe87145eefd1972903611` |
| `Il2CppInterop.Runtime.dll` | 1.5.3.0 | `cca76f2225b8ae2bed0ed3fdec0a4deba072f214fc89616c1c93b6da86c5ccba` |
| `notoserifkr_sdf` | UnityFS / Unity 6000.3.5f2 | `810e62255b2caddf93773bdf5127aec698dba1a25ae65f9144a22c6e95bed634` |

The clean-install log confirmed:

- `RlyehTextFix 1.1.0` loaded.
- Story hooks installed `2/2`.
- `TranslationTagMerge=True`.
- XUnity RichTextParser patch installed against assembly `5.6.1.0`.
- `notoserifkr_sdf` loaded as the TextMesh Pro fallback font.

The repository does not claim byte-for-byte reproducibility of the historical
XUnity build. The hashes above identify the binaries that were actually tested.
