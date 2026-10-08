using UnityEngine;

/// <summary>
/// 程序化生成占位音效（无需素材）。真素材就位后可整体替换为 Resources 加载。
/// </summary>
public static class SfxSynth
{
    private const int Rate = 44100;

    /// <summary>单音（wave: 0=正弦 1=方波 2=三角），指数衰减；freqEnd>0 时做频率滑动</summary>
    public static AudioClip Tone(float freq, float dur, float vol, float decay = 8f, int wave = 0, float freqEnd = -1f)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(Rate * dur));
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float f = freqEnd > 0f ? Mathf.Lerp(freq, freqEnd, t) : freq;
            phase += 2f * Mathf.PI * f / Rate;
            float s = Wave(phase, wave);
            data[i] = s * Mathf.Exp(-decay * t) * vol;
        }
        return Make("synth_tone", data);
    }

    /// <summary>白噪声爆发（打击感）</summary>
    public static AudioClip Noise(float dur, float vol, float decay = 12f)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(Rate * dur));
        var data = new float[n];
        var rng = new System.Random(20260101);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            data[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-decay * t) * vol;
        }
        return Make("synth_noise", data);
    }

    /// <summary>音符序列（上行琶音等）</summary>
    public static AudioClip Sequence(float[] freqs, float noteDur, float vol, int wave = 0)
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(Rate * noteDur));
        var data = new float[per * freqs.Length];
        for (int k = 0; k < freqs.Length; k++)
        {
            float phase = 0f;
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / per;
                phase += 2f * Mathf.PI * freqs[k] / Rate;
                data[k * per + i] = Wave(phase, wave) * Mathf.Exp(-6f * t) * vol;
            }
        }
        return Make("synth_seq", data);
    }

    private static float Wave(float phase, int wave)
    {
        switch (wave)
        {
            case 1: return Mathf.Sign(Mathf.Sin(phase)) * 0.6f;                       // 方波（降点音量避免刺耳）
            case 2: return Mathf.Asin(Mathf.Sin(phase)) * (2f / Mathf.PI);            // 三角波
            default: return Mathf.Sin(phase);
        }
    }

    private static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
