// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Server.DeadSpace.CentComm;

/// <summary>The server indexes all parallax IDs without loading client-only texture sources.</summary>
[Prototype("parallax")]
public sealed partial class ServerParallaxPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;
}

/// <summary>Rendering fields are validated and consumed by the client's ParallaxPrototype.</summary>
[TypeSerializer]
public sealed class ServerParallaxSerializer : ITypeSerializer<ServerParallaxPrototype, MappingDataNode>
{
    [SuppressMessage("Usage", "RA0039", Justification = "The prototype manager calls this serializer to instantiate its prototype.")]
    public ServerParallaxPrototype Read(ISerializationManager serializationManager, MappingDataNode node,
        IDependencyCollection dependencies, SerializationHookContext hookCtx, ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<ServerParallaxPrototype>? instanceProvider = null)
    {
        return new ServerParallaxPrototype { ID = node.Get<ValueDataNode>("id").Value };
    }

    public ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node,
        IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        return new ValidatedValueNode(node);
    }

    public DataNode Write(ISerializationManager serializationManager, ServerParallaxPrototype value,
        IDependencyCollection dependencies, bool alwaysWrite = false, ISerializationContext? context = null)
    {
        var node = new MappingDataNode();
        node.Add("id", value.ID);
        return node;
    }
}
