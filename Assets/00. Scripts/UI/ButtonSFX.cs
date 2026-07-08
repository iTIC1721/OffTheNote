using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSFX : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private string clickSfxName = "click";
    [SerializeField] private float volumeScale = 0.5f;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (AudioManager.Instance != null && !clickSfxName.Equals(""))
        {
            AudioManager.Instance.PlaySFX(clickSfxName, volumeScale);
        }
    }
}
