using System.Collections.Generic;
using System.Linq;
using Celeste.Simulation.Rendering;
using Celeste.Input.Gameplay.Bindings;

namespace Celeste.Unity
{
    public sealed partial class ModernControls
    {
        private bool tasRawInput;
        private readonly HashSet<int> tasKeys = new HashSet<int>();
        private readonly HashSet<int> tasDown = new HashSet<int>();
        private readonly HashSet<int> tasUp = new HashSet<int>();
        public bool TasRawInputActive => tasRawInput;
        public int TasMouseX { get; private set; }
        public int TasMouseY { get; private set; }
        public int TasMouseScroll { get; private set; }
        public bool TasMouseLeft { get; private set; }
        public bool TasMouseRight { get; private set; }
        public bool TasMouseMiddle { get; private set; }
        public bool TasMouseX1 { get; private set; }
        public bool TasMouseX2 { get; private set; }

        public void CaptureTasKeyboard(IEnumerable<int> values)
        {
            var next = new HashSet<int>(values);
            tasDown.Clear();
            tasUp.Clear();
            foreach (int key in next)
                if (!tasKeys.Contains(key))
                    tasDown.Add(key);
            foreach (int key in tasKeys)
                if (!next.Contains(key))
                    tasUp.Add(key);
            tasKeys.Clear();
            tasKeys.UnionWith(next);
            heldKeys = tasKeys.OrderBy(k => k).Select(k => new ControlBindingValue(k, KeyName(k))).ToArray();
            tasRawInput = true;
        }

        public void CaptureTasMouse(int x, int y, int scroll, bool left, bool right, bool middle, bool x1, bool x2)
        {
            TasMouseX = x;
            TasMouseY = y;
            TasMouseScroll = scroll;
            TasMouseLeft = left;
            TasMouseRight = right;
            TasMouseMiddle = middle;
            TasMouseX1 = x1;
            TasMouseX2 = x2;
        }

        public void ReleaseTasRawInput()
        {
            tasRawInput = false;
            tasKeys.Clear();
            tasDown.Clear();
            tasUp.Clear();
            CaptureTasMouse(0, 0, 0, false, false, false, false, false);
        }
    }
}
