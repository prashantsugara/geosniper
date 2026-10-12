param([string]$Python='python',[switch]$Preview,[switch]$Force)
$ErrorActionPreference='Stop'
# Force remains accepted for older build scripts. Neural narration is always regenerated.
$storyArguments=@((Join-Path $PSScriptRoot 'generate_story_audio.py'))
if($Preview){$storyArguments+='--preview'}
& $Python @storyArguments
if($LASTEXITCODE -ne 0){throw 'Neural narration generation failed. See the Python diagnostic above.'}
