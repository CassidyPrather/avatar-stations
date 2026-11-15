# Wirenook Avatar Stations

Seats and doorways for VRChat avatars: carry a friend along on a cloud, or
drop a portal that brings whoever steps through it to you.

Package: `net.wirenook.avatar-stations`. Needs the VRChat Avatars SDK 3.10.6
or newer, [Modular Avatar](https://modular-avatar.nadena.dev/),
[`net.wirenook.assets`](https://github.com/CassidyPrather/wirenook-assets)
and [`net.wirenook.world-sync`](https://github.com/CassidyPrather/world-sync).

## What is in it

Drag a prefab onto an avatar; Modular Avatar adds its menu entry, parameter
and animator at build time.

| Prefab | Menu | What it does | Stations | Synced bits |
| --- | --- | --- | --- | --- |
| `Wirenook Carry Seat` | Carry Seat | A cloud floats beside you; a friend clicks it to sit and is carried along. | 1 | 1 |
| `Wirenook Teleporter` | Catch-up Portal | A doorway appears a step in front of you and stays there. Anyone who clicks it arrives just behind you, wherever you have gone since. | 1 | 67 |
| `Wirenook Stations` | Stations ▸ | Both of the above behind one menu entry. | 2 | 68 |

### Carry seat

The cloud is not attached to you rigidly. It chases a point beside you
through a constraint with smoothing, so your starts, stops and turns reach
your passenger eased out instead of as jolts. It bobs slowly while it is
out. Switching it off stops new riders but does not throw off someone who
is already seated; they leave by moving, as with any VRChat seat.

### Catch-up portal

For when the group has moved on and someone is still back there, AFK or
slow. Drop the portal where they are, then walk on with everyone else. The
portal is a single station split in two: the part that can be clicked stays
at the doorway (held in the world by
[world-sync](https://github.com/CassidyPrather/world-sync)), while its
arrival point is a spot just behind you. The traveller arrives standing and
is carried with you until they walk or jump, which lets go.

Switching the portal off picks it up.

The portal's position is synced, not just frozen in place, which is where
its 66 extra bits go. Whoever is far enough behind to need it may well
load your avatar after you dropped it, and would otherwise find the
doorway wherever you happened to be standing at that moment. The
[world-sync README](https://github.com/CassidyPrather/world-sync) explains
how, and what about it is still untested.

## The station pool

VRChat keeps **six** stations working on one avatar and silently disables
the rest, counting every station from every source. Treat them as a shared
pool: before adding a prefab here to an avatar that already has seats, add
up the Stations column. The generator refuses to build a set that would
exceed six on its own.

There is no run-time sharing yet: each feature owns its station whether or
not it is in use. Letting a seat lend its station to a portal while idle is
the planned next step once more than a couple of features compete for them.

## Limits and unknowns

- Not yet run in the VRChat client. In particular, how the client treats a
  station whose arrival point is far from its collider is taken from how
  avatar teleporters are known to behave, not verified here.
- The constraint smoothing value (`0.35`) is a first guess at "eased but
  not laggy" and wants tuning in game.
- The wearer may be able to click their own stations; VRChat's
  documentation does not say.

## Rebuilding the generated assets

Everything under `Generated/` and `Prefabs/` is written by
`Tools > Wirenook > Avatar Stations > Rebuild`
(`Wirenook.AvatarStations.Editor.AvatarStationsBuild.Rebuild`). The models
come from `blender/`; see `blender/README.md`.

## Development

Requires Unity 2022.3.22f1 (see `ProjectSettings/ProjectVersion.txt`) via a VPM
client (prefer [ALCOM](https://vrc-get.anatawa12.com/en/alcom/)), and the
[.NET SDK](https://dotnet.microsoft.com/download) 11 for the headless checks.

The Unity project at the repository root is the development harness. It
takes `net.wirenook.assets` and `net.wirenook.world-sync` from checkouts of
[wirenook-assets](https://github.com/CassidyPrather/wirenook-assets) and
[world-sync](https://github.com/CassidyPrather/world-sync) beside this one. The simplest way to get that layout is to
work in [cass-avatar](https://github.com/CassidyPrather/cass-avatar), which
mounts this repository and its siblings as submodules under `modules/`.

```bash
scripts/check-meta.sh          # every visible file has a committed .meta
scripts/check-manifests.py     # package.json sanity across Packages/
scripts/format.sh --check      # dotnet-format over the CI-visible C#
scripts/test.sh                # headless NUnit run of Runtime/Core + Tests
scripts/build-packages.sh      # VPM .zip + .unitypackage per package -> dist/
```

Pre-commit hook: `git config core.hooksPath .githooks`

Release: bump `version` in the package's `package.json`, commit and push,
then create a GitHub release tagged with that version. CI attaches the `.zip`
and `.unitypackage`; the listing at
[CassidyPrather/vpm](https://github.com/CassidyPrather/vpm) picks them up
once this repository is in its `githubRepos`.

## Legal

Copyright 2026 Cassidy Prather <cassidy@wirenook.net>

Everything in this repository henceforth is licensed under
[AGPLv3](https://www.gnu.org/licenses/agpl-3.0.html) (See `LICENSE`) unless
otherwise specified.

As a special exception, the copyright holders give permission to link this
Program with the VRChat SDK and with Unity Technologies' proprietary engine
components, and to convey the resulting work under terms of your choice,
provided that you also convey the Corresponding Source of this Program under
the terms of the GNU Affero General Public License version 3 in all other
respects. (This exception also in `LICENSE_ADDENDUM`.)

## Credits

Descended from https://github.com/vrchat-community/template-package
([VRCHAT DISTRO LICENSE FILE](https://github.com/vrchat-community/template-package/blob/d9cf13fe9f56867cbf7315a4dbbf1901bc1537ec/Packages/com.vrchat.core.bootstrap/License.md))

Third-party material is inventoried in [CREDITS.md](CREDITS.md).
