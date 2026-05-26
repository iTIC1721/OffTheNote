using UnityEngine;

/// <summary>
/// 2단 점프 아이템.
/// 플레이어가 체공 중 획득하면 2단 점프 1회를 부여.
/// 획득 후 respawnTime 초 뒤에 다시 나타남.
/// </summary>
public class DoubleJumpItem : MonoBehaviour
{
    [SerializeField] private float respawnTime = 2f;

    [Header("Float Effect")]
    [SerializeField] private float floatAmplitude = 0.1f;  // 흔들림 크기
    [SerializeField] private float floatSpeed = 2f;         // 흔들림 속도

    private Vector3 initialLocalPosition;

    private bool isActive = true;
    private float respawnTimer = 0f;
    private SpriteRenderer sr;
    private Collider2D col;

    private PlayerController player;


    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        initialLocalPosition = transform.localPosition;
    }

    void Update()
    {
        if (!isActive)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
                SetActive(true);
            return;
        }

        // 위아래 흔들림
        float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.localPosition = initialLocalPosition + new Vector3(0, offsetY, 0);

        // 플레이어 콜라이더와 겹치는지 체크
        if (player != null && col.OverlapPoint(player.transform.position))
        {
            player.GiveDoubleJump();
            SetActive(false);
            respawnTimer = respawnTime;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        player.GiveDoubleJump();
        SetActive(false);
        respawnTimer = respawnTime;
    }

    void SetActive(bool active)
    {
        isActive = active;
        sr.enabled = active;
        col.enabled = active;

        // 비활성화 시 원래 위치로 복귀
        if (!active)
            transform.localPosition = initialLocalPosition;
    }
}