#tool "nuget:?package=GitVersion.CommandLine"
#addin "nuget:?package=Cake.FileHelpers"
#addin nuget:?package=Cake.VersionReader

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
var projectFile_Definitions = srcDir + Directory("CiTest_Definitions") + File("CiTest_Definitions.csproj");
var projectFile_Client = srcDir + Directory("CiTest_Client") + File("CiTest_Client.csproj");
var buildDir_Definitions = srcDir + Directory("CiTest_Definitions")+ Directory("bin") + Directory(configuration);
var buildDir_Client = srcDir + Directory("CiTest_Client")+ Directory("bin") + Directory(configuration);
var artifactsDir = Directory("./artifacts");

var nugetVersion_Definitions = "0.0.0";
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

Task("Restore_Definitions")
    .Does(() =>
{
    NuGetRestore(projectFile_Definitions);
});

Task("Build_Definitions")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore_Definitions")
    .IsDependentOn("UpdateAssemblyInfo")
    .Does(() =>
{
    MSBuild(projectFile_Definitions, settings =>
        settings.SetConfiguration(configuration));
});

Task("Pack_Definitions")
    .IsDependentOn("Build_Definitions")
    .Does(() =>
{
    nugetVersion_Definitions = GetVersionNumber(new FilePath(buildDir_Definitions + "net48/TestForCi.Definitions.dll"));
    var releaseNotes = FileReadLines(File("WHATSNEW.txt"));

    var nuGetPackSettings = new NuGetPackSettings {
        Id                       = "TestForCi.Definitions",
        Version                  = nugetVersion_Definitions,
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
            new NuSpecContent { Source = "net48/TestForCi.Definitions.dll", Target = "lib/net48" },
            new NuSpecContent { Source = "net48/TestForCi.Definitions.xml", Target = "lib/net48" },
            new NuSpecContent { Source = "netstandard2.0/TestForCi.Definitions.dll", Target = "lib/netstandard2.0" },
            new NuSpecContent { Source = "netstandard2.0/TestForCi.Definitions.xml", Target = "lib/netstandard2.0" },
            new NuSpecContent { Source = "netcoreapp3.1/TestForCi.Definitions.dll", Target = "lib/netcoreapp3.1" },
            new NuSpecContent { Source = "netcoreapp3.1/TestForCi.Definitions.xml", Target = "lib/netcoreapp3.1" },
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
      BuildSystem.AppVeyor.UploadArtifact("artifacts/TestForCi.Definitions." + nugetVersion_Definitions + ".nupkg");
    }
});

Task("Restore_Client")
    .IsDependentOn("Pack_Definitions")
    .Does(() =>
{
    NuGetRestore(projectFile_Client);
});

Task("Build_Client")
    .IsDependentOn("Restore_Client")
    .Does(() =>
{
    MSBuild(projectFile_Client, settings =>
        settings.SetConfiguration(configuration));
});

Task("Pack_Client")
    .IsDependentOn("Build_Client")
    .Does(() =>
{
    var nugetVersion_Client = GetVersionNumber(new FilePath(buildDir_Definitions + "net48/TestForCi.Client.dll"));
    var releaseNotes = FileReadLines(File("WHATSNEW.txt"));

    var nuGetPackSettings = new NuGetPackSettings {
        Id                       = "TestForCi.Client",
        Version                  = nugetVersion_Client,
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
            new NuSpecContent { Source = "net48/TestForCi.Client.dll", Target = "lib/net48" },
            new NuSpecContent { Source = "net48/TestForCi.Client.xml", Target = "lib/net48" },
            new NuSpecContent { Source = "netstandard2.0/TestForCi.Client.dll", Target = "lib/netstandard2.0" },
            new NuSpecContent { Source = "netstandard2.0/TestForCi.Client.xml", Target = "lib/netstandard2.0" },
            new NuSpecContent { Source = "netcoreapp3.1/TestForCi.Client.dll", Target = "lib/netcoreapp3.1" },
            new NuSpecContent { Source = "netcoreapp3.1/TestForCi.Client.xml", Target = "lib/netcoreapp3.1" },
        },
        Dependencies             = new [] {
            new NuSpecDependency { Id = "Newtonsoft.Json", Version = "12.0.3" },
            new NuSpecDependency { Id = "TestForCi.Definitions", Version = nugetVersion_Definitions }
        },
        BasePath                 = buildDir_Client,
        OutputDirectory          = artifactsDir
    };
    NuGetPack(nuGetPackSettings);

     if (BuildSystem.IsRunningOnAppVeyor)
    {
      BuildSystem.AppVeyor.UploadArtifact("artifacts/TestForCi.Client." + nugetVersion_Client + ".nupkg");
    }
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
