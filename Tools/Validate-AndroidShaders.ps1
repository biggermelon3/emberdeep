param([Parameter(Mandatory=$true)][string]$LogPath)
$ErrorActionPreference='Stop'
$buildLog=Get-Content -LiteralPath $LogPath -Raw
if($buildLog -notmatch 'ANDROID_RENDERING_PREFLIGHT_OK:' -or $buildLog -notmatch '- Mobile_RPAsset - Assets/Settings/Mobile_RPAsset\.asset') {
    throw 'Build did not verify the Android Mobile rendering profile.'
}
$passes=@(
    @{ Shader='Universal Render Pipeline/Lit'; Pass='ForwardLit' },
    @{ Shader='Universal Render Pipeline/Unlit'; Pass='Unlit' }
)
$results=foreach($required in $passes) {
    $shaderPattern='(?ms)^Compiling shader "'+[regex]::Escape($required.Shader)+'"\r?\n(?<body>.*?)(?=^Compiling (?:compute )?shader |^Serialized binary data for shader|\z)'
    $shaderMatches=[regex]::Matches($buildLog,$shaderPattern)
    if($shaderMatches.Count -eq 0) { throw "No compilation evidence for $($required.Shader). Run BuildClean for a complete shader report." }
    $shaderBody=$shaderMatches[$shaderMatches.Count-1].Groups['body'].Value
    $passPattern='(?s)  Pass "'+[regex]::Escape($required.Pass)+'" \([^)]*\)(?<body>.*?)(?=\r?\n  Pass |\z)'
    $passMatch=[regex]::Match($shaderBody,$passPattern)
    $countMatch=[regex]::Match($passMatch.Groups['body'].Value,'After scriptable stripping:\s*([\d.,]+)')
    if(!$countMatch.Success) { throw "No retained variant count for $($required.Shader)/$($required.Pass)." }
    $variantCount=[long]($countMatch.Groups[1].Value -replace '[^0-9]','')
    if($variantCount -le 0) { throw "Drawing shader was stripped: $($required.Shader)/$($required.Pass). Do not install this build." }
    [pscustomobject]@{Shader=$required.Shader;Pass=$required.Pass;RetainedVariants=$variantCount}
}
$results | ConvertTo-Json
