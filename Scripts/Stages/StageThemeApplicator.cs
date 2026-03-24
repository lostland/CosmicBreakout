using UnityEngine;
using System.Collections;

/// <summary>
/// 씬 시작 시 현재 스테이지 테마에 맞춰 파티클 색상, 배경 천체,
/// 벽돌 트레일 색상, 공/패들 컬러를 동적으로 설정한다.
/// </summary>
public class StageThemeApplicator : MonoBehaviour
{
    [Header("Particle Systems")]
    [SerializeField] ParticleSystem _backgroundStars;
    [SerializeField] ParticleSystem _ambientParticles;  // 성운/먼지 느낌
    [SerializeField] ParticleSystem _brickDestroyTemplate;

    [Header("Celestial Objects")]
    [SerializeField] SpriteRenderer[] _celestialObjects;  // 행성/달 이미지들

    [Header("Ball & Paddle")]
    [SerializeField] SpriteRenderer   _ballSprite;
    [SerializeField] TrailRenderer    _ballTrail;
    [SerializeField] SpriteRenderer   _paddleSprite;
    [SerializeField] ParticleSystem   _paddleAmbient;

    [Header("Light (URP 2D Light or Legacy)")]
    [SerializeField] Light2D[]        _sceneLights;   // 필요 시 URP Light2D 연결

    void Start()
    {
        int idx = GameManager.Instance?.CurrentStageIndex ?? 0;
        ApplyTheme(StageDatabase.GetStage(idx));
    }

    private void ApplyTheme(StageData sd)
    {
        ApplyStarParticles(sd);
        ApplyAmbientParticles(sd);
        ApplyBallPaddleColors(sd);
        ApplyCelestials(sd);
        ApplySceneLights(sd);
    }

    // ═════════════════════════════════════════════════════════════
    // 배경 별 파티클
    // ═════════════════════════════════════════════════════════════

    private void ApplyStarParticles(StageData sd)
    {
        if (_backgroundStars == null) return;
        var main = _backgroundStars.main;

        switch (sd.ThemeId)
        {
            case StageTheme.Earth:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.8f, 0.9f, 1.0f), Color.white);
                main.startSize  = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                break;
            case StageTheme.SolarSystem:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1.0f, 0.8f, 0.5f), Color.white);
                main.startSize  = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                break;
            case StageTheme.StarSystem:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.7f, 0.4f, 1.0f), new Color(0.4f, 1.0f, 0.9f));
                main.startSize  = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
                break;
            case StageTheme.GalacticCore:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1.0f, 0.9f, 0.3f), new Color(1.0f, 0.5f, 0.1f));
                main.startSize  = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
                break;
            case StageTheme.Extragalactic:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1.0f, 0.2f, 0.8f), new Color(0.2f, 0.6f, 1.0f));
                main.startSize  = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                break;
        }
        _backgroundStars.Play();
    }

    // ═════════════════════════════════════════════════════════════
    // 주변 파티클 (성운/먼지)
    // ═════════════════════════════════════════════════════════════

    private void ApplyAmbientParticles(StageData sd)
    {
        if (_ambientParticles == null) return;
        var main     = _ambientParticles.main;
        var emission = _ambientParticles.emission;

        // 스테이지가 높을수록 파티클 밀도 증가
        float densityMult = 1f + sd.ThemeId switch {
            StageTheme.GalacticCore  => 2f,
            StageTheme.Extragalactic => 3f,
            _                        => (float)sd.ThemeId * 0.5f
        };
        emission.rateOverTime = 8f * densityMult;

        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(sd.PrimaryColor.r, sd.PrimaryColor.g, sd.PrimaryColor.b, 0.15f),
            new Color(sd.SecondaryColor.r, sd.SecondaryColor.g, sd.SecondaryColor.b, 0.08f));

        _ambientParticles.Play();
    }

    // ═════════════════════════════════════════════════════════════
    // 공 / 패들 색상
    // ═════════════════════════════════════════════════════════════

    private void ApplyBallPaddleColors(StageData sd)
    {
        // 공: 스테이지 주 색상의 밝은 버전
        if (_ballSprite)
        {
            _ballSprite.color = Color.Lerp(sd.PrimaryColor, Color.white, 0.6f);
        }

        // 공 트레일
        if (_ballTrail)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new[] {
                    new GradientColorKey(Color.Lerp(sd.PrimaryColor, Color.white, 0.7f), 0f),
                    new GradientColorKey(sd.SecondaryColor, 0.5f),
                    new GradientColorKey(sd.PrimaryColor, 1f)
                },
                new[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0.3f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            _ballTrail.colorGradient = g;
        }

        // 패들: 스테이지 보조 색상
        if (_paddleSprite)
        {
            _paddleSprite.color = Color.Lerp(sd.SecondaryColor, Color.white, 0.3f);
        }

        // 패들 주변 파티클
        if (_paddleAmbient)
        {
            var main = _paddleAmbient.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                sd.PrimaryColor, sd.SecondaryColor);
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 천체 오브젝트 색상
    // ═════════════════════════════════════════════════════════════

    private void ApplyCelestials(StageData sd)
    {
        foreach (var sr in _celestialObjects)
        {
            if (sr == null) continue;
            // 스테이지 색상으로 천체 색조 변경
            sr.color = Color.Lerp(sd.PrimaryColor, Color.white, 0.2f);
        }
    }

    // ═════════════════════════════════════════════════════════════
    // 씬 라이팅
    // ═════════════════════════════════════════════════════════════

    private void ApplySceneLights(StageData sd)
    {
        // URP 2D Light를 사용할 경우 색상 변경
        // 일반 프로젝트에서는 Shader.SetGlobalColor 등으로 대체 가능
        foreach (var light in _sceneLights)
        {
            if (light == null) continue;
            // light.color = sd.PrimaryColor; // URP Light2D의 경우
        }
    }
}

// ══════════════════════════════════════════════════════════════════
// 스테이지별 웨이브 파라미터 설명 (주석)
// ══════════════════════════════════════════════════════════════════
// StageTheme.Earth        → waveAmp=2.5, freq=0.8  → 공기 흐름 같은 부드러운 물결
// StageTheme.SolarSystem  → waveAmp=3.0, freq=0.6  → 중력 진동, 느리고 묵직
// StageTheme.StarSystem   → waveAmp=3.5, freq=1.0  → 낯선 유기적 맥동
// StageTheme.GalacticCore → waveAmp=4.5, freq=1.3  → 강한 에너지 왜곡, 빠름
// StageTheme.Extragalactic→ waveAmp=5.5, freq=1.6  → 시공간 뒤틀림, 가장 격렬
