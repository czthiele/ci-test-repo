#tool "nuget:?package=GitVersion.CommandLine"
#addin "nuget:?package=Cake.FileHelpers"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var srcDir = Directory("./src");
var solutionFile = srcDir + File("CiTest.sln");
var buildDir = srcDir + Directory("bin") + Directory(configuration);
var artifactsDir = Directory("./artifacts");

var nugetVersion = "0.0.0";
var isDeveloperBuild = BuildSystem.IsLocalBuild;

var gitVersionInfo = GitVersion(new GitVersionSettings {
    OutputType = GitVersionOutput.Json
});

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Teardown(context =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
// PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("UpdateAssemblyInfo")
    .Does(() =>
{
    gitVersionInfo = GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true,
        OutputType = GitVersionOutput.Json
    });

    nugetVersion = isDeveloperBuild ? "0.0.0" : gitVersionInfo.NuGetVersion;

    Information("AssemblyVersion -> {0}", gitVersionInfo.AssemblySemVer);
    Information("AssemblyFileVersion -> {0}", $"{gitVersionInfo.MajorMinorPatch}.0");
    Information("AssemblyInformationalVersion -> {0}", gitVersionInfo.InformationalVersion);

    if(BuildSystem.AppVeyor.IsRunningOnAppVeyor)
    {
        Information("Nuget Version -> {0}", nugetVersion);
    }
    else
    {
        Warning("Nuget Version -> {0} (developer build)", nugetVersion);
    }
});

Task("AppVeyorSetup")
    .IsDependentOn("UpdateAssemblyInfo")
    .Does(() =>
{
    if(BuildSystem.AppVeyor.IsRunningOnAppVeyor)
    {
        var appVeyorBuildNumber = EnvironmentVariable("APPVEYOR_BUILD_NUMBER");
        var appVeyorBuildVersion = $"{nugetVersion}+{appVeyorBuildNumber}";
        Information("AppVeyor branch name is " + EnvironmentVariable("APPVEYOR_REPO_BRANCH"));
        Information("AppVeyor build version is " + appVeyorBuildVersion);
        BuildSystem.AppVeyor.UpdateBuildVersion(appVeyorBuildVersion);
    }
});

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildDir);
    CleanDirectory(artifactsDir);
});

Task("Restore")
    .Does(() =>
{
    NuGetRestore(solutionFile);
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("UpdateAssemblyInfo")
    .Does(() =>
{
    MSBuild(solutionFile, settings =>
        settings.SetConfiguration(configuration));
});

Task("Pack")
    .IsDependentOn("Build")
    .Does(() =>
{
    var releaseNotes = FileReadLines(File("WHATSNEW.txt"));

    var nuGetPackSettings = new NuGetPackSettings {
        Id                       = "TestForCi.Client",
        Version                  = nugetVersion,
        Title                    = "Just test title",
        Authors                  = new[] {"Alex GmbH"},
        Owners                   = new[] {"Alex GmbH"},
        Description              = "Test description",
        Summary                  = "Test summary",
        ProjectUrl               = new Uri("https://github.com/czthiele/ci-test-repo"),
        LicenseUrl               = new Uri("https://github.com/czthiele/ci-test-repo/blob/master/src/CiTest_Client/License.txt"),
        Copyright                = string.Format("Copyright © {0}",DateTime.Now.Year),
        ReleaseNotes             = releaseNotes,
        Tags                     = new [] {"TestTag"},
        RequireLicenseAcceptance = true,
        Files                    = new [] {
            new NuSpecContent { Source = "netcoreapp3.0/PiWeb.Api.dll", Target = "lib/netcoreapp3.0" },
            new NuSpecContent { Source = "netcoreapp3.0/PiWeb.Api.xml", Target = "lib/netcoreapp3.0" },
        },
        Dependencies             = new [] {
            new NuSpecDependency { Id = "Newtonsoft.Json", Version = "12.0.3" }
        },
        BasePath                 = buildDir,
        OutputDirectory          = artifactsDir
    };
    NuGetPack(nuGetPackSettings);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("AppVeyorSetup")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
