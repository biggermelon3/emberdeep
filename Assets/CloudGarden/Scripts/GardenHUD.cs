using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CloudGarden
{
    public sealed class GardenHUD : MonoBehaviour
    {
        CloudGardenGame game;
        Canvas canvas;
        RectTransform root;
        TextMeshProUGUI level, hint, notice, resources, progress, controls;
        UnityEngine.UI.Button next, rain, seed, storm;
        readonly List<UnityEngine.UI.Button> buttons = new List<UnityEngine.UI.Button>();
        readonly List<TextMeshPro> worldLabels = new List<TextMeshPro>();
        LineRenderer footprint;
        readonly Color ink = new Color(.13f, .24f, .28f), paper = new Color(.97f, .96f, .91f, .96f);
        float lastHandClick;

        public void Build(CloudGardenGame owner)
        {
            game = owner;
            var go = new GameObject("Garden interface", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(game.GardenRoot, false); canvas = go.GetComponent<Canvas>(); root = go.GetComponent<RectTransform>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("Event system", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var heading = Panel("Header", root, new Vector2(0, 1), new Vector2(30, -28), new Vector2(1020, 158), paper);
            Label("CLOUD / GARDEN", heading, new Vector2(22, -14), new Vector2(660, 45), 34, FontStyles.Bold);
            level = Label("01  /  A CLOUD IN YOUR HANDS", heading, new Vector2(24, -63), new Vector2(960, 25), 18, FontStyles.Bold);
            hint = Label("", heading, new Vector2(24, -100), new Vector2(968, 46), 21);
            var ledger = Panel("Cloud resources", root, new Vector2(1, 1), new Vector2(-30, -28), new Vector2(470, 158), paper);
            Label("YOUR WEATHER", ledger, new Vector2(22, -17), new Vector2(420, 26), 18, FontStyles.Bold);
            resources = Label("", ledger, new Vector2(22, -58), new Vector2(425, 83), 20);

            var footer = Panel("Weather tools", root, new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(1540, 164), paper);
            var row = new GameObject("Actions", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
            row.transform.SetParent(footer, false);
            var rr = row.GetComponent<RectTransform>(); rr.anchorMin = new Vector2(0, 1); rr.anchorMax = new Vector2(1, 1); rr.pivot = new Vector2(.5f, 1);
            rr.offsetMin = new Vector2(16, -72); rr.offsetMax = new Vector2(-16, -16);
            var layout = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.spacing = 9; layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = true;
            rain = Button("RAIN  /  1", row.transform, game.ToggleRain, new Color(.70f, .85f, .90f));
            storm = Button("SPARK  /  2", row.transform, game.Storm, new Color(.96f, .84f, .55f));
            seed = Button("GROW SEED  /  3", row.transform, game.GrowSeed, new Color(.76f, .87f, .72f));
            Button("WIDEN", row.transform, () => game.ShapeCloud(.18f, -.06f), new Color(.84f, .82f, .90f));
            Button("SQUISH", row.transform, () => game.ShapeCloud(-.1f, -.12f), new Color(.84f, .82f, .90f));
            Button("UNDO  /  Z", row.transform, game.Undo, new Color(.87f, .88f, .86f));
            Button("RESET  /  R", row.transform, game.RestartLevel, new Color(.87f, .88f, .86f));
            Button("RECYCLE", row.transform, game.Recycle, new Color(.87f, .88f, .86f));
            next = Button("CONTINUE", row.transform, game.NextLevel, new Color(.66f, .82f, .72f));
            notice = Label("", footer, new Vector2(20, -87), new Vector2(1500, 29), 20);
            controls = Label("Drag cloud: move   /   Right-drag or wheel: shape   /   Shift + wheel: height   /   Tab: switch cloud", footer, new Vector2(20, -123), new Vector2(1500, 25), 17);

            var side = Panel("Journey", root, new Vector2(0, .5f), new Vector2(30, -12), new Vector2(262, 192), paper);
            Label("RAIN WILL RETURN", side, new Vector2(18, -18), new Vector2(230, 25), 17, FontStyles.Bold);
            progress = Label("", side, new Vector2(18, -53), new Vector2(230, 87), 18);
            var resume = Button("RESUME SAVED GARDEN", side, game.ResumeGarden, new Color(.83f, .87f, .81f));
            var r = resume.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(.5f, 0); r.pivot = new Vector2(.5f, 0); r.anchoredPosition = new Vector2(0, 14); r.sizeDelta = new Vector2(226, 36);
            resume.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14;

            if (game.ReturnToWorkshop != null)
            {
                var back = Button("MAIN MENU", root, () => game.ReturnToWorkshop?.Invoke(), new Color(.83f, .87f, .81f));
                var br = back.GetComponent<RectTransform>(); br.anchorMin = br.anchorMax = new Vector2(1, .5f);
                br.pivot = new Vector2(1, .5f); br.anchoredPosition = new Vector2(-30, 0); br.sizeDelta = new Vector2(310, 52);
                back.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18;
            }
            AddWorldLabel("GARDEN 01", new Vector3(-.35f, .095f, -.29f));
            AddWorldLabel("GARDEN 02", new Vector3(.35f, .095f, -.29f));
            AddWorldLabel("RESERVOIR", new Vector3(0, .12f, .32f));
            AddWorldLabel("POWER", new Vector3(.43f, .42f, .25f));
            AddWorldLabel("MIST SPRING", new Vector3(-.43f, .30f, .26f));
            var marker = new GameObject("Weather footprint"); marker.transform.SetParent(game.GardenRoot, false);
            footprint = marker.AddComponent<LineRenderer>(); footprint.sharedMaterial = game.GlowMaterial;
            footprint.useWorldSpace = false;
            footprint.positionCount = 65; footprint.widthMultiplier = .004f; footprint.loop = false;
        }

        RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return rt;
        }
        TextMeshProUGUI Label(string text, Transform parent, Vector2 position, Vector2 size, float fontSize, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(text.Length > 0 ? text : "Live text", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = position; rt.sizeDelta = size;
            var label = go.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = fontSize; label.color = ink; label.fontStyle = style;
            label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; return label;
        }
        UnityEngine.UI.Button Button(string title, Transform parent, Action action, Color color)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            go.transform.SetParent(parent, false); go.GetComponent<UnityEngine.UI.Image>().color = color;
            var le = go.GetComponent<UnityEngine.UI.LayoutElement>(); le.minWidth = 100; le.preferredWidth = 148;
            var b = go.GetComponent<UnityEngine.UI.Button>(); b.onClick.AddListener(() => action());
            var text = Label(title, go.transform, Vector2.zero, Vector2.zero, 17, FontStyles.Bold);
            var tr = text.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(3, 1); tr.offsetMax = new Vector2(-3, -1); text.alignment = TextAlignmentOptions.Center;
            buttons.Add(b); return b;
        }
        void AddWorldLabel(string text, Vector3 local)
        {
            var go = new GameObject(text); go.transform.SetParent(game.GardenRoot, false); go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * .22f;
            var label = go.AddComponent<TextMeshPro>(); label.text = text; label.fontSize = 1.45f; label.color = ink; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(4, 1); label.textWrappingMode = TextWrappingModes.NoWrap;
            worldLabels.Add(label);
        }
        public void SetMode(bool xr)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = game.ViewCamera;
            root.sizeDelta = new Vector2(1600, 900);
            if (xr)
            {
                root.localScale = Vector3.one * .00075f;
                root.localPosition = new Vector3(0, .25f, .40f); root.localRotation = Quaternion.Euler(10, 0, 0);
            }
            else
            {
                // Match the existing desktop framing while keeping the canvas attached to the garden.
                const float distance = .7f;
                var camera = game.ViewCamera;
                float height = 2 * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                root.localScale = Vector3.one * Mathf.Min(height / 900, height * camera.aspect / 1600);
                root.localPosition = game.DesktopViewLocalPosition + game.DesktopViewLocalRotation * Vector3.forward * distance;
                root.localRotation = game.DesktopViewLocalRotation;
            }
            controls.text = xr ? "Pinch cloud to hold; open to release  /  Two hands: stretch  /  Point and pinch a weather button" : "Drag cloud: move   /   Right-drag or wheel: shape   /   Shift + wheel: height   /   Tab: switch cloud";
        }
        public void Refresh()
        {
            var c = game.ActiveCloud; if (c == null) return;
            level.text = (game.Level < 6 ? (game.Level + 1).ToString("00") + " / 06   " : "FREE GARDEN   ") + LevelGoalEvaluator.Titles[game.Level];
            hint.text = LevelGoalEvaluator.Hints[game.Level]; notice.text = game.Notice;
            resources.text = $"WATER  {c.resources.moisture:P0}     CHARGE  {c.resources.charge:P0}\nMIST  {game.Model.vapor:P0}     CLOUDS  {game.Clouds.Count}/{game.settings.maxClouds}";
            progress.text = $"Bloom  {game.Model.plants[0].growth:P0}  /  {game.Model.plants[1].growth:P0}\nPond  {game.Model.pond:P0}\nReturn cycles  {Mathf.Max(0, game.Model.cycles - game.CycleBaseline)} / 2";
            next.interactable = game.GoalComplete;
            seed.interactable = game.Model.vapor >= game.settings.seedCost && game.Clouds.Count < game.settings.maxClouds && c.resources.mass >= .45f;
            storm.interactable = c.resources.charge >= game.settings.stormCost;
            rain.GetComponentInChildren<TextMeshProUGUI>().text = c.raining ? "STOP RAIN / 1" : "RAIN / 1";
            foreach (var label in worldLabels) label.transform.rotation = game.ViewCamera.transform.rotation;
            for (int i = 0; i < 65; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                var p = game.WorldToGarden(c.transform.position);
                footprint.SetPosition(i, new Vector3(p.x + Mathf.Cos(a) * c.Radius, .065f, p.z + Mathf.Sin(a) * c.Radius));
            }
        }
        public bool TryHandButton(GardenHandPose hand)
        {
            if (Time.unscaledTime - lastHandClick < .3f) return false;
            Vector3 direction = (hand.tip - hand.wrist).normalized;
            Ray ray = new Ray(hand.tip, direction);
            var plane = new Plane(root.forward, root.position);
            if (!plane.Raycast(ray, out float distance) || distance > 2) return false;
            var point = ray.GetPoint(distance);
            foreach (var button in buttons)
            {
                if (!button.IsInteractable()) continue;
                var rect = button.GetComponent<RectTransform>();
                if (rect.rect.Contains(rect.InverseTransformPoint(point)))
                { button.onClick.Invoke(); lastHandClick = Time.unscaledTime; return true; }
            }
            return false;
        }
    }
}
