
# figure out the correct nuget package version (depends on whether this is a release or not)
$version = "$env:NUGET_RELEASE_VERSION"
if ("$env:APPVEYOR_REPO_TAG" -ne "true") # non-tagged (pre-release build)
{
	$version += "-unstable$env:APPVEYOR_BUILD_NUMBER"
}

# set the NUGET_VERSION env variable
[Environment]::SetEnvironmentVariable("NUGET_VERSION", "$version", "User")
Write-Host "NUGET_VERSION set as $version"
