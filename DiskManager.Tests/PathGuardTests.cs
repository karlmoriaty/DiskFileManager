using DiskManager.Services;

namespace DiskManager.Tests;

public class PathGuardTests
{
    private static readonly PathGuard Guard = new(
        new[] { @"C:\Windows", @"C:\Program Files" },
        new[] { @"C:\", @"C:\Users\tester" },
        new[] { @"C:\Windows\Temp" });

    [Theory]
    [InlineData(@"C:\Windows\System32\cmd.exe", true)]
    [InlineData(@"C:\Windows", true)]
    [InlineData(@"C:\Windows10\data.txt", false)]
    [InlineData(@"C:\Windows\Temp\a.tmp", false)]
    [InlineData(@"C:\Windows\Temp", true)]
    [InlineData(@"C:\Windows\TempX\a.tmp", true)]
    [InlineData(@"C:\", true)]
    [InlineData(@"C:\Users\tester", true)]
    [InlineData(@"C:\Users\tester\Documents\a.txt", false)]
    [InlineData(@"C:\Program Files\App\a.dll", true)]
    [InlineData(@"D:\data\a.txt", false)]
    public void IsProtected_RespectsBoundariesAndExceptions(string path, bool expected)
    {
        Assert.Equal(expected, Guard.IsProtected(path));
    }

    [Fact]
    public void IsSameOrInside_DoesNotMatchSiblingPrefix()
    {
        Assert.False(PathGuard.IsSameOrInside(@"C:\Windows10\file.txt", @"C:\Windows"));
        Assert.True(PathGuard.IsSameOrInside(@"C:\Windows\file.txt", @"C:\Windows"));
    }

    [Fact]
    public void Normalize_TrimsTrailingSeparatorButKeepsDriveRoot()
    {
        Assert.Equal(@"C:\Windows", PathGuard.Normalize(@"C:\Windows\"));
        Assert.Equal(@"C:\", PathGuard.Normalize(@"C:\"));
    }

    [Fact]
    public void EmptyPath_IsTreatedAsProtected()
    {
        Assert.True(Guard.IsProtected(string.Empty));
    }

    [Fact]
    public void DefaultGuard_ProtectsDriveRootsAndSystemDirectories_WithoutEnumeratingDrives()
    {
        var guard = PathGuard.CreateDefault();

        Assert.True(guard.IsProtected(@"Z:\"));
        Assert.True(guard.IsProtected(@"Z:\$Recycle.Bin\abc"));
        Assert.True(guard.IsProtected(@"Z:\System Volume Information\index"));
        Assert.False(guard.IsProtected(@"Z:\data\file.txt"));
    }
}