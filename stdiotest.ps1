param(
	[switch]$debug = $false
)
$ErrorActionPreference = "stop"

$logFile = "C:\temp\OmniSharp.log"
Set-Content -Path $logFile -Value $null

$psi = New-Object Diagnostics.ProcessStartInfo
$psi.FileName = "C:\src\omnisharp-roslyn\bin\Debug\OmniSharp.Stdio.Driver\net6.0\OmniSharp.exe"
$slnPath = 'C:\src\TryToDecompileFindUsages\TryToDecompileFindUsages.sln'
$psi.Arguments = "-s $($slnPath)"
#$psi.Arguments = '-s c:\src\StompNet\StompNet.sln'
if($debug)  {
	$psi.Arguments = "--debug -s $($slnPath)"
}
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true

$p = New-Object Diagnostics.Process
$p.StartInfo = $psi

$oStdOutBuilder = New-Object Text.StringBuilder
$oStdErrBuilder = New-Object Text.StringBuilder

$p.Start() | Out-Null

$end = $true
while($end) {
	$eventJson = $p.StandardOutput.ReadLine()
	$eventObj = ConvertFrom-Json $eventJson
	Add-Content -Path $logFile -Value $eventJson
	if($eventObj.Event -eq "started") {
		Write-Host "[*************************]: $($eventObj)"
	}
	elseif($eventObj.Event -ne "log") {
		Write-Host "[EVENT]: $($eventObj)"
	}
	Write-Host $eventObj.seq
	if($eventObj.seq -eq 26) {
		$end = $false
	}
}

function WaitForResponse {
	param(
		$seq
	)
	$result = $null
	while($result -eq $null) {
		$eventJson = $p.StandardOutput.ReadLine()
		Add-Content -Path $logFile -Value $eventJson
		$eventObj = ConvertFrom-Json $eventJson
		if($eventObj.Event -ne "log") {
			Write-Host "[EVENT]: $($eventJson)"
		}
		if($eventObj.Success) {
			$end = $false
			$result = $eventObj
		}
	}
	$result
}

function SendRequest {
	param(
		$commandName,
		$arguments,
		$seq
	)
	$command = [PSCustomObject]@{
		Command = $commandName
		arguments= $arguments
		Seq = $seq
	}

	$commandJson = ConvertTo-Json -Compress $command
	Write-Host "[SENDING] $($commandJson)"
	$p.StandardInput.WriteLine($commandJson)

	$response = WaitForResponse $seq
	$response
}

function RunGetDefinition {
	param(
		$seq
	)
# {
#   "Seq": 1008,
#   "Arguments": {
#     "FileName": "C:\\src\\TryToDecompileFindUsages\\TryToDecompileFindUsages\\FirstTestType.cs",
#     "Column": 34,
#     "Line": 6,
#     "WantMetadata": true,
#     "Buffer": "using ICSharpCode.Decompiler.Metadata;\r\nusing ICSharpCode.Decompiler.TypeSystem;\r\n\r\nnamespace TryToDecompileFindUsages\r\n{\r\n    public class FirstTestType : IModule\r\n    {\r\n        public SymbolKind SymbolKind { get; }\r\n        public string Name { get; }\r\n        public ICompilation Compilation { get; }\r\n        public IEnumerable<IAttribute> GetAssemblyAttributes()\r\n        {\r\n            throw new NotImplementedException();\r\n        }\r\n\r\n        public IEnumerable<IAttribute> GetModuleAttributes()\r\n        {\r\n            throw new NotImplementedException();\r\n        }\r\n\r\n        public bool InternalsVisibleTo(IModule module)\r\n        {\r\n            throw new NotImplementedException();\r\n        }\r\n\r\n        public ITypeDefinition? GetTypeDefinition(TopLevelTypeName topLevelTypeName)\r\n        {\r\n            throw new NotImplementedException();\r\n        }\r\n\r\n        public PEFile? PEFile { get; }\r\n        public bool IsMainModule { get; }\r\n        public string AssemblyName { get; }\r\n        public string FullAssemblyName { get; }\r\n        public INamespace RootNamespace { get; }\r\n        public IEnumerable<ITypeDefinition> TopLevelTypeDefinitions { get; }\r\n        public IEnumerable<ITypeDefinition> TypeDefinitions { get; }\r\n    }\r\n}"
#   },
#   "Type": "request",
#   "Command": "/gotodefinition"
# }
$filePath = "C:\Src\TryToDecompileFindUsages\TryToDecompileFindUsages\FirstTestType.cs"
$content = Get-Content $filePath

	$arguments = [PSCustomObject]@{
		FileName = "$($filePath)"
		Line = 6
		Column = 34
		WantMetadata = $true
		Timeout = 600000
		# Buffer = "$($content)"
	}
	
	$getDefintionResponse = SendRequest "/gotodefinition" $arguments $seq
	$seq++

	$arguments = [PSCustomObject]@{
		AssemblyName = $getDefintionResponse.Body.MetadataSource.AssemblyName
		TypeName = $getDefintionResponse.Body.MetadataSource.TypeName
		ProjectName = $getDefintionResponse.Body.MetadataSource.ProjectName
		Language = $getDefintionResponse.Body.MetadataSource.Language
		VersionNumber = $getDefintionResponse.Body.MetadataSource.VersionNumber
	}

	$arguments = [PSCustomObject]@{
		AssemblyName = $getDefintionResponse.Body.MetadataSource.AssemblyName
		TypeName = $getDefintionResponse.Body.MetadataSource.TypeName
		ProjectName = $getDefintionResponse.Body.MetadataSource.ProjectName
		Language = $getDefintionResponse.Body.MetadataSource.Language
		VersionNumber = $getDefintionResponse.Body.MetadataSource.VersionNumber
	}

	$seq
}

function RunGetImplementation {
	param(
		$seq
	)
	$arguments = [PSCustomObject]@{
		FileName = "C:\Src\TryToDecompileFindUsages\TryToDecompileFindUsages\FirstTestType.cs"
		Line = 6
		Column = 34
		WantMetadata = $true
		Timeout = 600000
	}
	
	$getDefintion = SendRequest "/findimplementations" $arguments $seq
	$seq++

	$getDefintion.Body.MetadataFiles | % {
		$arguments = [PSCustomObject]@{
			AssemblyName = $_.AssemblyName
			TypeName = $_.TypeName
			ProjectName = $_.ProjectName
			Language = $_.Language
			VersionNumber = $_.VersionNumber
		}

		$metaDataResponse = SendRequest "/metadata" $arguments $seq
		Write-Host $metaDataResponse.Body.Source
		$seq++
	}

	$seq
}

$seq = 54
while($true) {
	Write-Host -NoNewLine 'Press any key to send request (k will break the loop)...';
	$key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
	Write-Host ""
	if($key.Character -eq "k") {
		Write-Host "K was pressed breaking"
		$p.Kill()
		break
	}
	#$seq = RunGetDefinition $seq
	$seq = RunGetImplementation $seq
}
