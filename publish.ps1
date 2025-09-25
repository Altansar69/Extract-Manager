# Script to publish the ExtractManager project for different deployment models.

# --- Configuration ---
$ProjectName = "ExtractManager.csproj"
$OutputFolderName = "build"
$FrameworkDependentSubFolder = "framework-dependent"
$SelfContainedSubFolder = "self-contained"
$Rids = @("win-x64", "win-x86") # Runtime Identifiers to build for

function Remove-UnneededFiles([string]$dir) {
    if (-not (Test-Path $dir)) { return }
    Write-Host "Cleaning unneeded files in '$dir'..."
    Get-ChildItem -Path $dir -Recurse -Include @("*.pdb", "*.xml", "*.map") -ErrorAction SilentlyContinue |
        ForEach-Object {
            try { Remove-Item -Path $_.FullName -Force -ErrorAction Stop } catch { }
        }
    # NOTE: Do NOT remove *.deps.json or *.runtimeconfig.json; they are required by framework-dependent apps.
}

# --- Script Body ---
try {
    # Get the directory of the script, which should be the solution root.
    $SolutionDir = $PSScriptRoot
    if (-not $SolutionDir) {
        $SolutionDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    $ProjectFile = Join-Path -Path $SolutionDir -ChildPath $ProjectName
    $BaseOutputDir = Join-Path -Path $SolutionDir -ChildPath $OutputFolderName

    # Verify that the project file exists.
    if (-not (Test-Path $ProjectFile)) {
        Write-Error "Project file not found at '$ProjectFile'. Make sure this script is in the solution root directory."
        return
    }

    Write-Host "Solution Directory: $SolutionDir"
    Write-Host "Project File:       $ProjectFile"
    Write-Host "Base Output Dir:    $BaseOutputDir"
    Write-Host "--------------------------------------------------"

    # Clean the base output directory if it exists.
    if (Test-Path $BaseOutputDir) {
        Write-Host "Cleaning previous build output from '$OutputFolderName'..."
        Remove-Item -Path $BaseOutputDir -Recurse -Force
    }

    foreach ($rid in $Rids) {
        # --- Framework-Dependent Build ---
        $FdRidOutputDir = Join-Path -Path (Join-Path -Path $BaseOutputDir -ChildPath $FrameworkDependentSubFolder) -ChildPath $rid
        Write-Host "Publishing framework-dependent build ($rid) to '$FdRidOutputDir'..."
        # Force non-single-file for framework-dependent to avoid packaging differences.
        dotnet publish "$ProjectFile" -c Release -o "$FdRidOutputDir" -r $rid --self-contained false -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:DebugSymbols=false -p:DebugType=None -p:GenerateDocumentationFile=false
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Framework-dependent publish failed for RID '$rid'. See output for details."
            return
        }
        Remove-UnneededFiles $FdRidOutputDir
        Write-Host "Framework-dependent ($rid) publish completed successfully!"
        Write-Host "--------------------------------------------------"

        # --- Self-Contained Build ---
        $ScRidOutputDir = Join-Path -Path (Join-Path -Path $BaseOutputDir -ChildPath $SelfContainedSubFolder) -ChildPath $rid
        Write-Host "Publishing self-contained build ($rid) to '$ScRidOutputDir'..."
        dotnet publish "$ProjectFile" -c Release -o "$ScRidOutputDir" -r $rid --self-contained true -p:PublishReadyToRun=true -p:DebugSymbols=false -p:DebugType=None -p:GenerateDocumentationFile=false
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Self-contained publish failed for RID '$rid'. See output for details."
            return
        }
        Remove-UnneededFiles $ScRidOutputDir
        Write-Host "Self-contained ($rid) publish completed successfully!"
        Write-Host "--------------------------------------------------"
    }

    Write-Host "All publish operations completed successfully!"
    Write-Host "Published applications are located in: $BaseOutputDir"
}
catch {
    Write-Error "An unexpected error occurred: $_"
}
finally {
    Write-Host "Press any key to exit..."
    $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | Out-Null
}