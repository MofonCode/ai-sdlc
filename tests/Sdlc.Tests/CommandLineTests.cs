using Sdlc.Cli;

namespace Sdlc.Tests;

[TestClass]
public sealed class CommandLineTests
{
    [TestMethod]
    public void The_first_token_is_the_verb_and_is_lower_cased()
    {
        var line = CommandLine.Parse(["Constraints"]);

        Assert.AreEqual("constraints", line.Verb);
    }

    [TestMethod]
    public void No_arguments_leaves_the_verb_empty_so_the_caller_can_print_usage()
    {
        var line = CommandLine.Parse([]);

        Assert.AreEqual(string.Empty, line.Verb);
    }

    [TestMethod]
    public void An_option_first_leaves_the_verb_empty_rather_than_treating_it_as_one()
    {
        var line = CommandLine.Parse(["--brief"]);

        Assert.AreEqual(string.Empty, line.Verb);
        Assert.IsTrue(line.HasOption("brief"));
    }

    [TestMethod]
    public void An_option_followed_by_a_value_carries_it()
    {
        var line = CommandLine.Parse(["constraints", "--for", "intent"]);

        Assert.IsTrue(line.TryOption("for", out string? value));
        Assert.AreEqual("intent", value);
    }

    [TestMethod]
    public void An_option_written_with_an_equals_sign_carries_its_value_too()
    {
        var line = CommandLine.Parse(["constraints", "--for=spec"]);

        Assert.IsTrue(line.TryOption("for", out string? value));
        Assert.AreEqual("spec", value);
    }

    [TestMethod]
    public void An_option_with_nothing_after_it_is_a_flag_rather_than_an_error()
    {
        var line = CommandLine.Parse(["constraints", "--brief"]);

        Assert.IsTrue(line.HasOption("brief"));
        Assert.IsFalse(line.TryOption("brief", out string? value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void A_flag_before_another_option_does_not_swallow_it_as_its_value()
    {
        var line = CommandLine.Parse(["constraints", "--brief", "--for", "plan"]);

        Assert.IsTrue(line.HasOption("brief"));
        Assert.IsFalse(line.TryOption("brief", out _));
        Assert.IsTrue(line.TryOption("for", out string? artifact));
        Assert.AreEqual("plan", artifact);
    }

    [TestMethod]
    public void Positional_arguments_after_the_verb_keep_their_order()
    {
        var line = CommandLine.Parse(["refine", "20260929_1456_32", "spec"]);

        Assert.AreEqual("refine", line.Verb);
        CollectionAssert.AreEqual(new[] { "20260929_1456_32", "spec" }, line.Arguments.ToArray());
    }

    [TestMethod]
    public void An_option_name_is_matched_without_regard_to_case()
    {
        var line = CommandLine.Parse(["constraints", "--For", "intent"]);

        Assert.IsTrue(line.TryOption("for", out string? value));
        Assert.AreEqual("intent", value);
    }

    [TestMethod]
    public void A_cycle_id_is_a_positional_argument_and_not_mistaken_for_an_option_value()
    {
        var line = CommandLine.Parse(["status", "20260929_1456_32", "--stage", "spec"]);

        CollectionAssert.AreEqual(new[] { "20260929_1456_32" }, line.Arguments.ToArray());
        Assert.IsTrue(line.TryOption("stage", out string? stage));
        Assert.AreEqual("spec", stage);
    }
}
