using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Noetix.Sdk.Bumi.Native;
using Xunit;

namespace Noetix.Sdk.Bumi.Tests;

public class AbiContractTests
{
    // Sizes asserted by static_assert in native/include/noetix_bumi_c.h.
    [Theory]
    [InlineData(typeof(MotorState), 32)]
    [InlineData(typeof(MotorCmd), 48)]
    [InlineData(typeof(ImuData), 296)]
    [InlineData(typeof(JoyData), 72)]
    [InlineData(typeof(BmsData), 8)]
    [InlineData(typeof(RobotHardwareStatus), 1056)]
    [InlineData(typeof(NativeMediaHeader), 24)]
    [InlineData(typeof(NativeMediaSystemStatus), 32)]
    [InlineData(typeof(NativeMediaSystemError), 40)]
    [InlineData(typeof(NativeMediaAudioStream), 64)]
    [InlineData(typeof(NativeMediaVideoStream), 64)]
    public void Struct_size_matches_native_header(Type type, int expected)
    {
        Assert.Equal(expected, Marshal.SizeOf(type));
    }

    [Fact]
    public void Robot_status_field_offsets_match_native_layout()
    {
        Assert.Equal(0, (int)Marshal.OffsetOf<RobotHardwareStatus>(nameof(RobotHardwareStatus.ImuData)));
        Assert.Equal(296, (int)Marshal.OffsetOf<RobotHardwareStatus>(nameof(RobotHardwareStatus.RemoteData)));
        Assert.Equal(368, (int)Marshal.OffsetOf<RobotHardwareStatus>(nameof(RobotHardwareStatus.MotorData)));
        Assert.Equal(1040, (int)Marshal.OffsetOf<RobotHardwareStatus>(nameof(RobotHardwareStatus.BmsData)));
        Assert.Equal(1048, (int)Marshal.OffsetOf<RobotHardwareStatus>(nameof(RobotHardwareStatus.WorkMode)));
        Assert.Equal(24, (int)Marshal.OffsetOf<MotorState>(nameof(MotorState.MotorId)));
        Assert.Equal(26, (int)Marshal.OffsetOf<MotorState>(nameof(MotorState.Error)));
        Assert.Equal(28, (int)Marshal.OffsetOf<MotorState>(nameof(MotorState.Temperature)));
    }

    [Fact]
    public void Inline_arrays_have_expected_element_counts()
    {
        Assert.Equal(21, Unsafe.SizeOf<MotorStateArray>() / Unsafe.SizeOf<MotorState>());
        Assert.Equal(14, Unsafe.SizeOf<Int32Array14>() / sizeof(int));
    }

    // Values come from noetix::ControlCmd in the official common.h (SDK 3.1.0).
    [Fact]
    public void ControlCmd_values_match_official_enum()
    {
        string[] official =
        [
            "WALK", "SWING", "SHAKE", "CHEER", "RUN", "START", "SWITCH", "STARTTEACH", "SAVETEACH",
            "ENDTEACH", "PLAYTEACH", "DANCE", "FALLTOSTAND", "STANDTOFALL", "DANCE1", "DANCE2", "TEAR", "DEFAULT",
        ];

        var names = Enum.GetNames<ControlCmd>();
        Assert.Equal(official.Length, names.Length);
        for (var i = 0; i < official.Length; i++)
        {
            Assert.Equal(official[i], names[i].ToUpperInvariant());
            Assert.Equal((uint)i, (uint)Enum.Parse<ControlCmd>(names[i]));
        }
    }

    [Fact]
    public void Media_enum_values_match_official_enums()
    {
        Assert.Equal(["READY", "SLEEPED", "WAKEUPED", "EXIT"], Upper<WorkStatus>());
        Assert.Equal(
            ["SYSTEMLAUNCH", "CMDRESET", "AUDIOWAKEUPED", "CMDWAKEUPED", "AUDIOSLEEPED", "CMDSLEEPED", "TIMEOUTSLEEPED", "ERRORSLEEPED"],
            Upper<StatusChangeReason>());
        Assert.Equal(["TOWAKEUP", "TOSLEEP", "TORESET"], Upper<SystemControlType>());
    }

    // Every function declared in the C headers must have a P/Invoke with the same name.
    [Fact]
    public void Every_exported_C_function_has_a_binding()
    {
        var declared = DeclaredFunctions();

        var bound = new[] { typeof(NativeMethods), typeof(NativeLowMethods) }
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            .Select(m => m.Name)
            .Where(n => n.StartsWith("noetix_", StringComparison.Ordinal))
            .ToHashSet();

        Assert.True(declared.Count > 50);
        Assert.Empty(declared.Except(bound));
        Assert.Empty(bound.Except(declared));
    }

    [Fact]
    public void Every_exported_C_function_is_implemented()
    {
        var sourceDir = Path.GetDirectoryName(FindRepoFile("native", "src", "library.cpp"))!;
        var source = string.Join("\n", Directory.GetFiles(sourceDir, "*.cpp").Select(File.ReadAllText));

        // Macro-generated functions are expanded by name pattern.
        var expandedSource = source + "\n" + ExpandMacros(source);
        foreach (var name in DeclaredFunctions())
        {
            Assert.True(
                Regex.IsMatch(expandedSource, $@"\b{name}\s*\("),
                $"{name} is declared in a header but not defined in the native sources.");
        }
    }

    private static HashSet<string> DeclaredFunctions() =>
        new[] { "noetix_bumi_c.h", "noetix_bumi_low_c.h" }
            .Select(h => File.ReadAllText(FindRepoFile("native", "include", h)))
            .SelectMany(text => Regex.Matches(text, @"NOETIX_BUMI_API\s+[\w\s\*]+?\b(noetix_\w+)\s*\(").Select(m => m.Groups[1].Value))
            .ToHashSet();


    [Fact]
    public void SetJoint_requires_exactly_21_commands()
    {
        Assert.Throws<ArgumentException>(() => LowController.Instance.SetJoint(new MotorCmd[20]));
    }

    private static string[] Upper<T>() where T : struct, Enum =>
        Enum.GetNames<T>().Select(n => n.ToUpperInvariant()).ToArray();

    private static string ExpandMacros(string source)
    {
        var lines = new List<string>();
        foreach (Match m in Regex.Matches(source, @"^NOETIX_BOOL_ACCESSORS\((\w+)\)", RegexOptions.Multiline))
        {
            lines.Add($"int32_t noetix_media_get_{m.Groups[1].Value}( int32_t noetix_media_set_{m.Groups[1].Value}(");
        }

        foreach (Match m in Regex.Matches(source, @"^NOETIX_MEDIA_COMMAND\((\w+)\)", RegexOptions.Multiline))
        {
            lines.Add($"int32_t noetix_media_{m.Groups[1].Value}(");
        }

        return string.Join("\n", lines);
    }

    private static string FindRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine([dir.FullName, .. parts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }
}
