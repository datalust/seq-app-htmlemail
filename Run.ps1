echo "run: Publishing app binaries"

$id = "Seq.App.EmailPlus"
$src = "$PSScriptRoot/src/$id"

rm -Recurse -Force "$src/obj/publish"
& dotnet publish "$src/$id.csproj" -c Release -o "$src/obj/publish" --version-suffix=local

if($LASTEXITCODE -ne 0) { exit 1 }    

echo "run: Piping live Seq logs to the app"

& seqcli tail --json | & seqcli app run -d "$src/obj/publish" -p From=test@localhost -p To=test@localhost -p Host=localhost 2>&1 | & seqcli print
