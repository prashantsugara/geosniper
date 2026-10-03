import os
import shutil
import asyncio
import numpy as np
import soundfile as sf
import edge_tts

OUTPUT_DIR = r"c:\Users\Geeta Sugara\Documents\ChatGPT\sniper2\Assets\Resources\Audio"
FOOTSTEPS_SRC = r"c:\Users\Geeta Sugara\Documents\ChatGPT\sniper2\temp_footsteps\footsteps"
FOOTSTEPS_DEST = os.path.join(OUTPUT_DIR, "Footsteps")

os.makedirs(OUTPUT_DIR, exist_ok=True)
os.makedirs(FOOTSTEPS_DEST, exist_ok=True)

# 1. Copy Footsteps
surface_mappings = {
    "boots": "Concrete",
    "grass": "Dirt",
    "gravel": "Gravel",
    "metal": "Metal",
    "wood": "Wood"
}

print("=== Setting up Footstep Audio ===")
for src_folder, surf_name in surface_mappings.items():
    src_dir = os.path.join(FOOTSTEPS_SRC, src_folder)
    if not os.path.exists(src_dir):
        print(f"Warning: {src_dir} not found")
        continue
    
    ogg_files = sorted([f for f in os.listdir(src_dir) if f.endswith(".ogg") and not f.startswith("._")])
    for idx, f in enumerate(ogg_files[:5]):
        src_path = os.path.join(src_dir, f)
        dest_filename = f"Footstep_{surf_name}_{idx}.ogg"
        dest_path = os.path.join(FOOTSTEPS_DEST, dest_filename)
        shutil.copyfile(src_path, dest_path)
        print(f"Copied {dest_filename}")
        
    # Copy fallback to root Audio dir
    if ogg_files:
        primary_dest = os.path.join(OUTPUT_DIR, f"Footstep{surf_name}.ogg")
        shutil.copyfile(os.path.join(src_dir, ogg_files[0]), primary_dest)
        print(f"Created primary fallback Footstep{surf_name}.ogg")

print("Footsteps copied successfully!\n")

# 2. Generate Real Radio Audio with Tactical DSP
def apply_tactical_radio_effects(audio_data, sr, is_urgent=False):
    if audio_data.ndim > 1:
        audio_data = np.mean(audio_data, axis=1)
        
    target_sr = 44100
    if sr != target_sr:
        num_target_samples = int(len(audio_data) * target_sr / sr)
        audio_data = np.interp(
            np.linspace(0, len(audio_data), num_target_samples, endpoint=False),
            np.arange(len(audio_data)),
            audio_data
        )
        sr = target_sr

    fft = np.fft.rfft(audio_data)
    freqs = np.fft.rfftfreq(len(audio_data), 1.0 / sr)
    
    # Tactical radio bandpass (350Hz - 3200Hz)
    gain = np.zeros_like(freqs)
    low_cutoff = 380.0
    high_cutoff = 3100.0
    
    for i, f in enumerate(freqs):
        if f < 200:
            gain[i] = 0.01
        elif f < low_cutoff:
            gain[i] = 0.01 + 0.99 * ((f - 200) / (low_cutoff - 200))
        elif f <= high_cutoff:
            gain[i] = 1.0
        elif f <= 3800:
            gain[i] = 1.0 - 0.95 * ((f - high_cutoff) / (3800 - high_cutoff))
        else:
            gain[i] = 0.02
            
    filtered = np.fft.irfft(fft * gain, n=len(audio_data))
    
    # Non-linear harmonic saturation (radio mic drive)
    drive = 2.4 if is_urgent else 1.8
    saturated = np.tanh(filtered * drive) * 0.78
    
    # RF background hiss
    rng = np.random.RandomState(42)
    rf_noise = rng.normal(0, 0.02, len(saturated))
    voice_active = np.abs(saturated) > 0.015
    body = saturated + rf_noise * (0.5 + 0.5 * voice_active.astype(float))
    
    # Squelch burst start (55ms)
    squelch_in_len = int(sr * 0.055)
    t_in = np.linspace(0, 1, squelch_in_len)
    burst_in = rng.normal(0, 0.25, squelch_in_len) * np.exp(-t_in * 3.5)
    click_len = int(sr * 0.007)
    burst_in[:click_len] += np.sin(2 * np.pi * 980 * np.linspace(0, 0.007, click_len)) * 0.3
    
    # Squelch tail end (70ms)
    squelch_out_len = int(sr * 0.070)
    t_out = np.linspace(0, 1, squelch_out_len)
    burst_out = rng.normal(0, 0.28, squelch_out_len) * np.exp(-t_out * 4.0)
    chirp_len = int(sr * 0.03)
    chirp = np.sin(2 * np.pi * 1800 * np.linspace(0, 0.03, chirp_len)) * 0.2 * np.exp(-np.linspace(0, 1, chirp_len)*3.0)
    burst_out[:chirp_len] += chirp
    
    full = np.concatenate([burst_in, body, burst_out])
    peak = np.max(np.abs(full))
    if peak > 0:
        full = full / peak * 0.88
    return full.astype(np.float32), sr

voice_scripts = [
    # HQ Dispatch
    {
        "name": "HQDispatch_0",
        "voice": "en-US-ChristopherNeural",
        "text": "Command to Vanguard. Infiltration successful. Hostile forces are patrolling the sector. Proceed to waypoint and identify high value targets. Over.",
        "rate": "+0%",
        "urgent": False
    },
    {
        "name": "HQDispatch_1",
        "voice": "en-US-ChristopherNeural",
        "text": "Vanguard actual, this is Tactical Operations. You are green to engage hostile combatants at your discretion. Maintain radio discipline.",
        "rate": "+0%",
        "urgent": False
    },
    {
        "name": "HQDispatch_2",
        "voice": "en-US-ChristopherNeural",
        "text": "All stations, Vanguard is on the ground. Be advised, enemy patrols reported in the district. Stay sharp. Out.",
        "rate": "+0%",
        "urgent": False
    },
    # Enemy Patrol Chatter
    {
        "name": "EnemyChatter_0",
        "voice": "en-US-GuyNeural",
        "text": "Echo two, sector is quiet. Continuing scheduled patrol.",
        "rate": "+0%",
        "urgent": False
    },
    {
        "name": "EnemyChatter_1",
        "voice": "en-US-EricNeural",
        "text": "Check your sectors. Eyes open, command said we might have intruders.",
        "rate": "+0%",
        "urgent": False
    },
    {
        "name": "EnemyChatter_2",
        "voice": "en-US-BrianNeural",
        "text": "Nothing to report on grid four. Moving to next checkpoint.",
        "rate": "+0%",
        "urgent": False
    },
    {
        "name": "EnemyChatter_3",
        "voice": "en-US-SteffanNeural",
        "text": "Roger that. Staying on perimeter sweep.",
        "rate": "+0%",
        "urgent": False
    },
    # Enemy Alert Barks
    {
        "name": "EnemyAlert_0",
        "voice": "en-US-GuyNeural",
        "text": "Wait! Did you hear that? Someone's out there!",
        "rate": "+15%",
        "urgent": True
    },
    {
        "name": "EnemyAlert_1",
        "voice": "en-US-EricNeural",
        "text": "Hold up! Movement on the perimeter! Check your corners!",
        "rate": "+15%",
        "urgent": True
    },
    {
        "name": "EnemyAlert_2",
        "voice": "en-US-BrianNeural",
        "text": "Hostile spotted! Sound the alarm!",
        "rate": "+20%",
        "urgent": True
    },
    # Enemy Combat Barks
    {
        "name": "EnemyCombat_0",
        "voice": "en-US-GuyNeural",
        "text": "Sniper! Take cover!",
        "rate": "+25%",
        "urgent": True
    },
    {
        "name": "EnemyCombat_1",
        "voice": "en-US-EricNeural",
        "text": "Flank around the building! Suppress that position!",
        "rate": "+20%",
        "urgent": True
    },
    {
        "name": "EnemyCombat_2",
        "voice": "en-US-BrianNeural",
        "text": "He's up on the roof! Keep your heads down!",
        "rate": "+25%",
        "urgent": True
    }
]

async def generate_all_radio():
    print("=== Generating Authentic Military Radio Clips ===")
    temp_mp3 = "temp_voice.mp3"
    for item in voice_scripts:
        print(f"Generating {item['name']} ({item['voice']})...")
        communicate = edge_tts.Communicate(item["text"], item["voice"], rate=item["rate"])
        await communicate.save(temp_mp3)
        
        raw_data, sr = sf.read(temp_mp3)
        proc_data, target_sr = apply_tactical_radio_effects(raw_data, sr, is_urgent=item["urgent"])
        
        out_wav = os.path.join(OUTPUT_DIR, item["name"] + ".wav")
        sf.write(out_wav, proc_data, target_sr)
        print(f"  -> Saved {out_wav} ({len(proc_data)/target_sr:.2f}s)")
        
        # If HQDispatch_0, also save as HQDispatch.wav
        if item["name"] == "HQDispatch_0":
            hq_default = os.path.join(OUTPUT_DIR, "HQDispatch.wav")
            sf.write(hq_default, proc_data, target_sr)
            print(f"  -> Saved default {hq_default}")
            
    if os.path.exists(temp_mp3):
        os.remove(temp_mp3)
    print("All radio voice clips generated successfully!\n")

asyncio.run(generate_all_radio())
