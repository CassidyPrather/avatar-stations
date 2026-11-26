using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.Dynamics;
using Wirenook.Assets;
using Wirenook.Assets.Editor;
using Wirenook.WorldSync.Editor;
using Station = VRC.SDKBase.VRCStation;

namespace Wirenook.AvatarStations.Editor
{
    /// <summary>
    /// Generates the package's prefabs: a cloud a friend can ride on, and a
    /// doorway that brings whoever steps through it to the wearer.
    ///
    /// VRChat allows six stations on an avatar and silently disables the
    /// rest, so stations are a pool every feature draws from. Each prefab
    /// here uses exactly one; <see cref="StationBudget"/> is the limit, and
    /// the README keeps the tally of who uses how many.
    /// </summary>
    public static class AvatarStationsBuild
    {
        public const string Package = "Packages/net.wirenook.avatar-stations";
        private const string Models = Package + "/Models";
        private const string Generated = Package + "/Generated";
        private const string Prefabs = Package + "/Prefabs";

        public const string SeatPrefab = Prefabs + "/Wirenook Carry Seat.prefab";
        public const string TeleporterPrefab = Prefabs + "/Wirenook Teleporter.prefab";
        public const string SeatsPrefab = Prefabs + "/Wirenook Seats.prefab";
        public const string BundlePrefab = Prefabs + "/Wirenook Stations.prefab";
        public const string EventBundlePrefab = Prefabs + "/Wirenook Stations (Event).prefab";

        /// <summary>Stations VRChat keeps working on one avatar.</summary>
        public const int StationBudget = 6;

        /// <summary>Checks the portal's synced anchor as far as the editor can.</summary>
        [MenuItem("Tools/Wirenook/Avatar Stations/Verify")]
        public static string Verify()
        {
            var report = "Portal: " + SyncedAnchorCheck.Run(TeleporterPrefab, Generated + "/teleporter-fx.controller", "WN/Portal") +
                         System.Environment.NewLine +
                         "Seats: " + SyncedAnchorCheck.Run(SeatsPrefab, Generated + "/seats-fx.controller", "WN/Seats");
            Debug.Log("[avatar-stations] " + report);
            return report;
        }

        [MenuItem("Tools/Wirenook/Avatar Stations/Rebuild")]
        public static void Rebuild()
        {
            CarrySeat();
            Teleporter();
            Seats();
            Bundle(BundlePrefab, SeatPrefab, TeleporterPrefab);
            Bundle(EventBundlePrefab, SeatPrefab, TeleporterPrefab, SeatsPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[avatar-stations] Rebuilt prefabs and animators.");
        }

        // How the carry seat eases after the wearer. Each is a VRChat
        // constraint smoothing factor: the seconds a constraint takes to
        // cover 63% of the way to where it should be, so larger is slower
        // and zero is rigid. Untested in the client; tune them there.

        /// <summary>
        /// How soon the seat makes up ground when the wearer walks off or
        /// stops. With <see cref="SeatSettleSeconds"/> it sets how far the
        /// seat trails a wearer on the move: their speed times the sum,
        /// which at a 4 m/s run is 0.9 m.
        /// </summary>
        private const float SeatFollowSeconds = 0.12f;

        /// <summary>
        /// How soon the seat comes round when the wearer turns. Much slower
        /// than <see cref="SeatFollowSeconds"/>: a thumbstick turn is 45
        /// degrees in a single frame, and it is the passenger's whole view
        /// that turns with the seat. At 0.6 one such turn reaches the seat
        /// as a swing that peaks near 50 degrees a second and is nine tenths
        /// done after a second and a half.
        /// </summary>
        private const float SeatTurnSeconds = 0.6f;

        /// <summary>
        /// A second, short easing after the two above, which is what makes
        /// the seat start moving gently. One easing alone moves fastest in
        /// its first instant: after a snap turn the seat would set off at
        /// full speed and only then slow down.
        /// </summary>
        private const float SeatSettleSeconds = 0.1f;

        /// <summary>
        /// A cloud that trails beside the wearer with a passenger on it. The
        /// cloud is not parented to the avatar's motion directly: it is eased
        /// after the wearer, so starts, stops and turns reach the passenger
        /// softened rather than rigid.
        ///
        /// Where the wearer stands and which way they face are eased
        /// separately, the facing far more slowly, and the seat hangs off the
        /// eased result at arm's length. A turn therefore carries the seat
        /// round the wearer in an arc, at the slow pace of the facing,
        /// instead of dragging it straight to its new place (0.64 m sideways
        /// for one thumbstick turn, and through the wearer for a half turn).
        ///
        /// All the easing happens under an object that is held still in the
        /// world. VRChat's documentation gives the smoothing formula as a
        /// blend from the constraint's "current" pose and does not say
        /// whether that pose has already been carried along by the object's
        /// parent. Under the avatar it could have been, and then a turn of
        /// the avatar would leave nothing to ease; under a parent that never
        /// moves, both readings give the same answer.
        /// </summary>
        private static void CarrySeat()
        {
            const string parameter = "WN/Seat";
            FeaturePrefab.Edit(SeatPrefab, root =>
            {
                // Where the wearer stands. It moves with the avatar, rigidly.
                var follow = FeaturePrefab.Child(root, "Follow");
                follow.transform.localPosition = Vector3.zero;
                follow.transform.localRotation = Quaternion.identity;

                // Held still in the world from the animator's first frame
                // (see the layer below). Where it is held does not matter.
                var world = FeaturePrefab.Child(root, "World");
                WorldAnchor.Configure(world, follow.transform);

                var swing = FeaturePrefab.Child(world, "Swing");
                var position = FeaturePrefab.Ensure<VRCPositionConstraint>(swing);
                position.ClearSources();
                position.AddSource(new VRCConstraintSource { SourceTransform = follow.transform, Weight = 1f });
                position.PositionOffset = Vector3.zero;
                position.IsActive = true;
                // Unlocked constraints re-measure their offset instead of moving the object.
                position.Locked = true;
                position.FreezeToWorld = false;
                position.SmoothingFactor = SeatFollowSeconds;

                var rotation = FeaturePrefab.Ensure<VRCRotationConstraint>(swing);
                rotation.ClearSources();
                rotation.AddSource(new VRCConstraintSource { SourceTransform = follow.transform, Weight = 1f });
                rotation.RotationOffset = Vector3.zero;
                rotation.IsActive = true;
                rotation.Locked = true;
                rotation.FreezeToWorld = false;
                rotation.SmoothingFactor = SeatTurnSeconds;

                // Beside and a little behind: out of the wearer's own way,
                // close enough to chat over a shoulder.
                var beside = FeaturePrefab.Child(swing, "Beside");
                beside.transform.localPosition = new Vector3(-0.7f, 0.15f, -0.45f);
                beside.transform.localRotation = Quaternion.identity;

                // Prefabs built before the seat was eased in the world keep
                // it directly under the root. Move that one rather than make
                // a second: its station is the one already on people's avatars.
                var seatHolder = root.transform.Find("Seat");
                if (seatHolder != null) seatHolder.SetParent(world.transform, false);
                var seat = FeaturePrefab.Child(world, "Seat");
                // Where the constraint will put it, for anything that shows
                // the prefab without running constraints.
                seat.transform.localPosition = beside.transform.localPosition;
                seat.transform.localRotation = Quaternion.identity;
                var constraint = FeaturePrefab.Ensure<VRCParentConstraint>(seat);
                constraint.ClearSources();
                constraint.AddSource(new VRCConstraintSource
                {
                    SourceTransform = beside.transform,
                    Weight = 1f,
                    ParentPositionOffset = Vector3.zero,
                    ParentRotationOffset = Vector3.zero,
                });
                constraint.IsActive = true;
                constraint.Locked = true;
                constraint.FreezeToWorld = false;
                constraint.SmoothingFactor = SeatSettleSeconds;

                var model = RigidProp.Build(seat, Models + "/cloud-seat.fbx", Generated);
                var cloud = FeaturePrefab.Find(model, "CloudSeat");

                var station = FeaturePrefab.Child(seat, "Station");
                station.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                ConfigureStation(station, station.transform, seated: true, new Vector3(0.75f, 0.5f, 0.6f), new Vector3(0f, 0.1f, 0f));

                var fx = ControllerBuilder.Rebuild(Generated + "/carry-seat-fx.controller");
                var stationPath = FeaturePrefab.PathTo(root, station.transform);
                var cloudPath = FeaturePrefab.PathTo(root, cloud);
                var worldPath = FeaturePrefab.PathTo(root, world.transform);
                var rest = cloud.localPosition;
                PopToggle.Layer(fx, "Carry Seat", parameter, cloudPath, (clip, phase) =>
                {
                    // In every state, the seat's being out or not included:
                    // someone may still be seated after it is switched off,
                    // and the easing must not change under them.
                    WorldAnchor.Freeze(clip, worldPath, true);

                    // The seat can only be taken while the cloud is fully out.
                    // Switching the collider off stops new riders without
                    // throwing off whoever is seated, which disabling the
                    // station itself would do.
                    clip.Constant(stationPath, typeof(BoxCollider), "m_Enabled", phase == "on" ? 1f : 0f);
                    Gate(clip, stationPath, phase == "on");
                    if (phase != "on") return;

                    // A slow bob, so the cloud floats rather than slides.
                    var bob = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1.6f, 0.035f), new Keyframe(3.2f, 0f));
                    for (var key = 0; key < bob.length; key++) bob.SmoothTangents(key, 0f);
                    Offset(clip, cloudPath, rest, cloud.parent.InverseTransformDirection(Vector3.up), bob);
                    clip.Loop();
                });
                fx.Save();

                FeaturePrefab.Merge(root, fx.Controller);
                FeaturePrefab.Parameters(root, FeaturePrefab.SyncedBool(parameter));
                FeaturePrefab.InstallMenu(root);
                FeaturePrefab.MenuToggle(root, "Carry Seat", "carry", parameter);
                FeaturePrefab.Describe(root, "carry-seat", "Carry seat", "carry", "carry seat", 60);
            });
        }

        /// <summary>
        /// A doorway dropped in the world that leads to the wearer: someone
        /// left behind steps through it and arrives just behind the wearer,
        /// wherever the group has got to. It is one station whose clickable
        /// part stays at the doorway while its arrival point travels with
        /// the avatar.
        /// </summary>
        private static void Teleporter()
        {
            const string parameter = "WN/Portal";
            SyncedAnchor.Prepare();
            FeaturePrefab.Edit(TeleporterPrefab, root =>
            {
                var carry = FeaturePrefab.Child(root, "Carry");
                carry.transform.localPosition = new Vector3(0f, 0f, 1f);
                // Turned to face the wearer, so it is entered from their side.
                carry.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var arrival = FeaturePrefab.Child(root, "Arrival");
                arrival.transform.localPosition = new Vector3(0f, 0f, -0.8f);

                // Aimed: switching the portal on shows it to the wearer where the right hand
                // points, and a fist sets it down. A step in front is where it goes when the
                // hand points at nothing.
                var aim = AimedCarry.Configure(root, carry.transform, parameter + "/Aim");

                var entrance = FeaturePrefab.Child(root, "Entrance");
                // Synced, not just frozen: whoever is far enough behind to
                // need the portal may well load the avatar after it was dropped.
                var rig = SyncedAnchor.Configure(root, entrance, aim.Carry, parameter);
                var model = RigidProp.Build(entrance, Models + "/portal.fbx", Generated);
                var frame = FeaturePrefab.Find(model, "Portal");
                var swirl = FeaturePrefab.Find(model, "Portal.Swirl");

                var station = FeaturePrefab.Child(entrance, "Station");
                // Standing, not seated: the traveller arrives on their feet
                // and walks or jumps off to let go.
                ConfigureStation(station, arrival.transform, seated: false, new Vector3(0.8f, 1.8f, 0.35f), new Vector3(0f, 0.95f, 0f));
                WirenookSounds.Source(entrance, shared: false);

                var fx = ControllerBuilder.Rebuild(Generated + "/teleporter-fx.controller");
                var stationPath = FeaturePrefab.PathTo(root, station.transform);
                var swirlPath = FeaturePrefab.PathTo(root, swirl);
                // The swirl spins about the doorway's own axis; which of the
                // bone's axes that is depends on the export.
                var through = swirl.InverseTransformDirection(model.transform.forward);
                var axis = DominantAxis(through);
                var states = rig.DropLayers(fx, "Teleporter", parameter, aim, FeaturePrefab.PathTo(root, frame), (clip, phase) =>
                    {
                        if (phase != "on") return;
                        clip.Curve(swirlPath, typeof(Transform), "localEulerAnglesRaw." + axis, AnimationCurve.Linear(0f, 0f, 3f, 360f));
                        clip.Loop();
                    });
                WirenookSounds.PlayOnEnter(states.Appear, FeaturePrefab.PathTo(root, entrance.transform) + "/" + WirenookSounds.SourceName, "portal");

                // The doorway can be stepped through once it has been set down, not while it
                // is still being carried about on the end of a ray.
                AnimationClip Door(bool open) => fx.Clip(open ? "teleporter door open" : "teleporter door shut", clip =>
                {
                    clip.Constant(stationPath, typeof(BoxCollider), "m_Enabled", open ? 1f : 0f);
                    Gate(clip, stationPath, open);
                });
                var door = fx.Layer("Teleporter Door");
                var shut = door.State("Shut", Door(false));
                var open = door.State("Open", Door(true));
                shut.To(open).IfTrue(parameter).IfTrue(aim.Dropped);
                open.To(shut).IfFalse(parameter).Or().IfFalse(aim.Dropped);
                fx.Save();

                FeaturePrefab.Merge(root, fx.Controller);
                FeaturePrefab.Parameters(root, rig.Parameters().Concat(aim.Parameters()).Prepend(FeaturePrefab.SyncedBool(parameter)).ToArray());
                FeaturePrefab.InstallMenu(root);
                FeaturePrefab.MenuToggle(root, "Catch-up Portal", "catch-up", parameter);
                FeaturePrefab.Describe(root, "teleporter", "Catch-up portal", "catch-up", "teleporter", 70);
            });
        }

        // Where the seats stand, as blender/build_props.py models them: in a
        // ring round a lamp, or in a row on an arc before a screen.
        private const float SeatHeight = 0.32f;
        private const float RingRadius = 1.15f;
        private const float ScreenAhead = 1.6f;
        private const float ScreenCentre = 1.3f;
        private const float RowRadius = 2.6f;
        private static readonly float[] RingAngles = { 45f, 135f, 225f, 315f };
        private static readonly float[] RowAngles = { -30f, -10f, 10f, 30f };

        /// <summary>
        /// Four seats to put down for a group: in a ring round a lamp, or in
        /// a row facing a screen. The screen shows the world's video player
        /// where the world shares its picture (see the shader), and a test
        /// card where it does not.
        ///
        /// Each arrangement is a single mesh and the two are swapped, rather
        /// than four cushions that move: left in the world, every cushion
        /// would be a mesh and a material slot of its own. The four stations
        /// do move, to wherever the cushions of the arrangement on show are.
        /// </summary>
        private static void Seats()
        {
            const string parameter = "WN/Seats";
            const string row = "WN/SeatsRow";

            var shader = Shader.Find("Wirenook/Video Screen");
            var material = AssetFolders.LoadOrCreate(Generated + "/video-screen.mat", () => new Material(shader));
            material.shader = shader;
            EditorUtility.SetDirty(material);

            // The picture: 16 by 9, facing back towards the seats.
            var picture = AssetFolders.LoadOrCreate(Generated + "/video-screen-quad.asset", () => new Mesh());
            picture.Clear();
            picture.name = "video-screen-quad";
            picture.vertices = new[] { new Vector3(-0.96f, -0.54f), new Vector3(0.96f, -0.54f), new Vector3(0.96f, 0.54f), new Vector3(-0.96f, 0.54f) };
            picture.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            picture.normals = Enumerable.Repeat(Vector3.back, 4).ToArray();
            picture.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            picture.RecalculateBounds();
            EditorUtility.SetDirty(picture);

            var ring = RingAngles.Select(angle => Place(Quaternion.Euler(0f, angle, 0f) * Vector3.forward * RingRadius, Vector3.zero)).ToArray();
            var screenFoot = Vector3.forward * ScreenAhead;
            var theatre = RowAngles.Select(angle => Place(screenFoot - Quaternion.Euler(0f, angle, 0f) * Vector3.forward * RowRadius, screenFoot)).ToArray();

            SyncedAnchor.Prepare();
            FeaturePrefab.Edit(SeatsPrefab, root =>
            {
                var carry = FeaturePrefab.Child(root, "Carry");
                // Far enough ahead that the wearer is outside the ring.
                carry.transform.localPosition = new Vector3(0f, 0f, RingRadius + 0.75f);

                var place = FeaturePrefab.Child(root, "Place");
                var rig = SyncedAnchor.Configure(root, place, carry.transform, parameter);
                var model = RigidProp.Build(place, Models + "/seats.fbx", Generated);
                var circle = FeaturePrefab.Find(model, "Seats.Ring");
                var facing = FeaturePrefab.Find(model, "Seats.Row");

                var screen = FeaturePrefab.Child(facing.gameObject, "Screen");
                screen.transform.SetPositionAndRotation(
                    model.transform.TransformPoint(new Vector3(0f, ScreenCentre, ScreenAhead)), model.transform.rotation);
                FeaturePrefab.Ensure<MeshFilter>(screen).sharedMesh = picture;
                var renderer = FeaturePrefab.Ensure<MeshRenderer>(screen);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // Drawn by a shader of this package's own, and mobile builds
                // may only use VRChat's. There the surround stands empty.
                FeaturePrefab.Ensure<Wirenook.Assets.WirenookPcOnly>(screen);

                var seats = new List<string>();
                for (var i = 0; i < ring.Length; i++)
                {
                    var seat = FeaturePrefab.Child(place, "Seat " + (i + 1));
                    seat.transform.localPosition = ring[i].position;
                    seat.transform.localRotation = Quaternion.Euler(0f, ring[i].heading, 0f);
                    ConfigureStation(seat, seat.transform, seated: true, new Vector3(0.6f, 0.5f, 0.6f), new Vector3(0f, 0.1f, 0f));
                    seats.Add(FeaturePrefab.PathTo(root, seat.transform));
                }

                var fx = ControllerBuilder.Rebuild(Generated + "/seats-fx.controller");
                fx.Bool(parameter).Bool(row);
                PopToggle.Layer(fx, "Seats Ring", FeaturePrefab.PathTo(root, circle),
                    on => on.IfTrue(parameter).IfFalse(row), off => off.IfFalse(parameter).Or().IfTrue(row));
                PopToggle.Layer(fx, "Seats Row", FeaturePrefab.PathTo(root, facing),
                    on => on.IfTrue(parameter).IfTrue(row), off => off.IfFalse(parameter).Or().IfFalse(row));
                rig.AddLayers(fx, parameter, PopToggle.DisappearSeconds);

                AnimationClip Stations(string name, (Vector3 position, float heading)[] places, bool open) =>
                    fx.Clip("seats " + name, clip =>
                    {
                        for (var i = 0; i < seats.Count; i++)
                        {
                            clip.Constant(seats[i], typeof(Transform), "m_LocalPosition.x", places[i].position.x)
                                .Constant(seats[i], typeof(Transform), "m_LocalPosition.y", places[i].position.y)
                                .Constant(seats[i], typeof(Transform), "m_LocalPosition.z", places[i].position.z)
                                .Constant(seats[i], typeof(Transform), "localEulerAnglesRaw.x", 0f)
                                .Constant(seats[i], typeof(Transform), "localEulerAnglesRaw.y", places[i].heading)
                                .Constant(seats[i], typeof(Transform), "localEulerAnglesRaw.z", 0f)
                                .Constant(seats[i], typeof(BoxCollider), "m_Enabled", open ? 1f : 0f);
                            Gate(clip, seats[i], open);
                        }
                    });

                var layer = fx.Layer("Seats Stations");
                var away = layer.State("Away", Stations("away", ring, false));
                var inRing = layer.State("Ring", Stations("ring", ring, true));
                var inRow = layer.State("Row", Stations("row", theatre, true));
                away.To(inRing).IfTrue(parameter).IfFalse(row);
                away.To(inRow).IfTrue(parameter).IfTrue(row);
                inRing.To(inRow).IfTrue(parameter).IfTrue(row);
                inRow.To(inRing).IfTrue(parameter).IfFalse(row);
                inRing.To(away).IfFalse(parameter);
                inRow.To(away).IfFalse(parameter);
                fx.Save();

                FeaturePrefab.Merge(root, fx.Controller);
                FeaturePrefab.Parameters(root, rig.Parameters()
                    .Prepend(FeaturePrefab.SyncedBool(row)).Prepend(FeaturePrefab.SyncedBool(parameter)).ToArray());
                FeaturePrefab.InstallMenu(root);
                FeaturePrefab.MenuFolder(root, "Seats", "ring");
                FeaturePrefab.MenuToggle(FeaturePrefab.Child(root, "Menu Seats"), "Seats", "ring", parameter);
                FeaturePrefab.MenuToggle(FeaturePrefab.Child(root, "Menu Screen"), "Face A Screen", "screen", row);
                FeaturePrefab.Describe(root, "seats", "Seats", "ring", "seats", 55);
            });
        }

        /// <summary>A seat at a spot, on top of its cushion, turned to face a point.</summary>
        private static (Vector3 position, float heading) Place(Vector3 spot, Vector3 towards)
        {
            var look = towards - spot;
            return (new Vector3(spot.x, SeatHeight, spot.z), Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
        }

        private static void ConfigureStation(GameObject holder, Transform location, bool seated, Vector3 size, Vector3 centre)
        {
            var station = FeaturePrefab.Ensure<VRCStation>(holder);
            station.PlayerMobility = Station.Mobility.Immobilize;
            station.seated = seated;
            station.canUseStationFromStation = true;
            station.disableStationExit = false;
            station.stationEnterPlayerLocation = location;
            // VRChat rejects stations whose entry and exit are more than two
            // metres apart; using one transform for both cannot break that.
            station.stationExitPlayerLocation = location;

            // The collider is what a player clicks. It starts disabled and the
            // animator enables it, while the station component stays enabled
            // throughout: one that is disabled at upload never works.
            var collider = FeaturePrefab.Ensure<BoxCollider>(holder);
            collider.isTrigger = true;
            collider.size = size;
            collider.center = centre;
            collider.enabled = false;
        }

        /// <summary>
        /// Shrinks a station to nothing while it should not be taken. On PC
        /// the collider switch beside it already does that. Android and iOS
        /// builds have no collider of their own to switch: VRChat disables
        /// colliders on avatars there and gives each station a trigger
        /// itself, which nothing here can turn off, but a trigger on an
        /// object scaled to zero has no size to click.
        /// </summary>
        private static void Gate(ClipBuilder clip, string stationPath, bool open)
        {
            PopToggle.Scale(clip, stationPath, AnimationCurve.Constant(0f, 1f / 60f, open ? 1f : 0f));
        }

        private static void Offset(ClipBuilder clip, string path, Vector3 rest, Vector3 direction, AnimationCurve amount)
        {
            foreach (var (name, component, origin) in new[]
                     {
                         ("x", direction.x, rest.x), ("y", direction.y, rest.y), ("z", direction.z, rest.z),
                     })
            {
                var curve = new AnimationCurve(amount.keys
                    .Select(key => new Keyframe(key.time, origin + key.value * component, key.inTangent * component, key.outTangent * component))
                    .ToArray());
                clip.Curve(path, typeof(Transform), "m_LocalPosition." + name, curve);
            }
        }

        private static string DominantAxis(Vector3 direction)
        {
            var x = Mathf.Abs(direction.x);
            var y = Mathf.Abs(direction.y);
            var z = Mathf.Abs(direction.z);
            return x > y && x > z ? "x" : y > z ? "y" : "z";
        }

        /// <summary>A set of these features behind one "Stations" menu entry.</summary>
        private static void Bundle(string bundlePath, params string[] featurePrefabs)
        {
            FeaturePrefab.Edit(bundlePath, root =>
            {
                FeaturePrefab.InstallMenu(root);
                FeaturePrefab.MenuFolder(root, "Stations", "seat");
                foreach (var path in featurePrefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance = root.transform.Cast<Transform>()
                                       .Select(child => child.gameObject)
                                       .FirstOrDefault(child => PrefabUtility.GetCorrespondingObjectFromSource(child) == prefab)
                                   ?? (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    var installer = instance.GetComponent<ModularAvatarMenuInstaller>();
                    if (installer != null) Object.DestroyImmediate(installer);
                }

                var stations = root.GetComponentsInChildren<VRCStation>(true).Length;
                if (stations > StationBudget)
                    throw new InvalidDataException("The stations set uses " + stations + " stations; VRChat disables all but " + StationBudget + ".");
            });
        }
    }
}
