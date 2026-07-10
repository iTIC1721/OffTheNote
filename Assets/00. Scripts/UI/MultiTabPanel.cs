using System;
using UnityEngine;
using UnityEngine.UI;

public class MultiTabPanel : MonoBehaviour
{
    [Serializable]
    public class TabEntry
    {
        public Button tabButton;
        public GameObject panel;
    }

    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Button exitButton;
    [SerializeField] private TabEntry[] tabs;

    private int currentIndex = 0;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (exitButton != null) exitButton.onClick.AddListener(DisablePanel);
        
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i] != null && tabs[i].tabButton != null) tabs[i].tabButton.onClick.AddListener(() => MoveTab(index));
            if (tabs[i] != null && tabs[i].panel != null) tabs[i].panel.SetActive(false);
        }

        mainPanel.SetActive(false);
    }

    public void MoveTab(int index)
    {
        Debug.Log($"Current Index: {currentIndex}, Dest Index: {index}");
        if (index >= tabs.Length) return;

        SetCurrentTab(tabs[currentIndex], tabs[index]);
        currentIndex = index;
    }

    protected virtual void SetCurrentTab(TabEntry prev, TabEntry now)
    {
        if (prev != null && prev.panel != null) prev.panel.SetActive(false);
        if (now != null && now.panel != null) now.panel.SetActive(true);
    }

    public virtual void EnablePanel()
    {
        if (mainPanel != null) mainPanel.SetActive(true);

        MoveTab(currentIndex);
    }

    public virtual void DisablePanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
    }
}
