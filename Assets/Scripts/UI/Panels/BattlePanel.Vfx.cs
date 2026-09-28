using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Roguelike;
using Roguelike.Data;
using Roguelike.Core;

/// <summary>
/// BattlePanel 分部类 —— 出牌演出：投射物 / 命中特效 / 拖尾 / 闪光 / 精灵缓存。
/// （纯拆分，逻辑未改动）
/// </summary>
public partial class BattlePanel
{
    /// <summary>把选中的手牌生成投射物，沿弧线飞向敌人；多张错开起飞但「同时命中」</summary>
    private void SpawnPlayProjectiles(List<CardData> cards)
    {
        if (damageTextContainer == null) return;

        // 先收集有效的手牌位置（避免手牌被刷新后找不到）
        var targets = new List<Vector3>();
        foreach (var card in cards)
        {
            var ui = FindCardUI(card);
            if (ui != null) targets.Add(ui.transform.position);
        }
        if (targets.Count == 0) return;

        Vector3 targetWorld = GetEnemyAnchorWorld();

        float stagger = Mathf.Max(0f, cardFlyStagger);
        float baseDuration = Mathf.Max(0.05f, cardFlyDuration);
        // 统一命中时刻：最后一发起飞后 baseDuration 秒
        float arrival = baseDuration + stagger * (targets.Count - 1);

        for (int i = 0; i < targets.Count; i++)
        {
            Vector3 from = targets[i];

            // 牌化作特效的瞬间：原地闪一下
            SpawnImpactFlash(from);

            Image projImg = CreateProjectile();
            Image trailImg = cardFlyTrail ? CreateFollowTrail() : null;

            float delay = i * stagger;
            float duration = Mathf.Max(0.05f, arrival - delay);

            activeProjectiles++;
            StartCoroutine(FlyProjectileRoutine(projImg, trailImg, from, targetWorld, delay, duration, GetTrailFrames()));
        }
    }

    private System.Collections.IEnumerator FlyProjectileRoutine(Image projImg, Image trailImg, Vector3 from, Vector3 to, float delay, float duration, Sprite[] frames)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        RectTransform projRt = projImg != null ? projImg.rectTransform : null;
        RectTransform trailRt = trailImg != null ? trailImg.rectTransform : null;
        if (projRt == null)
        {
            OnProjectileFinished();
            yield break;
        }

        duration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;

        // 弧线：控制点在中点上方，横向距离越大弧越高，再加随机抖动
        Vector3 mid = (from + to) * 0.5f;
        float arc = cardFlyArc + Mathf.Abs(to.x - from.x) * cardFlyArcPerWidth;
        Vector3 control = mid + new Vector3(UnityEngine.Random.Range(-60f, 60f), arc, 0f);

        Vector3 prevPos = from;
        Vector3 tail = from;
        float frameTimer = 0f;
        int frameIndex = 0;

        while (elapsed < duration && projRt != null)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            float et = t * t * (3f - 2f * t); // smoothstep 缓动

            // 二次贝塞尔弧线
            Vector3 a = Vector3.Lerp(from, control, et);
            Vector3 b = Vector3.Lerp(control, to, et);
            projRt.position = Vector3.Lerp(a, b, et);

            // 运动方向
            Vector3 moveDir = projRt.position - prevPos;
            if (moveDir.sqrMagnitude < 0.0001f) moveDir = to - from;
            prevPos = projRt.position;

            // 横向特效旋转对齐飞行方向
            if (cardProjectileSpin)
                projRt.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg);

            // 缩放：先弹大再缩小
            float scale = t < 0.2f
                ? Mathf.Lerp(1f, 1.2f, t / 0.2f)
                : Mathf.Lerp(1.2f, 0.6f, (t - 0.2f) / 0.8f);
            projRt.localScale = Vector3.one * scale;

            // 序列帧投射物
            if (projImg != null && frames != null && frames.Length > 0)
            {
                frameTimer += dt;
                float ft = 1f / Mathf.Max(1f, cardTrailFrameRate);
                while (frameTimer >= ft)
                {
                    frameTimer -= ft;
                    frameIndex = (frameIndex + 1) % frames.Length;
                }
                projImg.sprite = frames[frameIndex];
            }

            // ===== 跟随式拖尾 =====
            if (trailRt != null)
            {
                Vector3 head = projRt.position;
                tail = Vector3.Lerp(tail, head, 1f - Mathf.Exp(-9f * dt));

                Vector3 dir = head - tail;
                if (dir.sqrMagnitude < 0.0001f) dir = to - from;

                float dist = dir.magnitude;
                if (dist > 3f)
                {
                    trailRt.gameObject.SetActive(true);
                    trailRt.position = head;
                    // 枢轴在左端(头部)；+180° 让本地 +X 指向身后，贴图左边=头部贴在投射物上
                    trailRt.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 180f);
                    trailRt.sizeDelta = new Vector2(dist, cardTrailSize.y * scale);

                    if (trailImg != null)
                    {
                        float alpha = 0.55f * Mathf.Clamp01(1f - Mathf.Max(0f, t - 0.6f) / 0.4f);
                        trailImg.color = new Color(1f, 1f, 1f, alpha);
                    }
                }
                else
                {
                    trailRt.gameObject.SetActive(false);
                }
            }

            yield return null;
        }

        if (trailRt != null) Roguelike.Core.PoolManager.Return("CardTrail", trailRt.GetComponent<Image>());
            if (projRt != null)
            {
                if (cardFlyImpact) SpawnHitEffect(projRt.position);
                Roguelike.Core.PoolManager.Return("Projectile", projRt.GetComponent<Image>());
            }
            OnProjectileFinished();
        }

    /// <summary>创建投射物（牌打出后变成的特效）</summary>
    private Image CreateProjectile()
    {
        var img = Roguelike.Core.PoolManager.Get<Image>("Projectile");
        if (img == null)
        {
            // 池未就绪时降级
            if (damageTextContainer == null) return null;
            var go = new GameObject("PlayProjectile", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(damageTextContainer, false);
            go.transform.SetAsLastSibling();
            img = go.GetComponent<Image>();
        }
        else
        {
            img.transform.SetParent(damageTextContainer, false);
            img.transform.SetAsLastSibling();
        }

        img.sprite = GetProjectileSprite();
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.95f);

        var rt = img.rectTransform;
        rt.sizeDelta = cardProjectileSize;
        rt.localScale = Vector3.one;
        rt.localPosition = Vector3.zero;

        return img;
    }

    private Sprite projectileSpriteCache;
    private bool projectileSpriteLoaded;

    private Sprite GetProjectileSprite()
    {
        if (cardProjectileSprite != null) return cardProjectileSprite;

        if (!projectileSpriteLoaded)
        {
            projectileSpriteLoaded = true;
            if (!string.IsNullOrEmpty(cardProjectileSpritePath))
                projectileSpriteCache = Resources.Load<Sprite>(cardProjectileSpritePath);
        }
        if (projectileSpriteCache != null) return projectileSpriteCache;

        var frames = GetTrailFrames();
        if (frames != null && frames.Length > 0) return frames[0];

        var trail = GetTrailSprite();
        if (trail != null) return trail;

        return GetRuntimeCircleSprite();
    }

    // ===== 命中特效 =====

    /// <summary>命中特效：优先预制体，其次序列帧，都没有则退回运行时圆光</summary>
    private void SpawnHitEffect(Vector3 worldPos)
    {
        if (hitEffectPrefab != null)
        {
            var go = hitEffectWorldSpace
                ? Instantiate(hitEffectPrefab)
                : Instantiate(hitEffectPrefab, damageTextContainer);

            if (go != null)
            {
                go.transform.position = worldPos;
                if (hitEffectRandomRotation && !hitEffectWorldSpace)
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                Destroy(go, Mathf.Max(0.1f, hitEffectLifetime));
            }
            return;
        }

        var frames = GetHitEffectFrames();
        if (frames != null && frames.Length > 0)
        {
            SpawnHitEffectAnim(worldPos, frames);
            return;
        }

        SpawnImpactFlash(worldPos);
    }

    private void SpawnHitEffectAnim(Vector3 worldPos, Sprite[] frames)
    {
        if (damageTextContainer == null) return;

        var img = Roguelike.Core.PoolManager.Get<Image>("HitEffect");
        if (img == null)
        {
            // 池未就绪时降级
            var go = new GameObject("HitEffect", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(damageTextContainer, false);
            go.transform.SetAsLastSibling();
            img = go.GetComponent<Image>();
        }
        else
        {
            img.transform.SetParent(damageTextContainer, false);
            img.transform.SetAsLastSibling();
        }

        img.sprite = frames[0];
        img.raycastTarget = false;
        img.color = Color.white;

        var rt = img.rectTransform;
        rt.position = worldPos;
        rt.sizeDelta = hitEffectSize;
        if (hitEffectRandomRotation)
            rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

        StartCoroutine(HitEffectAnimRoutine(rt, img, frames));
    }

    private System.Collections.IEnumerator HitEffectAnimRoutine(RectTransform rt, Image img, Sprite[] frames)
    {
        float frameTime = 1f / Mathf.Max(1f, hitEffectFrameRate);
        float total = frameTime * frames.Length;
        float elapsed = 0f;

        while (elapsed < total && rt != null)
        {
            elapsed += Time.deltaTime;
            int frame = Mathf.Clamp(Mathf.FloorToInt(elapsed / frameTime), 0, frames.Length - 1);
            if (img != null) img.sprite = frames[frame];
            yield return null;
        }
        Roguelike.Core.PoolManager.Return("HitEffect", img);
    }

    private Sprite[] hitFramesCache;
    private bool hitFramesLoaded;

    private Sprite[] GetHitEffectFrames()
    {
        if (hitEffectFrames != null && hitEffectFrames.Length > 0) return hitEffectFrames;

        if (!hitFramesLoaded)
        {
            hitFramesLoaded = true;
            if (!string.IsNullOrEmpty(hitEffectFramesPath))
            {
                var loaded = Resources.LoadAll<Sprite>(hitEffectFramesPath);
                if (loaded != null && loaded.Length > 0)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    hitFramesCache = loaded;
                }
            }
        }
        return hitFramesCache;
    }

    /// <summary>创建跟随式拖尾（枢轴在右端=头部，向身后拉伸）</summary>
    private Image CreateFollowTrail()
    {
        var img = Roguelike.Core.PoolManager.Get<Image>("CardTrail");
        if (img == null)
        {
            // 池未就绪时降级
            if (damageTextContainer == null) return null;
            var go = new GameObject("CardTrailRibbon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(damageTextContainer, false);
            go.transform.SetAsFirstSibling();
            img = go.GetComponent<Image>();
        }
        else
        {
            img.transform.SetParent(damageTextContainer, false);
            img.transform.SetAsFirstSibling();
        }

        img.sprite = GetTrailSprite() ?? GetRuntimeStreakSprite();
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.55f);

        var rt = img.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);   // 左端=头部
        rt.sizeDelta = new Vector2(0f, cardTrailSize.y);
        img.gameObject.SetActive(false);

        return img;
    }

    private static Sprite runtimeStreakSprite;

    /// <summary>运行时生成一条柔光渐变条（右端实、左端透明、中间亮），无需素材</summary>
    private static Sprite GetRuntimeStreakSprite()
    {
        if (runtimeStreakSprite == null)
        {
            const int w = 128;
            const int h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float vy = 1f - Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f; // 中间亮
                vy *= vy;
                for (int x = 0; x < w; x++)
                {
                    float vx = (x + 0.5f) / w; // 0=左(头) 1=右(尾)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, (1f - vx) * vy));
                }
            }
            tex.Apply();
            runtimeStreakSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            runtimeStreakSprite.name = "RuntimeStreak";
        }
        return runtimeStreakSprite;
    }

    private void SpawnImpactFlash(Vector3 worldPos)
    {
        if (damageTextContainer == null) return;

        var go = new GameObject("ImpactFlash", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(damageTextContainer, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.sprite = GetRuntimeCircleSprite();
        img.raycastTarget = false;
        img.color = new Color(1f, 0.92f, 0.6f, 0.85f);

        var rt = go.GetComponent<RectTransform>();
        rt.position = worldPos;
        rt.sizeDelta = new Vector2(70f, 70f);

        StartCoroutine(ImpactFlashRoutine(rt, img));
    }

    private System.Collections.IEnumerator ImpactFlashRoutine(RectTransform rt, Image img)
    {
        const float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.9f, t);
            if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(0.85f, 0f, t));
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private static Sprite runtimeCircleSprite;

    private static Sprite GetRuntimeCircleSprite()
    {
        if (runtimeCircleSprite == null)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(1f - d / r);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            runtimeCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            runtimeCircleSprite.name = "RuntimeCircle";
        }
        return runtimeCircleSprite;
    }

}
