using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI worldLabel;
    [SerializeField] private TextMeshProUGUI worldName;
    [SerializeField] private TextMeshProUGUI worldDesc;
    [SerializeField] private TextMeshProUGUI worldShortDesc;
    [SerializeField] private TextMeshProUGUI worldProgress;
    [SerializeField] private TextMeshProUGUI worldProgressBar;
    [SerializeField] private Image worldIcon;
    [SerializeField] private GameObject lockOverlay;

    public void Setup(WorldData world)
    {
        int total = world.stageFiles.Count;
        int unlocked = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetUnlockedCount(world.worldId)
            : 0;
        int cleared = Mathf.Max(0, unlocked - 1);

        bool playable = unlocked > 0;

        if (worldLabel) worldLabel.text = $"WORLD {world.worldId.Split('_')[1].PadLeft(2, '0')}";
        if (worldName) worldName.text = world.displayName;
        if (worldDesc) worldDesc.text = world.displayDesc;
        if (worldShortDesc) worldShortDesc.text = world.displayShortDesc;
        if (worldProgress) worldProgress.text = $"{cleared} / {total}";
        if (worldProgressBar)
        {
            string progress = "";
            int i = 0;
            for (; i < cleared; i++) progress += "бс";
            for (; i < total; i++) progress += "бр";
            worldProgressBar.text = progress;
        }
        if (worldIcon) worldIcon.sprite = world.displayIcon;
        if (lockOverlay) lockOverlay.SetActive(!playable);
    }
}