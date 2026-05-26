using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class MapPieceBackground : MonoBehaviour
{
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.3f, 0.8f);
    [SerializeField] private int sortingOrder = -1;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        sr.color = backgroundColor;
        sr.drawMode = SpriteDrawMode.Sliced; // size 조정을 위해 필요
        sr.sprite = CreateWhiteSprite();
    }

    void Start()
    {
        FitToClickCollider();
    }

    public void FitToClickCollider()
    {
        // 루트의 클릭용 BoxCollider2D 기준
        BoxCollider2D clickCol = GetComponent<BoxCollider2D>();
        if (clickCol == null) return;

        // localScale 건드리지 않고 size만 조정
        sr.size = clickCol.size;
        transform.localPosition = clickCol.offset; // 콜라이더 offset 반영
    }

    Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f),
                             pixelsPerUnit: 1f);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = CreateWhiteSprite();
        sr.drawMode = SpriteDrawMode.Sliced;
        FitToClickCollider();
    }
#endif
}