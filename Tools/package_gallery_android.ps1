$ErrorActionPreference='Stop'
$galleryRoot=Split-Path $PSScriptRoot -Parent
$galleryJdk='C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK'
$galleryGradle='C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle/lib/gradle-launcher-9.1.0.jar'
$galleryPatch=Join-Path $galleryRoot '.utmp/tools/gallery-java-pipe'
$gallerySource=Join-Path $galleryPatch 'src/sun/nio/ch'
$galleryClasses=Join-Path $galleryPatch 'classes'
New-Item $gallerySource,$galleryClasses -ItemType Directory -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$galleryArchive=[IO.Compression.ZipFile]::OpenRead((Join-Path $galleryJdk 'lib/src.zip'))
try {
    $galleryReader=[IO.StreamReader]::new($galleryArchive.GetEntry('java.base/sun/nio/ch/PipeImpl.java').Open())
    try {$galleryPipe=$galleryReader.ReadToEnd()}finally{$galleryReader.Dispose()}
} finally {$galleryArchive.Dispose()}
# Preserve the bundled OpenJDK source and license. Only the temporary build JVM
# uses TCP for its internal selector pipe when Windows AF_UNIX connect fails.
$galleryNeedle='if (preferUnixDomain && UnixDomainSockets.isSupported())'
if(!$galleryPipe.Contains($galleryNeedle)){throw 'Bundled PipeImpl changed; review compatibility patch.'}
$galleryPipe=$galleryPipe.Replace($galleryNeedle,'if (false && UnixDomainSockets.isSupported())')
$galleryJavaFile=Join-Path $gallerySource 'PipeImpl.java'
[IO.File]::WriteAllText($galleryJavaFile,$galleryPipe)
& (Join-Path $galleryJdk 'bin/javac.exe') '--patch-module' ('java.base='+ (Join-Path $galleryPatch 'src')) '-d' $galleryClasses $galleryJavaFile
if($LASTEXITCODE -ne 0){throw 'Temporary Java compatibility compile failed'}
$galleryPreviousOptions=$env:JAVA_TOOL_OPTIONS
try {
    $env:JAVA_TOOL_OPTIONS='--patch-module="java.base='+$galleryClasses+'" -Djava.net.preferIPv4Stack=true'
    Push-Location (Join-Path $galleryRoot '.utmp/android-gallery-build/Library/Bee/Android/Prj/IL2CPP/Gradle')
    try {
        & (Join-Path $galleryJdk 'bin/java.exe') '-classpath' $galleryGradle 'org.gradle.launcher.GradleMain' '--no-daemon' '--max-workers=2' '-Dorg.gradle.jvmargs=-Xmx2048m' 'assembleDebug' '--stacktrace' *> (Join-Path $galleryRoot 'output/reference-building/gradle-package.log')
        if($LASTEXITCODE -ne 0){throw 'Gradle packaging failed; inspect gradle-package.log.'}
    }finally{Pop-Location}
}finally{$env:JAVA_TOOL_OPTIONS=$galleryPreviousOptions}
Write-Output 'Gradle APK packaging succeeded.'
