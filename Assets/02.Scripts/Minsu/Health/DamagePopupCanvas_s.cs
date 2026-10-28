using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum DamagePopupKind { Normal, Intermediate, Final, Charged }

[RequireComponent(typeof(Canvas))]
public class DamagePopupCanvas_s : MonoBehaviour
{
    private const int MaximumPopups = 48;
    private static DamagePopupCanvas_s _instance;
    private readonly List<Popup_s> _pool = new List<Popup_s>();
    private RectTransform _canvasRect;
    private Camera _camera;
    private Font _font;
    private int _replaceIndex;

    private sealed class Popup_s
    {
        public RectTransform Rect;
        public Text Label;
        public CanvasGroup Group;
        public Vector3 Point;
        public float Age;
        public float Duration;
        public float Rise;
        public float Horizontal;
        public float Scale;
        public bool Active;
    }

    public static void Show(int amount, Vector3 point, Color color, int fontSize,
        float duration, float rise, float horizontal, float scale)
    {
        if (amount <= 0) return;
        if (_instance == null)
        {
            var root = new GameObject("Damage Popup Canvas (Runtime)", typeof(RectTransform), typeof(Canvas));
            _instance = root.AddComponent<DamagePopupCanvas_s>();
        }
        var popup = _instance.GetPopup();
        popup.Point = point;
        popup.Label.text = amount.ToString();
        popup.Label.color = color;
        popup.Label.fontSize = Mathf.Max(12, fontSize);
        popup.Age = 0f;
        popup.Duration = Mathf.Max(0.1f, duration);
        popup.Rise = rise;
        popup.Horizontal = horizontal;
        popup.Scale = Mathf.Max(0.1f, scale);
        popup.Active = true;
        popup.Group.alpha = 0f;
        popup.Rect.gameObject.SetActive(true);
    }

    private void Awake()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        _canvasRect = (RectTransform)transform;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private Popup_s GetPopup()
    {
        foreach (var popup in _pool)
        {
            if (!popup.Active) return popup;
        }
        if (_pool.Count >= MaximumPopups)
        {
            var reused = _pool[_replaceIndex];
            _replaceIndex = (_replaceIndex + 1) % MaximumPopups;
            return reused;
        }
        var child = new GameObject("Damage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(CanvasGroup), typeof(Outline));
        child.transform.SetParent(transform, false);
        var rect = (RectTransform)child.transform;
        rect.sizeDelta = new Vector2(240f, 100f);
        var label = child.GetComponent<Text>();
        label.font = _font;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        var outline = child.GetComponent<Outline>();
        outline.effectColor = new Color(0.03f, 0.05f, 0.08f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var group = child.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var created = new Popup_s { Rect = rect, Label = label, Group = group };
        _pool.Add(created);
        return created;
    }

    private void LateUpdate()
    {
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        foreach (var popup in _pool)
        {
            if (!popup.Active) continue;
            popup.Age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(popup.Age / popup.Duration);
            if (t >= 1f)
            {
                popup.Active = false;
                popup.Rect.gameObject.SetActive(false);
                continue;
            }
            if (_camera == null)
            {
                popup.Group.alpha = 0f;
                continue;
            }
            Vector3 screen = _camera.WorldToScreenPoint(popup.Point);
            if (screen.z <= 0f || !_camera.pixelRect.Contains(new Vector2(screen.x, screen.y)))
            {
                popup.Group.alpha = 0f;
                continue;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 position);
            float movement = 1f - (1f - t) * (1f - t);
            popup.Rect.anchoredPosition = position + new Vector2(popup.Horizontal * (0.6f + movement * 0.4f), popup.Rise * movement);
            float pop = popup.Age < 0.06f ? Mathf.Lerp(0.65f, 1.25f, popup.Age / 0.06f)
                : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((popup.Age - 0.06f) / 0.1f));
            popup.Rect.localScale = Vector3.one * (popup.Scale * pop);
            popup.Group.alpha = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
