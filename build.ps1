Push-Location
Set-Location (Split-Path $PSCommandPath -Parent)

Write-Verbose "Checking PAKET..."
./.paket/paket.bootstraper.exe
./.paket/paket.exe restore
if($LASTEXITCODE -gt 0){
    Write-Verbose " Error."
    return $LASTEXITCODE
}else {
    Write-Verbose " Done."
}

./packages/GitVersion.CommandLine/tools/GitVersion.exe
./packages/FAKE/tools/Fake.exe build.fsx
./packages/ReportGenerator/tools/ReportGenerator.exe "-reports:.\.builds\opencover.coverage.xml" "-targetdir:.\.builds\coverreport"
./packages/OpenCoverToCoberturaConverter/tools/OpenCoverToCoberturaConverter.exe -input:.\.builds\opencover.coverage.xml -output:./.builds/cobertura.coverage.xml -sources:./MarDocs

Pop-Location