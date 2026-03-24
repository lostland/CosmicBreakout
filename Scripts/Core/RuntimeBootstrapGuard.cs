using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;

/// <summary>
/// 씬/매니저 세팅이 누락된 상태로 실행했을 때
/// "아무것도 안 보이는" 상황을 방지하기 위한 런타임 가드.
/// - Bootstrap 씬에서 코어 매니저를 보장하고 MainMenu 로딩 시도
/// - 비어있는 씬이면 OnGUI 디버그 오버레이 표시
/// </summary>
public class RuntimeBootstrapGuard : MonoBehaviour
{
    private static RuntimeBootstrapGuard _instance;
    private bool _showOverlay;
    private string _diagnostic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_instance != null) return;

        GameObject go = new GameObject("RuntimeBootstrapGuard");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<RuntimeBootstrapGuard>();
        SceneManager.sceneLoaded += _instance.OnSceneLoaded;
        _instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        // Bootstrap 씬이 비어있어도 진입할 수 있도록 코어 매니저 보장
        if (scene.name == "Bootstrap")
        {
            EnsureCoreManagers();
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
                SceneManager.LoadScene("MainMenu");
        }

        _showOverlay = IsLikelyEmptyScene(scene);
        _diagnostic = BuildDiagnostic(scene);
    }

    private static void EnsureCoreManagers()
    {
        EnsureSingletonObject<GameManager>("GameManager");
        EnsureSingletonObject<SaveManager>("SaveManager");
        EnsureSingletonObject<AudioManager>("AudioManager");
    }

    private static void EnsureSingletonObject<T>(string objectName) where T : Component
    {
        if (FindFirstObjectByType<T>() != null) return;
        GameObject go = new GameObject(objectName);
        DontDestroyOnLoad(go);
        go.AddComponent<T>();
    }

    private bool IsLikelyEmptyScene(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        int meaningful = 0;

        foreach (var root in roots)
        {
            if (root == null || !root.activeInHierarchy) continue;
            if (root.name == "RuntimeBootstrapGuard") continue;
            if (root.GetComponentInChildren<Camera>(true) != null && root.transform.childCount == 0 && root.GetComponents<Component>().Length <= 4)
                continue;

            meaningful++;
        }

        // 카메라만 있거나 사실상 빈 씬이면 true
        return meaningful == 0;
    }

    private string BuildDiagnostic(Scene scene)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"현재 씬: {scene.name}");
        sb.AppendLine($"루트 오브젝트 수: {scene.rootCount}");
        sb.AppendLine($"GameManager 존재: {(FindFirstObjectByType<GameManager>() != null ? "예" : "아니오")}");
        sb.AppendLine($"SaveManager 존재: {(FindFirstObjectByType<SaveManager>() != null ? "예" : "아니오")}");
        sb.AppendLine($"AudioManager 존재: {(FindFirstObjectByType<AudioManager>() != null ? "예" : "아니오")}");
        sb.AppendLine();
        sb.AppendLine("체크 권장:");
        sb.AppendLine("1) File > Build Settings > Scenes In Build 에 Bootstrap/MainMenu/StageSelect/GameScene 추가");
        sb.AppendLine("2) 시작 씬을 Bootstrap으로 열고 Play");
        sb.AppendLine("3) MainMenu/GameScene에 Canvas, EventSystem, 필수 매니저/컨트롤러 배치 여부 확인");
        return sb.ToString();
    }

    private void OnGUI()
    {
        if (!_showOverlay) return;

        const int width = 860;
        const int height = 420;
        Rect box = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);

        GUI.Box(box, "CosmicBreakout 실행 가이드");

        GUILayout.BeginArea(new Rect(box.x + 20, box.y + 40, box.width - 40, box.height - 60));
        GUILayout.Label("현재 씬이 사실상 비어 있어서 화면에 아무것도 안 보이는 상태입니다.");
        GUILayout.Space(8);
        GUILayout.TextArea(_diagnostic, GUILayout.Height(240));
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("MainMenu 씬 열기", GUILayout.Height(32)))
        {
            if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
            else Debug.LogWarning("MainMenu 씬이 Build Settings에 없습니다.");
        }

        if (GUILayout.Button("GameScene 씬 열기", GUILayout.Height(32)))
        {
            if (Application.CanStreamedLevelBeLoaded("GameScene")) SceneManager.LoadScene("GameScene");
            else Debug.LogWarning("GameScene 씬이 Build Settings에 없습니다.");
        }

        if (GUILayout.Button("로그로 체크리스트 출력", GUILayout.Height(32)))
        {
            Debug.Log(_diagnostic);
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
