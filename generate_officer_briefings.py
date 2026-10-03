import os
import asyncio
import numpy as np
import soundfile as sf
import edge_tts

OUTPUT_DIR = r"c:\Users\Geeta Sugara\Documents\ChatGPT\sniper2\Assets\Resources\Audio\Briefings"
os.makedirs(OUTPUT_DIR, exist_ok=True)

OFFICER_VOICE = "en-US-ChristopherNeural"

BRIEFINGS = [
    {
        "id": 0,
        "title": "FIRST CONTACT",
        "text": "Overwatch, this is Command. Target is confirmed on site. You are clear to engage on your mark. Make it clean. Out."
    },
    {
        "id": 1,
        "title": "VIP OVERWATCH",
        "text": "Vanguard, our informant is moving through hostile territory. Keep your eyes on the perimeter rooftops and protect the asset at all costs. Out."
    },
    {
        "id": 2,
        "title": "FUGITIVE SPRINT",
        "text": "Target is on the run! Do not let him reach the extraction vehicle. Lead your shot and put him down immediately! Out."
    },
    {
        "id": 3,
        "title": "SILENT INFILTRATION",
        "text": "Heavy patrol density in this compound. Suppress your weapon, stay in the shadows, and eliminate the sentries without raising the alarm. Out."
    },
    {
        "id": 4,
        "title": "RIVAL DUEL",
        "text": "Heads up marksman. Enemy sniper locked in an elevated roost. Find his optic glint before he acquires your position. Out."
    },
    {
        "id": 5,
        "title": "HOT EXTRACTION",
        "text": "Friendly chopper is en route for extraction. Provide defensive overwatch and eliminate all incoming hostile pursuit squads. Out."
    },
    {
        "id": 6,
        "title": "PLAZA WARLORD",
        "text": "Syndicate commander spotted in the civic plaza. High value target. Ensure positive identification before taking the shot. Out."
    },
    {
        "id": 7,
        "title": "CONVOY GUARDIAN",
        "text": "Hostile ambush incoming on friendly transport. Neutralize the heavy gunners before our convoy takes critical damage. Out."
    },
    {
        "id": 8,
        "title": "PERIMETER BREACH",
        "text": "Hostiles are attempting to breach our outer perimeter. Hold the line marksman, no hostiles pass your sector. Out."
    },
    {
        "id": 9,
        "title": "HARBOR SWEEP",
        "text": "Smuggling vessel docked at the industrial port. Neutralize the guards and secure the shipment. Out."
    },
    {
        "id": 10,
        "title": "BLACKSITE ASSAULT",
        "text": "All stations, this is Blacksite command. High value intel inside the bunker. Neutralize the defense detail and clear the zone. Out."
    },
    {
        "id": 11,
        "title": "APEX RECKONING",
        "text": "This is it sniper. The syndicate leader is surrounded by elite bodyguards. One shot, one kill. Make every bullet count. Out."
    }
]

def apply_military_radio_dsp(audio_data, sr):
    if audio_data.ndim > 1:
        audio_data = np.mean(audio_data, axis=1)

    target_sr = 44100
    if sr != target_sr:
        num_target = int(len(audio_data) * target_sr / sr)
        audio_data = np.interp(
            np.linspace(0, len(audio_data), num_target, endpoint=False),
            np.arange(len(audio_data)),
            audio_data
        )
        sr = target_sr

    # Military radio bandpass: 300Hz to 3400Hz
    fft = np.fft.rfft(audio_data)
    freqs = np.fft.rfftfreq(len(audio_data), 1.0 / sr)
    gain = np.zeros_like(freqs)
    low_cutoff = 300.0
    high_cutoff = 3400.0

    for i, f in enumerate(freqs):
        if f < 160:
            gain[i] = 0.02
        elif f < low_cutoff:
            gain[i] = 0.02 + 0.98 * ((f - 160) / (low_cutoff - 160))
        elif f <= high_cutoff:
            gain[i] = 1.0
        elif f <= 4200:
            gain[i] = 1.0 - 0.95 * ((f - high_cutoff) / (4200 - high_cutoff))
        else:
            gain[i] = 0.03

    filtered = np.fft.irfft(fft * gain, n=len(audio_data))

    # Heavy radio tube compression & warm saturation
    filtered = np.tanh(filtered * 2.2) * 0.80

    # Subtle RF transmission noise
    rng = np.random.RandomState(42)
    rf_noise = rng.normal(0, 0.015, len(filtered))
    voice_active = np.abs(filtered) > 0.012
    voice_body = filtered + rf_noise * (0.5 + 0.5 * voice_active.astype(float))

    # Squelch key-in burst (65ms)
    squelch_in_len = int(sr * 0.065)
    t_in = np.linspace(0, 1, squelch_in_len)
    burst_in = rng.normal(0, 0.26, squelch_in_len) * np.exp(-t_in * 3.8)
    click_len = int(sr * 0.009)
    burst_in[:click_len] += np.sin(2 * np.pi * 920 * np.linspace(0, 0.009, click_len)) * 0.35

    # Squelch key-out tail (80ms) with roger beep chirp
    squelch_out_len = int(sr * 0.080)
    t_out = np.linspace(0, 1, squelch_out_len)
    burst_out = rng.normal(0, 0.28, squelch_out_len) * np.exp(-t_out * 4.2)
    chirp_len = int(sr * 0.036)
    chirp = np.sin(2 * np.pi * 1750 * np.linspace(0, 0.036, chirp_len)) * 0.22 * np.exp(-np.linspace(0, 1, chirp_len) * 3.2)
    burst_out[:chirp_len] += chirp

    # Combine
    full_audio = np.concatenate([burst_in, voice_body, burst_out])
    peak = np.max(np.abs(full_audio))
    if peak > 0:
        full_audio = full_audio / peak * 0.88

    return full_audio.astype(np.float32), sr

async def generate_single_briefing(item):
    b_id = item["id"]
    text = item["text"]
    out_path = os.path.join(OUTPUT_DIR, f"Briefing_{b_id}.wav")
    temp_wav = os.path.join(OUTPUT_DIR, f"temp_{b_id}.mp3")

    print(f"Generating Officer Voice for Mission {b_id}: {item['title']}...")
    communicate = edge_tts.Communicate(text, OFFICER_VOICE)
    await communicate.save(temp_wav)

    with sf.SoundFile(temp_wav) as f:
        data = f.read(dtype='float32')
        sr = f.samplerate

    dsp_audio, final_sr = apply_military_radio_dsp(data, sr)
    sf.write(out_path, dsp_audio, final_sr, subtype='PCM_16')

    try:
        if os.path.exists(temp_wav):
            os.remove(temp_wav)
    except Exception:
        pass

    print(f"-> Saved: Briefing_{b_id}.wav ({len(dsp_audio)/final_sr:.2f}s)")

async def main():
    print("=== Generating Heavy Army Officer Radio Briefings ===")
    for item in BRIEFINGS:
        await generate_single_briefing(item)
    print("=== All Officer Audio Briefings Completed! ===")

if __name__ == "__main__":
    asyncio.run(main())
