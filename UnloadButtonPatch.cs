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
    private static readonly AccessTools.FieldRef<ToggleButton, Image> PinBackground =
        AccessTools.FieldRefAccess<ToggleButton, Image>("_image");
    private static readonly AccessTools.FieldRef<ToggleButton, Sprite> PinIdleSprite =
        AccessTools.FieldRefAccess<ToggleButton, Sprite>("_onSprite");

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

        button ??= Create(pin.parent, PinBackground(____pinModeButton), PinIdleSprite(____pinModeButton));
        button.GetComponent<UnloadButtonPosition>().Bind(pin, ____lockModeButton?.transform as RectTransform);
        button.GetComponent<HoverTooltipArea>().Init(context.Tooltip,
            "Выгрузить лут\nС выделением на ЧВК — только выделенное.\nБез снимка рейда — вся съёмная экипировка.", rawText: true);
        string profileId = player.Profile_0.Id;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LootSorting.Unload(profileId));
        button.gameObject.SetActive(true);
    }

    private static Button Create(Transform parent, Image reference, Sprite idleSprite)
    {
        var control = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement), typeof(UnloadButtonPosition), typeof(HoverTooltipArea));
        control.transform.SetParent(parent, false);
        control.GetComponent<LayoutElement>().ignoreLayout = true;

        var background = control.GetComponent<Image>();
        background.sprite = idleSprite != null ? idleSprite : reference.sprite;
        background.type = reference.type;
        background.color = reference.color;

        var button = control.GetComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        button.colors = colors;

        var icon = new GameObject("UnloadIcon", typeof(RectTransform), typeof(UnloadIconGraphic))
            .GetComponent<UnloadIconGraphic>();
        icon.rectTransform.SetParent(control.transform, false);
        icon.rectTransform.anchorMin = new Vector2(0.22f, 0.22f);
        icon.rectTransform.anchorMax = new Vector2(0.78f, 0.78f);
        icon.rectTransform.offsetMin = Vector2.zero;
        icon.rectTransform.offsetMax = Vector2.zero;
        icon.color = new Color32(195, 196, 191, 255);
        icon.raycastTarget = false;
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

internal sealed class UnloadIconGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        var rect = rectTransform.rect;
        Vector2 Point(float x, float y) => rect.min + Vector2.Scale(rect.size, new Vector2(x, y));

        var tip = Point(0.5f, 0.40f);
        AddLine(Point(0.5f, 0.88f), tip);
        AddLine(Point(0.29f, 0.61f), tip);
        AddLine(Point(0.71f, 0.61f), tip);

        var left = Point(0.13f, 0.15f);
        var right = Point(0.87f, 0.15f);
        AddLine(Point(0.13f, 0.42f), left);
        AddLine(left, right);
        AddLine(right, Point(0.87f, 0.42f));

        void AddLine(Vector2 start, Vector2 end)
        {
            var direction = (end - start).normalized;
            var edge = new Vector2(-direction.y, direction.x) * (rect.width * 0.045f);
            int first = vertices.currentVertCount;
            vertices.AddVert(start + edge, color, Vector2.zero);
            vertices.AddVert(end + edge, color, Vector2.zero);
            vertices.AddVert(end - edge, color, Vector2.zero);
            vertices.AddVert(start - edge, color, Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first + 2, first + 3, first);
        }
    }
}
