using UnityEngine;

/// <summary>
/// 카메라가 플레이어를 부드럽게 따라가는 스크립트
/// Main Camera 오브젝트에 붙임
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;          // 플레이어 Transform
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0, 1, -10);

    [Header("Bounds (선택사항 — 0이면 비활성)")]
    [SerializeField] private float minX = -50f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float minY = -50f;
    [SerializeField] private float maxY = 50f;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = target.position + offset;

        // 선택적 경계 클램핑
        desired.x = Mathf.Clamp(desired.x, minX, maxX);
        desired.y = Mathf.Clamp(desired.y, minY, maxY);

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}