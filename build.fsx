#r "paket:
nuget Fake.Core.Target
nuget Fake.DotNet.Cli
nuget Fake.DotNet.MSBuild
nuget Fake.DotNet.Paket //"
#load "./.fake/build.fsx/intellisense.fsx"
// include Fake modules, see Fake modules section

#if !FAKE
    #r "netstandard"
    #r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open System.Text.RegularExpressions
open Fake.Core
open Fake.DotNet

module Projects =
    let winFormsLib = "Interstellar.WinForms.Chromium/Interstellar.WinForms.Chromium.fsproj"
    let wpfLib = "Interstellar.Wpf.Chromium/Interstellar.Wpf.Chromium.fsproj"

module Solutions =
    let windows = "Interstellar.Windows.sln"
    let macos = "Interstellar.MacOS.sln"

let projAsTarget (projFileName: string) = projFileName.Split('/').[0].Replace(".", "_")

let runDotNet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let addTargets targets (defaults: MSBuildParams) = { defaults with Targets = defaults.Targets @ targets }
let addTarget target (defaults: MSBuildParams) = { defaults with Targets = defaults.Targets @ [target] }

let quiet (defaults: MSBuildParams) = { defaults with Verbosity = Some MSBuildVerbosity.Quiet }

type PackageVersionInfo = { versionName: string; versionChanges: string }

let scrapeChangelog () =
    let changelog = Fake.IO.File.readAsString "CHANGELOG.md"
    let regex = Regex("""## (?<Version>.*)\n+(?<Changes>(.|\n)*?)##""")
    let result = seq {
        for m in regex.Matches changelog ->
            {   versionName = m.Groups.["Version"].Value
                versionChanges =
                    m.Groups.["Changes"].Value.Trim()
                        .Replace("    *", "    ◦")
                        .Replace("*", "•")
                        .Replace("    ", "\t") }
    }
    result

let changelog = scrapeChangelog () |> Seq.toList

let addVersionInfo (versionInfo: PackageVersionInfo) (defaults: MSBuildParams) =
    { defaults with
        Properties = defaults.Properties @ ["Version", versionInfo.versionName
                                            "PackageReleaseNotes", versionInfo.versionChanges] }

let addProperties props defaults = { defaults with Properties = [yield! defaults.Properties; yield! props]}

let msbuild setParams project =
    let versionInfo = changelog |> List.head
    let buildMode = Environment.environVarOrDefault "buildMode" "Release"
    project |> MSBuild.build (
        quiet <<
        setParams <<
        addProperties ["Configuration", buildMode] <<
        addVersionInfo versionInfo << setParams
    )
    

// *** Define Targets ***
Target.create "PackageDescription" (fun _ ->
    let changelog = scrapeChangelog ()
    let currentVersion = Seq.head changelog
    let str = sprintf "Changes in package version %s\n%s" currentVersion.versionName currentVersion.versionChanges
    Trace.log str
)

Target.create "Clean" (fun _ ->
    Trace.log " --- Cleaning --- "
    if Environment.isWindows then
        msbuild (addTarget "Clean") Projects.winFormsLib
        msbuild (addTarget "Clean") Projects.wpfLib
    else
        msbuild (addTarget "Clean") Solutions.macos
)

Target.create "Build" (fun _ ->
    Trace.log " --- Building --- "
    if Environment.isWindows then
        msbuild (addTarget "Restore") Solutions.windows
    else
        msbuild (addTarget "Restore") Solutions.macos
    if Environment.isWindows then
        msbuild (addTarget "Build") Projects.winFormsLib
        msbuild (addTarget "Build") Projects.wpfLib
)

Target.create "Pack" (fun _ ->
    Trace.log " --- Packing NuGet packages --- "
    if Environment.isWindows then
        //msbuild (addTargets ["Pack"; projAsTarget Projects.winFormsLib; projAsTarget Projects.wpfLib]) Solutions.windows
        let msbuild f = msbuild (addTarget "Pack" << addProperties ["SolutionDir", __SOURCE_DIRECTORY__] << f)
        msbuild id Projects.winFormsLib
        msbuild id Projects.wpfLib
)

open Fake.Core.TargetOperators

// *** Define Dependencies ***
"Clean"
    ==> "Build"
    ==> "Pack"

// *** Start Build ***
Target.runOrDefault "Build"