namespace Shirobot.Plugin.KotoSub.Infrastructure;

/// <summary>
/// Marks a type that knowingly breaks an architecture limit. The architecture tests list every exception
/// and fail if their number grows, so each one needs a reason.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
internal sealed class ArchitectureExceptionAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
