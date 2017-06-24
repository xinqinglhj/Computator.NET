#addin Cake.Coveralls
#addin nuget:?package=Cake.Codecov
#addin nuget:?package=Cake.AppPackager

#tool coveralls.net
#tool nuget:?package=OpenCover
#tool nuget:?package=NUnit.ConsoleRunner
#tool nuget:?package=Codecov

//////////////////////////////////////////////////////////////////////
// ENVIRONMENTAL VARIABLES
//////////////////////////////////////////////////////////////////////
var coverallsRepoToken = EnvironmentVariable("COVERALLS_REPO_TOKEN");//"KEH5rJaqCoWoCV2MhkrMlClj3SVIlB2Eu0YK4mqmhRM+ANEfGiFyROo2RWHkJXQz"
var configuration = (EnvironmentVariable("configuration") ?? EnvironmentVariable("build_config")) ?? "Release";
var netmoniker = EnvironmentVariable("netmoniker") ?? "net461";
var travisOsName = EnvironmentVariable("TRAVIS_OS_NAME");
var dotNetCore = EnvironmentVariable("DOTNETCORE");
string monoVersion = null;
string monoVersionShort = null;

Type type = Type.GetType("Mono.Runtime");
if (type != null)
{
	var displayName = type.GetMethod("GetDisplayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
	if (displayName != null)
	{
		monoVersion = displayName.Invoke(null, null).ToString();
		monoVersionShort = string.Join(".",System.Text.RegularExpressions.Regex.Match(monoVersion,@"(\d+\.\d+(?:\.\d+(?:\.\d+)?)?)").Value.Split(".".ToCharArray(),StringSplitOptions.RemoveEmptyEntries).Take(3));
	}
}

var isMonoButSupportsMsBuild = monoVersion!=null && System.Text.RegularExpressions.Regex.IsMatch(monoVersion,@"([5-9]|\d{2,})\.\d+\.\d+(\.\d+)?");



//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");


//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var solution = "Computator.NET" + (netmoniker != "net461" ? "."+netmoniker : "") + ".sln";
var mainProject = "Computator.NET/Computator.NET" + (netmoniker != "net461" ? "."+netmoniker : "") + ".csproj";
var installerProject = "Computator.NET.Setup/Computator.NET.Setup.csproj";
var unitTestsProject = "Computator.NET.Tests/Computator.NET.Tests.csproj";
var integrationTestsProject = "Computator.NET.IntegrationTests/Computator.NET.IntegrationTests.csproj";

var allTestsBinaries = "**/bin/" + configuration+ "/" + netmoniker + "/*Test*.dll";
var integrationTestsBinaries = "Computator.NET.IntegrationTests/"+"bin/" + configuration+ "/" + netmoniker + "/*Test*.dll";
var unitTestsBinaries = "Computator.NET.Tests/"+"bin/" + configuration+ "/" + netmoniker + "/*Test*.dll";
var netVersion =  System.Text.RegularExpressions.Regex.Replace(netmoniker.ToLowerInvariant().Replace("net",""), ".{1}", "$0.").TrimEnd('.');

var msBuildSettings = new MSBuildSettings {
	ArgumentCustomization = args=>args.Append(@" /p:TargetFramework="+netmoniker),//args=>args.Append(@" /p:TargetFrameworkVersion=v"+netVersion),
    Verbosity = Verbosity.Minimal,
    ToolVersion = MSBuildToolVersion.Default,//The highest available MSBuild tool version//VS2017
    Configuration = configuration,
    PlatformTarget = PlatformTarget.MSIL,
	MSBuildPlatform = MSBuildPlatform.Automatic,
	DetailedSummary = true,
    };

	if(!IsRunningOnWindows() && isMonoButSupportsMsBuild)
	{
		if(System.Environment.OSVersion.Platform != System.PlatformID.MacOSX)
			msBuildSettings.ToolPath = new FilePath(@"/usr/lib/mono/msbuild/15.0/bin/MSBuild.dll");//hack for Linux bug - missing MSBuild path
		else
			msBuildSettings.ToolPath = new FilePath(@"/Library/Frameworks/Mono.framework/Versions/"+monoVersionShort+@"/lib/mono/msbuild/15.0/bin/MSBuild.exe");
	}

var xBuildSettings = new XBuildSettings {
	ArgumentCustomization = args=>args.Append(@" /p:TargetFramework="+netmoniker),//args=>args.Append(@" /p:TargetFrameworkVersion=v"+netVersion),
    Verbosity = Verbosity.Minimal,
    ToolVersion = XBuildToolVersion.Default,//The highest available XBuild tool version//NET40
    Configuration = configuration,
    //PlatformTarget = PlatformTarget.MSIL,
    };

var monoEnvVars = new Dictionary<string,string>() { {"DISPLAY", "99.0"},{"MONO_WINFORMS_XIM_STYLE", "disabled"} };

if(!IsRunningOnWindows())
{
	if(msBuildSettings.EnvironmentVariables==null)
		msBuildSettings.EnvironmentVariables = new Dictionary<string,string>();
	if(xBuildSettings.EnvironmentVariables==null)
		xBuildSettings.EnvironmentVariables = new Dictionary<string,string>();

	foreach(var monoEnvVar in monoEnvVars)
	{
		msBuildSettings.EnvironmentVariables.Add(monoEnvVar.Key,monoEnvVar.Value);
		xBuildSettings.EnvironmentVariables.Add(monoEnvVar.Key,monoEnvVar.Value);
	}
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Install-Linux")
	.Does(() =>
{
	StartProcess("sudo", "apt-get install git-all");
	StartProcess("git", "pull");
	StartProcess("sudo", @"apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF");
	StartProcess("echo", @"""deb http://download.mono-project.com/repo/debian wheezy main"" | sudo tee  /etc/apt/sources.list.d/mono-xamarin.list");
	StartProcess("echo", @"""deb http://download.mono-project.com/repo/debian wheezy-apache24-compat main"" | sudo tee -a /etc/apt/sources.list.d/mono-xamarin.list");
	StartProcess("sudo", "apt-get install libgcc1-*");
	StartProcess("sudo", "apt-get install libc6-*");
	StartProcess("sudo", "apt-get install mono-complete");
	StartProcess("sudo", "apt-get install monodevelop --fix-missing");
	StartProcess("sudo", "apt-get install libmono-webbrowser4.0-cil");
	StartProcess("sudo", "apt-get install libgluezilla");
	StartProcess("sudo", "apt-get install curl");
	StartProcess("sudo", "apt-get install libgtk2.0-dev");
});

Task("Clean")
	.Does(() =>
{
	DeleteDirectories(GetDirectories("Computator.NET*/**/bin"), recursive:true);
	DeleteDirectories(GetDirectories("Computator.NET*/**/obj"), recursive:true);
	DeleteDirectories(GetDirectories("AppPackages"), recursive:true);
});

Task("Restore")
	.IsDependentOn("Clean")
	.Does(() =>
{

if(dotNetCore=="1")
	StartProcess("dotnet", "restore "+solution);
else
	NuGetRestore("./"+solution);
if (travisOsName == "linux")
{
	StartProcess("sudo", "apt-get install libgsl2");
	System.Environment.SetEnvironmentVariable("DISPLAY", "99.0", System.EnvironmentVariableTarget.Process);//StartProcess("export", "DISPLAY=:99.0");
	
	var xvfvProcessSettings = new ProcessSettings() { Arguments="--start --quiet --pidfile /tmp/custom_xvfb_99.pid --make-pidfile --background --exec /usr/bin/Xvfb -- :99 -ac -screen 0 1280x1024x16" };
	if(xvfvProcessSettings.EnvironmentVariables==null)
		xvfvProcessSettings.EnvironmentVariables=new Dictionary<string,string>();
	foreach(var envVar in monoEnvVars)
		xvfvProcessSettings.EnvironmentVariables.Add(envVar.Key, envVar.Value);
	StartProcess(@"/sbin/start-stop-daemon", xvfvProcessSettings);
	
	System.Environment.SetEnvironmentVariable("DISPLAY", "99.0", System.EnvironmentVariableTarget.Process);//StartProcess("export", "DISPLAY=:99.0");
	StartProcess("sleep", "3");//give xvfb some time to start
}
else if(travisOsName == "osx")
{
	StartProcess("brew", "install gsl");
}

if(travisOsName=="linux" || travisOsName=="osx")
	System.Environment.SetEnvironmentVariable("MONO_WINFORMS_XIM_STYLE", "disabled", System.EnvironmentVariableTarget.Process);//StartProcess("export", "MONO_WINFORMS_XIM_STYLE=disabled");
});

Task("Build")
	.IsDependentOn("Restore")
	.Does(() =>
{
	if(IsRunningOnWindows() || isMonoButSupportsMsBuild)
	{
	  // Use MSBuild
	  MSBuild(mainProject, msBuildSettings);
	  MSBuild(unitTestsProject, msBuildSettings);
	  MSBuild(integrationTestsProject, msBuildSettings);
	  
	}
	else
	{
	  // Use XBuild
	  XBuild(mainProject, xBuildSettings);
	  XBuild(unitTestsProject, xBuildSettings);
	  XBuild(integrationTestsProject, xBuildSettings);
	}
});

Task("UnitTests")
	.IsDependentOn("Build")
	.Does(() =>
{
	NUnit3(unitTestsBinaries, new NUnit3Settings() {
		Labels = NUnit3Labels.All,
		//NoResults = true
		});
});

Task("IntegrationTests")
	.IsDependentOn("Build")
	.Does(() =>
{
	NUnit3(integrationTestsBinaries, new NUnit3Settings() {
		Labels = NUnit3Labels.All,
		//NoResults = true
		});
});

Task("AllTests")
	.IsDependentOn("Build")
	.Does(() =>
{
	NUnit3(allTestsBinaries, new NUnit3Settings() {
		Labels = NUnit3Labels.All,
		//NoResults = true
		});
});

Task("Calculate-Coverage")
	.IsDependentOn("Build")
	.Does(() =>
{
	OpenCover(tool => {
  tool.NUnit3(allTestsBinaries,
	new NUnit3Settings {
	  NoResults = true,
	  //InProcess = true,
	  //Domain = Domain.Single,
	  Where = "cat!=LongRunningTests",
	  ShadowCopy = false,
	});
  },
  new FilePath("coverage.xml"),
  (new OpenCoverSettings()
	{
		Register="user",
		SkipAutoProps = true,
		
	})
	.WithFilter("+[Computator.NET*]*")
	.WithFilter("-[Computator.NET.Core]Computator.NET.Core.Properties.*")
	.WithFilter("-[Computator.NET.Tests]*")
	.WithFilter("-[Computator.NET.IntegrationTests]*")
	.ExcludeByAttribute("*.ExcludeFromCodeCoverage*"));
});

Task("Upload-Coverage")
	.IsDependentOn("Calculate-Coverage")
	.Does(() =>
{
	CoverallsNet("coverage.xml", CoverallsNetReportType.OpenCover, new CoverallsNetSettings()
	{
		RepoToken = coverallsRepoToken
	});

	Codecov("coverage.xml");
});


Task("Build-Installer")
	.IsDependentOn("Build")
	.Does(() =>
{
	if(IsRunningOnWindows())
	{
	  // Use MSBuild
	  //msBuildSettings.ArgumentCustomization=null;
	  MSBuild(installerProject, new MSBuildSettings
	  {
		Verbosity = Verbosity.Minimal,
		ToolVersion = MSBuildToolVersion.Default,//The highest available MSBuild tool version//VS2017
		Configuration = configuration,
		PlatformTarget = PlatformTarget.MSIL,
		MSBuildPlatform = MSBuildPlatform.Automatic,
		DetailedSummary = true,
	  });
	}
	else
	{
		Warning("Building installer is currently not supported on Unix");
	}
});

Task("Build-Uwp")
	.IsDependentOn("Build")
	.Does(() =>
{
	if(IsRunningOnWindows())
	{
		var packageFiles = @"AppPackages/PackageFiles";
		
		CopyDirectory(@"Computator.NET/bin/"+configuration,packageFiles);
		CopyFileToDirectory(@"build-uwp/AppxManifest.xml",packageFiles);
		CopyFileToDirectory(@"build-uwp/Registry.dat",packageFiles);

		CopyDirectory(@"Computator.NET.Core/Special/windows-x64",packageFiles);
		CopyDirectory(@"Graphics/Assets",packageFiles+@"/Assets");
		
		CopyDirectory(@"Computator.NET.Core/TSL Examples",packageFiles+@"/VFS/Users/ContainerAdministrator/Documents/Computator.NET/TSL Examples");
		CopyDirectory(@"Computator.NET.Core/Static/fonts",packageFiles+@"/VFS/Windows/Fonts");

		var programFilesPath = System.Environment.Is64BitOperatingSystem  ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
		var makePriPaths = GetFiles(programFilesPath + @"\Windows Kits\10\bin\x86\makepri.exe");
		var makePriPath = makePriPaths.First();
		StartProcess(makePriPath, @"createconfig /cf AppPackages\PackageFiles\priconfig.xml /dq en-US");//makepri createconfig /cf AppPackages\PackageFiles\priconfig.xml /dq en-US
		StartProcess(makePriPath, @"new /pr AppPackages\PackageFiles /cf AppPackages\PackageFiles\priconfig.xml");//makepri new /pr AppPackages\PackageFiles /cf AppPackages\PackageFiles\priconfig.xml	
		
		MoveFiles(@"*.pri", packageFiles);// move /y .\*.pri AppPackages\PackageFiles
		
		AppPack("AppPackages/Computator.NET.appx", new DirectoryPath("AppPackages/PackageFiles"));		
		Sign(GetFiles("AppPackages/*.appx"), new SignToolSignSettings {
			TimeStampUri = new Uri("http://timestamp.digicert.com"),
            DigestAlgorithm = SignToolDigestAlgorithm.Sha256,
            CertPath = @"build-uwp/Computator.NET_TemporaryKey.pfx",
		});
	}
	else
	{
		Warning("Building Universal Windows App is currently not supported on Unix");
	}
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
	.IsDependentOn("UnitTests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
