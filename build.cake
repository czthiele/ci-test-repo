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
var buildDir_Definitions = srcDir + Directory("CiTest_Definitions")+ Directory("bin") + Directory(configuration);
var buildDir_Client = srcDir + Directory("CiTest_Client")+ Directory("bin") + Directory(configuration);
var artifactsDir = Directory("./artifacts");

var nugetVersion = "0.0.0";
var isDeveloperBuild = BuildSystem.IsLocalBuild;

var gitVersionInfo = GitVersion(new GitVersionSettings {
    UpdateAssemblyInfo = false,
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
        UpdateAssemblyInfo = false,
        OutputType = GitVersionOutput.Json
    });

    nugetVersion = isDeveloperBuild ? "0.0.0" : gitVersionInfo.NuGetVersion;
    Information("VersionFromFile -> {0}", FileReadLines(new FilePath("src/CiTest_Client/Version.yml")));
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
    CleanDirectory(buildDir_Definitions);
    CleanDirectory(buildDir_Client);
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

Task("Pack_Definitions")
    .IsDependentOn("Build")
    .Does(() =>
{
    var releaseNotes = FileReadLines(File("WHATSNEW.txt"));

    var nuGetPackSettings = new NuGetPackSettings {
        Id                       = "TestForCi.Definitions",
        Version                  = nugetVersion,
        Title                    = "Just test title",
        Authors                  = new[] {"Alex GmbH"},
        Owners                   = new[] {"Alex GmbH"},
        Description              = "Test description definitions",
        Summary                  = "Test summary definitions",
        ProjectUrl               = new Uri("https://github.com/czthiele/ci-test-repo"),
        LicenseUrl               = new Uri("https://github.com/czthiele/ci-test-repo/blob/master/src/CiTest_Client/License.txt"),
        Copyright                = string.Format("Copyright © {0}",DateTime.Now.Year),
        ReleaseNotes             = releaseNotes,
        Tags                     = new [] {"TestTag definitions"},
        RequireLicenseAcceptance = true,
        Files                    = new [] {
            new NuSpecContent { Source = "netcoreapp3.1/TestForCi.Definitions.dll", Target = "lib/netcoreapp3.1" },
        },
        Dependencies             = new [] {
            new NuSpecDependency { Id = "Newtonsoft.Json", Version = "12.0.3" }
        },
        BasePath                 = buildDir_Definitions,
        OutputDirectory          = artifactsDir
    };
    NuGetPack(nuGetPackSettings);

    if (BuildSystem.IsRunningOnAppVeyor)
    {
      BuildSystem.AppVeyor.UploadArtifact("artifacts/TestForCi.Definitions.0.1.0.nupkg");
    }
});

Task("Pack_Client")
    .IsDependentOn("Pack_Definitions")
    .Does(() =>
{
    var clientAssemblyInfo2 = ParseAssemblyInfo("./src/CiTest_Client/CiTest_Client.csproj");
    Information("AssemblyVersion (Addin) -> {0}", clientAssemblyInfo2.AssemblyVersion);
    Information("AssemblyFileVersion (Addin) -> {0}", clientAssemblyInfo2.AssemblyFileVersion);
    Information("AssemblyInformationalVersion (Addin) -> {0}", clientAssemblyInfo2.AssemblyInformationalVersion);

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
            new NuSpecContent { Source = "netcoreapp3.1/TestForCi.Client.dll", Target = "lib/netcoreapp3.1" },
        },
        Dependencies             = new [] {
            new NuSpecDependency { Id = "Newtonsoft.Json", Version = "12.0.3" },
            new NuSpecDependency { Id = "TestForCi.Definitions", Version = "0.1.0" }
        },
        BasePath                 = buildDir_Client,
        OutputDirectory          = artifactsDir
    };
    NuGetPack(nuGetPackSettings);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("AppVeyorSetup")
    .IsDependentOn("Pack_Client")
    .IsDependentOn("Pack_Definitions");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
