using System.Collections.Generic;
using UnityEngine;

/// <summary>可用的音效（占位音由 SfxSynth 生成；有真素材后替换 BuildClips）</summary>
public enum Sfx
{
    Click,       // 通用按钮
    CardSelect,  // 选牌
    CardPlay,    // 出牌
    Hit,         // 命中
    HitHeavy,    // 玩家受击 / 重击
    Shield,      // 获得防御
    Heal,        // 回复
    Draw,        // 抽牌
    Coin,        // 金币
    Reward,      // 奖励 / 胜利
    Error,       // 失败 / 无效操作
    Boss,        // Boss 出手
}

/// <summary>
/// 全局音频管理器：SFX（多声道池）+ BGM。主音量跟随 GameSettings。
/// 由 GameBootstrap 在启动时创建（DontDestroyOnLoad）。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const int Voices = 12;

    private AudioSource[] pool;
    private int next;
    private AudioSource bgm;
    private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();

    // 轻微去重：同一音效极短时间内不重复播（避免同帧大量叠加爆音）
    private Sfx lastSfx;
    private float lastTime = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildSources();
        BuildClips();
    }

    private void BuildSources()
    {
        pool = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            pool[i] = s;
        }

        bgm = gameObject.AddComponent<AudioSource>();
        bgm.playOnAwake = false;
        bgm.loop = true;
        bgm.spatialBlend = 0f;
        bgm.volume = 0.4f;
    }

    private void BuildClips()
    {
        // 优先用 Resources/Audio/SFX/<文件名> 的真素材；没有就用程序化占位音
        clips[Sfx.Click]      = LoadOr("click",       () => SfxSynth.Tone(1400f, 0.05f, 0.22f, 22f, 1));
        clips[Sfx.CardSelect] = LoadOr("card_select", () => SfxSynth.Tone(900f, 0.06f, 0.20f, 16f));
        clips[Sfx.CardPlay]   = LoadOr("card_play",   () => SfxSynth.Tone(720f, 0.16f, 0.28f, 6f, 0, 220f));
        clips[Sfx.Hit]        = LoadOr("hit",         () => SfxSynth.Noise(0.10f, 0.32f, 20f));
        clips[Sfx.HitHeavy]   = LoadOr("hit_heavy",   () => SfxSynth.Noise(0.22f, 0.42f, 9f));
        clips[Sfx.Shield]     = LoadOr("shield",      () => SfxSynth.Tone(300f, 0.16f, 0.26f, 8f, 2, 520f));
        clips[Sfx.Heal]       = LoadOr("heal",        () => SfxSynth.Sequence(new[] { 523f, 784f }, 0.09f, 0.24f));
        clips[Sfx.Draw]       = LoadOr("card_draw",   () => SfxSynth.Tone(1000f, 0.06f, 0.16f, 14f));
        clips[Sfx.Coin]       = LoadOr("coin",        () => SfxSynth.Sequence(new[] { 1320f, 1760f }, 0.05f, 0.20f, 1));
        clips[Sfx.Reward]     = LoadOr("reward",      () => SfxSynth.Sequence(new[] { 523f, 659f, 784f, 1047f }, 0.08f, 0.24f));
        clips[Sfx.Error]      = LoadOr("error",       () => SfxSynth.Tone(160f, 0.18f, 0.30f, 6f, 1));
        clips[Sfx.Boss]       = LoadOr("boss",        () => SfxSynth.Noise(0.40f, 0.38f, 4f));
    }

    /// <summary>优先 Resources/Audio/SFX/&lt;file&gt;，没有则用占位音</summary>
    private static AudioClip LoadOr(string file, System.Func<AudioClip> fallback)
    {
        var clip = Resources.Load<AudioClip>("Audio/SFX/" + file);
        return clip != null ? clip : fallback();
    }

    /// <summary>播放音效</summary>
    public void Play(Sfx id, float volume = 1f, float pitch = 1f)
    {
        if (pool == null) return;

        if (id == lastSfx && Time.unscaledTime - lastTime < 0.03f) return;
        lastSfx = id;
        lastTime = Time.unscaledTime;

        if (clips.TryGetValue(id, out var c) && c != null)
        {
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = pitch;
            s.PlayOneShot(c, Mathf.Clamp01(volume));
        }
    }

    /// <summary>播放任意 clip（有真素材时可用）</summary>
    public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || pool == null) return;
        var s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = pitch;
        s.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public void PlayBgm(AudioClip clip, float volume = 0.4f)
    {
        if (clip == null || bgm == null) return;
        if (bgm.clip == clip && bgm.isPlaying) return;
        bgm.clip = clip;
        bgm.volume = Mathf.Clamp01(volume);
        bgm.Play();
    }

    public void StopBgm()
    {
        if (bgm != null) bgm.Stop();
    }

    /// <summary>从 Resources 加载 BGM 并循环播放（没有则静默，不报错）</summary>
    public void PlayBgmFromResources(string resourcePath = "Audio/BGM", float volume = 0.4f)
    {
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip != null) PlayBgm(clip, volume);
    }
}
