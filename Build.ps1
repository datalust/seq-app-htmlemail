# This script originally (c) 2016 Serilog Contributors - license Apache 2.0

echo "build: Build started"

Push-Location $PSScriptRoot

if(Test-Path .\artifacts) {
	echo "build: Cleaning .\artifacts"
	Remove-Item .\artifacts -Force -Recurse
}

& dotnet restore --no-cache
if($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }    

$branch = @{ $true = $env:APPVEYOR_REPO_BRANCH; $false = $(git symbolic-ref --short -q HEAD) }[$env:APPVEYOR_REPO_BRANCH -ne $NULL];
$revision = @{ $true = "{0:00000}" -f [convert]::ToInt32("0" + $env:APPVEYOR_BUILD_NUMBER, 10); $false = "local" }[$env:APPVEYOR_BUILD_NUMBER -ne $NULL];
$suffix = @{ $true = ""; $false = "$($branch.Substring(0, [math]::Min(10,$branch.Length)))-$revision"}[$branch -eq "main" -and $revision -ne "local"]

echo "build: Version suffix is $suffix"

foreach ($src in ls src/*) {
    Push-Location $src

    echo "build: Packaging project in $src"
    
    foreach ($tfm in @("netstandard2.0", "net6.0")) {
        if ($suffix) {
            & dotnet publish -c Release -o "./obj/publish/$tfm" -f $tfm --version-suffix=$suffix
        } else {
            & dotnet publish -c Release -o "./obj/publish/$tfm" -f $tfm
        }
        if($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }    
    }

    if ($suffix) {
        & dotnet pack -c Release -o ..\..\artifacts --no-build --version-suffix=$suffix
    } else {
        & dotnet pack -c Release -o ..\..\artifacts --no-build
    }
    if($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }    

    Pop-Location
}

foreach ($test in ls test/*.Tests) {
    Push-Location $test

    echo "build: Testing project in $test"

    & dotnet test -c Release
    if($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

    Pop-Location
}

Pop-Location
