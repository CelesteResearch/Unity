using System;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Celeste.Unity.Editor
{
    // Runtime collections can shrink between inspector updates. The installed
    // Odin version enumerates only the current child count when disposing a
    // property, leaving cached drawers beyond that count undisposed.
    [DrawerPriority(1, 0, 0)]
    public sealed class RuntimeCollectionLifetimeDrawer : OdinDrawer, IDisposable
    {
        private static readonly Action<PropertyChildren, int> DisposeFromIndex =
            (Action<PropertyChildren, int>)typeof(PropertyChildren)
                .GetMethod("ClearAndDisposeChildrenFromIndex", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action<PropertyChildren, int>));

        private PropertyChildren children;
        private int previousCount;

        public override bool CanDrawProperty(InspectorProperty property) =>
            property.ChildResolver is ICollectionResolver;

        protected override void Initialize()
        {
            children = Property.Children;
            previousCount = children.Count;
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var count = children.Count;
            if (count < previousCount) DisposeFromIndex(children, count);
            previousCount = count;
            CallNextDrawer(label);
        }

        public void Dispose()
        {
            if (children == null) return;
            // Retained children remain owned by Odin's normal disposal path.
            // Use Odin's own removal routine to clear all three child caches.
            DisposeFromIndex(children, children.Count);
            children = null;
        }
    }
}
