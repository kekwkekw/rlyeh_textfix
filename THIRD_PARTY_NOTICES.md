# Third-Party Notices

The root MIT License applies only to project-original RlyehTextFix code, helper scripts,
and project-original documentation. Patch material derived from third-party open-source
projects remains under the applicable upstream license and is not relicensed by the root MIT License.

## XUnity.AutoTranslator

- Upstream: https://github.com/bbepis/XUnity.AutoTranslator
- Tested upstream: v5.6.1 / `7f1f3b9e8fc7d93a97734773804ba9c8fdf57714`
- License: MIT
- Local modification: `patches/XUnity.AutoTranslator-v5.6.1/PluginLoader.patch`
- License copy: `licenses/XUnity-AutoTranslator-MIT.txt`

## Il2CppInterop

- Upstream: https://github.com/BepInEx/Il2CppInterop
- Tested upstream: v1.5.3 / `dbda1cb353b0f4253345dc45136d170b9e50a5a0`
- License expression: `LGPL-3.0-only`
- Local modification: `patches/Il2CppInterop-v1.5.3/InjectorHelpers.patch`
- Canonical license text: https://github.com/BepInEx/Il2CppInterop/blob/dbda1cb353b0f4253345dc45136d170b9e50a5a0/LICENSE
- Local provenance notice: `licenses/Il2CppInterop-LGPL-3.0-only.NOTICE.md`

This source repository publishes only the patch and exact upstream provenance; it does not
redistribute a modified Il2CppInterop binary. If such a binary is redistributed separately,
the distributor must satisfy the applicable LGPL-3.0-only source, notice, and license-copy
requirements for that binary.

## BepInEx

- Upstream: https://github.com/BepInEx/BepInEx
- Tested build: 6.0.0-be.785 / `6abdba47eeebe08552282e7a58ef0f4a9ab60b62`
- License expression: `LGPL-2.1-only`
- This source repository does not redistribute BepInEx binaries.

## Unity Doorstop

- Upstream: https://github.com/NeighTools/UnityDoorstop
- Version used by the tested BepInEx build: 4.5.0
- License: LGPL-2.1
- This source repository does not redistribute Doorstop binaries.

## Noto Serif CJK KR

- Upstream: https://github.com/notofonts/noto-cjk
- Source family: Noto Serif CJK KR Bold
- License: SIL Open Font License 1.1
- The tested TMP asset bundle is named `notoserifkr_sdf` and contains a TMP
  asset named `RlyehNotoSerifKR SDF`.
- The font bundle is not stored in this source repository. If distributed
  as a release asset, include the OFL license and notice files.

## Game / Unity / TextMesh Pro

No ownership is claimed over the game, Unity, TextMesh Pro, game text, game
assets, executable files, or generated game assemblies. They are not included
in this source repository.
