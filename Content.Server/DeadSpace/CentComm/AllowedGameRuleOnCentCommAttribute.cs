// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.CentComm;

/// <summary>Allows station events with this component to be manually targeted at Central Command.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowedGameRuleOnCentCommAttribute : Attribute;
