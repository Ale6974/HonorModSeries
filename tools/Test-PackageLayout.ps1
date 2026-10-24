param([string[]]$Versions = @('1.6','1.7'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
# Read PE metadata only. Never load or execute the game assemblies.
if (!('HonorPackageMetadata' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
public static class HonorPackageMetadata {
    public static string Framework(string path) {
        using (var stream = File.OpenRead(path)) using (var pe = new PEReader(stream)) {
            var md = pe.GetMetadataReader();
            foreach (var handle in md.GetAssemblyDefinition().GetCustomAttributes()) {
                var attr = md.GetCustomAttribute(handle);
                if (attr.Constructor.Kind != HandleKind.MemberReference) continue;
                var member = md.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                if (member.Parent.Kind != HandleKind.TypeReference) continue;
                var type = md.GetTypeReference((TypeReferenceHandle)member.Parent);
                if (md.GetString(type.Namespace) != "System.Runtime.Versioning" || md.GetString(type.Name) != "TargetFrameworkAttribute") continue;
                var blob = md.GetBlobReader(attr.Value);
                if (blob.ReadUInt16() != 1) throw new InvalidDataException();
                return blob.ReadSerializedString();
            }
        }
        throw new InvalidDataException("Missing framework metadata.");
    }
}
'@
}
$json = @('Microsoft.Bcl.AsyncInterfaces.dll','System.Buffers.dll','System.IO.Pipelines.dll','System.Memory.dll',
    'System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','System.Text.Encodings.Web.dll',
    'System.Text.Json.dll','System.Threading.Tasks.Extensions.dll')
$mods = @((Get-ChildItem (Join-Path $repo 'Mods') -Directory)) + @(Get-Item (Join-Path $repo 'TestMods/Honor.Contracts.Preview'))
foreach ($mod in $mods) {
    [xml]$about = Get-Content (Join-Path $mod.FullName 'About/About.xml') -Raw
    [xml]$folders = Get-Content (Join-Path $mod.FullName 'LoadFolders.xml') -Raw
    foreach ($version in $Versions) {
        if ($version -notin @('1.6','1.7')) { throw 'Unsupported layout version.' }
        if ($version -notin @($about.ModMetaData.supportedVersions.li)) { throw "Missing metadata: $($mod.Name) / $version" }
        $node = $folders.SelectSingleNode("loadFolders/v$version")
        if (!$node) { throw "Missing load folders: $($mod.Name) / $version" }
        $paths = @($node.SelectNodes('li') | ForEach-Object InnerText)
        if ('Common' -notin $paths -or $version -notin $paths -or $paths.Count -ne @($paths | Select-Object -Unique).Count) { throw 'Missing or duplicate load paths.' }
        $other = if ($version -eq '1.6') { '1.7' } else { '1.6' }
        if ($other -in $paths) { throw 'Both runtime folders would be loaded.' }
        foreach ($path in $paths) {
            if ($path -notin @('Common','Common-Ideology',$version)) { throw 'Unexpected load path.' }
            if (!(Test-Path -LiteralPath (Join-Path $mod.FullName $path))) { throw "Missing content: $path" }
        }
        $assemblyRoot = Join-Path $mod.FullName "$version/Assemblies"
        $expected = @($mod.Name + '.dll')
        if ($mod.Name -eq 'Honor.Core') { $expected += 'Honor.Rules.dll' }
        if ($mod.Name -eq 'Honor.Contracts.Preview') { $expected += 'Honor.Contracts.Rules.dll'; if ($version -eq '1.6') { $expected += $json } }
        $actual = @(Get-ChildItem -LiteralPath $assemblyRoot -File -Filter *.dll | ForEach-Object Name)
        if (Compare-Object ($expected | Sort-Object) ($actual | Sort-Object)) { throw "Missing or unexpected DLLs: $($mod.Name) / $version" }
        foreach ($dll in $actual | Where-Object { $_ -like 'Honor.*' }) {
            $framework = [HonorPackageMetadata]::Framework((Join-Path $assemblyRoot $dll))
            $wanted = if ($version -eq '1.6') { '.NETFramework,Version=v4.7.2' } else { '.NETCoreApp,Version=v10.0' }
            if ($framework -ne $wanted) { throw "Wrong runtime: $dll / $version / $framework" }
        }
        if (Get-ChildItem (Join-Path $mod.FullName 'Common') -Recurse -File -Filter *.dll) { throw 'Common data must not contain runtime DLLs.' }
        Write-Output "PASS: $($mod.Name) $version runtime selection and DLL allowlist."
    }
}
if (!(Test-Path (Join-Path $repo 'TestMods/Honor.Contracts.Preview/About/Preview.png')) -or
    (Get-Item (Join-Path $repo 'TestMods/Honor.Contracts.Preview/About/Preview-Workshop.jpg')).Length -ge 1000000) { throw 'Contracts cover missing or too large.' }
