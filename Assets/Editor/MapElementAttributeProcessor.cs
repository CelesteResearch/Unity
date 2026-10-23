using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Simulation;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

// Keep inspector dependencies outside the portable data model.
public sealed class MapElementAttributeProcessor : OdinAttributeProcessor<global::Celeste.Core.Maps.MapElement>
{
    public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member,
        List<Attribute> attributes)
    {
        if (member.Name == nameof(global::Celeste.Core.Maps.MapElement.Name) ||
            member.Name == nameof(global::Celeste.Core.Maps.MapElement.Attributes) ||
            member.Name == nameof(global::Celeste.Core.Maps.MapElement.Children))
            attributes.Add(new ShowInInspectorAttribute());
    }
}
