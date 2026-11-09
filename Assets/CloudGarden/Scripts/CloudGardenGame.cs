using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden
{
    public sealed class CloudGardenGame : MonoBehaviour
    {
        public GardenSettings settings;
        public GardenPlacementAnchor placementAnchor;
        public GardenModel Model { get; private set; } = new GardenModel();
        public readonly List<CloudShapeController> Clouds = new List<CloudShapeController>();
        public CloudShapeController ActiveCloud { get; private set; }
        public Camera ViewCamera { get; private set; }
        public GardenHUD Hud { get; private set; }
        public Material GlowMaterial => view.GlowMaterial;
        public Vector3 WorldOrigin => world.position;
        public Transform GardenRoot => world;
        public GameObject VirtualEnvironment { get; private set; }
        public bool VirtualEnvironmentVisible => VirtualEnvironment.activeSelf;
        public Vector3 DesktopViewLocalPosition => desktopPosition;
        public Quaternion DesktopViewLocalRotation => desktopRotation;
        public int Level { get; private set; }
        public int CycleBaseline { get; private set; }
        public bool ShapedAndPlaced { get; private set; }
        public bool GoalComplete => LevelGoalEvaluator.Complete(Level, Model, ShapedAndPlaced, CycleBaseline);
        public bool IsXR { get; private set; }
        public bool UsesExternalPlayer { get; private set; }
        public Action ReturnToWorkshop;
        public void ConfigurePlayer(Camera camera, IHandPoseProvider hands, bool xr)
        { ViewCamera = camera; externalHands = hands; IsXR = xr; UsesExternalPlayer = true; }
        IHandPoseProvider externalHands;
        public string Notice { get; private set; } = "Welcome, weather keeper.";
        public string SaveKey => "CloudGarden.VerticalSlice.v1";
        Transform world;
        GardenWorldView view;
        GardenInteraction interaction;
        readonly List<GardenSnapshot> undo = new List<GardenSnapshot>();
        GardenSnapshot checkpoint;
        Transform[] handMarkers = new Transform[2];
        AudioSource audioSource;
        AudioClip rainTone, seedTone, stormTone, successTone;
        bool celebrated, lastXR;
        Vector3 desktopPosition;
        Quaternion desktopRotation;
        Color skyColor;
        bool originalFog;
        readonly CloudContactCharging contactCharging = new CloudContactCharging();

        [Serializable] public sealed class CloudSnapshot
        {
            public Vector3 position, shape;
            public float deformation;
            public Vector3 dentPoint;
            public float dentDepth;
            public bool raining;
            public CloudResourceState resources;
        }
        [Serializable] public sealed class GardenSnapshot
        {
            public GardenModel model;
            public CloudSnapshot[] clouds;
            public int activeIndex, level, cycleBaseline;
            public bool shapedAndPlaced;
        }

        void Awake()
        {
            if (settings == null) settings = ScriptableObject.CreateInstance<GardenSettings>();
            if (!ViewCamera) ViewCamera = Camera.main;
            skyColor = ViewCamera.backgroundColor; originalFog = RenderSettings.fog;
            if (placementAnchor == null) placementAnchor = GardenPlacementAnchor.CreateDefault(transform);
            world = new GameObject("Living terrarium").transform;
            world.SetParent(placementAnchor.transform, false);
            VirtualEnvironment = new GameObject("VirtualEnvironment");
            VirtualEnvironment.transform.SetParent(world, false);
            view = world.gameObject.AddComponent<GardenWorldView>(); view.Build(this);
            desktopPosition = transform.InverseTransformPoint(ViewCamera.transform.position) - GardenPlacementAnchor.DefaultPosition;
            desktopRotation = Quaternion.Inverse(transform.rotation) * ViewCamera.transform.rotation;
            interaction = gameObject.AddComponent<GardenInteraction>(); interaction.game = this;
            interaction.PoseProvider = IsXR ? externalHands : null;
            for (int i = 0; i < 2; i++)
            {
                handMarkers[i] = GardenWorldView.ChildPrimitive(transform, i == 0 ? "Left pinch marker" : "Right pinch marker", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .025f, view.CloudMaterial).transform;
                handMarkers[i].gameObject.SetActive(false);
            }
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 0; audioSource.volume = .18f;
            rainTone = Tone("Water", 620, .15f); seedTone = Tone("Cloud seed", 880, .35f);
            stormTone = Tone("Soft thunder", 95, .35f); successTone = Tone("Garden awakened", 1046, .55f);
            SpawnCloud(GardenToWorld(new Vector3(-.27f, .50f, -.06f)));
            Hud = gameObject.AddComponent<GardenHUD>(); Hud.Build(this);
            lastXR = !IsXR; ConfigureCamera();
            checkpoint = Capture();
        }

        void ConfigureCamera()
        {
            if (lastXR == IsXR) return;
            lastXR = IsXR;
            if (Hud != null) Hud.SetMode(IsXR);
        }
        void LateUpdate() { if (!IsXR) UpdateDesktopView(); }
        void UpdateDesktopView()
        {
            ViewCamera.transform.SetPositionAndRotation(GardenToWorld(desktopPosition), world.rotation * desktopRotation);
        }

        void Update()
        {
            ConfigureCamera();
            float dt = Mathf.Min(Time.deltaTime, .05f);
            AdvanceSimulation(dt);
            view.Refresh(Model, dt);
            if (GoalComplete && !celebrated) { celebrated = true; Notice = "Beautiful. Your garden remembers. Continue when you are ready."; Play(successTone); }
            Hud.Refresh();
        }

        public void AdvanceSimulation(float dt)
        {
            Model.Tick(dt, settings);
            contactCharging.Tick(Clouds, dt, settings);
            foreach (var cloud in Clouds)
            {
                cloud.GetComponent<WeatherEmitter>().Tick(dt);
                // Spring slowly restores any cloud, even after every other resource was spent.
                cloud.resources.charge = Mathf.Clamp01(cloud.resources.charge + dt * .025f);
                cloud.resources.mass = Mathf.Clamp(cloud.resources.mass + dt * .006f, .2f, 1);
                cloud.resources.stability = Mathf.Clamp01(cloud.resources.stability + dt * .04f);
                if (!cloud.raining && cloud.resources.moisture < .45f)
                {
                    float refill = Mathf.Min(Model.vapor, dt * .025f, .45f - cloud.resources.moisture);
                    cloud.resources.moisture += refill; Model.vapor -= refill;
                }
            }
        }

        public CloudShapeController SpawnCloud(Vector3 position)
        {
            var go = new GameObject("Cloud " + (Clouds.Count + 1)); go.transform.SetParent(world, true); go.transform.position = ClampCloud(position);
            var cloud = go.AddComponent<CloudShapeController>(); cloud.Initialize(view.CloudMaterial);
            go.AddComponent<WeatherEmitter>().Initialize(this, cloud, view.RainMaterial);
            Clouds.Add(cloud); Select(cloud); return cloud;
        }
        public void Select(CloudShapeController cloud)
        {
            ActiveCloud = cloud;
            foreach (var c in Clouds) c.Selected = c == cloud;
        }
        public void SelectNext() { if (Clouds.Count > 0) Select(Clouds[(Clouds.IndexOf(ActiveCloud) + 1) % Clouds.Count]); }
        public CloudShapeController NearestCloud(Vector3 position)
        {
            CloudShapeController nearest = null; float min = float.MaxValue;
            foreach (var c in Clouds) { float d = (c.transform.position - position).sqrMagnitude; if (d < min) { min = d; nearest = c; } }
            return nearest;
        }
        public Vector3 ClampCloud(Vector3 p)
        {
            var local = WorldToGarden(p);
            return GardenToWorld(new Vector3(Mathf.Clamp(local.x, -.6f, .6f), Mathf.Clamp(local.y, .32f, .8f), Mathf.Clamp(local.z, -.32f, .38f)));
        }
        public Vector3 GardenToWorld(Vector3 p) => world.TransformPoint(p);
        public Vector3 WorldToGarden(Vector3 p) => world.InverseTransformPoint(p);
        public void SetPlacementAnchor(GardenPlacementAnchor anchor)
        {
            if (anchor == null || anchor.transform.IsChildOf(world)) throw new ArgumentException("放置锚点必须独立于生态箱。");
            interaction.CancelGesture();
            placementAnchor = anchor; world.SetParent(anchor.transform, false);
            world.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); world.localScale = Vector3.one;
            contactCharging.Reset();
        }
        public void SetVirtualEnvironmentVisible(bool visible)
        {
            VirtualEnvironment.SetActive(visible);
            ViewCamera.backgroundColor = visible ? skyColor : (IsXR ? Color.clear : Color.black);
            RenderSettings.fog = visible && originalFog;
        }
        public void ReleaseCloud()
        {
            ActiveCloud.transform.position = ClampCloud(ActiveCloud.transform.position);
            if (ActiveCloud.deformation > .08f) ShapedAndPlaced = true;
            Notice = "Cloud resting in the sky.";
        }
        public void ShapeCloud(float width, float height)
        { SaveUndo(); ActiveCloud.Sculpt(ActiveCloud.shape.x + width, ActiveCloud.shape.y + height); Play(rainTone); }
        public void KneadCloud()
        {
            SaveUndo(); ActiveCloud.Sculpt(ActiveCloud.shape.x + .035f, ActiveCloud.shape.y + .08f);
            ActiveCloud.dentDepth *= .6f;
            ActiveCloud.resources.stability = Mathf.Clamp01(ActiveCloud.resources.stability + .06f);
        }
        public void PatCloud(Vector3 point)
        {
            SaveUndo(); ActiveCloud.Sculpt(ActiveCloud.shape.x + .03f, ActiveCloud.shape.y + .05f);
            ActiveCloud.Indent(point, .018f);
            ActiveCloud.resources.stability = Mathf.Clamp01(ActiveCloud.resources.stability + .04f);
        }
        public void ToggleRain()
        {
            SaveUndo(); ActiveCloud.raining = !ActiveCloud.raining;
            Notice = ActiveCloud.raining ? "Rain returns water to the earth." : "The rain is resting."; Play(rainTone);
        }
        public void Storm()
        {
            if (ActiveCloud.resources.charge < settings.stormCost) { Notice = "Let the cloud gather a little more charge."; return; }
            SaveUndo(); ActiveCloud.resources.SpendCharge(settings.stormCost);
            var p = WorldToGarden(ActiveCloud.transform.position);
            bool hit = Model.Strike(new Vector2(p.x, p.z), ActiveCloud.Radius);
            Vector3 target = GardenToWorld(hit ? new Vector3(GardenModel.Tower.x, .3f, GardenModel.Tower.y) : new Vector3(p.x, .05f, p.z));
            ActiveCloud.GetComponent<WeatherEmitter>().Flash(target);
            Notice = hit ? "The tower caught your spark. Water can travel now." : "A harmless rumble. Try the brass tower."; Play(stormTone);
        }
        public void GrowSeed()
        {
            if (Clouds.Count >= settings.maxClouds) { Notice = "The sky is full. Recycle one cloud to make room."; return; }
            if (Model.vapor < settings.seedCost || ActiveCloud.resources.mass < .45f) { Notice = "Gather more rising mist. Your spring will always replenish it."; return; }
            SaveUndo();
            if (Model.TryGrowSeed(settings, Clouds.Count, ActiveCloud.resources))
            { SpawnCloud(GardenToWorld(new Vector3(.20f, .56f, -.02f))); Notice = "Water has become a new cloud. The circle continues."; Play(seedTone); }
        }
        public void Recycle()
        {
            if (Clouds.Count <= 1) { Notice = "Keep one cloud to care for the garden."; return; }
            SaveUndo(); var old = ActiveCloud; Clouds.Remove(old); old.gameObject.SetActive(false); Destroy(old.gameObject);
            Model.vapor = Mathf.Clamp01(Model.vapor + .12f); Select(Clouds[0]); Notice = "A cloud returns to mist.";
        }
        public void NextLevel()
        {
            if (!GoalComplete) return;
            Level++; if (Level == 5) CycleBaseline = Model.cycles;
            celebrated = false; undo.Clear(); Notice = LevelGoalEvaluator.Hints[Level]; checkpoint = Capture();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(checkpoint)); PlayerPrefs.Save();
        }
        public void SaveUndo()
        {
            undo.Add(Capture()); if (undo.Count > 20) undo.RemoveAt(0);
        }
        public void Undo()
        {
            if (undo.Count == 0) { Notice = "Nothing to undo yet."; return; }
            var state = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); Restore(state); Notice = "One moment, gently rewound.";
        }
        public void RestartLevel() { Restore(checkpoint); undo.Clear(); Notice = "A fresh start for this chapter."; }
        public void NewGarden()
        {
            var fresh = new GardenSnapshot { model = new GardenModel(), clouds = new[] { new CloudSnapshot { position = new Vector3(-.27f, .5f, -.06f), shape = Vector3.one, resources = new CloudResourceState() } } };
            Restore(fresh); checkpoint = Capture(); undo.Clear(); Notice = "A new little world.";
        }
        public void ResumeGarden()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) { Notice = "Your first chapter has not been saved yet."; return; }
            try
            {
                var saved = JsonUtility.FromJson<GardenSnapshot>(PlayerPrefs.GetString(SaveKey));
                if (saved == null || saved.model == null || saved.clouds == null || saved.clouds.Length == 0 || saved.level < 0 || saved.level > 6) throw new Exception("Invalid garden save");
                Restore(saved); checkpoint = Capture(); undo.Clear(); Notice = "Welcome back to your garden.";
            }
            catch (Exception) { Notice = "That garden could not be restored. Your current garden is safe."; }
        }
        public GardenSnapshot Capture()
        {
            var saved = new GardenSnapshot { model = Model.Copy(), level = Level, cycleBaseline = CycleBaseline, shapedAndPlaced = ShapedAndPlaced, activeIndex = Clouds.IndexOf(ActiveCloud), clouds = new CloudSnapshot[Clouds.Count] };
            for (int i = 0; i < Clouds.Count; i++)
            {
                var c = Clouds[i]; saved.clouds[i] = new CloudSnapshot { position = WorldToGarden(c.transform.position), shape = c.shape, deformation = c.deformation, dentPoint = c.dentPoint, dentDepth = c.dentDepth, raining = c.raining, resources = c.resources.Copy() };
            }
            return saved;
        }
        public void Restore(GardenSnapshot saved)
        {
            interaction.CancelGesture();
            contactCharging.Reset();
            foreach (var c in Clouds) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            Clouds.Clear(); Model = saved.model.Copy(); Level = saved.level; CycleBaseline = saved.cycleBaseline; ShapedAndPlaced = saved.shapedAndPlaced; celebrated = false;
            foreach (var s in saved.clouds)
            {
                var c = SpawnCloud(GardenToWorld(s.position)); c.shape = s.shape; c.deformation = s.deformation; c.dentPoint = s.dentPoint; c.dentDepth = s.dentDepth; c.raining = s.raining; c.resources = s.resources.Copy();
            }
            Select(Clouds[Mathf.Clamp(saved.activeIndex, 0, Clouds.Count - 1)]);
        }
        public void ShowHand(int index, GardenHandPose pose)
        { handMarkers[index].gameObject.SetActive(pose.tracked && !UsesExternalPlayer); if (pose.tracked) handMarkers[index].position = pose.tip; }
        void Play(AudioClip clip) { audioSource.PlayOneShot(clip); }
        static AudioClip Tone(string name, float frequency, float seconds)
        {
            const int rate = 22050; var data = new float[(int)(rate * seconds)];
            for (int i = 0; i < data.Length; i++) { float t = i / (float)rate; data[i] = Mathf.Sin(t * frequency * 6.283f) * Mathf.Sin(i / (float)data.Length * Mathf.PI) * Mathf.Exp(-t * 5) * .3f; }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void OnDestroy()
        {
            RenderSettings.fog = originalFog;
            if (ViewCamera != null) ViewCamera.backgroundColor = skyColor;
            if (world != null) Destroy(world.gameObject);
            foreach (var clip in new[] { rainTone, seedTone, stormTone, successTone }) if (clip != null) Destroy(clip);
        }
    }
}
