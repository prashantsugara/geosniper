import os
import asyncio
import numpy as np
import soundfile as sf
import edge_tts

def apply_tactical_radio_effects(audio_data, sr):
    # Ensure float32 mono
    if audio_data.ndim > 1:
        audio_data = np.mean(audio_data, axis=1)
    
    # Resample to 44100 if needed
    target_sr = 44100
    if sr != target_sr:
        num_target_samples = int(len(audio_data) * target_sr / sr)
        audio_data = np.interp(
            np.linspace(0, len(audio_data), num_target_samples, endpoint=False),
            np.arange(len(audio_data)),
            audio_data
        )
        sr = target_sr

    # Bandpass filter using FFT: Keep 300Hz to 3400Hz (military tactical radio standard)
    fft = np.fft.rfft(audio_data)
    freqs = np.fft.rfftfreq(len(audio_data), 1.0 / sr)
    
    # Smooth roll-off filter
    gain = np.zeros_like(freqs)
    low_cutoff = 350.0
    high_cutoff = 3200.0
    
    for i, f in enumerate(freqs):
        if f < 200:
            gain[i] = 0.01
        elif f < low_cutoff:
            gain[i] = 0.01 + 0.99 * ((f - 200) / (low_cutoff - 200))
        elif f <= high_cutoff:
            gain[i] = 1.0
        elif f <= 4000:
            gain[i] = 1.0 - 0.95 * ((f - high_cutoff) / (4000 - high_cutoff))
        else:
            gain[i] = 0.02
            
    filtered_voice = np.fft.irfft(fft * gain, n=len(audio_data))
    
    # Soft saturation / compression (radio mic clipping)
    filtered_voice = np.tanh(filtered_voice * 2.2) * 0.75
    
    # Subtle continuous background RF noise during transmission
    rng = np.random.RandomState(42)
    rf_noise = rng.normal(0, 0.022, len(filtered_voice))
    voice_active = np.abs(filtered_voice) > 0.01
    voice_body = filtered_voice + rf_noise * (0.6 + 0.4 * voice_active.astype(float))
    
    # Mic key-in squelch burst (60ms)
    squelch_in_len = int(sr * 0.065)
    t_in = np.linspace(0, 1, squelch_in_len)
    burst_in = rng.normal(0, 0.28, squelch_in_len) * np.exp(-t_in * 3.5)
    # add click
    click_len = int(sr * 0.008)
    burst_in[:click_len] += np.sin(2 * np.pi * 950 * np.linspace(0, 0.008, click_len)) * 0.35
    
    # Mic key-out squelch tail (75ms)
    squelch_out_len = int(sr * 0.08)
    t_out = np.linspace(0, 1, squelch_out_len)
    burst_out = rng.normal(0, 0.32, squelch_out_len) * np.exp(-t_out * 4.0)
    # roger chirp at end
    chirp_len = int(sr * 0.035)
    chirp = np.sin(2 * np.pi * 1750 * np.linspace(0, 0.035, chirp_len)) * 0.22 * np.exp(-np.linspace(0, 1, chirp_len)*3.0)
    burst_out[:chirp_len] += chirp
    
    # Assemble full transmission
    full_audio = np.concatenate([burst_in, voice_body, burst_out])
    
    # Normalize peak to -1dB (0.89)
    peak = np.max(np.abs(full_audio))
    if peak > 0:
        full_audio = full_audio / peak * 0.88
        
    return full_audio.astype(np.float32), sr

print("DSP function ready")
