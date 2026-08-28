using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主次危险按钮与假 Acrylic 玻璃底。禁止纯白铺满；ColorTint，不要 Transition.Fade。
/// 挡 AR 的面板 Image.a 必须 ≤ 0.55。
/// </summary>
public sealed class InspectUiTheme
{
    public const float Gap = 16f;
    public const float CompactGap = 8f;
    public const float ButtonH = 56f;
    public const float RowH = 56f;
    public const float CompactRowH = 80f;
    public const float GlassCoolA = 0.22f;
    public const float GlassWarmA = 0.40f;
    public const float GlassPanelA = 0.50f;
    public const float GlassMaxA = 0.55f;

    public readonly Color BgCoolGray;
    public readonly Color BgWarmGray;
    public readonly Color Primary;
    public readonly Color OnPrimary;
    public readonly Color Secondary;
    public readonly Color OnSecondary;
    public readonly Color Danger;
    public readonly Color OnDanger;
    public readonly Color Divider;
    public readonly Color SoftEdge;
    public readonly Color ShadowColor;
    public readonly Sprite Round;

    public InspectUiTheme()
    {
        ColorUtility.TryParseHtmlString("#D6DCE4", out BgCoolGray);
        ColorUtility.TryParseHtmlString("#E6E2DC", out BgWarmGray);
        ColorUtility.TryParseHtmlString("#2C4A6E", out Primary);
        ColorUtility.TryParseHtmlString("#F7F5F2", out OnPrimary);
        ColorUtility.TryParseHtmlString("#D4D0C8", out Secondary);
        ColorUtility.TryParseHtmlString("#3F3E3C", out OnSecondary);
        ColorUtility.TryParseHtmlString("#E8D0CC", out Danger);
        ColorUtility.TryParseHtmlString("#8B3A3A", out OnDanger);
        ColorUtility.TryParseHtmlString("#C5C1BA", out Divider);
        ColorUtility.TryParseHtmlString("#B8B4AC", out SoftEdge);
        ShadowColor = new Color(0.29f, 0.29f, 0.28f, 0.22f);
        Round = CreateRoundSprite(64, 16);
    }

    public static Font UiFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
               ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    public void StylePrimaryButton(Button button, Image image, Text label)
    {
        StyleSliced(image, Primary);
        if (label != null)
            label.color = OnPrimary;
        StyleColorTint(button);
        EnsureShadow(image.gameObject);
    }

    public void StyleSecondaryButton(Button button, Image image, Text label)
    {
        StyleSliced(image, Secondary);
        if (label != null)
            label.color = OnSecondary;
        StyleColorTint(button);
        EnsureShadow(image.gameObject);
        var outline = image.gameObject.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
        outline.effectColor = SoftEdge;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    public void StyleDangerButton(Button button, Image image, Text label)
    {
        StyleSliced(image, Danger);
        if (label != null)
            label.color = OnDanger;
        StyleColorTint(button);
        EnsureShadow(image.gameObject);
    }

    public void StyleSliced(Image image, Color color)
    {
        image.sprite = Round;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = true;
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    public void StyleGlass(Image image, Color rgb, float alpha, bool raycast)
    {
        StyleSliced(image, WithAlpha(rgb, Mathf.Min(alpha, GlassMaxA)));
        image.raycastTarget = raycast;
    }

    public void StyleDim(Image image, Color rgb, float alpha)
    {
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = WithAlpha(rgb, Mathf.Min(alpha, GlassMaxA));
        image.raycastTarget = true;
    }

    public GameObject CreateGlassBar(Transform parent, string name, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var cool = new GameObject("GlassCool", typeof(RectTransform), typeof(Image));
        cool.transform.SetParent(go.transform, false);
        StretchFull(cool.GetComponent<RectTransform>());
        StyleGlass(cool.GetComponent<Image>(), BgCoolGray, GlassCoolA, raycast);
        var warm = new GameObject("GlassWarm", typeof(RectTransform), typeof(Image));
        warm.transform.SetParent(go.transform, false);
        StretchFull(warm.GetComponent<RectTransform>());
        StyleGlass(warm.GetComponent<Image>(), BgWarmGray, GlassWarmA, false);
        return go;
    }

    public Text CreateAnchoredText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(12f, 4f);
        rt.offsetMax = new Vector2(-12f, -4f);
        var t = go.GetComponent<Text>();
        t.font = UiFont();
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Normal;
        t.color = color;
        t.alignment = alignment;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public static void StyleColorTint(Button button)
    {
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.fadeDuration = 0.15f;
        colors.colorMultiplier = 1f;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.80f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        button.colors = colors;
    }

    public GameObject CreateCard(Transform parent, string name, Color background)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Shadow));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        StyleSliced(img, background);
        EnsureShadow(go);
        return go;
    }

    public Text CreateLabel(Transform parent, string text, ref float y, int fontSize, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-32f, fontSize + 12f);
        y -= fontSize + Gap;
        var t = go.GetComponent<Text>();
        t.font = UiFont();
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Normal;
        t.color = color;
        t.alignment = TextAnchor.MiddleLeft;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public InputField CreateInput(Transform parent, string placeholder, string value, ref float y)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(Shadow));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-32f, ButtonH);
        y -= ButtonH + Gap;
        StyleSliced(go.GetComponent<Image>(), BgCoolGray);
        EnsureShadow(go);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), 12f);
        var text = textGo.GetComponent<Text>();
        text.font = UiFont();
        text.fontSize = 26;
        text.fontStyle = FontStyle.Normal;
        text.color = OnSecondary;
        text.supportRichText = false;
        text.alignment = TextAnchor.MiddleLeft;

        var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phGo.transform.SetParent(go.transform, false);
        Stretch(phGo.GetComponent<RectTransform>(), 12f);
        var ph = phGo.GetComponent<Text>();
        ph.font = UiFont();
        ph.fontSize = 26;
        ph.fontStyle = FontStyle.Normal;
        ph.color = new Color(OnSecondary.r, OnSecondary.g, OnSecondary.b, 0.55f);
        ph.text = placeholder;
        ph.alignment = TextAnchor.MiddleLeft;

        var field = go.GetComponent<InputField>();
        field.textComponent = text;
        field.placeholder = ph;
        field.text = value ?? "";
        return field;
    }

    public Button CreatePrimary(Transform parent, string label, ref float y)
    {
        return CreateButton(parent, label, ref y, StylePrimaryButton);
    }

    public Button CreateSecondary(Transform parent, string label, ref float y)
    {
        return CreateButton(parent, label, ref y, StyleSecondaryButton);
    }

    public Button CreateDanger(Transform parent, string label, ref float y)
    {
        return CreateButton(parent, label, ref y, StyleDangerButton);
    }

    public Button CreateSplitButton(Transform parent, string label, float anchorMinX, float anchorMaxX, System.Action<Button, Image, Text> style)
    {
        return CreateSplitButton(parent, label, anchorMinX, anchorMaxX, style, Gap * 0.5f, 6f, 26);
    }

    public Button CreateSplitButton(Transform parent, string label, float anchorMinX, float anchorMaxX, System.Action<Button, Image, Text> style, float padX, float padY, int fontSize)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorMinX, 0f);
        rt.anchorMax = new Vector2(anchorMaxX, 1f);
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        var text = AddButtonLabel(go.transform, label, fontSize);
        style(btn, img, text);
        return btn;
    }

    Button CreateButton(Transform parent, string label, ref float y, System.Action<Button, Image, Text> style)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-32f, ButtonH);
        y -= ButtonH + Gap;
        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        var text = AddButtonLabel(go.transform, label);
        style(btn, img, text);
        return btn;
    }

    public Text AddButtonLabel(Transform parent, string label)
    {
        return AddButtonLabel(parent, label, 26);
    }

    public Text AddButtonLabel(Transform parent, string label, int fontSize)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>(), 0f);
        var t = go.GetComponent<Text>();
        t.font = UiFont();
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Normal;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = label;
        t.raycastTarget = false;
        return t;
    }

    public void EnsureShadow(GameObject go)
    {
        var shadow = go.GetComponent<Shadow>() ?? go.AddComponent<Shadow>();
        shadow.effectColor = ShadowColor;
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;
    }

    public static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, 4f);
        rt.offsetMax = new Vector2(-pad, -4f);
    }

    public static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Sprite CreateRoundSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var r2 = radius * radius;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = 0;
                var dy = 0;
                if (x < radius)
                    dx = radius - x;
                else if (x >= size - radius)
                    dx = x - (size - radius - 1);
                if (y < radius)
                    dy = radius - y;
                else if (y >= size - radius)
                    dy = y - (size - radius - 1);
                var inside = dx == 0 || dy == 0 || dx * dx + dy * dy <= r2;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        tex.Apply(false, false);
        return Sprite.Create(
            tex,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }
}
