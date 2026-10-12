import os
import shutil
import subprocess

repo_root = r"c:\Users\Geeta Sugara\Documents\ChatGPT\sniper2"
ffmpeg_bin = os.path.join(repo_root, r".utmp\promo-tools\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe")
audio_file = os.path.join(repo_root, r"Store\Promo\geosniper-original-score.wav")

ads_dir = os.path.join(repo_root, "output", "ads")
os.makedirs(ads_dir, exist_ok=True)
assets_dir = os.path.join(ads_dir, "assets")
os.makedirs(assets_dir, exist_ok=True)

source_images = [
    r"C:\Users\Geeta Sugara\.gemini\antigravity\brain\b24a8588-b4f7-4abd-9d7f-4fe92914a112\ad_satellite_recon_1791572189119.jpg",
    r"C:\Users\Geeta Sugara\.gemini\antigravity\brain\b24a8588-b4f7-4abd-9d7f-4fe92914a112\ad_sniper_scope_1791572205655.jpg",
    r"C:\Users\Geeta Sugara\.gemini\antigravity\brain\b24a8588-b4f7-4abd-9d7f-4fe92914a112\ad_sniper_shot_1791572223554.jpg",
    r"C:\Users\Geeta Sugara\.gemini\antigravity\brain\b24a8588-b4f7-4abd-9d7f-4fe92914a112\ad_title_card_1791572240028.jpg"
]

target_names = ["01_recon.jpg", "02_scope.jpg", "03_shot.jpg", "04_title.jpg"]
local_paths = []

for src, name in zip(source_images, target_names):
    dest = os.path.join(assets_dir, name)
    shutil.copyfile(src, dest)
    local_paths.append(dest)
    print(f"Copied {name} -> {dest}")

output_video = os.path.join(ads_dir, "geosniper_ad_custom.mp4")

# Let's render individual animated 3s clips (90 frames each at 30fps) with zoom/pan and text overlays
# Clip 1: Recon (0 to 3s)
clip1_mp4 = os.path.join(assets_dir, "clip1.mp4")
cmd1 = [
    ffmpeg_bin, "-y",
    "-loop", "1", "-i", local_paths[0],
    "-vf", "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,"
           "zoompan=z='min(zoom+0.0015,1.2)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=90:s=1080x1920:fps=30,"
           "drawtext=fontfile='C\\:/Windows/Fonts/arialbd.ttf':text='SATELLITE RECON':fontcolor=0x00FF88:fontsize=64:x=(w-text_w)/2:y=240:shadowcolor=black@0.8:shadowx=3:shadowy=3,"
           "drawtext=fontfile='C\\:/Windows/Fonts/arialbd.ttf':text='TARGET ACQUIRED':fontcolor=white:fontsize=44:x=(w-text_w)/2:y=320:shadowcolor=black@0.8:shadowx=2:shadowy=2",
    "-t", "3.0",
    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-r", "30",
    clip1_mp4
]
print("Rendering clip 1...")
res = subprocess.run(cmd1, capture_output=True, text=True)
if res.returncode != 0:
    print(res.stderr)
    raise RuntimeError("Clip 1 failed")

# Clip 2: Scope (3s)
clip2_mp4 = os.path.join(assets_dir, "clip2.mp4")
cmd2 = [
    ffmpeg_bin, "-y",
    "-loop", "1", "-i", local_paths[1],
    "-vf", "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,"
           "zoompan=z='min(zoom+0.0012,1.15)':x='iw/2-(iw/zoom/2)':y='ih*0.48-(ih/zoom/2)':d=90:s=1080x1920:fps=30,"
           "drawtext=fontfile='C\\:/Windows/Fonts/arialbd.ttf':text='CALCULATE WIND & DROP':fontcolor=0x00E0FF:fontsize=56:x=(w-text_w)/2:y=240:shadowcolor=black@0.8:shadowx=3:shadowy=3,"
           "drawtext=fontfile='C\\:/Windows/Fonts/arialbd.ttf':text='850M DISTANCE':fontcolor=white:fontsize=44:x=(w-text_w)/2:y=310:shadowcolor=black@0.8:shadowx=2:shadowy=2",
    "-t", "3.0",
    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-r", "30",
    clip2_mp4
]
print("Rendering clip 2...")
res = subprocess.run(cmd2, capture_output=True, text=True)
if res.returncode != 0:
    print(res.stderr)
    raise RuntimeError("Clip 2 failed")

# Clip 3: Shot (3s)
clip3_mp4 = os.path.join(assets_dir, "clip3.mp4")
cmd3 = [
    ffmpeg_bin, "-y",
    "-loop", "1", "-i", local_paths[2],
    "-vf", "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,"
           "zoompan=z='min(zoom+0.002,1.2)':x='iw/2-(iw/zoom/2)':y='ih*0.4-(ih/zoom/2)':d=90:s=1080x1920:fps=30,"
           "drawtext=fontfile='C\\:/Windows/Fonts/arialbd.ttf':text='ONE SHOT. ZERO ASSIST.':fontcolor=0xFFAA00:fontsize=58:x=(w-text_w)/2:y=240:shadowcolor=black@0.8:shadowx=3:shadowy=3",
    "-t", "3.0",
    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-r", "30",
    clip3_mp4
]
print("Rendering clip 3...")
res = subprocess.run(cmd3, capture_output=True, text=True)
if res.returncode != 0:
    print(res.stderr)
    raise RuntimeError("Clip 3 failed")

# Clip 4: Title / CTA (3s)
clip4_mp4 = os.path.join(assets_dir, "clip4.mp4")
cmd4 = [
    ffmpeg_bin, "-y",
    "-loop", "1", "-i", local_paths[3],
    "-vf", "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,"
           "zoompan=z='min(zoom+0.0008,1.06)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=90:s=1080x1920:fps=30",
    "-t", "3.0",
    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-r", "30",
    clip4_mp4
]
print("Rendering clip 4...")
res = subprocess.run(cmd4, capture_output=True, text=True)
if res.returncode != 0:
    print(res.stderr)
    raise RuntimeError("Clip 4 failed")

# Concatenate clips and merge with audio track
concat_txt = os.path.join(assets_dir, "concat.txt")
with open(concat_txt, "w") as f:
    f.write(f"file '{clip1_mp4}'\n")
    f.write(f"file '{clip2_mp4}'\n")
    f.write(f"file '{clip3_mp4}'\n")
    f.write(f"file '{clip4_mp4}'\n")

print("Merging final ad with audio...")
final_cmd = [
    ffmpeg_bin, "-y",
    "-f", "concat", "-safe", "0", "-i", concat_txt,
    "-i", audio_file,
    "-map", "0:v:0",
    "-map", "1:a:0",
    "-t", "12.0",
    "-af", "afade=t=out:st=11.0:d=1.0",
    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "fast", "-crf", "18",
    "-c:a", "aac", "-b:a", "192k",
    "-shortest",
    output_video
]

res = subprocess.run(final_cmd, capture_output=True, text=True)
if res.returncode != 0:
    print(res.stderr)
    raise RuntimeError("Final merge failed")

print(f"SUCCESS! Rendered ad to: {output_video}")
