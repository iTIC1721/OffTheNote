# Off The Note

「Off The Note」는 손그림 펜 노이즈 아웃라인과 스티키노트(포스트잇) 질감을 컨셉으로 한 Unity 2D 퍼즐 플랫포머입니다. 플레이어는 맵에 배치된 종이 조각(MapPiece)을 드래그해 이동시키거나, 고정된 핀을 축으로 회전시키거나, 축을 뒤집어(Flip) 발판의 배치와 방향을 재구성하며 스스로 목표 지점(Goal)까지의 경로를 만들어 나갑니다. 충돌·이동 로직을 Rigidbody 없이 직접 설계하고, 조각의 상태(Fixed/Movable/Pinned/Flippable)에 따라 서로 다른 조작·렌더링 규칙이 하나의 시스템으로 맞물리도록 구현했습니다.

**핵심 기술:** Rigidbody 없이 직접 구현한 커스텀 2D 물리 · Unity Editor 확장 기반 자체 스테이지 툴 · Perlin Noise 기반 절차적 손그림 아웃라인

- 개발 기간: 2026.05.26 ~ 2026.07.10 (약 6주)
- 개발 인원: 1인
- 빌드 대상: PC (Windows, Standalone)
- 코드 규모: C# 스크립트 38개 · 약 7,600줄 (자체 제작 Stage Editor 약 1,400줄 포함)
- 콘텐츠 규모: 플레이 가능 스테이지 52개
- 개발 방식: Git 버전 관리

## 기술 스택

| 구분 | 내용 |
|---|---|
| 엔진 / 렌더링 | Unity 6000.3.16f1 (Unity 6), Universal Render Pipeline 17.3.0 |
| 언어 | C# |
| 데이터 구조 | ScriptableObject(WorldData/WorldListData/AudioLibrary) + JSON(StreamingAssets, 스테이지 52개) |
| 개발 도구 | Visual Studio, 자체 제작 Stage Editor(Unity Editor 확장) |

## 핵심 구현

### 1. MapPiece 상태 관리 및 정렬 시스템
조각의 행동을 Movable(이동) / Pinned(회전) / Flippable(뒤집기) 세 개의 상호 배타적 bool 필드로 정의하고, 셋 다 꺼져 있을 때를 Fixed(고정)로 계산해 파생시킵니다. 인스펙터에서 필요한 상호작용만 켜는 방식으로 조각 하나하나의 행동을 조합할 수 있습니다.

```csharp
// MapPiece.cs
[SerializeField] private bool isMovable = true;
[SerializeField] private bool isPinned = false;
[SerializeField] private bool isFlippable = false;

public bool IsFixed => !isMovable && !isPinned && !isFlippable;
```

렌더링 정렬도 이 상태와 연동됩니다. Fixed 조각은 배경 그룹으로 분류되고, 같은 그룹 안에서는 면적(GetPlatformArea)이 작은 조각이 앞에 오도록 정렬해 큰 조각에 작은 조각이 가려지지 않게 했습니다.

```csharp
// MapPieceManager.cs
if (!piece.IsMovable && !piece.IsPinned && !piece.IsFlippable) fixedPieces.Add(piece);
fixedPieces.Sort((a, b) => b.GetPlatformArea().CompareTo(a.GetPlatformArea()));
```

### 2. 핀 회전 — 충돌 인지형 점진 회전
고정된 핀을 축으로 90도씩 회전하는 기믹입니다. 손을 뗄 때 한 번에 목표 각도로 스냅하는 대신, 시작 각도부터 목표 각도까지의 경로 전체를 일정 간격(stepDeg)으로 나눠 검사해 중간 경로가 벽에 막혀 있으면 스냅을 취소합니다. 목표 지점 하나만 검사할 때 발생하던 벽 클리핑 버그를 근본적으로 해결한 부분입니다.

```csharp
// MapPiece.cs
bool IsRotationPathBlocked(float fromAngle, float toAngle, float stepDeg = 5f)
{
    float delta = Mathf.DeltaAngle(fromAngle, toAngle);
    float sign = Mathf.Sign(delta);
    float travelled = 0f;

    while (Mathf.Abs(travelled) < Mathf.Abs(delta))
    {
        float step = sign * Mathf.Min(stepDeg, Mathf.Abs(delta) - Mathf.Abs(travelled));
        travelled += step;
        float angle = fromAngle + travelled;
        if (IsRotationBlocked(angle)) return true;
    }
    return false;
}
```

### 3. Flip(뒤집기) — 드래그 기반 오브젝트 뒤집기
X 또는 Y축을 기준으로 scale을 조정해 조각을 뒤집는 기믹입니다. 드래그 거리를 회전축까지의 거리(참조 거리)로 나눈 뒤 각도로 환산해, 축에서 멀리 잡고 돌릴수록 적은 이동으로도 반 바퀴가 도는 물리적으로 직관적인 조작감을 구현했습니다.

```csharp
// MapPiece.cs
float dragAmount = flipAxis == FlipAxis.X ? -mouseDelta.y : mouseDelta.x;

// 월드 단위 → 플립 각도 변환
float worldUnitsPerHalfFlip = flipDragReferenceDistance * Mathf.PI;
float deltaAngle = (dragAmount / worldUnitsPerHalfFlip) * 180f;
```

뒤집는 동안에는 조각의 Platform 콜라이더를 비활성화해 외부 플레이어는 자연스럽게 통과시키고, 조각 위에 타고 있던 플레이어는 부모-자식 관계를 유지한 채 입력만 정지시켜 위치가 함께 회전하도록 처리했습니다.

### 4. 플레이어 커스텀 물리 컨트롤러
Unity 내장 Physics2D(Rigidbody)를 그대로 쓰면 자식 오브젝트가 부모 오브젝트를 따라가지 않기에 "플레이어가 회전·플립하는 조각의 자식이 되어 함께 움직이면서도, 조각과는 별개로 정밀한 이동 판정을 유지해야 하는" 이 게임의 핵심 기믹을 다루기 까다로웠습니다. Rigidbody의 물리 스텝과 조각의 kinematic한 위치·회전 갱신이 같은 프레임에서 충돌하면 지터가 발생하기 쉬웠기 때문에, transform.position을 직접 제어하는 방식을 택해 조각의 이동·회전·Flip과 같은 타이밍에 플레이어 위치를 갱신할 수 있도록 했습니다. 일반 이동 충돌·정지 상태의 겹침 보정·외부에서 밀려오는 조각과의 겹침 보정을 각각 독립된 함수로 분리해, 책임이 섞이면 지터나 끼임이 발생한다는 점을 트러블슈팅을 통해 확인하고 경계를 명확히 유지했습니다.

```csharp
// PlayerController.cs
Vector2 ResolveCollision(Vector2 moveAmount)               // 이동 중 충돌 처리
void ResolveOverlap()                                       // 정지 상태에서의 겹침(끼임) 보정
public void ResolveExternalOverlap(MapPiece currentPiece)   // 외부에서 밀려오는 조각과의 겹침 보정
```

입력은 New Input System 패키지가 프로젝트에 포함되어 있지만, 실제 이동/점프 입력은 `Input.GetAxisRaw`/`Input.GetButtonDown` 기반의 레거시 Input Manager로 처리하고 있습니다. 개발 속도를 우선한 선택이었고, 다음 단계로 New Input System(InputAction) 기반으로 전환할 계획입니다.

### 5. 커스텀 스테이지 에디터
콘텐츠 제작 속도를 높이기 위해 Unity Editor 확장(EditorWindow)을 활용한 자체 스테이지 에디터(StageEditorWindow, 약 1,400줄)를 직접 제작했습니다. 좌측 속성 패널과 우측 뷰포트로 구성된 레이아웃에서 드래그·줌·팬으로 스테이지 전체를 편집하며, Ctrl+S(저장)·Ctrl+Z/Y(Undo/Redo)·Ctrl+C/V(복사·붙여넣기) 등 범용 에디터 단축키도 구현했습니다. Undo/Redo는 Command 패턴이 아니라, 변경 시점마다 stageData 전체를 JSON으로 직렬화해 스택에 쌓아두었다가 복원하는 스냅샷 방식으로 구현했습니다. 개별 커맨드 객체를 만드는 것보다 구현이 단순하고, 에디터 툴 특성상 데이터 규모가 크지 않아 메모리 비용도 감수할 만하다고 판단했습니다.

```csharp
// StageEditorWindow.cs
void TryCycleSelection(Vector2 wm)
{
    var hitPieces = new List<int>();
    for (int i = 0; i < stageData.mapPieces.Count; i++)
    {
        var piece = stageData.mapPieces[i];
        if (InRect(wm, piece.position.ToVector2(), piece.colliderSize.ToVector2()))
            hitPieces.Add(i);
    }
    if (hitPieces.Count <= 1) return;
    int posInHit = hitPieces.IndexOf(selectedPieceIndex);
    if (posInHit >= 0)
    {
        selectedPieceIndex = hitPieces[(posInHit + 1) % hitPieces.Count];
        selectedPlatformIndex = -1;
        Repaint();
    }
}
```

"겹친 맵 조각을 에디터 상에서 선택하기 어렵다"는 피드백이 있었기에, 겹친 조각이 많은 구간에서는 같은 위치를 드래그 없이 재클릭하면 위 TryCycleSelection()으로 겹친 조각들을 순서대로 순환 선택하도록 했고, 클릭 의도와 드래그 의도가 뒤섞이지 않도록 didDrag 플래그로 명확히 분리했습니다.

### 6. 손그림 아웃라인 라인 메시
게임의 시각적 정체성인 "손으로 그은 듯 조금씩 우글거리는" 펜 노이즈 외곽선을 구현하기 위해, 단순 스프라이트 아웃라인이 아닌 Perlin noise 기반의 절차적 라인 메시를 직접 제작했습니다(OutlineObject). 오브젝트의 bounds를 따라 윤곽선 정점을 배치하되, 각 정점에 Perlin noise 값을 수직 방향 오프셋으로 더해 규칙적이지 않은 손떨림 느낌을 만들었습니다.

```csharp
// OutlineObject.cs
float noiseVal = PerlinNoise1D(ratio * noiseFrequency + side * 10f + t) * 2f - 1f;
pos += perp * noiseVal * noiseAmount;
```

오브젝트의 lossyScale을 기준으로 스케일을 역보정(SyncOutlineScale)해, 부모 스케일이나 Flip 애니메이션과 무관하게 선 굵기가 항상 일정하게 유지되도록 했습니다. 이 라인 메시 기법은 MapPiece 외곽선뿐 아니라 플레이어 사망 이펙트(ExplodeEffect)가 생성하는 절차적 파편(둥근 사각형/X자/울퉁불퉁한 원)에도 동일하게 재사용해 이펙트 연출에서도 아트 스타일이 끊기지 않도록 했습니다. 현재는 사각형 bounds 기준으로만 외곽선을 그리기 때문에, 투명 영역이 큰 스프라이트의 실제 픽셀 윤곽을 따라가지는 못한다는 한계가 있어 다음 개선 과제로 남아 있습니다.

## 트러블슈팅

### 회전 플랫폼에서 내려온 뒤에도 남아있는 플레이어 회전값
핀으로 회전하는 조각 위에 서 있다가 내려오면, 플레이어가 조각을 따라 기울어진 회전 상태를 그대로 유지한 채 남아있는 문제가 있었습니다. 플레이어가 조각의 자식(child)으로 reparent되어 함께 움직이는 구조이기 때문에 부모의 world rotation이 그대로 자식에게 전파되었고, 분리되는 순간에도 그 프레임까지 누적된 회전 값이 남아 있었던 것이 원인이었습니다.

회전이 발생할 수 있는 모든 경로를 한 번에 덮기 위해 매 프레임 후단에서 강제로 정합성을 맞추는 방식을 택했습니다.

```csharp
// PlayerController.cs
void LateUpdate()
{
    // 부모(MapPiece) 회전에 영향받지 않도록 항상 월드 rotation 고정
    transform.rotation = Quaternion.identity;
}
```

이후 플레이어는 어떤 조각 위에서 내려오든 항상 올바른 직립 상태를 유지했고, "부모 회전과 무관하게 플레이어 표시 회전은 고정"이라는 동일한 원칙을 이후 Flip 기능을 추가할 때도 재사용할 수 있었습니다.

### 핀 회전 스냅 시 벽을 통과하는 문제
핀 회전을 빠르게 조작한 뒤 손을 떼 스냅될 때, 중간에 벽이 있는데도 조각이 벽을 뚫고 목표 각도까지 스냅되는 현상이 있었습니다. 충돌 판정 함수가 회전 경로의 시작·중간을 보지 않고 최종 목표 각도 지점 하나만 검사하고 있었던 것이 원인이었습니다.

목표 각도만 보는 대신 시작 각도부터 목표 각도까지를 고정 간격으로 나눠 전체 경로를 스텝별로 검사하는 IsRotationPathBlocked()를 새로 작성했고, 경로 중 어느 한 스텝이라도 막혀 있으면 스냅을 취소하고 이전에 유효했던 스냅각으로 폴백하도록 했습니다. "충돌 검사를 했는가"가 아니라 "충돌 검사를 경로 전체에 대해 했는가"가 핵심이었다는 점을 이 과정에서 확인했고, 이후 3° 단위의 점진 회전 로직에도 동일한 경로 검사 방식을 적용해 회전 중 충돌 감지의 일관성을 확보했습니다.

### 이펙트 코루틴이 파괴된 오브젝트를 참조하며 발생한 MissingReferenceException
플레이어 사망 이펙트(ExplodeEffect)를 만든 뒤, 짧은 시간에 연속으로 사망하는 상황에서 MissingReferenceException이 발생했습니다. 코루틴을 소유한 MonoBehaviour보다 코루틴 자체의 수명이 길어질 수 있다는 점이 원인이었고, 코루틴 핸들을 저장했다가 Destroy 전에 StopCoroutine을 호출하는 방식으로는 여러 이펙트 인스턴스가 겹치는 상황을 완전히 막지 못했습니다.

패치보다 구조를 바꾸는 방향으로 접근을 전환해, 코루틴 소유권을 ExplodeEffect 자신이 아니라 각 이펙트 루트에 붙는 전용 자식 컴포넌트로 이전했습니다.

```csharp
// ExplodeEffect.cs
private class EffectRunner : MonoBehaviour
{
    public void Run(IEnumerator routine) => StartCoroutine(routine);
}
```

`Destroy(root)` 한 번으로 자식에 있는 코루틴까지 자동으로 정리되어 명시적인 StopCoroutine 호출에 의존할 필요가 없어졌고, 부수적으로 여러 이펙트가 서로를 취소하지 않고 동시에 존재할 수 있게 되었습니다. '수명이 짧은 쪽이 수명이 긴 리소스를 정리하게 하지 말고, 소유권 자체를 수명이 맞는 쪽으로 옮긴다'는 원칙을 이후 이펙트/코루틴 설계 전반에 적용했습니다.

### 여러 조각 사이에 낀 플레이어(Squish) 처리
이동하는 조각들 사이에 플레이어가 깊게 끼이면 뚫고 나오지 못하거나 비정상적으로 튕겨 나가는 문제가 있었습니다. BoxCastAll, OverlapBoxAll, 콜라이더 shrink 조정 등 압착을 사전에 막는 방향으로 여러 차례 접근했지만, 방법마다 정상적인 좁은 틈 통과가 막히거나 반대로 감지가 누락되는 부작용이 있어 근본적으로 해결되지 않았습니다.

압착을 막으려는 시도 대신, 감지되면 즉시 인정하고 복구하는 방향으로 전환했습니다.

```csharp
// MapPieceManager.cs
ColliderDistance2D dist = Physics2D.Distance(playerCol, overlap);
// ...
if (totalOverlapDepth > 0.3f) player.Respawn();
```

겹쳐 있는 모든 Platform 콜라이더의 겹침 깊이를 합산해 임계값(0.3f)을 넘으면 별도의 회피 로직 없이 바로 리스폰시키도록 했고, 복잡한 사전 방지 로직을 모두 걷어내고도 하드 프리즈나 튕김 없이 자연스럽게 안정화되었습니다.

### Flip 애니메이션 중 아웃라인 메시가 찌그러지는 문제
MapPiece를 Flip으로 뒤집는 동안, 손그림 아웃라인을 그리는 OutlineObject의 라인 메시가 비율에 맞지 않게 찌그러지는 현상이 있었습니다. OutlineObject는 lossyScale 기준으로 스케일을 역보정해 항상 일정한 굵기의 선을 유지하는 구조였는데, Flip 애니메이션이 localScale을 실시간으로 변형시키는 과정과 이 역보정 로직이 서로 다른 타이밍에 개입하면서 중간 프레임에 왜곡이 발생했습니다.

기존 역보정 로직 자체를 건드리는 대신, Flip으로 인한 스케일 변화분만 별도 값으로 노출해 메시 좌표 계산 단계에 직접 곱해 넣는 방식으로 분리했습니다. 두 보정이 서로 간섭하지 않고 각자의 책임(원근 보정 vs Flip 보정)만 담당하도록 역할을 나눈 것이 핵심이었고, 이후 Flip 애니메이션 전체 구간에서 아웃라인 두께와 비율이 일정하게 유지되었습니다. 기존 함수를 확장하기보다 새 책임을 위한 필드/단계를 별도로 추가하는 쪽이 회귀를 줄인다는 것을 이 과정에서 확인했습니다.

### MapPieceManager.Instance가 초기화 시점에 아직 null인 문제
조각의 상태 색상을 갱신하는 RefreshColor()가 씬 시작 직후 종종 호출되지 않아, 일부 조각이 초기 색상을 반영하지 못한 채로 시작하는 문제가 있었습니다. Unity의 Awake 실행 순서는 오브젝트 간에 보장되지 않기 때문에, MapPiece의 Awake 체인이 MapPieceManager보다 먼저 실행되면 참조하는 싱글톤 Instance가 아직 null이었던 것이 원인이었습니다.

Script Execution Order로 실행 순서 자체를 강제하기보다(유지보수 부담이 크다고 판단), 호출 시점에 null 가드를 추가해 조용히 스킵하도록 하고, 모든 오브젝트의 Awake가 끝난 뒤 보장되는 Start()에서 동일한 초기화를 한 번 더 호출해 재확인하는 방식을 택했습니다. Unity에서 Awake 순서를 코드로 보장하려는 시도는 프로젝트가 커질수록 깨지기 쉬운 가정이라는 것을 배웠고, 순서에 의존하는 대신 '늦게 초기화돼도 안전한' 방어적 구조(null 가드 + Start 재확인)를 다른 싱글톤 의존 초기화 코드에도 기본값으로 적용했습니다.

## 회고

- **가장 많이 배운 것:** 증상이 나타난 지점을 바로 패치하기 전에 전체 코드 흐름(플레이어가 Rigidbody 없이 transform으로 움직인다는 점, 특정 조각 위에서는 플레이어가 그 조각의 자식이 된다는 점 등)을 먼저 재확인하는 습관. '더 정교한 해법'이 항상 더 나은 해법은 아니라는 점도 반복적으로 확인했습니다 — 멀티레이 시뮬레이션보다 리스폰이, 실행 순서 강제보다 null 가드가 더 안정적이고 유지보수하기 쉬웠습니다.
- **다음에 다르게 할 것:** MapPiece.cs가 드래그/핀 회전/플립 로직을 모두 포함하며 약 920줄로 커진 상태입니다. MapPieceDrag/MapPiecePinRotation/MapPieceFlip/MapPieceVisual로 책임을 분리하는 리팩터링을 다음 단계로 진행할 계획이며, 이 구조 위에 접기(folding)·찢기(tearing) 등 새 기믹을 얹을 수 있도록 미리 설계하고 있습니다.
- **개선 계획:** MapPiece 책임 분리 리팩터링, 레거시 Input Manager에서 New Input System(InputAction)으로 전환, 조각 수가 많은 스테이지에서의 프로파일링(Draw Call/GC)과 오브젝트 풀링 적용, 투명 스프라이트를 위한 실제 픽셀 윤곽 기반 아웃라인 처리.
