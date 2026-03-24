using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// GameScene가 비어 있을 때 즉시 플레이 가능한 최소 브레이크아웃 월드를 생성한다.
/// </summary>
public class QuickPlayableBootstrap : MonoBehaviour
{
    private static Sprite _squareSprite;

    public static bool TryBuild(Scene scene)
    {
        if (scene.name != "GameScene") return false;
        if (FindFirstObjectByType<QuickRuntimeManager>() != null) return false;

        var root = new GameObject("QuickRuntimeGame");
        var mgr = root.AddComponent<QuickRuntimeManager>();

        EnsureCamera();
        CreateBounds(root.transform);

        var paddle = CreatePaddle(root.transform);
        var ball = CreateBall(root.transform, paddle.transform, mgr);
        mgr.BindBall(ball);

        int brickCount = CreateBricks(root.transform, mgr);
        mgr.SetInitialState(brickCount);

        return true;
    }

    private static void EnsureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            camObj.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(0, 0, -10);
            cam.orthographic = true;
            cam.orthographicSize = 5;
        }
    }

    private static GameObject CreatePaddle(Transform parent)
    {
        var go = new GameObject("QuickPaddle");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(0f, -4.2f, 0f);
        go.transform.localScale = new Vector3(2.2f, 0.35f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquareSprite();
        sr.color = new Color(0.9f, 0.95f, 1f, 1f);

        go.AddComponent<BoxCollider2D>();
        go.AddComponent<QuickPaddleController>();
        return go;
    }

    private static QuickBallController CreateBall(Transform parent, Transform paddle, QuickRuntimeManager mgr)
    {
        var go = new GameObject("QuickBall");
        go.transform.SetParent(parent);
        go.transform.position = paddle.position + Vector3.up * 0.45f;
        go.transform.localScale = Vector3.one * 0.28f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquareSprite();
        sr.color = Color.white;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        go.AddComponent<CircleCollider2D>();

        var ball = go.AddComponent<QuickBallController>();
        ball.Initialize(paddle, mgr);
        return ball;
    }

    private static int CreateBricks(Transform parent, QuickRuntimeManager mgr)
    {
        int rows = 6;
        int cols = 10;
        float startX = -5.4f;
        float startY = 3.6f;
        float stepX = 1.2f;
        float stepY = 0.55f;

        int count = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var b = new GameObject($"QuickBrick_{r}_{c}");
                b.transform.SetParent(parent);
                b.transform.position = new Vector3(startX + c * stepX, startY - r * stepY, 0f);
                b.transform.localScale = new Vector3(1.05f, 0.4f, 1f);

                var sr = b.AddComponent<SpriteRenderer>();
                sr.sprite = GetSquareSprite();
                sr.color = Color.HSVToRGB((r * 0.1f + c * 0.03f) % 1f, 0.65f, 1f);

                b.AddComponent<BoxCollider2D>();
                var brick = b.AddComponent<QuickBrick>();
                brick.Setup(mgr);
                count++;
            }
        }

        return count;
    }

    private static void CreateBounds(Transform parent)
    {
        CreateWall("WallLeft", new Vector2(-8.4f, 0f), new Vector2(0.5f, 12f), parent);
        CreateWall("WallRight", new Vector2(8.4f, 0f), new Vector2(0.5f, 12f), parent);
        CreateWall("WallTop", new Vector2(0f, 5.2f), new Vector2(18f, 0.5f), parent);

        var dz = new GameObject("DeathZone");
        dz.transform.SetParent(parent);
        dz.transform.position = new Vector3(0f, -5.45f, 0f);
        var bc = dz.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(18f, 0.5f);
        bc.isTrigger = true;
        dz.tag = "Untagged";
    }

    private static void CreateWall(string name, Vector2 pos, Vector2 size, Transform parent)
    {
        var w = new GameObject(name);
        w.transform.SetParent(parent);
        w.transform.position = pos;
        var bc = w.AddComponent<BoxCollider2D>();
        bc.size = size;
    }

    private static Sprite GetSquareSprite()
    {
        if (_squareSprite != null) return _squareSprite;

        var tex = new Texture2D(2, 2);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        _squareSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return _squareSprite;
    }
}

public class QuickRuntimeManager : MonoBehaviour
{
    private static readonly string[] MainMenuCandidates =
    {
        "MainMenu", "Scenes/MainMenu", "Assets/Scenes/MainMenu.unity"
    };

    private QuickBallController _ball;
    private int _lives = 3;
    private int _bricksRemaining;
    private bool _isClear;
    private bool _isGameOver;

    public void BindBall(QuickBallController ball) => _ball = ball;

    public void SetInitialState(int brickCount)
    {
        _bricksRemaining = brickCount;
        _isClear = false;
        _isGameOver = false;
    }

    public void OnBrickDestroyed(GameObject brick)
    {
        if (brick == null) return;
        Destroy(brick);
        _bricksRemaining = Mathf.Max(0, _bricksRemaining - 1);
        if (_bricksRemaining == 0) _isClear = true;
    }

    public void OnBallLost()
    {
        _lives--;
        if (_lives <= 0)
        {
            _lives = 0;
            _isGameOver = true;
            return;
        }

        _ball?.ResetToPaddle();
    }

    private void Update()
    {
        if ((_isClear || _isGameOver) && Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(12, 10, 400, 24), $"[Quick Play] Lives: {_lives}  Bricks: {_bricksRemaining}");
        GUI.Label(new Rect(12, 34, 600, 24), "마우스로 패들 이동 / 클릭(또는 Space)으로 발사");

        if (_isClear)
            GUI.Label(new Rect(12, 58, 400, 24), "클리어! (R: 다시 시작)");
        else if (_isGameOver)
            GUI.Label(new Rect(12, 58, 400, 24), "게임 오버! (R: 다시 시작)");

        if (GUI.Button(new Rect(12, 86, 140, 30), "MainMenu 이동"))
            TryLoadScene(MainMenuCandidates);
    }

    private static void TryLoadScene(IEnumerable<string> candidates)
    {
        foreach (string c in candidates)
        {
            if (!Application.CanStreamedLevelBeLoaded(c)) continue;
            SceneManager.LoadScene(c);
            return;
        }
    }

    public bool IsBlocked => _isClear || _isGameOver;
}

public class QuickPaddleController : MonoBehaviour
{
    private float _left = -7f;
    private float _right = 7f;

    private void Update()
    {
        float x = Camera.main.ScreenToWorldPoint(Input.mousePosition).x;
        x = Mathf.Clamp(x, _left, _right);
        transform.position = new Vector3(x, transform.position.y, 0f);
    }
}

public class QuickBallController : MonoBehaviour
{
    private Transform _paddle;
    private QuickRuntimeManager _manager;
    private Rigidbody2D _rb;
    private bool _launched;
    private float _speed = 7.8f;

    public void Initialize(Transform paddle, QuickRuntimeManager manager)
    {
        _paddle = paddle;
        _manager = manager;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (_manager != null && _manager.IsBlocked) return;

        if (!_launched)
        {
            if (_paddle != null)
                transform.position = _paddle.position + Vector3.up * 0.45f;

            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                _launched = true;
                _rb.linearVelocity = new Vector2(Random.Range(-0.35f, 0.35f), 1f).normalized * _speed;
            }
        }
        else if (_rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * _speed;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.GetComponent<QuickPaddleController>() != null)
        {
            float hit = transform.position.x - col.transform.position.x;
            float nx = Mathf.Clamp(hit / 1.1f, -0.9f, 0.9f);
            Vector2 dir = new Vector2(nx, Mathf.Sqrt(Mathf.Max(0.1f, 1f - nx * nx))).normalized;
            _rb.linearVelocity = dir * _speed;
            return;
        }

        QuickBrick brick = col.gameObject.GetComponent<QuickBrick>();
        if (brick != null) brick.Break();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.name == "DeathZone")
            _manager?.OnBallLost();
    }

    public void ResetToPaddle()
    {
        _launched = false;
        _rb.linearVelocity = Vector2.zero;
        if (_paddle != null)
            transform.position = _paddle.position + Vector3.up * 0.45f;
    }
}

public class QuickBrick : MonoBehaviour
{
    private QuickRuntimeManager _manager;

    public void Setup(QuickRuntimeManager manager)
    {
        _manager = manager;
    }

    public void Break()
    {
        _manager?.OnBrickDestroyed(gameObject);
    }
}
