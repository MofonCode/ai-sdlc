namespace Sdlc.Core.Solutions;

/// <summary>
/// Where the solution being governed is.
/// </summary>
/// <remarks>
/// <para>A type rather than a <see cref="string"/> parameter, so the container can supply it. That
/// sounds like ceremony and is the thing that decides how every operation in the gate is shaped:
/// while the root was a bare string, no handler could be registered by type, because a container has
/// nothing to resolve a <c>string</c> from. Every registration was a factory closing over the value,
/// fourteen of them, and two of those also closed over <see cref="TimeProvider"/> -- so no test
/// could substitute a clock through the container at all.</para>
///
/// <para>With this, a handler is registered as
/// <c>AddTransient&lt;IRequestHandler&lt;Request, Response&gt;, Handler&gt;()</c> and nothing is
/// captured. That is what lets a handler be <c>private</c> inside its operation's holder: the
/// registration names the type, the registration lives inside the holder where that type is
/// accessible, and the holder is the only thing in the program that can see it.</para>
///
/// <para>It carries a path and no behaviour on purpose. An interface here would be an abstraction
/// over a string, and the thing worth abstracting -- how the filesystem is reached -- is not
/// something any of these handlers do directly.</para>
/// </remarks>
/// <param name="Path">The absolute path to the solution's root directory.</param>
public sealed record SolutionRoot(string Path)
{
    /// <summary>The path, so a call site reads as it did when this was a string.</summary>
    /// <returns>The absolute path.</returns>
    public override string ToString() => Path;
}
