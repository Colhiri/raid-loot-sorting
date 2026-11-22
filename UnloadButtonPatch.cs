using System.Linq;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RaidLootSorting;

[HarmonyPatch(typeof(SimpleStashPanel), nameof(SimpleStashPanel.Show))]
internal static class UnloadButtonPatch
{
    private const string ButtonName = "RaidLootSortingButton";

    private static void Postfix(CompoundItem item, bool inRaid, ItemsPanel.EItemsTab tab, ToggleEFT ____sortingTableTab)
    {
        if (____sortingTableTab == null || ____sortingTableTab.transform is not RectTransform tableButton)
        {
            return;
        }

        var toolbar = tableButton.parent;
        var button = toolbar.Find(ButtonName)?.GetComponent<Button>();
        if (inRaid || tab != ItemsPanel.EItemsTab.Gear || !____sortingTableTab.gameObject.activeSelf
            || !LootSorting.TryGetStash(ItemUiContext.Instance, out var player, out var stash) || item != stash)
        {
            button?.gameObject.SetActive(false);
            return;
        }

        button ??= Create(toolbar, ____sortingTableTab.GetComponentInChildren<TextMeshProUGUI>(true));
        button.GetComponent<UnloadButtonPosition>().Bind(tableButton);
        string profileId = player._clientProfile.Id;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LootSorting.Unload(profileId));
        button.gameObject.SetActive(true);
    }

    private static Button Create(Transform parent, TextMeshProUGUI? referenceLabel)
    {
        var control = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement), typeof(UnloadButtonPosition));
        var rect = (RectTransform)control.transform;
        rect.SetParent(parent, false);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(144f, 20f);
        control.GetComponent<LayoutElement>().ignoreLayout = true;

        var background = control.GetComponent<Image>();
        background.color = new Color32(39, 48, 53, 255);

        var button = control.GetComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.colorMultiplier = 1.25f;
        colors.normalColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(rect, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(4f, 0f);
        label.rectTransform.offsetMax = new Vector2(-4f, 0f);
        label.font = referenceLabel?.font ?? TMP_Settings.defaultFontAsset;
        label.fontSize = 13f;
        label.color = new Color32(209, 206, 195, 255);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = "ВЫГРУЗИТЬ ВЕСЬ ЛУТ";
        return button;
    }
}

internal sealed class UnloadButtonPosition : MonoBehaviour
{
    private RectTransform? sortingTable;
    private RectTransform? reference;
    private readonly Vector3[] corners = new Vector3[4];

    internal void Bind(RectTransform tableButton)
    {
        sortingTable = tableButton;
        reference = null;
    }

    private void LateUpdate()
    {
        if (sortingTable == null || !sortingTable.gameObject.activeInHierarchy)
        {
            return;
        }

        // The stash header is laid out after SimpleStashPanel.Show returns.
        if (reference == null)
        {
            reference = sortingTable.GetComponentsInChildren<Image>()
                .FirstOrDefault(image => image.rectTransform.rect.width is >= 90f and <= 140f
                    && image.rectTransform.rect.height is >= 15f and <= 25f)?.rectTransform
                ?? sortingTable.GetComponentInChildren<TextMeshProUGUI>()?.rectTransform
                ?? sortingTable;
        }

        reference.GetWorldCorners(corners);
        var toolbar = transform.parent;
        var rightCenter = toolbar.InverseTransformPoint((corners[2] + corners[3]) * 0.5f);
        transform.localPosition = rightCenter + new Vector3(5f, 0f, 0f);
    }
}
