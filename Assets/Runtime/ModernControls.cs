using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Input;
using Celeste.Input.Gameplay;
using Celeste.Simulation.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Celeste.Input.Gameplay.Bindings;
using Celeste.Game.Controls;

namespace Celeste.Unity
{
    // Unity owns physical device reads only. Thresholds, buffers, repeat and overlap stay in GameInput.
    public sealed partial class ModernControls : IDisposable
    {
        sealed class PadFrame
        {
            public bool Attached;
            public int Digital;
            public Vector2 Left, Right;
            public float LeftTrigger, RightTrigger;

            public float Value(int button)
            {
                switch (button)
                {
                    case 2097152: return -Left.x;
                    case 1073741824: return Left.x;
                    case 268435456: return Left.y;
                    case 536870912: return -Left.y;
                    case 16777216: return Right.y;
                    case 33554432: return -Right.y;
                    case 67108864: return Right.x;
                    case 134217728: return -Right.x;
                    case 4194304: return RightTrigger;
                    case 8388608: return LeftTrigger;
                    default: return (Digital & button) == button ? 1 : 0;
                }
            }

            public bool HasInput(PadFrame previous) => Attached && !previous.Attached || Digital != previous.Digital ||
                                                       LeftTrigger > .01f || RightTrigger > .01f ||
                                                       Left.magnitude > .01f || Right.magnitude > .01f;
        }

        readonly Dictionary<GameAction, IInputBinding> bindings = new Dictionary<GameAction, IInputBinding>();

        readonly Dictionary<GameAction, ManualBinding> manual = new Dictionary<GameAction, ManualBinding>();

        // MANUAL-FRAME ALIASES, derived from the live controller bindings. See BuildAliasMap.
        // Each call allocates its own list rather than sharing a buffer: `CaptureManual` expands three
        // sets that may be the SAME array reference, and a shared buffer would clear the set it is
        // still enumerating.
        Dictionary<GameAction, GameAction[]> aliasCache;

        GameAction[] Expand(ICollection<GameAction> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<GameAction>();
            var map = aliasCache ?? (aliasCache = BuildAliasMap());
            var buffer = new List<GameAction>(source.Count + 2);
            foreach (var action in source)
            {
                if (!buffer.Contains(action)) buffer.Add(action);
                if (!map.TryGetValue(action, out var same)) continue;
                foreach (var alias in same)
                    if (!buffer.Contains(alias))
                        buffer.Add(alias);
            }

            return buffer.ToArray();
        }

        sealed class RumbleSlot
        {
            public float Time, Strength;
        }

        readonly RumbleSlot[] rumbles = Enumerable.Range(0, 4).Select(_ => new RumbleSlot()).ToArray();
        readonly Gamepad[] physicalPads = new Gamepad[4];

        readonly List<PadFrame>[] pendingPads =
            Enumerable.Range(0, 4).Select(_ => new List<PadFrame> { new PadFrame() }).ToArray();

        readonly PadFrame[] pads = Enumerable.Range(0, 4).Select(_ => new PadFrame()).ToArray();
        readonly HashSet<int> keys = new HashSet<int>(), down = new HashSet<int>(), up = new HashSet<int>();
        ControlBindingValue[] heldKeys = Array.Empty<ControlBindingValue>();
        bool manualMode, controllerFocus;
        int selectedIndex;
        readonly InputContext inputContext = new InputContext();
        public ControlBindingSettings Settings { get; private set; }
        public GameInput Input { get; private set; }
        public Action AfterInitialize;
        public Keyboard KeyboardSource { get; set; }

        public int DeviceSelectedIndex
        {
            get => selectedIndex;
            set
            {
                int next = Math.Max(0, Math.Min(3, value));
                if (next != selectedIndex)
                {
                    selectedIndex = next;
                    Initialize();
                }
            }
        }

        public bool Disabled
        {
            get => inputContext.Disabled;
            set => inputContext.Disabled = value;
        }

        public string OverrideInputPrefix { get; set; }
        public bool ControllerAttached => SelectedPad != null;
        public bool GuiInputController => CurrentGuiPrefix != "keyboard";

        public string CurrentGuiPrefix => !string.IsNullOrEmpty(OverrideInputPrefix) ? OverrideInputPrefix :
            controllerFocus ? "xb1" : "keyboard";

        public string AttachedGuiPrefix => !string.IsNullOrEmpty(OverrideInputPrefix) ? OverrideInputPrefix :
            ControllerAttached ? "xb1" : "keyboard";

        public IReadOnlyList<ControlBindingValue> HeldKeys => heldKeys;
        public float RumbleTime => rumbles[selectedIndex].Time;
        public float RumbleStrength => rumbles[selectedIndex].Strength;

        Gamepad SelectedPad
        {
            get
            {
                SynchronizePads();
                return physicalPads[selectedIndex];
            }
        }

        public bool IsControllerFocused { get; private set; }
        public float GetRumbleTime(int index) => rumbles[index].Time;
        public float GetRumbleStrength(int index) => rumbles[index].Strength;

        void SynchronizePads()
        {
            for (int i = 0; i < 4; i++)
                if (physicalPads[i] != null && !physicalPads[i].added)
                    physicalPads[i] = null;
            foreach (var connected in Gamepad.all)
            {
                if (Array.IndexOf(physicalPads, connected) >= 0) continue;
                int free = Array.IndexOf(physicalPads, null);
                if (free >= 0) physicalPads[free] = connected;
            }
        }

        public ModernControls()
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                manual.Add(action, new ManualBinding());
                bindings.Add(action, new Bound(this, action));
            }

            var settings = new ControlBindingSettings();
            settings.AfterLoad();
            Bind(settings);
        }

        // `aliasCache` is dropped here, not updated: the settings object is mutable and a remap goes
        // through this call, so caching across a remap would answer with the OLD button table -- and a
        // wrong alias is indistinguishable from a working one at every call site that reads it.
        public void Bind(ControlBindingSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            aliasCache = null;
        }

        public GameInput Create()
        {
            Input = new GameInput(inputContext, bindings, selectedIndex);
            return Input;
        }

        public void Initialize()
        {
            if (Input != null)
            {
                Input.Initialize(bindings, selectedIndex);
                AfterInitialize?.Invoke();
            }
        }

        public void Sample(bool focused = true, bool active = true, bool keyboardSuppressed = false)
        {
            manualMode = false;
            var keyboard = KeyboardSource ?? Keyboard.current;
            var now = new HashSet<int>();
            if (focused && active && !keyboardSuppressed && keyboard != null && keyboard.added)
            {
                foreach (var item in keyNames)
                    if (TryMapKey(item.Key, out Key key) &&
                        keyboard[key].ReadValue() >= keyboard[key].pressPointOrDefault)
                        now.Add(item.Key);
                // XNA reports both Enter locations as Keys.Enter.
                if (keyboard.numpadEnterKey.isPressed) now.Add(13);
            }

            foreach (int key in now)
                if (!keys.Contains(key))
                    down.Add(key);
            foreach (int key in keys)
                if (!now.Contains(key))
                    up.Add(key);
            keys.Clear();
            keys.UnionWith(now);
            heldKeys = keys.OrderBy(k => k).Select(k => new ControlBindingValue(k, KeyName(k))).ToArray();
            SynchronizePads();
            bool activity = false, attached = false;
            for (int i = 0; i < 4; i++)
            {
                var current = focused && active && physicalPads[i] != null ? ReadPad(physicalPads[i]) : new PadFrame();
                if (focused && active)
                {
                    if (!pads[i].Attached && current.Attached) IsControllerFocused = true;
                    activity |= current.HasInput(pads[i]);
                    attached |= current.Attached;
                }

                pads[i] = current;
                pendingPads[i].Add(current);
            }

            if (focused && active)
            {
                if (activity) controllerFocus = true;
                if (!attached || !activity && keys.Count > 0) controllerFocus = false;
            }
        }

        public void CaptureManual(params GameAction[] held) => CaptureManual(held, null, null);

        // The edge-carrying overload. `pressed`/`released` are the module's own per-binding edges for
        // THIS frame; both are null for the console/direct caller, which then gets the held-derived
        // edge from the one Sample site.
        //
        // PAIRED INVARIANT, stated because the type system does not carry it: `held` and the two edge
        // arrays are ONE VALUE split for type-compatibility, so nothing stops them describing different
        // frames -- "two definitions that can drift" in a new costume. They must therefore be published
        // from one site and cleared from one site (TasManualInputSink). The check below converts a
        // silent desync into a loud one, which has been the only reliable way to keep a paired invariant
        // alive in this codebase.
        public void CaptureManual(GameAction[] held, GameAction[] pressed, GameAction[] released,
            bool expandAliases = true)
        {
            manualMode = true;
            held = held ?? Array.Empty<GameAction>();
            // Legacy logical manual frames expand through the user's binding table.
            // Already-resolved physical frames (including TAS) explicitly opt out.
            //
            // TAS resolves BindingHelper's temporary fixed bindings in its shared
            // library. It must not inherit a player's current key/controller mapping.
            // Console/direct callers retain the ordinary manual interpretation.
            //
            // The edge arrays are expanded by the SAME function, so a press edge exists exactly when
            // its action is held -- the precondition `ManualBinding.Sample` clamps with `&& value` is
            // preserved by construction rather than by the clamp. Expanding only `held` would give
            // `MenuConfirm` a held state with no press edge on the frame the button went down, i.e. a
            // menu that can only be confirmed by a SECOND press.
            // TAS supplies the mod's fixed-binding result, not a request to press
            // every user-bound button behind a logical action. Ordinary manual
            // callers retain their existing user-binding expansion.
            if (expandAliases)
            {
                held = Expand(held);
                if (pressed != null) pressed = Expand(pressed);
                if (released != null) released = Expand(released);
            }

            // AN INCONSISTENT PAIR DEGRADES TO "NO EDGES SUPPLIED" -- i.e. to the derived edge, which is
            // exactly the pre-change behaviour, so nothing new can go wrong and the console path is
            // untouched. The shipping path must always produce a SEMANTICALLY VALID result; the GATE is
            // the only thing that refuses, via a fixture that provokes this desync and must make the gate
            // fail. Same rule as `&& value` in Sample below, applied twice: degrade to the last known-good
            // semantics in shipping, make the violation loud at the gate.
            //
            // A throw here would be worse than useless for THIS project: the method is differential
            // measurement, so a crash converts a code bug into a dead session with NO artifact, whereas a
            // wrong-but-running session preserves the evidence that locates the bug.
            if ((pressed == null) != (released == null))
            {
                pressed = null;
                released = null;
            }

            // Analog state has already been published for this frame. Keep it
            // separate from digital buttons: a neutral stick must not suppress a
            // held D-pad button, and threshold edges use the preceding tick's stick.
            foreach (var item in manual)
            {
                // null propagates, so an absent edge array means "derive it", not "no edge": passing
                // `false` here would suppress the fallback and silently drop every console press.
                bool? p = pressed == null ? (bool?)null : Array.IndexOf(pressed, item.Key) >= 0;
                bool? r = released == null ? (bool?)null : Array.IndexOf(released, item.Key) >= 0;
                item.Value.Sample(Array.IndexOf(held, item.Key) >= 0, p, r, physicalEdges: !expandAliases);
            }
        }

        // Each directional binding receives one signed thumbstick component
        // (+ means this direction). The axis identifies the producer's X/Y
        // component, not a second overriding channel inside the same binding.
        public void SetManualAnalog(GameAction action, int axis, float value)
        {
            if (axis != 0 && axis != 1) throw new ArgumentOutOfRangeException(nameof(axis));
            if (!manual.TryGetValue(action, out var binding)) return;
            binding.Analog = value;
            binding.AnalogBound = true;
        }

        // Clears every manual analog component at once. Called by the TAS sink when it hands the
        // device back to physical input, so a half-written stick cannot outlive the run.
        public void ClearManualAnalog()
        {
            foreach (var item in manual)
            {
                item.Value.Analog = item.Value.PreviousAnalog = 0f;
                item.Value.AnalogBound = false;
            }
        }

        public void EndSimulationFrame()
        {
            down.Clear();
            up.Clear();
            for (int i = 0; i < 4; i++)
            {
                pendingPads[i].Clear();
                pendingPads[i].Add(pads[i]);
            }

            foreach (var value in manual.Values)
            {
                value.Press = value.Release = false;
                value.PreviousAnalog = value.Analog;
            }
        }

        public bool AnyGamepadConfirmPressed(out int gamepadIndex)
        {
            if (Input == null) throw new InvalidOperationException("Create input before querying confirm.");
            int previous = Input[GameAction.MenuConfirm].GamepadIndex;
            gamepadIndex = -1;
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    Input[GameAction.MenuConfirm].GamepadIndex = i;
                    if (Input[GameAction.MenuConfirm].Pressed)
                    {
                        gamepadIndex = i;
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                Input[GameAction.MenuConfirm].GamepadIndex = previous;
            }
        }

        public bool KeyboardPressed(int code) => !Disabled && (tasRawInput ? tasDown : down).Contains(code);

        // OuiChapterPanel reads the raw first gamepad's Start edge, not the
        // remappable Pause binding.  TAS manual frames encode S as that Start
        // button, so use its unbuffered edge while manual input is active.
        public bool FirstGamepadStartPressed => !Disabled && (manualMode
            ? tasRawInput && manual[GameAction.Pause].Press
            : ButtonPressed(16, 0));

        public bool KeyboardHeld(int code) => !Disabled && (tasRawInput ? tasKeys : keys).Contains(code);
        public bool KeyboardReleased(int code) => !Disabled && (tasRawInput ? tasUp : up).Contains(code);
        public bool ButtonPressed(int code) => PadEdge(code, 0, true, false);
        public bool ButtonPressed(int code, int index) => PadEdge(code, 0, true, false, index);
        public bool AnalogPressed(int code, float threshold) => PadEdge(code, threshold, true, true);
        public bool AnalogPressed(int code, float threshold, int index) => PadEdge(code, threshold, true, true, index);
        public bool ButtonReleased(int code) => PadEdge(code, 0, false, false);
        public bool ButtonReleased(int code, int index) => PadEdge(code, 0, false, false, index);
        public bool AnalogReleased(int code, float threshold) => PadEdge(code, threshold, false, true);

        public bool AnalogReleased(int code, float threshold, int index) =>
            PadEdge(code, threshold, false, true, index);

        bool PadEdge(int code, float threshold, bool pressed, bool thresholded, int index = -1)
        {
            if (Disabled) return false;
            var history = pendingPads[index < 0 ? selectedIndex : index];
            for (int i = 1; i < history.Count; i++)
            {
                bool before = thresholded ? Check(history[i - 1], code, threshold) : history[i - 1].Value(code) > 0;
                bool after = thresholded ? Check(history[i], code, threshold) : history[i].Value(code) > 0;
                if (pressed ? after && !before : !after && before) return true;
            }

            return false;
        }

        static bool IsAnalog(int code) => code >= 2097152;

        static bool Check(PadFrame state, int code, float threshold) => Array.IndexOf(ButtonCodes, code) < 0 ? false :
            IsAnalog(code) ? state.Value(code) >= threshold : code != 2048 && state.Value(code) > 0;

        public float ControllerAxis(int code, float threshold) => ControllerAxis(code, threshold, selectedIndex);

        public float ControllerAxis(int code, float threshold, int index) =>
            Disabled || !Check(pads[index], code, threshold) ? 0 : pads[index].Value(code);

        static PadFrame ReadPad(Gamepad gamepad)
        {
            var result = new PadFrame { Attached = gamepad.added };
            if (!result.Attached) return result;
            var left = gamepad.leftStick.ReadUnprocessedValue();
            var right = gamepad.rightStick.ReadUnprocessedValue();
            result.Left = new Vector2(StickAxis(left.x, false), StickAxis(left.y, false));
            result.Right = new Vector2(StickAxis(right.x, true), StickAxis(right.y, true));
            result.LeftTrigger = TriggerAxis(gamepad.leftTrigger.ReadUnprocessedValue());
            result.RightTrigger = TriggerAxis(gamepad.rightTrigger.ReadUnprocessedValue());
            ButtonControl[] controls =
            {
                gamepad.dpad.up, gamepad.dpad.down, gamepad.dpad.left, gamepad.dpad.right, gamepad.startButton,
                gamepad.selectButton, gamepad.leftStickButton, gamepad.rightStickButton, gamepad.leftShoulder,
                gamepad.rightShoulder, gamepad.TryGetChildControl<ButtonControl>("systemButton"), gamepad.buttonSouth,
                gamepad.buttonEast, gamepad.buttonWest, gamepad.buttonNorth
            };
            for (int i = 0; i < controls.Length; i++)
                if (controls[i] != null && controls[i].isPressed)
                    result.Digital |= ButtonCodes[i];
            return result;
        }

        // Recover signed XInput-sized coordinates from the standard normalized InputSystem device state.
        // Never stack Unity's radial stickDeadzone processor on XNA's independent-axis deadzone.
        public static float StickAxis(float raw, bool right)
        {
            int value = (int)Math.Round(Mathf.Clamp(raw, -1, 1) * (raw < 0 ? 32768f : 32767f));
            return LinearDeadZone(value, 32767, right ? 8689 : 7849);
        }

        public static float TriggerAxis(float raw) =>
            LinearDeadZone((int)Math.Round(Mathf.Clamp01(raw) * 255), 255, 30);

        static float LinearDeadZone(float value, float max, float deadzone)
        {
            if (value < -deadzone) value += deadzone;
            else
            {
                if (!(value > deadzone)) return 0;
                value -= deadzone;
            }

            return Mathf.Clamp(value / (max - deadzone), -1, 1);
        }

        public void Rumble(int strength, int length, int amount = 2)
        {
            float[] strengths = { .15f, .4f, 1f, .05f };
            float[] lengths = { .1f, .25f, .5f, 1f, 2f };
            RumbleSpecific(strengths[strength], lengths[length], amount);
        }

        // amount follows original RumbleAmount: Off=0, Half=1, On=2 (confirmed at host call boundary).
        public void RumbleSpecific(float strength, float time, int amount = 2)
        {
            if (Disabled || amount == 0) return;
            if (amount == 1) strength *= .5f;
            var state = rumbles[selectedIndex];
            if (state.Time <= 0 || strength > state.Strength || strength == state.Strength && time > state.Time)
            {
                SelectedPad?.SetMotorSpeeds(strength, strength);
                state.Strength = strength;
                state.Time = time;
            }
        }

        public void TickRumble(float deltaTime, bool active = true)
        {
            SynchronizePads();
            for (int i = 0; i < 4; i++)
            {
                var state = rumbles[i];
                if (state.Time > 0)
                {
                    state.Time -= deltaTime;
                    if (active && state.Time <= 0) physicalPads[i]?.SetMotorSpeeds(0, 0);
                }

                if (!active) physicalPads[i]?.SetMotorSpeeds(0, 0);
            }
        }

        public void StopRumble()
        {
            SelectedPad?.SetMotorSpeeds(0, 0);
            rumbles[selectedIndex].Time = 0;
        }

        public void Clear()
        {
            SynchronizePads();
            for (int i = 0; i < 4; i++)
            {
                physicalPads[i]?.SetMotorSpeeds(0, 0);
                rumbles[i].Time = 0;
                rumbles[i].Strength = 0;
            }

            keys.Clear();
            down.Clear();
            up.Clear();
            heldKeys = Array.Empty<ControlBindingValue>();
            for (int i = 0; i < 4; i++)
            {
                pads[i] = new PadFrame();
                pendingPads[i].Clear();
                pendingPads[i].Add(pads[i]);
            }

            controllerFocus = IsControllerFocused = false;
            foreach (var value in manual.Values)
            {
                value.Held = value.Press = value.Release = false;
                value.Analog = value.PreviousAnalog = 0f;
                value.AnalogBound = false;
            }
        }

        public void Dispose() => Clear();

        public ControlBindingState Binding(GameAction action)
        {
            switch (action)
            {
                case GameAction.Left: return Settings.Left;
                case GameAction.Right: return Settings.Right;
                case GameAction.Up: return Settings.Up;
                case GameAction.Down: return Settings.Down;
                case GameAction.LeftMoveOnly: return Settings.LeftMoveOnly;
                case GameAction.RightMoveOnly: return Settings.RightMoveOnly;
                case GameAction.UpMoveOnly: return Settings.UpMoveOnly;
                case GameAction.DownMoveOnly: return Settings.DownMoveOnly;
                case GameAction.LeftDashOnly: return Settings.LeftDashOnly;
                case GameAction.RightDashOnly: return Settings.RightDashOnly;
                case GameAction.UpDashOnly: return Settings.UpDashOnly;
                case GameAction.DownDashOnly: return Settings.DownDashOnly;
                case GameAction.Jump: return Settings.Jump;
                case GameAction.Dash: return Settings.Dash;
                case GameAction.Grab: return Settings.Grab;
                case GameAction.Talk: return Settings.Talk;
                case GameAction.CrouchDash: return Settings.DemoDash;
                case GameAction.Pause: return Settings.Pause;
                case GameAction.QuickRestart: return Settings.QuickRestart;
                case GameAction.MenuLeft: return Settings.MenuLeft;
                case GameAction.MenuRight: return Settings.MenuRight;
                case GameAction.MenuUp: return Settings.MenuUp;
                case GameAction.MenuDown: return Settings.MenuDown;
                case GameAction.MenuConfirm: return Settings.Confirm;
                case GameAction.MenuCancel: return Settings.Cancel;
                case GameAction.MenuJournal: return Settings.Journal;
                case GameAction.Escape: return FixedEscape;
                case GameAction.MountainLeft: return MountainLeft;
                case GameAction.MountainRight: return MountainRight;
                case GameAction.MountainUp: return MountainUp;
                case GameAction.MountainDown: return MountainDown;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        // ==========================================================================================
        // MANUAL-FRAME ALIASES: the SAME-PHYSICAL-BUTTON set, which the physical path gets for free
        // and the manual path does not.
        //
        // WHY THIS EXISTS, measured rather than assumed. CelesteTAS feeds BUTTONS: `D` sets the D-pad
        // down, `J` sets gamepad `A` (BindingHelper.cs:72-99 -> InputHelper.FeedInputs). The original
        // then reads ACTIONS, and its default bindings put TWO actions on each of those buttons --
        // Settings.cs:291 `MenuDown.Add(LeftThumbstickDown, DPadDown)` alongside
        // Settings.cs:`Down.Add(...DPadDown)`, and Settings.cs:`Confirm.Add(Buttons.A)` alongside
        // `Jump.Add(Buttons.A, ...)`. So in the original `D` moves a MENU selection and `J` confirms
        // one, with no special case anywhere.
        //
        // The port routes each TAS letter to ONE GameAction (TasAction.cs:120-171) and its manual path
        // answers `Axis`/`Pressed`/`Released` from `manual[action]` alone -- it never consults the
        // controller bindings (ModernControls.cs `Bound`, manual branch). So the fan-out that makes
        // `D` navigate a menu was simply absent, and every TAS that opens the pause menu to skip a
        // cutscene froze on its first selection move instead. That was the owner's sighting verbatim:
        // *"只打开了 esc 菜单，但是没有切换到跳过剧情按钮并确认，完全停在那"* (2A.tas:100-101 is
        // `1,S` then `1,D,J`, i.e. open, move, confirm).
        //
        // WHY THE PAIRING IS COMPUTED FROM THE BINDINGS AND NOT WRITTEN DOWN. A hand-written table
        // (`Down->MenuDown`, `Jump->MenuConfirm`, ...) is a SECOND definition of the button map, and
        // the two definitions can drift -- here they would drift the moment the owner rebinds a key,
        // which is a standing requirement (*"我是希望那些按键能和原版一样正常改键这些的"*). Reading the
        // same `ControlBindingState.Controller` list the physical path reads makes drift
        // unrepresentable: remap `MenuDown` to a different button and the alias follows, because it is
        // derived from what the remap wrote.
        //
        // KEYBOARD IS DELIBERATELY EXCLUDED. Upstream does bind `MenuDown` to Keys.Down as well, but a
        // TAS letter is a button, not a key: `TasAction` never emits a keyboard code, so no keyboard
        // alias is reachable. Adding one would create alias pairs that no TAS frame can trigger and
        // that would have to be justified separately -- an untested rule is worse than an absent one.
        // ==========================================================================================
        Dictionary<GameAction, GameAction[]> BuildAliasMap()
        {
            var shared = new Dictionary<string, List<GameAction>>(StringComparer.Ordinal);
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                ControlBindingState state;
                // `Binding` throws for an action it does not name; every GameAction in this port is
                // named, but a new enum member must not turn a TAS frame into an exception, so an
                // unnamed action simply contributes no aliases.
                try
                {
                    state = Binding(action);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }

                if (state == null || state.Controller == null) continue;
                foreach (var button in state.Controller)
                {
                    if (string.IsNullOrEmpty(button.Name)) continue;
                    if (!shared.TryGetValue(button.Name, out var list))
                        shared[button.Name] = list = new List<GameAction>();
                    if (!list.Contains(action)) list.Add(action);
                }
            }

            var result = new Dictionary<GameAction, GameAction[]>();
            foreach (var pair in shared)
            {
                if (pair.Value.Count < 2) continue; // a button only one action reads: nothing to fan out
                foreach (var action in pair.Value)
                {
                    var others = pair.Value.Where(a => a != action);
                    result[action] = others.ToArray();
                }
            }

            return result;
        }

        static ControlBindingState Fixed(string key, string button = null)
        {
            var binding = new ControlBindingState();
            binding.Keyboard.Add(ControlBindingSettings.Key(key));
            if (button != null) binding.Controller.Add(ControlBindingSettings.Button(button));
            return binding;
        }

        static readonly ControlBindingState FixedEscape = Fixed("Escape"),
            MountainLeft = Fixed("A", "RightThumbstickLeft"),
            MountainRight = Fixed("D", "RightThumbstickRight"),
            MountainUp = Fixed("W", "RightThumbstickUp"),
            MountainDown = Fixed("S", "RightThumbstickDown");

        sealed class Bound : IInputBinding
        {
            readonly ModernControls owner;
            readonly GameAction action;

            public Bound(ModernControls owner, GameAction action)
            {
                this.owner = owner;
                this.action = action;
            }

            public float Axis(int gamepadIndex, float threshold)
            {
                if (owner.Disabled) return 0;
                if (owner.manualMode)
                {
                    // BindingHelper puts the thumbstick before the D-pad.
                    // Binding.Axis returns the first nonzero value meeting its
                    // threshold, then falls back to the digital binding.
                    var m = owner.manual[action];
                    if (m.AnalogBound && m.Analog >= threshold && m.Analog != 0f) return m.Analog;
                    return m.Held ? 1 : 0;
                }

                var b = owner.Binding(action);
                foreach (var key in b.Keyboard)
                    if (owner.KeyboardHeld(key.Code))
                        return 1;
                foreach (var button in b.Controller)
                {
                    float value = owner.ControllerAxis(button.Code, threshold, gamepadIndex);
                    if (value != 0) return value;
                }

                return 0;
            }

            public bool Check(int gamepadIndex, float threshold)
            {
                if (owner.Disabled) return false;
                if (owner.manualMode)
                {
                    var m = owner.manual[action];
                    return m.Held || m.AnalogBound && m.Analog >= threshold;
                }

                return Axis(gamepadIndex, threshold) != 0;
            }

            public bool Pressed(int gamepadIndex, float threshold)
            {
                if (owner.Disabled) return false;
                if (owner.manualMode)
                {
                    var m = owner.manual[action];
                    return m.Press || m.AnalogBound && m.Analog >= threshold && m.PreviousAnalog < threshold;
                }

                var b = owner.Binding(action);
                foreach (var key in b.Keyboard)
                    if (owner.KeyboardPressed(key.Code))
                        return true;
                foreach (var button in b.Controller)
                    if (owner.PadEdge(button.Code, threshold, true, true, gamepadIndex))
                        return true;
                return false;
            }

            public bool Released(int gamepadIndex, float threshold)
            {
                if (owner.Disabled) return false;
                if (owner.manualMode)
                {
                    var m = owner.manual[action];
                    return m.Release || m.AnalogBound && m.PreviousAnalog >= threshold && m.Analog < threshold;
                }

                var b = owner.Binding(action);
                foreach (var key in b.Keyboard)
                    if (owner.KeyboardReleased(key.Code))
                        return true;
                foreach (var button in b.Controller)
                    if (owner.PadEdge(button.Code, threshold, false, true, gamepadIndex))
                        return true;
                return false;
            }
        }

        sealed class ManualBinding
        {
            public bool Held, Press, Release;

            // Digital edges and thresholded analog edges are independent, as
            // in Monocle.Binding.Pressed/Released (including binding handoffs).
            public float Analog, PreviousAnalog;
            public bool AnalogBound;

            public void Sample(bool value) => Sample(value, null, null);

            // THE ONE DEFINITION SITE for the manual channel's edges. `pressed`/`released` are supplied
            // by the caller that knows the real physical binding -- the TAS module's per-binding edge
            // (TasInputSource: `pressed = (first && !previousFirst) || (second && !previousSecond)`).
            // The derived formula is the fallback for the caller that only knows the held set:
            // GameInputSession.cs:61, console/direct manual input, which has no physical bindings
            // behind it. One rule, one site -- never a second, weaker definition of Pressed.
            //
            // A press requires a held binding. A release-while-held is valid for
            // resolved physical inputs (Binding.Released ORs each binding's edge).
            // Only the legacy logical manual fallback uses a single-button clamp.
            public void Sample(bool value, bool? pressed, bool? released, bool physicalEdges = false)
            {
                bool p = (pressed ?? (value && !Held)) && value;
                // Binding.Released ORs per-button edges: one button can be
                // released while another binding keeps this same action held.
                bool r = (released ?? (!value && Held)) && (physicalEdges || !value);
                Held = value;
                Press |= p;
                Release |= r;
            }
        }
    }
}
