// URP(Universal Render Pipeline)가 설치된 경우 이 파일을 삭제하고
// using UnityEngine.Rendering.Universal; 를 대신 사용하세요.
//
// URP가 없는 표준 파이프라인 환경에서 컴파일 오류 없이 사용하기 위한 스텁입니다.

#if !UNITY_URP_INSTALLED
namespace UnityEngine
{
    /// <summary>
    /// URP Light2D 컴포넌트 스텁.
    /// URP 패키지가 없는 환경에서 StageThemeApplicator.cs의 컴파일을 허용한다.
    /// URP 설치 후 이 파일을 제거하고 실제 Light2D를 사용하세요.
    /// </summary>
    public class Light2D : MonoBehaviour
    {
        public Color color;
        public float intensity;
        public float pointLightOuterRadius;
    }
}
#endif
