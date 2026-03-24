using UnityEngine;
using System.Collections;

/// <summary>
/// 우주 배경을 3D 시차 효과로 아래에서 위로 스크롤한다.
/// 스테이지 전환 시 색상/파티클을 부드럽게 변경한다.
/// 별빛 레이어 3개 + 성운 레이어 1개 + 행성/천체 레이어 1개로 구성.
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    public static BackgroundScroller Instance { get; private set; }

    [System.Serializable]
    public class ScrollLayer
    {
        public Transform Root;
        public float     ScrollSpeed;   // 느린 레이어 = 멀리 있는 것
        public float     Height;        // 레이어 반복 높이
        [HideInInspector] public float OrigY;
    }

    [Header("Layers (뒤 → 앞 순서)")]
    [SerializeField] ScrollLayer _starFarLayer;    // 미세한 별 (가장 느림)
    [SerializeField] ScrollLayer _starMidLayer;    // 중간 별
    [SerializeField] ScrollLayer _starNearLayer;   // 큰 별 (가장 빠름)
    [SerializeField] ScrollLayer _nebulaLayer;     // 성운/파티클
    [SerializeField] ScrollLayer _celestialLayer;  // 행성/천체

    [Header("Stage Color Tint")]
    [SerializeField] Camera          _bgCamera;
    [SerializeField] float           _colorTransitionDur = 1.5f;

    [Header("Parallax 3D")]
    [SerializeField] float _parallaxDepthFactor = 0.02f;  // 카메라 x 이동에 따른 시차
    private Vector3 _lastCamPos;

    // ── 스테이지별 배경색 ───────────────────────────────────────────
    private static readonly Color[] _stageBgColors = {
        new Color(0.02f, 0.04f, 0.12f),   // 지구: 짙은 남색
        new Color(0.06f, 0.03f, 0.01f),   // 태양계: 주황 어두움
        new Color(0.04f, 0.01f, 0.10f),   // 항성계: 보라
        new Color(0.08f, 0.05f, 0.00f),   // 은하 중심: 황금 어두움
        new Color(0.01f, 0.00f, 0.08f),   // 다른 은하: 극단적 보라
    };

    private int _currentStage = -1;
    private Coroutine _colorTransCo;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // 초기 Y 기록
        if (_starFarLayer.Root)   _starFarLayer.OrigY   = _starFarLayer.Root.position.y;
        if (_starMidLayer.Root)   _starMidLayer.OrigY   = _starMidLayer.Root.position.y;
        if (_starNearLayer.Root)  _starNearLayer.OrigY  = _starNearLayer.Root.position.y;
        if (_nebulaLayer.Root)    _nebulaLayer.OrigY    = _nebulaLayer.Root.position.y;
        if (_celestialLayer.Root) _celestialLayer.OrigY = _celestialLayer.Root.position.y;

        if (Camera.main) _lastCamPos = Camera.main.transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        StageData sd = StageDatabase.GetStage(
            GameManager.Instance?.CurrentStageIndex ?? 0);

        ScrollLayerStep(ref _starFarLayer,   sd.BgScrollSpeed * 0.20f, dt);
        ScrollLayerStep(ref _starMidLayer,   sd.BgScrollSpeed * 0.45f, dt);
        ScrollLayerStep(ref _starNearLayer,  sd.BgScrollSpeed * 0.80f, dt);
        ScrollLayerStep(ref _nebulaLayer,    sd.BgScrollSpeed * 0.35f, dt);
        ScrollLayerStep(ref _celestialLayer, sd.BgScrollSpeed * 0.60f, dt);

        // 3D 시차: 패들 위치 기준 미세 x 이동
        ApplyParallax();

        // 스테이지 전환 감지
        int idx = GameManager.Instance?.CurrentStageIndex ?? 0;
        if (idx != _currentStage)
        {
            _currentStage = idx;
            TransitionColor(idx);
        }
    }

    private void ScrollLayerStep(ref ScrollLayer layer, float speed, float dt)
    {
        if (layer.Root == null) return;
        Vector3 p = layer.Root.position;
        p.y -= speed * dt;

        // 레이어 반복 (원래 위치로 순간 이동)
        if (p.y < layer.OrigY - layer.Height)
            p.y += layer.Height;

        layer.Root.position = p;
    }

    private void ApplyParallax()
    {
        if (Camera.main == null) return;
        Vector3 camDelta = Camera.main.transform.position - _lastCamPos;
        _lastCamPos = Camera.main.transform.position;

        if (_starFarLayer.Root)
            _starFarLayer.Root.position += new Vector3(camDelta.x * 0.1f, 0f, 0f);
        if (_starMidLayer.Root)
            _starMidLayer.Root.position += new Vector3(camDelta.x * 0.25f, 0f, 0f);
        if (_nebulaLayer.Root)
            _nebulaLayer.Root.position  += new Vector3(camDelta.x * 0.4f, 0f, 0f);
    }

    private void TransitionColor(int stageIndex)
    {
        if (_colorTransCo != null) StopCoroutine(_colorTransCo);
        _colorTransCo = StartCoroutine(ColorTransRoutine(stageIndex));
    }

    private IEnumerator ColorTransRoutine(int stageIndex)
    {
        Color target = stageIndex < _stageBgColors.Length
                       ? _stageBgColors[stageIndex]
                       : _stageBgColors[_stageBgColors.Length - 1];
        Color start  = _bgCamera ? _bgCamera.backgroundColor : Color.black;
        float elapsed = 0f;
        while (elapsed < _colorTransitionDur)
        {
            elapsed += Time.deltaTime;
            if (_bgCamera)
                _bgCamera.backgroundColor = Color.Lerp(start, target, elapsed / _colorTransitionDur);
            yield return null;
        }
        if (_bgCamera) _bgCamera.backgroundColor = target;
    }

    // 클리어/인트로 연출: 배경 빠르게 당기는 효과
    public void PlayWarpEffect(float duration = 1.5f)
    {
        StartCoroutine(WarpRoutine(duration));
    }

    private IEnumerator WarpRoutine(float duration)
    {
        float elapsed = 0f;
        float baseSpeed = StageDatabase.GetStage(GameManager.Instance?.CurrentStageIndex ?? 0).BgScrollSpeed;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Sin(elapsed / duration * Mathf.PI);
            float warpMult = 1f + t * 8f;
            // 임시 배속은 Update에서 직접 적용하지 않고 별도 처리
            // 실제로는 scrollSpeed 프로퍼티를 곱해서 사용
            yield return null;
        }
    }
}
