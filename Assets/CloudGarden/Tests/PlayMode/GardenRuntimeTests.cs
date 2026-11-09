using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CloudGarden.Tests
{
    public sealed class GardenRuntimeTests
    {
        CloudGardenGame game;
        Mouse testMouse;
        Keyboard testKeyboard;
        InputDevice[] physicalDevices;
        InputSettings previousInputSettings, testInputSettings;
        string previousSave;
        const string SaveKey = "CloudGarden.VerticalSlice.v1";
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousSave = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            yield return SceneManager.LoadSceneAsync("CloudGardenDemo");
            yield return null;
            game = Object.FindFirstObjectByType<CloudGardenGame>();
            Assert.NotNull(game); Assert.AreEqual(1, game.Clouds.Count);
        }
        [TearDown] public void RestoreSave()
        {
            if (testMouse != null) { InputSystem.RemoveDevice(testMouse); testMouse = null; }
            if (testKeyboard != null) { InputSystem.RemoveDevice(testKeyboard); testKeyboard = null; }
            if (physicalDevices != null) foreach (var device in physicalDevices) if (device.added) InputSystem.EnableDevice(device);
            physicalDevices = null;
            if (previousInputSettings != null)
            {
                InputSystem.settings = previousInputSettings;
                Object.DestroyImmediate(testInputSettings);
                previousInputSettings = testInputSettings = null;
            }
            if (previousSave == null) PlayerPrefs.DeleteKey(SaveKey); else PlayerPrefs.SetString(SaveKey, previousSave);
            PlayerPrefs.Save();
        }
        void Simulate(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) game.AdvanceSimulation(.05f);
        }
        void RainAt(Vector2 point, float seconds)
        {
            game.ActiveCloud.transform.position = game.GardenToWorld(new Vector3(point.x, .48f, point.y));
            if (!game.ActiveCloud.raining) game.ToggleRain();
            Simulate(seconds);
            if (game.ActiveCloud.raining) game.ToggleRain();
        }
        [UnityTest] public IEnumerator UndoAndResetRestoreShapesResourcesAndEcology()
        {
            var before = game.Capture(); game.ShapeCloud(.7f, -.2f);
            game.ActiveCloud.Indent(game.ActiveCloud.transform.position + Vector3.up * .1f, .04f);
            game.Model.Rain(GardenModel.Beds[0], .2f, .6f); game.Undo();
            Assert.AreEqual(before.clouds[0].shape, game.ActiveCloud.shape);
            Assert.AreEqual(0, game.ActiveCloud.dentDepth);
            Assert.AreEqual(before.model.plants[0].water, game.Model.plants[0].water);
            game.Model.vapor = 1; game.GrowSeed(); Assert.AreEqual(2, game.Clouds.Count);
            game.RestartLevel(); Assert.AreEqual(1, game.Clouds.Count); Assert.AreEqual(0, game.Model.seedsCreated);
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<CloudShapeController>(FindObjectsSortMode.None).Length);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator AllSixLessonsReachFreeGardenWithoutResourceCheats()
        {
            game.ShapeCloud(.35f, -.12f); game.ReleaseCloud(); Assert.IsTrue(game.GoalComplete); game.NextLevel();
            RainAt(GardenModel.Beds[0], 18); Assert.IsTrue(game.GoalComplete, "First plant should bloom"); game.NextLevel();
            Simulate(24); RainAt(GardenModel.Pond, 12); Simulate(30);
            game.GrowSeed(); Assert.AreEqual(2, game.Clouds.Count, "Rain / spring must fund the new seed");
            Assert.IsTrue(game.GoalComplete); game.NextLevel();
            RainAt(GardenModel.Beds[1], 18); Assert.IsTrue(game.GoalComplete, "Both banks should bloom"); game.NextLevel();
            Simulate(25); RainAt(GardenModel.Pond, 8);
            game.ActiveCloud.transform.position = game.WorldOrigin + new Vector3(GardenModel.Tower.x, .5f, GardenModel.Tower.y);
            game.Storm(); game.Storm(); Simulate(8);
            Assert.IsTrue(game.GoalComplete, "Two sparks plus pond water must drive the pump"); game.NextLevel();
            Simulate(40); game.GrowSeed(); Simulate(40); game.GrowSeed();
            Assert.IsTrue(game.GoalComplete, "Two returned-water seed cycles must complete the garden"); game.NextLevel();
            Assert.AreEqual(6, game.Level); Assert.AreEqual(4, game.Clouds.Count);
            game.ResumeGarden(); Assert.AreEqual(6, game.Level);
            yield return null; LogAssert.NoUnexpectedReceived();
        }
        void PrepareDesktopInput()
        {
            // Batch mode has no focused Game View. Isolate this test's input routing.
            previousInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testInputSettings;
            physicalDevices = InputSystem.devices.Where(d => d.enabled && (d is Mouse || d is Keyboard)).ToArray();
            foreach (var device in physicalDevices) InputSystem.DisableDevice(device);
            testMouse = InputSystem.AddDevice<Mouse>(); testKeyboard = InputSystem.AddDevice<Keyboard>();
        }
        [UnityTest] public IEnumerator DesktopMouseCanGrabShapeAndReleaseCloud()
        {
            PrepareDesktopInput();
            var mouse = Mouse.current;
            // Batch mode may not expose a physical mouse; still exercise the real input path.
            if (mouse == null) mouse = testMouse = InputSystem.AddDevice<Mouse>();
            var cloud = game.ActiveCloud;
            Vector2 screen = game.ViewCamera.WorldToScreenPoint(cloud.transform.position);
            var state = new MouseState { position = screen };
            InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left)); yield return null;
            state.position = screen + new Vector2(55, 10); InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left, false)); yield return null;
            Assert.Greater(Vector3.Distance(cloud.transform.position, game.WorldOrigin + new Vector3(-.27f, .50f, -.06f)), .01f);
            InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Right)); yield return null;
            state.delta = new Vector2(40, -15); state.position += state.delta;
            InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Right)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = state.position }); yield return null;
            Assert.Greater(cloud.deformation, .08f);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator PlacementMovesWeatherCanvasAndSnapshotsTogether()
        {
            var initial = game.Capture();
            var canvas = game.GardenRoot.GetComponentInChildren<Canvas>();
            var uiPosition = canvas.transform.localPosition;
            game.placementAnchor.SetPose(new Vector3(1.2f, .9f, -.8f), Quaternion.Euler(8, 83, 0));
            yield return null;
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
            Assert.AreEqual(uiPosition, canvas.transform.localPosition);
            Assert.Less(Vector3.Distance(initial.clouds[0].position, game.Capture().clouds[0].position), .0001f);
            var clamped = game.WorldToGarden(game.ClampCloud(game.GardenToWorld(new Vector3(3, 2, -4))));
            Assert.Less(Vector3.Distance(clamped, new Vector3(.6f, .8f, -.32f)), .0001f);
            game.SetVirtualEnvironmentVisible(false);
            Assert.IsFalse(game.VirtualEnvironment.activeInHierarchy);
            Assert.IsTrue(canvas.isActiveAndEnabled);
            RainAt(GardenModel.Beds[0], 18);
            Assert.Greater(game.Model.plants[0].growth, .5f);
            game.ActiveCloud.transform.position = game.GardenToWorld(new Vector3(GardenModel.Tower.x, .5f, GardenModel.Tower.y));
            float energy = game.Model.energy; game.Storm(); Assert.Greater(game.Model.energy, energy);
            game.Undo(); Assert.AreEqual(energy, game.Model.energy);
            game.RestartLevel();
            Assert.Less(Vector3.Distance(initial.clouds[0].position, game.Capture().clouds[0].position), .0001f);
            var replacement = new GameObject("Replacement table anchor").AddComponent<GardenPlacementAnchor>();
            replacement.SetPose(new Vector3(-.7f, .8f, .4f), Quaternion.Euler(0, -45, 0));
            game.SetPlacementAnchor(replacement);
            Assert.AreSame(replacement.transform, game.GardenRoot.parent);
            Assert.AreEqual(Vector3.zero, game.GardenRoot.localPosition);
            Assert.AreSame(game.GardenRoot, canvas.transform.parent);
            game.SetVirtualEnvironmentVisible(true);
            Assert.IsTrue(game.VirtualEnvironment.activeInHierarchy);
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        sealed class ReplayHands : IHandPoseProvider
        {
            public GardenHandPose left, right;
            public GardenHandPose Read(bool isLeft) => isLeft ? left : right;
        }
        static GardenHandPose Hand(Vector3 position, float curl = 0, bool pinch = false)
            => new GardenHandPose { tracked = true, fingersValid = true, curl = curl, pinching = pinch,
                palm = position, tip = position, wrist = position + Vector3.up * .08f };
        GardenInteraction Replay(ReplayHands poses)
        {
            var input = game.GetComponent<GardenInteraction>(); input.enabled = false; input.PoseProvider = poses; return input;
        }

        [UnityTest] public IEnumerator ReplayedFistTogglesRainOnceAndCanUndo()
        {
            var hands = new ReplayHands { left = Hand(game.ActiveCloud.transform.position, 1) };
            var input = Replay(hands);
            for (int i = 0; i < 11; i++) input.ProcessHands(.05f);
            Assert.IsFalse(game.ActiveCloud.raining);
            input.ProcessHands(.05f); Assert.IsTrue(game.ActiveCloud.raining);
            for (int i = 0; i < 30; i++) input.ProcessHands(.05f);
            Assert.IsTrue(game.ActiveCloud.raining);
            hands.left = default; input.ProcessHands(.05f);
            hands.left = Hand(game.ActiveCloud.transform.position, 1);
            for (int i = 0; i < 15; i++) input.ProcessHands(.05f);
            Assert.IsTrue(game.ActiveCloud.raining);
            game.Undo(); Assert.IsFalse(game.ActiveCloud.raining);
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ReplayedGrabStretchAndTrackingRecoveryDoNotJump()
        {
            Vector3 center = game.ActiveCloud.transform.position;
            var hands = new ReplayHands { left = Hand(center + Vector3.left * .1f, 0, true), right = Hand(center + Vector3.right * .1f, 0, true) };
            var input = Replay(hands); input.ProcessHands(.02f);
            hands.left.palm.x -= .03f; hands.right.palm.x += .03f; input.ProcessHands(.02f);
            Assert.Greater(game.ActiveCloud.shape.x, 1);
            Vector3 position = game.ActiveCloud.transform.position, shape = game.ActiveCloud.shape;
            hands.left = default; hands.right.palm.z += .03f; input.ProcessHands(.02f);
            Assert.AreEqual(position, game.ActiveCloud.transform.position);
            hands.left = Hand(center + new Vector3(-.2f, .07f, .05f), 0, true); input.ProcessHands(.02f);
            Assert.AreEqual(shape, game.ActiveCloud.shape);
            Assert.AreEqual(position, game.ActiveCloud.transform.position);
            hands.left.pinching = hands.right.pinching = false; input.ProcessHands(.02f);
            Assert.IsTrue(game.ShapedAndPlaced);
            game.Undo(); Assert.AreEqual(Vector3.one, game.ActiveCloud.shape);
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ReplayedKneadAndPatPreserveResourcesAndUndo()
        {
            var before = game.Capture(); Vector3 center = game.ActiveCloud.transform.position;
            var hands = new ReplayHands { left = Hand(center + Vector3.left * .1f), right = Hand(center + Vector3.right * .1f) };
            var input = Replay(hands); input.ProcessHands(.02f);
            for (int i = 0; i < 3; i++)
            { hands.left.palm.z += .02f; hands.right.palm.z -= .02f; input.ProcessHands(.02f); }
            Assert.Greater(game.ActiveCloud.shape.y, 1);
            Assert.AreEqual(before.clouds[0].resources.mass, game.ActiveCloud.resources.mass);
            Assert.AreEqual(before.clouds[0].resources.moisture, game.ActiveCloud.resources.moisture);
            game.Undo(); Assert.AreEqual(Vector3.one, game.ActiveCloud.shape);
            center = game.ActiveCloud.transform.position;
            hands.right = default; hands.left = Hand(center + Vector3.up * .15f); input.ProcessHands(.02f);
            hands.left.palm.y -= .02f; input.ProcessHands(.02f);
            hands.left.palm.y -= .02f; input.ProcessHands(.02f);
            Assert.Greater(game.ActiveCloud.dentDepth, 0);
            game.Undo(); Assert.AreEqual(0, game.ActiveCloud.dentDepth);
            Assert.AreEqual(Vector3.one, game.ActiveCloud.shape);
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ContactChargeUsesLocalMotionAndUndoResetsItsHistory()
        {
            var first = game.ActiveCloud; first.transform.localPosition = new Vector3(0, .5f, 0);
            var second = game.SpawnCloud(game.GardenToWorld(new Vector3(.3f, .5f, 0)));
            first.resources.charge = second.resources.charge = .1f;
            game.AdvanceSimulation(.05f); float stationary = first.resources.charge - .1f;
            game.placementAnchor.SetPose(new Vector3(3, 1, -2), Quaternion.Euler(0, 90, 0));
            float before = first.resources.charge; game.AdvanceSimulation(.05f);
            Assert.AreEqual(stationary, first.resources.charge - before, .00001f);
            game.SaveUndo(); before = first.resources.charge;
            second.transform.localPosition += Vector3.forward * .02f; game.AdvanceSimulation(.05f);
            Assert.Greater(first.resources.charge - before, stationary);
            game.Undo(); Assert.AreEqual(before, game.Clouds[0].resources.charge);
            game.AdvanceSimulation(.05f); Assert.AreEqual(before + stationary, game.Clouds[0].resources.charge, .00001f);
            game.Clouds[0].resources.charge = 1; game.AdvanceSimulation(.05f);
            Assert.AreEqual(1, game.Clouds[0].resources.charge);
            yield return null; LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator DesktopGestureKeysAndWorldSpaceButtonWorkWithEnvironmentOff()
        {
            PrepareDesktopInput();
            var keyboard = Keyboard.current;
            if (keyboard == null) keyboard = testKeyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = Mouse.current;
            if (mouse == null) mouse = testMouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K)); yield return null;
            Assert.Greater(game.ActiveCloud.shape.y, 1);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            game.Undo(); Assert.AreEqual(Vector3.one, game.ActiveCloud.shape);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P)); yield return null;
            Assert.Greater(game.ActiveCloud.dentDepth, 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F10)); yield return null;
            Assert.IsFalse(game.VirtualEnvironmentVisible);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            var button = System.Array.Find(game.GardenRoot.GetComponentsInChildren<UnityEngine.UI.Button>(), b => b.name == "RAIN  /  1");
            Assert.NotNull(button);
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            Vector2 screen = game.ViewCamera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
            var mouseState = new MouseState { position = screen };
            InputSystem.QueueStateEvent(mouse, mouseState); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, mouseState.WithButton(MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, mouseState.WithButton(MouseButton.Left, false)); yield return null; yield return null;
            Assert.IsTrue(game.ActiveCloud.raining, "The world-space rain button must receive mouse clicks.");
            yield return null; LogAssert.NoUnexpectedReceived();
        }
    }
}
