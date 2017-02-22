#r @"packages/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Testing.XUnit2
open Fake.OpenCoverHelper

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let buildRoot = "./.builds"
let buildDir = buildRoot @@ "/builds"
let testDir = buildRoot @@ "/tests"
let packagesDir = "./packages"
let nugetSource = "https://api.nuget.org/v3/index.json"

Target "Clean" (fun _ ->
    CleanDir buildRoot
)

Target "RestorePackages" (fun _ ->
    !! "./sources/*.sln"
    |> Seq.iter (RestoreMSSolutionPackages (fun p ->
            { p with
                Sources = nugetSource :: p.Sources
                OutputPath = packagesDir
                Retries = 4 }))
 )

Target "Compile" (fun _ ->
    !! "./sources/**/*.csproj"
        |> MSBuild buildDir "Build" 
            [ 
                "Configuration", "Release"
                "Platform", "AnyCPU"
            ]
        |> Log "AppBuild-Output: "
)

Target "CompileTests" (fun _ ->
    !! "./sources/**/*.Tests.csproj"
        |> MSBuildRelease testDir "Build"
        |> Log "AppBuild-Output: "
)

Target "Test" (fun _ ->
    let flip21 f a b = f b a
    let openCoverArgs = flip21 (sprintf "%s -xml %s") (buildRoot @@ "xunit.tests.xml")
    let runOpenCover = OpenCover (fun p -> 
        { p with
            Register = RegisterUser
            ExePath = "./packages/OpenCover/tools/OpenCover.Console.exe"
            Filter = "+[*]* -[xunit*]*"
            TestRunnerExePath = "./packages/xunit.runner.console/tools/xunit.console.exe";
            Output = buildRoot @@ "opencover.coverage.xml"
            OptionalArguments = "-targetargs:-noshadow"
        })
    !! (testDir + @"\*Tests*.dll")
        |> Seq.map openCoverArgs
        |> Seq.iter runOpenCover
)

Target "CreatePackage" (fun _ ->
    let gitVersion = ExecProcessRedirected (fun info ->
                        info.FileName <- ".\packages\GitVersion.CommandLine\\tools\GitVersion.exe"
                        info.WorkingDirectory <- System.Environment.CurrentDirectory
                     ) (System.TimeSpan.FromMinutes 5.0)
                     |> (fun (result,messages) -> messages)
                     |> Seq.filter (fun x -> not x.IsError)
                     |> Seq.map (fun x ->
                        System.Text.RegularExpressions.Regex.Match(x.Message, "\"MajorMinorPatch\":\"(.*?)\"")
                     )
                     |> Seq.filter (fun x -> x.Success)
                     |> Seq.map (fun x -> x.Groups.[1].Value)
                     |> Seq.exactlyOne
    //let version = System.Text.RegularExpressions.Regex.Match(gitVersion, "\"MajorMinorPatch\":\"(.*?)\"")
    CopyFiles packagesDir []
    CreateDir (buildDir @@ "nuget")
    NuGetPack (fun p -> 
        {p with
            Authors = ["Daniel Frederico Lins Leite"]
            Project = "MachinaAurum.AkkaNet.Collections.SqlServer"
            Description = "Reader and Writer SqlServer collections Actors in Akka.net"                               
            OutputPath = buildDir @@ "nuget"
            Summary = "Reader and Writer SqlServer collections Actors in Akka.net" 
            WorkingDir = buildDir
            Version = gitVersion
            AccessKey = "myAccesskey"
            Publish = true }) "MachinaAurum.AkkaNet.Collections.SqlServer.nuspec"
)

Target "Default" id

"Clean"
  ==> "RestorePackages"
  ==> "Compile"
  ==> "CompileTests"
  ==> "Test"
  ==> "CreatePackage"
  ==> "Default"

RunTargetOrDefault "Default"
