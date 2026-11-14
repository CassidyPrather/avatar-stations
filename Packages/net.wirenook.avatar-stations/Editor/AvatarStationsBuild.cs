using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.Dynamics;
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
        public const string BundlePrefab = Prefabs + "/Wirenook Stations.prefab";

        /// <summary>Stations VRChat keeps working on one avatar.</summary>
        public const int StationBudget = 6;

        [MenuItem("Tools/Wirenook/Avatar Stations/Rebuild")]
        public static void Rebuild()
        {
            CarrySeat();
            Teleporter();
            Bundle();
            AssetDatabase.SaveAssets();
            Debug.Log("[avatar-stations] Rebuilt prefabs and animators.");
        }

        /// <summary>
        /// A cloud that trails beside the wearer with a passenger on it. The
        /// cloud is not parented to the avatar's motion directly: it chases a
        /// point beside the wearer through a smoothed constraint, so starts,
        /// stops and turns reach the passenger eased rather than rigid.
        /// </summary>
        private static void CarrySeat()
        {
            const string parameter = "WN/Seat";
            FeaturePrefab.Edit(SeatPrefab, root =>
            {
                var follow = FeaturePrefab.Child(root, "Follow");
                // Beside and a little behind: out of the wearer's own way,
                // close enough to chat over a shoulder.
                follow.transform.localPosition = new Vector3(-0.7f, 0.15f, -0.45f);

                var seat = FeaturePrefab.Child(root, "Seat");
                var constraint = FeaturePrefab.Ensure<VRCParentConstraint>(seat);
                constraint.ClearSources();
                constraint.AddSource(new VRCConstraintSource { SourceTransform = follow.transform, Weight = 1f });
                constraint.IsActive = true;
                constraint.Locked = true;
                constraint.SmoothingFactor = 0.35f;

                var model = RigidProp.Build(seat, Models + "/cloud-seat.fbx", Generated);
                var cloud = FeaturePrefab.Find(model, "CloudSeat");

                var station = FeaturePrefab.Child(seat, "Station");
                station.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                ConfigureStation(station, station.transform, seated: true, new Vector3(0.75f, 0.5f, 0.6f), new Vector3(0f, 0.1f, 0f));

                var fx = ControllerBuilder.Rebuild(Generated + "/carry-seat-fx.controller");
                var stationPath = FeaturePrefab.PathTo(root, station.transform);
                var cloudPath = FeaturePrefab.PathTo(root, cloud);
                var rest = cloud.localPosition;
                PopToggle.Layer(fx, "Carry Seat", parameter, cloudPath, (clip, phase) =>
                {
                    // The seat can only be taken while the cloud is fully out.
                    // Switching the collider off stops new riders without
                    // throwing off whoever is seated, which disabling the
                    // station itself would do.
                    clip.Constant(stationPath, typeof(BoxCollider), "m_Enabled", phase == "on" ? 1f : 0f);
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
            FeaturePrefab.Edit(TeleporterPrefab, root =>
            {
                var carry = FeaturePrefab.Child(root, "Carry");
                carry.transform.localPosition = new Vector3(0f, 0f, 1f);
                // Turned to face the wearer, so it is entered from their side.
                carry.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var arrival = FeaturePrefab.Child(root, "Arrival");
                arrival.transform.localPosition = new Vector3(0f, 0f, -0.8f);

                var entrance = FeaturePrefab.Child(root, "Entrance");
                WorldAnchor.Configure(entrance, carry.transform);
                var model = RigidProp.Build(entrance, Models + "/portal.fbx", Generated);
                var frame = FeaturePrefab.Find(model, "Portal");
                var swirl = FeaturePrefab.Find(model, "Portal.Swirl");

                var station = FeaturePrefab.Child(entrance, "Station");
                // Standing, not seated: the traveller arrives on their feet
                // and walks or jumps off to let go.
                ConfigureStation(station, arrival.transform, seated: false, new Vector3(0.8f, 1.8f, 0.35f), new Vector3(0f, 0.95f, 0f));
                WirenookSounds.Source(entrance);

                var fx = ControllerBuilder.Rebuild(Generated + "/teleporter-fx.controller");
                var stationPath = FeaturePrefab.PathTo(root, station.transform);
                var swirlPath = FeaturePrefab.PathTo(root, swirl);
                // The swirl spins about the doorway's own axis; which of the
                // bone's axes that is depends on the export.
                var through = swirl.InverseTransformDirection(model.transform.forward);
                var axis = DominantAxis(through);
                var states = WorldAnchor.DropLayer(fx, "Teleporter", parameter,
                    FeaturePrefab.PathTo(root, entrance.transform), FeaturePrefab.PathTo(root, frame), (clip, phase) =>
                    {
                        clip.Constant(stationPath, typeof(BoxCollider), "m_Enabled", phase == "on" ? 1f : 0f);
                        if (phase != "on") return;
                        clip.Curve(swirlPath, typeof(Transform), "localEulerAnglesRaw." + axis, AnimationCurve.Linear(0f, 0f, 3f, 360f));
                        clip.Loop();
                    });
                WirenookSounds.PlayOnEnter(states.Appear, FeaturePrefab.PathTo(root, entrance.transform) + "/" + WirenookSounds.SourceName, "portal");
                fx.Save();

                FeaturePrefab.Merge(root, fx.Controller);
                FeaturePrefab.Parameters(root, FeaturePrefab.SyncedBool(parameter));
                FeaturePrefab.InstallMenu(root);
                FeaturePrefab.MenuToggle(root, "Catch-up Portal", "catch-up", parameter);
                FeaturePrefab.Describe(root, "teleporter", "Catch-up portal", "catch-up", "teleporter", 70);
            });
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

        /// <summary>Both features behind one "Stations" menu entry.</summary>
        private static void Bundle()
        {
            FeaturePrefab.Edit(BundlePrefab, root =>
            {
                FeaturePrefab.InstallMenu(root);
                FeaturePrefab.MenuFolder(root, "Stations", "seat");
                foreach (var path in new[] { SeatPrefab, TeleporterPrefab })
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
