using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 历史记录滑层：GET 列表、展开 X/Y/Z 三行、有权限则 PUT。
/// </summary>
public sealed class InspectHistoryPanel
{
    readonly InspectUiTheme m_Theme;
    readonly InspectARApp m_App;
    readonly GameObject m_Root;
    readonly Transform m_Content;
    readonly Text m_Status;
    readonly Dictionary<string, string> m_DraftPriority = new Dictionary<string, string>();
    string m_ExpandedId;

    public InspectHistoryPanel(InspectUiTheme theme, InspectARApp app, Transform canvas)
    {
        m_Theme = theme;
        m_App = app;
        m_Root = new GameObject("HistoryOverlay", typeof(RectTransform), typeof(Image));
        m_Root.transform.SetParent(canvas, false);
        InspectUiTheme.StretchFull(m_Root.GetComponent<RectTransform>());
        theme.StyleDim(m_Root.GetComponent<Image>(), theme.BgCoolGray, InspectUiTheme.GlassMaxA);

        var card = theme.CreateCard(m_Root.transform, "HistoryCard", InspectUiTheme.WithAlpha(theme.BgWarmGray, InspectUiTheme.GlassPanelA));
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.04f, 0.06f);
        cardRt.anchorMax = new Vector2(0.96f, 0.94f);
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;

        var y = -16f;
        theme.CreateLabel(card.transform, "历史记录", ref y, 32, theme.OnSecondary);
        var close = theme.CreateSecondary(card.transform, "关闭", ref y);
        close.onClick.AddListener(Hide);
        m_Status = theme.CreateLabel(card.transform, "", ref y, 22, theme.OnSecondary);

        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
        scrollGo.transform.SetParent(card.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(16f, 16f);
        scrollRt.offsetMax = new Vector2(-16f, y - 8f);
        var scrollImg = scrollGo.GetComponent<Image>();
        theme.StyleSliced(scrollImg, theme.BgCoolGray);
        scrollGo.GetComponent<Mask>().showMaskGraphic = true;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        InspectUiTheme.StretchFull(viewport.GetComponent<RectTransform>());

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 20f);
        m_Content = content.transform;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRt;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        Hide();
    }

    public void Show()
    {
        m_Root.SetActive(true);
        m_ExpandedId = null;
        m_DraftPriority.Clear();
        m_Status.text = "正在加载…";
        m_App.StartHistoryLoad();
    }

    public void Hide()
    {
        if (m_Root != null)
            m_Root.SetActive(false);
    }

    public bool IsOpen => m_Root != null && m_Root.activeSelf;

    public void Render(InspectIssueDto[] issues, string message)
    {
        for (var i = m_Content.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(m_Content.GetChild(i).gameObject);

        m_Status.text = message ?? "";
        if (issues == null || issues.Length == 0)
        {
            SetContentHeight(40f);
            return;
        }

        float y = -8f;
        for (var i = 0; i < issues.Length; i++)
            y = AddRow(issues[i], y);

        SetContentHeight(Mathf.Abs(y) + 24f);
    }

    float AddRow(InspectIssueDto issue, float y)
    {
        if (issue == null)
            return y;
        var expanded = issue.id == m_ExpandedId;
        var row = m_Theme.CreateCard(m_Content, "Issue_" + issue.id, m_Theme.BgWarmGray);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        var height = expanded ? 340f : 88f;
        rt.sizeDelta = new Vector2(-8f, height);
        y -= height + InspectUiTheme.Gap;

        var innerY = -8f;
        var title = string.IsNullOrEmpty(issue.title) ? "(无标题)" : issue.title;
        m_Theme.CreateLabel(row.transform, title + "  ·  " + (issue.priority ?? "") + "  ·  " + (issue.status ?? ""), ref innerY, 24, m_Theme.OnSecondary);

        var open = m_Theme.CreateSecondary(row.transform, expanded ? "收起" : "展开", ref innerY);
        var captured = issue;
        open.onClick.AddListener(() =>
        {
            m_ExpandedId = expanded ? null : captured.id;
            m_App.StartHistoryLoad();
        });

        if (!expanded)
            return y;

        var desc = string.IsNullOrEmpty(issue.description) ? "（无描述）" : issue.description;
        m_Theme.CreateLabel(row.transform, desc, ref innerY, 22, m_Theme.OnSecondary);
        var pos = issue.position;
        var x = pos != null ? pos.x : 0f;
        var py = pos != null ? pos.y : 0f;
        var z = pos != null ? pos.z : 0f;
        m_Theme.CreateLabel(row.transform, $"X 坐标：{x:F2}", ref innerY, 22, m_Theme.OnSecondary);
        m_Theme.CreateLabel(row.transform, $"Y 坐标：{py:F2}", ref innerY, 22, m_Theme.OnSecondary);
        m_Theme.CreateLabel(row.transform, $"Z 坐标：{z:F2}", ref innerY, 22, m_Theme.OnSecondary);

        if (!m_App.CanEditIssue(issue))
        {
            m_Theme.CreateLabel(row.transform, m_App.UserRole == "viewer" ? "只读账号不能编辑。" : "只能编辑自己提交的问题。", ref innerY, 22, m_Theme.OnDanger);
            return y;
        }

        var titleField = m_Theme.CreateInput(row.transform, "标题", issue.title ?? "", ref innerY);
        var descField = m_Theme.CreateInput(row.transform, "描述", issue.description ?? "", ref innerY);

        var rowPrio = new GameObject("Prio", typeof(RectTransform));
        rowPrio.transform.SetParent(row.transform, false);
        var pRt = rowPrio.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0f, 1f);
        pRt.anchorMax = new Vector2(1f, 1f);
        pRt.pivot = new Vector2(0.5f, 1f);
        pRt.anchoredPosition = new Vector2(0f, innerY);
        pRt.sizeDelta = new Vector2(-32f, 48f);
        innerY -= 48f + InspectUiTheme.Gap;
        var labels = new[] { "low", "medium", "high" };
        var current = DraftPriority(issue);
        var prioButtons = new Button[labels.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var p = labels[i];
            var btn = m_Theme.CreateSplitButton(rowPrio.transform, p, i / 3f, (i + 1) / 3f,
                p == current ? m_Theme.StylePrimaryButton : m_Theme.StyleSecondaryButton);
            prioButtons[i] = btn;
            var capturedP = p;
            btn.onClick.AddListener(() =>
            {
                SetDraftPriority(captured.id, capturedP);
                for (var j = 0; j < labels.Length; j++)
                {
                    var img = prioButtons[j].GetComponent<Image>();
                    var lab = prioButtons[j].GetComponentInChildren<Text>();
                    if (labels[j] == capturedP)
                        m_Theme.StylePrimaryButton(prioButtons[j], img, lab);
                    else
                        m_Theme.StyleSecondaryButton(prioButtons[j], img, lab);
                }
            });
        }

        var save = m_Theme.CreatePrimary(row.transform, "保存", ref innerY);
        save.onClick.AddListener(() =>
            m_App.StartHistorySave(captured.id, titleField.text, descField.text, DraftPriority(captured)));
        return y;
    }

    string DraftPriority(InspectIssueDto issue)
    {
        if (issue == null)
            return "medium";
        if (!string.IsNullOrEmpty(issue.id) && m_DraftPriority.TryGetValue(issue.id, out var drafted) && !string.IsNullOrEmpty(drafted))
            return drafted;
        return string.IsNullOrEmpty(issue.priority) ? "medium" : issue.priority;
    }

    void SetDraftPriority(string id, string priority)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(priority))
            return;
        m_DraftPriority[id] = priority;
    }

    void SetContentHeight(float height)
    {
        var rt = m_Content.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, Mathf.Max(height, 40f));
    }
}

[Serializable]
public sealed class InspectIssueDto
{
    public string id;
    public string title;
    public string description;
    public string priority;
    public string status;
    public InspectPositionDto position;
    public string submitterId;
    public string submitterName;
}

[Serializable]
public sealed class InspectPositionDto
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public sealed class InspectIssueListDto
{
    public InspectIssueDto[] issues;
}

[Serializable]
public sealed class InspectErrorDto
{
    public string error;
}

[Serializable]
public sealed class InspectLoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public sealed class InspectLoginUserDto
{
    public string id;
    public string username;
    public string role;
}

[Serializable]
public sealed class InspectLoginResponse
{
    public string token;
    public InspectLoginUserDto user;
}

[Serializable]
public sealed class InspectIssueRequest
{
    public string title;
    public string description;
    public string priority;
    public InspectPositionDto position;
}

[Serializable]
public sealed class InspectPutRequest
{
    public string title;
    public string description;
    public string priority;
}
