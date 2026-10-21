[CmdletBinding()]
param(
	[string]$EnvFile = (Join-Path $PSScriptRoot '../../docker/.env.dev'),
	[switch]$CheckOnly,
	[switch]$VerifySmtp,
	[string]$Recipient
)

$ErrorActionPreference = 'Stop'
$mapping = @{
	SMTP_OAUTH_CLIENT_ID = 'Smtp__OAuth__ClientId'
	SMTP_OAUTH_TENANT_ID = 'Smtp__OAuth__TenantId'
	SMTP_OAUTH_USERNAME = 'Smtp__OAuth__Username'
	SMTP_OAUTH_CACHE_KEY = 'Smtp__OAuth__CacheKey'
	SMTP_OAUTH_CACHE_DIRECTORY = 'Smtp__OAuth__CachePath'
}
if ($VerifySmtp) {
	$mapping['SMTP_HOST'] = 'Smtp__Host'
	$mapping['SMTP_PORT'] = 'Smtp__Port'
}
$values = @{}
$previous = @{}
try {
	if ($Recipient -and -not $VerifySmtp) { throw 'Recipient requires VerifySmtp.' }
	foreach ($line in [System.IO.File]::ReadLines((Resolve-Path -LiteralPath $EnvFile).Path)) {
		if ($line -notmatch '^\s*(?:export\s+)?([A-Z_]+)\s*=\s*(.*?)\s*$') { continue }
		$name = $Matches[1]
		$value = $Matches[2]
		if (-not $mapping.ContainsKey($name)) { continue }
		if ($values.ContainsKey($name)) { throw 'Duplicate OAuth setting; review the private environment file.' }
		$singleQuoted = $value.StartsWith("'")
		if ($singleQuoted) {
			if ($value -notmatch "^'([^']*)'\s*(?:#.*)?$") {
				throw "Invalid single-quoted setting: $name. Use one literal value per line."
			}
			$value = $Matches[1]
		}
		elseif ($value.StartsWith('"')) {
			if ($value -notmatch '^"([^"]*)"\s*(?:#.*)?$') {
				throw "Invalid double-quoted setting: $name. Escaped quotes are not supported."
			}
			$value = $Matches[1]
		}
		else {
			$value = ($value -replace '\s+#.*$', '').TrimEnd()
		}
		if (-not $singleQuoted -and ($value.Contains('$') -or $value.Contains('`'))) {
			throw "Interpolation is not supported in setting: $name. Use a literal value compatible with Compose."
		}
		$values[$name] = $value
	}
	if (-not $values.ContainsKey('SMTP_OAUTH_TENANT_ID')) { $values.SMTP_OAUTH_TENANT_ID = 'consumers' }
	if ($VerifySmtp) {
		if (-not $values.ContainsKey('SMTP_HOST')) { $values.SMTP_HOST = 'smtp-mail.outlook.com' }
		if (-not $values.ContainsKey('SMTP_PORT')) { $values.SMTP_PORT = '587' }
	}
	foreach ($name in $mapping.Keys) {
		if ([string]::IsNullOrWhiteSpace($values[$name])) { throw "Missing setting: $name" }
	}
	$clientId = [Guid]::Empty
	if (-not [Guid]::TryParse($values.SMTP_OAUTH_CLIENT_ID, [ref]$clientId)) { throw 'Invalid OAuth client identifier.' }
	$key = [Convert]::FromBase64String($values.SMTP_OAUTH_CACHE_KEY)
	try { if ($key.Length -ne 32) { throw 'OAuth cache key must contain 32 bytes.' } }
	finally { [Array]::Clear($key, 0, $key.Length) }
	$directory = $values.SMTP_OAUTH_CACHE_DIRECTORY
	if (-not [System.IO.Path]::IsPathRooted($directory) -or -not (Test-Path -LiteralPath $directory -PathType Container)) {
		throw 'The private OAuth cache directory must already exist at an absolute host path.'
	}
	if ($CheckOnly) {
		Write-Host 'Required OAuth settings are present. Consent, directory permissions and SMTP have not been verified.'
		return
	}
	foreach ($name in $mapping.Keys) {
		$target = $mapping[$name]
		$previous[$target] = [Environment]::GetEnvironmentVariable($target, 'Process')
		$value = $values[$name]
		if ($name -eq 'SMTP_OAUTH_CACHE_DIRECTORY') { $value = Join-Path $directory 'mail.cache' }
		[Environment]::SetEnvironmentVariable($target, $value, 'Process')
	}
	$operation = 'authorize'
	if ($VerifySmtp) {
		$operation = 'verify-smtp'
		$target = 'Smtp__VerificationRecipient'
		$previous[$target] = [Environment]::GetEnvironmentVariable($target, 'Process')
		[Environment]::SetEnvironmentVariable($target, $Recipient, 'Process')
	}
	& dotnet run --project $PSScriptRoot -- $operation
	if ($LASTEXITCODE -ne 0) { throw 'Authorization did not complete. Review the private terminal result without sharing codes or tokens.' }
}
finally {
	foreach ($target in $previous.Keys) {
		[Environment]::SetEnvironmentVariable($target, $previous[$target], 'Process')
	}
	$values.Clear()
}
