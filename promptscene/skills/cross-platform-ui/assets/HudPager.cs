using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PromptScene.Core.UI;

/// <summary>
/// 페이지 넘김 — v6 목업의 viewport/track을 uGUI로 옮긴 것. 드래그·휠로 4개씩 넘긴다.
///
/// ScrollRect를 쓰지 않는 이유: 스냅·플릭 임계값·"드래그 중에는 클릭을 죽인다"는 세 가지가 전부
/// 우리 규칙(HudTheme.Page*)이고, ScrollRect를 그 규칙에 맞추는 설정이 직접 구현보다 길고 불투명하다.
/// 잘려 보이는 건 RectMask2D가 한다(목업의 overflow:hidden).
///
/// ⚠ 목업 v5가 고친 함정을 그대로 안고 온다: 브라우저에서는 setPointerCapture가 걸리면 click이
/// 캡처 대상으로 재타깃되어 버튼에 도달하지 못했다(v4에서 토글이 죽은 원인). uGUI에는 그 API가 없지만
/// **같은 증상**이 다른 경로로 생긴다 — 드래그로 손이 흔들리면 pointer-up에서 버튼 onClick이 그대로
/// 발화한다. 그래서 임계값(DragThresholdPx)을 넘은 드래그를 <see cref="ConsumedDrag"/>로 표시하고
/// 바인더의 클릭 핸들러가 그 프레임의 클릭을 버린다.
///
/// 직렬 필드 0 (§3b) — 전부 <see cref="Configure"/>로 런타임 배선한다.
/// </summary>
public class HudPager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private RectTransform _viewport, _track;
    private int _pages = 1;
    private int _page;
    private float _pageW;
    private float _dragDx;
    private bool _dragging;
    private float _targetX, _currentX;
    private float _dotsHideAt;
    private Action<int> _onPageChanged;

    /// <summary>이 프레임의 클릭은 드래그의 꼬리다 — 버튼이 무시해야 한다.</summary>
    public bool ConsumedDrag { get; private set; }
    public int Page => _page;
    public int Pages => _pages;
    /// <summary>점을 지금 보여야 하는가(넘기는 동안 + DotsRevealSeconds).</summary>
    public bool DotsVisible => Time.unscaledTime < _dotsHideAt;

    public void Configure(RectTransform viewport, RectTransform track, int pages, float pageWidth, Action<int> onPageChanged)
    {
        _viewport = viewport; _track = track;
        _pages = Mathf.Max(1, pages);
        _pageW = pageWidth;
        _onPageChanged = onPageChanged;
        _page = Mathf.Clamp(_page, 0, _pages - 1);
        _targetX = _currentX = -_page * _pageW;
        Apply();
    }

    public void GoTo(int page)
    {
        _page = Mathf.Clamp(page, 0, _pages - 1);
        _targetX = -_page * _pageW;
        Reveal();
        _onPageChanged?.Invoke(_page);
    }

    /// <summary>점을 띄우고 타이머를 다시 감는다. 넘길 때만 보이는 게 목업의 규칙이다.</summary>
    public void Reveal() => _dotsHideAt = Time.unscaledTime + HudTheme.DotsRevealSeconds;

    public void OnBeginDrag(PointerEventData e)
    {
        if (_pages <= 1) return;
        _dragging = true; _dragDx = 0f; ConsumedDrag = false; Reveal();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_dragging) return;
        _dragDx += e.delta.x;
        if (Mathf.Abs(_dragDx) > HudTheme.DragThresholdPx) ConsumedDrag = true;   // 이 이상이면 클릭이 아니다
        _currentX = _targetX + _dragDx;
        Apply();
        Reveal();
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_dragging) return;
        _dragging = false;
        float t = _dragDx; _dragDx = 0f;
        if (_pageW > 0f && Mathf.Abs(t) > _pageW * HudTheme.PageFlickFraction) GoTo(_page + (t < 0 ? 1 : -1));
        else GoTo(_page);
    }

    public void OnScroll(PointerEventData e)
    {
        if (_pages <= 1) return;
        float d = Mathf.Abs(e.scrollDelta.x) > Mathf.Abs(e.scrollDelta.y) ? e.scrollDelta.x : -e.scrollDelta.y;
        if (Mathf.Abs(d) < 0.01f) return;
        GoTo(_page + (d > 0f ? 1 : -1));
    }

    private void LateUpdate()
    {
        if (!_dragging && !Mathf.Approximately(_currentX, _targetX))
        {
            // 목업의 transition .22s ease-out 에 해당. 프레임률과 무관하도록 지수 감쇠를 쓴다.
            _currentX = Mathf.Lerp(_currentX, _targetX, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if (Mathf.Abs(_currentX - _targetX) < 0.5f) _currentX = _targetX;
            Apply();
        }
        if (ConsumedDrag && !_dragging) ConsumedDrag = false;   // 클릭 한 번만 삼킨다
    }

    private void Apply()
    {
        if (_track == null) return;
        var p = _track.anchoredPosition;
        _track.anchoredPosition = new Vector2(_currentX, p.y);
    }
}
