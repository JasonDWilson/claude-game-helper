using ClaudeGameHelper.Core.Watch;

namespace ClaudeGameHelper.Tests.Watch;

public class FrameDiffTests
{
    private static byte[] Frame(byte value) => Enumerable.Repeat(value, FrameDiff.Width * FrameDiff.Height).ToArray();

    [Fact]
    public void IdenticalFramesDifferByZero() => Assert.Equal(0, FrameDiff.Difference(Frame(80), Frame(80)));

    [Fact]
    public void BlackVersusWhiteIsOne() => Assert.Equal(1, FrameDiff.Difference(Frame(0), Frame(255)));

    [Fact]
    public void NoPreviousFrameCountsAsChanged() => Assert.Equal(1, FrameDiff.Difference(null, Frame(0)));

    [Fact]
    public void SizeMismatchCountsAsChanged() => Assert.Equal(1, FrameDiff.Difference(new byte[10], Frame(0)));

    [Fact]
    public void SmallChangeIsBelowTheThreshold()
    {
        var a = Frame(100);
        var b = Frame(100);
        for (var i = 0; i < 50; i++)
            b[i] = 200;

        Assert.True(FrameDiff.Difference(a, b) < FrameDiff.SkipBelow);
    }

    [Fact]
    public void LargeChangeIsAboveTheThreshold()
    {
        var b = Frame(100);
        for (var i = 0; i < b.Length / 2; i++)
            b[i] = 0;

        Assert.True(FrameDiff.Difference(Frame(100), b) > FrameDiff.SkipBelow);
    }
}
