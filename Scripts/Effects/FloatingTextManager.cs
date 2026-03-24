using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 화면 임의 위치에 팝업 텍스트를 띄운다.
/// 벽돌 파괴 콤보 숫자, 아이템 이름, 지루함 방지 이벤트 알림 등에 사용.
/// </summary>
public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    [SerializeField] FloatingText _prefab;
    [SerializeField] int          _poolSize = 20;
    [SerializeField] Canvas       _canvas;

    private Queue<FloatingText> _pool = new Queue<FloatingText>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        for (int i = 0; i < _poolSize; i++)
        {
            var ft = Instantiate(_prefab, _canvas.transform);
            ft.gameObject.SetActive(false);
            _pool.Enqueue(ft);
        }
    }

    /// <summary>
    /// worldPos: 월드 좌표. canvas가 Screen Space Overlay면 자동 변환.
    /// </summary>
    public void Show(string text, Vector3 worldPos, float duration = 1.0f,
                     Color? color = null, int fontSize = 22)
    {
        FloatingText ft = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(_prefab, _canvas.transform);
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        ft.Play(text, screenPos, duration, color ?? Color.white, fontSize, () => _pool.Enqueue(ft));
    }

    /// <summary>코인 획득 수량 팝업 (노란색)</summary>
    public void ShowCoin(int amount, Vector3 worldPos)
    {
        Show($"+{amount}", worldPos, 0.8f, new Color(1f, 0.9f, 0.2f), 20);
    }

    /// <summary>콤보 팝업</summary>
    public void ShowCombo(int combo, Vector3 worldPos)
    {
        Show($"COMBO x{combo}!", worldPos, 1.0f, new Color(1f, 0.5f, 0.1f), 26);
    }
}

// ══════════════════════════════════════════════════════════════════
// FloatingText: 개별 팝업 텍스트
// ══════════════════════════════════════════════════════════════════

public class FloatingText : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _text;
    [SerializeField] RectTransform   _rt;

    private System.Action _onComplete;

    public void Play(string msg, Vector2 screenPos, float duration,
                     Color color, int fontSize, System.Action onComplete)
    {
        _onComplete = onComplete;
        _text.text     = msg;
        _text.color    = color;
        _text.fontSize = fontSize;
        _rt.position   = screenPos;
        gameObject.SetActive(true);
        StartCoroutine(Animate(duration));
    }

    private IEnumerator Animate(float duration)
    {
        float elapsed  = 0f;
        Vector3 origin = _rt.localPosition;
        Color   startColor = _text.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / duration;

            // 위로 떠오름
            _rt.localPosition = origin + Vector3.up * (t * 60f);

            // 크기: 처음에 커졌다 줄어듦
            float scale = t < 0.2f ? Mathf.Lerp(1.3f, 1f, t / 0.2f) : 1f;
            _rt.localScale = Vector3.one * scale;

            // 후반부 페이드아웃
            float alpha = t > 0.6f ? Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f) : 1f;
            _text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            yield return null;
        }

        gameObject.SetActive(false);
        _rt.localPosition = origin;
        _onComplete?.Invoke();
    }
}
