using Sdlc.Core;
using Sdlc.Core.Init;

namespace Sdlc.Tests;

/// <summary>
/// The release stamp in a solution's framework-owned README (D-93).
/// </summary>
[TestClass]
public sealed class FrameworkStampTests
{
    [TestMethod]
    public void The_readme_adoption_writes_is_stamped_with_this_release()
    {
        Assert.AreEqual(Release.Version, FrameworkStamp.Read(Templates.Readme()));
    }

    [TestMethod]
    public void A_line_reads_back_as_the_version_it_was_written_with()
    {
        Assert.AreEqual("1.2.3-preview.4", FrameworkStamp.Read("# .sdlc\n\n" + FrameworkStamp.Line("1.2.3-preview.4") + "\n"));
    }

    [TestMethod]
    public void A_readme_from_before_stamps_reads_as_unstamped()
    {
        Assert.IsNull(FrameworkStamp.Read("# .sdlc\n\nThis directory is how a change gets from a request to a merged pull request.\n"));
    }

    [TestMethod]
    public void Releases_compare_by_number_and_an_unreadable_one_is_never_current()
    {
        Assert.AreEqual(0, FrameworkStamp.Compare("0.23.0", "0.23.0"));
        Assert.IsTrue(FrameworkStamp.Compare("0.22.0", "0.23.0") < 0);
        Assert.IsTrue(FrameworkStamp.Compare("0.9.0", "0.23.0") < 0, "Numerically, not as text.");
        Assert.IsTrue(FrameworkStamp.Compare("0.24.0", "0.23.0") > 0);
        Assert.IsTrue(FrameworkStamp.Compare("unknown", "0.23.0") < 0);
    }
}
