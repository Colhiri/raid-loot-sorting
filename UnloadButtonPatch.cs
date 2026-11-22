using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RaidLootSorting;

[HarmonyPatch(typeof(SimpleStashPanel), nameof(SimpleStashPanel.Show))]
internal static class UnloadButtonPatch
{
    private const string ButtonName = "RaidLootSortingButton";

    private static void Postfix(CompoundItem item, bool inRaid, ItemsPanel.EItemsTab tab,
        ToggleButton ____pinModeButton, ToggleButton ____lockModeButton)
    {
        if (____pinModeButton == null || ____pinModeButton.transform is not RectTransform pin)
        {
            return;
        }

        var button = pin.parent.Find(ButtonName)?.GetComponent<Button>();
        var context = ItemUiContext.Instance;
        if (inRaid || tab != ItemsPanel.EItemsTab.Gear || !pin.gameObject.activeSelf
            || !LootSorting.TryGetStash(context, out var player, out var stash) || item != stash)
        {
            button?.gameObject.SetActive(false);
            return;
        }

        button ??= Create(pin.parent);
        button.GetComponent<UnloadButtonPosition>().Bind(pin, ____lockModeButton?.transform as RectTransform);
        button.GetComponent<HoverTooltipArea>().Init(context.Tooltip,
            ModText.Translate(
                "Unload loot\nWith a PMC selection, unloads the selected items.\nWithout a raid snapshot, unloads all removable gear.",
                "Выгрузить лут\nС выделением на ЧВК выгружает выбранные предметы.\nБез снимка рейда выгружает всю съёмную экипировку."),
            rawText: true);
        string profileId = player._clientProfile.Id;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LootSorting.Unload(profileId));
        button.gameObject.SetActive(true);
    }

    private static Button Create(Transform parent)
    {
        var control = new GameObject(ButtonName, typeof(RectTransform), typeof(UnloadButtonGraphic), typeof(Button),
            typeof(LayoutElement), typeof(UnloadButtonPosition), typeof(HoverTooltipArea));
        control.transform.SetParent(parent, false);
        control.GetComponent<LayoutElement>().ignoreLayout = true;

        var button = control.GetComponent<Button>();
        button.targetGraphic = control.GetComponent<UnloadButtonGraphic>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.normalColor = new Color(0.94f, 0.94f, 0.94f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        button.colors = colors;
        return button;
    }
}

internal sealed class UnloadButtonPosition : MonoBehaviour
{
    private RectTransform pin = null!;
    private RectTransform? lockButton;

    internal void Bind(RectTransform pinButton, RectTransform? adjacentButton)
    {
        pin = pinButton;
        lockButton = adjacentButton;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (pin == null)
        {
            return;
        }

        var rect = (RectTransform)transform;
        rect.anchorMin = pin.anchorMin;
        rect.anchorMax = pin.anchorMax;
        rect.pivot = pin.pivot;
        rect.sizeDelta = pin.sizeDelta;
        rect.localScale = pin.localScale;
        rect.localRotation = pin.localRotation;
        float spacing = lockButton != null && lockButton.parent == pin.parent
            ? pin.localPosition.y - lockButton.localPosition.y : 0;
        float step = Mathf.Max(spacing, pin.rect.height + 2f);
        rect.localPosition = pin.localPosition + Vector3.up * step;
    }
}

internal sealed class UnloadButtonGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        var rect = rectTransform.rect;
        var background = new Color32(49, 57, 61, 255);
        var ink = new Color32(194, 198, 197, 255);

        // Match the size and clipped corner of the neighboring stash buttons.
        AddQuad(0f, 0f, 1f, 0.85f, background);
        AddQuad(0.15f, 0.85f, 1f, 1f, background);
        AddTriangle(new Vector2(0f, 0.85f), new Vector2(0.15f, 1f), new Vector2(0.15f, 0.85f), background);

        // Transfer arrow and the outline of its destination.
        AddQuad(0.25f, 0.47f, 0.46f, 0.53f, ink);
        AddTriangle(new Vector2(0.44f, 0.39f), new Vector2(0.44f, 0.61f), new Vector2(0.58f, 0.5f), ink);
        AddQuad(0.56f, 0.30f, 0.75f, 0.355f, ink);
        AddQuad(0.695f, 0.355f, 0.75f, 0.645f, ink);
        AddQuad(0.56f, 0.645f, 0.75f, 0.70f, ink);

        void AddVertex(Vector2 point, Color32 tint)
            => vertices.AddVert(rect.min + Vector2.Scale(rect.size, point), tint, Vector2.zero);

        void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Color32 tint)
        {
            int first = vertices.currentVertCount;
            AddVertex(a, tint);
            AddVertex(b, tint);
            AddVertex(c, tint);
            vertices.AddTriangle(first, first + 1, first + 2);
        }

        void AddQuad(float left, float bottom, float right, float top, Color32 tint)
        {
            int first = vertices.currentVertCount;
            AddVertex(new Vector2(left, bottom), tint);
            AddVertex(new Vector2(left, top), tint);
            AddVertex(new Vector2(right, top), tint);
            AddVertex(new Vector2(right, bottom), tint);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first + 2, first + 3, first);
        }
    }
}
