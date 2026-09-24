using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始界面的美术：背景、面板底板、按钮都**可以用玩家提供的图片替换**
/// （key：<c>menu_background</c> / <c>menu_panel</c> / <c>menu_button</c>，放进
/// <c>Assets/Resources/ArtOverride/</c> 即可）。
///
/// 图片是可选的：**没放图片就自己用代码画** ——
/// 背景画一张纵向渐变（深绿 → 稍亮的绿），面板与按钮画白色圆角矩形九宫格，
/// 再乘上各控件原本的颜色，所以观感和原来一致，也永远不会因为缺素材而变成空白。
///
/// 面板与按钮**保留各自的颜色**（颜色承担了「哪个按钮干什么」的信息），图片只换形状 / 纹理。
/// </summary>
[DefaultExecutionOrder(1100)]
public class MenuArt : MonoBehaviour
{
    [Header("引用（留空就按名字自动找）")]
    [Tooltip("整屏背景（背景图 / 渐变都套在它上面）")]
    public Image background;
    [Tooltip("面板底板：主面板 / 设置面板 / 地图面板")]
    public Image[] panels;
    [Tooltip("所有按钮")]
    public Button[] buttons;

    [Header("程序化背景（没有 menu_background 图片时用）")]
    public Color gradientTop = new Color(0.15f, 0.21f, 0.16f);
    public Color gradientBottom = new Color(0.38f, 0.47f, 0.32f);
    [Tooltip("渐变贴图的像素高度（越高越细腻）")]
    public int gradientHeight = 128;

    [Header("程序化面板 / 按钮（没有图片时用）")]
    [Tooltip("圆角矩形贴图边长（像素）")]
    public int roundRectSize = 64;
    [Tooltip("圆角半径（像素）")]
    public int cornerRadius = 20;

    [Header("日志")]
    public bool logToConsole = true;

    readonly List<UnityEngine.Object> generated = new List<UnityEngine.Object>();
    Sprite cachedGradient;
    Sprite cachedRoundRect;

    void Awake()
    {
        Resolve();
        ApplyNow();
    }

    void OnDestroy()
    {
        // 倒着销毁：先生成的是贴图、后生成的是引用它的 Sprite，Sprite 先走
        for (int i = generated.Count - 1; i >= 0; i--)
            if (generated[i] != null) Destroy(generated[i]);
        generated.Clear();
    }

    /// <summary>没手填引用时按名字找：Background / *Panel / 所有 Button。</summary>
    void Resolve()
    {
        if (background == null)
        {
            Transform found = transform.Find("Background");
            if (found != null) background = found.GetComponent<Image>();
        }

        if (panels == null || panels.Length == 0)
        {
            List<Image> list = new List<Image>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.EndsWith("Panel")) continue;
                Image image = child.GetComponent<Image>();
                if (image != null) list.Add(image);
            }
            panels = list.ToArray();
        }

        if (buttons == null || buttons.Length == 0)
            buttons = GetComponentsInChildren<Button>(true);
    }

    /// <summary>套用一次（运行中新放了图片也可以手动调用）。</summary>
    public void ApplyNow()
    {
        int backgroundFromImage = ApplyBackground();
        int panelsFromImage = ApplyPanels();
        int buttonsFromImage = ApplyButtons();

        if (!logToConsole) return;
        Debug.Log("[ArtOverride] 开始界面：背景 " + (backgroundFromImage > 0 ? "用图片" : "程序绘制渐变")
            + "，面板 " + panelsFromImage + "/" + (panels != null ? panels.Length : 0) + " 用图片"
            + "，按钮 " + buttonsFromImage + "/" + (buttons != null ? buttons.Length : 0) + " 用图片。");
    }

    /// <summary>背景：有图片用图片（整张铺满），没有就程序画渐变。</summary>
    int ApplyBackground()
    {
        if (background == null) return 0;

        Sprite art = ArtOverride.Get(ArtKeys.MenuBackground);
        background.sprite = art != null ? art : GenerateGradient();
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = Color.white;
        return art != null ? 1 : 0;
    }

    /// <summary>面板底板：有图片用图片，没有就程序画圆角矩形（颜色保留）。</summary>
    int ApplyPanels()
    {
        if (panels == null) return 0;

        Sprite art = ArtOverride.Get(ArtKeys.MenuPanel);
        Sprite fallback = art != null ? null : GenerateRoundRect();
        int fromImage = 0;

        for (int i = 0; i < panels.Length; i++)
        {
            Image image = panels[i];
            if (image == null) continue;
            image.sprite = art != null ? art : fallback;
            image.type = Image.Type.Sliced;
            if (art != null) fromImage++;
        }
        return fromImage;
    }

    /// <summary>按钮：有图片用图片，没有就程序画圆角矩形（颜色保留）。</summary>
    int ApplyButtons()
    {
        if (buttons == null) return 0;

        Sprite art = ArtOverride.Get(ArtKeys.MenuButton);
        Sprite fallback = art != null ? null : GenerateRoundRect();
        int fromImage = 0;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;

            Image image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image == null) continue;

            image.sprite = art != null ? art : fallback;
            image.type = Image.Type.Sliced;
            if (art != null) fromImage++;
        }
        return fromImage;
    }

    // ---------------- 程序化贴图 ----------------

    Sprite GenerateGradient()
    {
        if (cachedGradient != null) return cachedGradient;

        int height = Mathf.Max(2, gradientHeight);
        Texture2D texture = NewTexture(1, height);
        for (int y = 0; y < height; y++)
        {
            float t = y / (height - 1f);                       // 0 = 底部
            texture.SetPixel(0, y, Color.Lerp(gradientBottom, gradientTop, t));
        }
        texture.Apply();

        cachedGradient = Sprite.Create(texture, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        Keep(cachedGradient);
        return cachedGradient;
    }

    Sprite GenerateRoundRect()
    {
        if (cachedRoundRect != null) return cachedRoundRect;

        int size = Mathf.Max(8, roundRectSize);
        int radius = Mathf.Clamp(cornerRadius, 1, size / 2 - 1);

        Texture2D texture = NewTexture(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 到圆角矩形的有符号距离（负 = 在里面），用来做 1 像素抗锯齿
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float alpha = Mathf.Clamp01(0.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        cachedRoundRect = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, border);
        Keep(cachedRoundRect);
        return cachedRoundRect;
    }

    Texture2D NewTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        generated.Add(texture);
        return texture;
    }

    void Keep(Sprite sprite)
    {
        sprite.hideFlags = HideFlags.HideAndDontSave;
        generated.Add(sprite);
    }
}
