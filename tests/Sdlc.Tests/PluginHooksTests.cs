using Sdlc.Core.Hooks;
using Sdlc.Core.Init;

namespace Sdlc.Tests;

/// <summary>
/// The plugin's copy of a hook steps aside where the solution's own configuration runs the same one, so each check
/// speaks once (D-172).
/// </summary>
[TestClass]
public sealed class PluginHooksTests
{
    [TestMethod]
    public void An_adopted_solution_answers_both_hooks_itself_from_any_directory_inside_it()
    {
        // Claude Code merges identical hooks across settings files, never a plugin's with a settings file's: every
        // turn-end message in a governed solution appeared twice.
        string root = Solution(Templates.HookConfig(HookHost.Claude));
        string inside = Directory.CreateDirectory(Path.Combine(root, "src", "Dice")).FullName;

        Assert.IsTrue(PluginHooks.AnsweredBySettings(inside, HookHost.Claude, turnEnd: false));
        Assert.IsTrue(PluginHooks.AnsweredBySettings(inside, HookHost.Claude, turnEnd: true));
    }

    [TestMethod]
    public void A_repository_not_yet_adopted_is_guarded_by_the_plugin_copy()
    {
        // Before adoption the plugin's hook is the only thing denying an agent 'sdlc init'.
        string root = Solution(settings: null);

        Assert.IsFalse(PluginHooks.AnsweredBySettings(root, HookHost.Claude, turnEnd: false));
    }

    [TestMethod]
    public void A_settings_file_without_the_framework_hook_or_one_nobody_can_read_leaves_the_plugin_copy_speaking()
    {
        string other = Solution("""{ "hooks": { "PreToolUse": [ { "hooks": [ { "type": "command", "command": "lint" } ] } ] } }""");
        string broken = Solution("{ not json");

        Assert.IsFalse(PluginHooks.AnsweredBySettings(other, HookHost.Claude, turnEnd: false));
        Assert.IsFalse(PluginHooks.AnsweredBySettings(other, HookHost.Claude, turnEnd: true));
        Assert.IsFalse(PluginHooks.AnsweredBySettings(broken, HookHost.Claude, turnEnd: false));
    }

    [TestMethod]
    public void The_plugin_marks_its_tool_and_turn_end_hooks_and_not_its_session_start()
    {
        string hooks = File.ReadAllText(Path.Combine(RepoPaths.Root, "hooks", "hooks.json"));

        StringAssert.Contains(hooks, "\"sdlc hook --host claude --source plugin\"");
        StringAssert.Contains(hooks, "\"sdlc hook --host claude --event turn-end --source plugin\"");
        Assert.IsFalse(hooks.Contains("session-start --source", StringComparison.Ordinal), "Session start is the plugin's alone; adoption writes none.");
    }

    private static string Solution(string? settings)
    {
        string root = Path.Combine(Path.GetTempPath(), "sdlc-plugin-hooks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, ".git"));

        if (settings is not null)
        {
            Directory.CreateDirectory(Path.Combine(root, ".claude"));
            File.WriteAllText(Path.Combine(root, ".claude", "settings.json"), settings);
        }

        return root;
    }
}
