using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

/// <summary>
/// 씬/프리팹 세팅이 비어 있는 샘플 프로젝트에서도 최소 플레이 가능 상태를 자동 구성한다.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class SampleSceneBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateBootstrap()
    {
        if (FindObjectOfType<SampleSceneBootstrap>() != null) return;
        if (SceneManager.GetActiveScene().name != "SampleScene") return;

        var go = new GameObject("SampleSceneBootstrap");
        go.AddComponent<SampleSceneBootstrap>();
    }

    void Start()
    {
        EnsureCoreGameplay();
    }

    private void EnsureCoreGameplay()
    {
        EnsureSingleton<GameManager>("GameManager");
        EnsureSingleton<SaveManager>("SaveManager");

        if (FindObjectOfType<GameSceneController>() != null) return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            var camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        var gameplayRoot = new GameObject("RuntimeGameplayRoot");
        var layoutMgr = gameplayRoot.AddComponent<BrickLayoutManager>();
        var ballMgr = gameplayRoot.AddComponent<BallManager>();
        var itemMgr = gameplayRoot.AddComponent<ItemManager>();
        var bgScroller = gameplayRoot.AddComponent<BackgroundScroller>();
        var sceneController = gameplayRoot.AddComponent<GameSceneController>();

        var paddle = CreatePaddle();
        var ballPrefab = CreateBallPrefab();
        var brickPrefab = CreateBrickPrefab();
        CreateBounds();

        SetPrivate(layoutMgr, "_brickPrefab", brickPrefab);
        SetPrivate(layoutMgr, "_bossCorePrefab", brickPrefab);
        SetPrivate(ballMgr, "_ballPrefab", ballPrefab);
        SetPrivate(ballMgr, "_paddle", paddle);

        SetPrivate(sceneController, "_layoutMgr", layoutMgr);
        SetPrivate(sceneController, "_paddle", paddle);
        SetPrivate(sceneController, "_ballMgr", ballMgr);
        SetPrivate(sceneController, "_itemMgr", itemMgr);
        SetPrivate(sceneController, "_bgScroller", bgScroller);
    }

    private T EnsureSingleton<T>(string name) where T : Component
    {
        var exist = FindObjectOfType<T>();
        if (exist != null) return exist;
        return new GameObject(name).AddComponent<T>();
    }

    private PaddleController CreatePaddle()
    {
        var go = new GameObject("Paddle");
        go.transform.position = new Vector3(0f, -4.2f, 0f);
        TrySetTag(go, "Paddle");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = new Color(0.25f, 0.85f, 1f);
        go.transform.localScale = new Vector3(2.4f, 0.4f, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        return go.AddComponent<PaddleController>();
    }

    private BallController CreateBallPrefab()
    {
        var go = new GameObject("RuntimeBallPrefab");
        go.SetActive(false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = Color.white;
        go.transform.localScale = Vector3.one * 0.35f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        go.AddComponent<CircleCollider2D>().radius = 0.5f;

        var tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.1f;
        tr.startWidth = 0.08f;
        tr.endWidth = 0.01f;
        tr.material = new Material(Shader.Find("Sprites/Default"));

        go.SetActive(true);
        return go.AddComponent<BallController>();
    }

    private BrickController CreateBrickPrefab()
    {
        var go = new GameObject("RuntimeBrickPrefab");
        TrySetTag(go, "Brick");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = new Color(0.9f, 0.9f, 1f);
        go.transform.localScale = new Vector3(1.1f, 0.5f, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        var brick = go.AddComponent<BrickController>();
        SetPrivate(brick, "_sprite", sr);
        return brick;
    }

    private void CreateBounds()
    {
        CreateWall("WallLeft", new Vector2(-8.4f, 0f), new Vector2(0.5f, 14f), "Wall");
        CreateWall("WallRight", new Vector2(8.4f, 0f), new Vector2(0.5f, 14f), "Wall");
        CreateWall("WallTop", new Vector2(0f, 6.2f), new Vector2(17f, 0.5f), "Ceiling");

        var death = CreateWall("DeathZone", new Vector2(0f, -6.3f), new Vector2(17f, 0.6f), "DeathZone");
        death.isTrigger = true;
    }

    private BoxCollider2D CreateWall(string name, Vector2 pos, Vector2 size, string tagName)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        TrySetTag(go, tagName);
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        return col;
    }

    private static Sprite _white;
    private Sprite CreateWhiteSprite()
    {
        if (_white != null) return _white;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _white;
    }

    private void SetPrivate(object target, string fieldName, object value)
    {
        if (target == null) return;
        var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f != null) f.SetValue(target, value);
    }

    private void TrySetTag(GameObject go, string tagName)
    {
        if (go == null) return;
        try { go.tag = tagName; }
        catch { }
    }
}
