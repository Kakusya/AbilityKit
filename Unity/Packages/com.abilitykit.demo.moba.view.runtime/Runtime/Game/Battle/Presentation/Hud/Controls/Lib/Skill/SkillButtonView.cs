using System;
using AbilityKit.Protocol.Moba.StateSync;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    public sealed class SkillButtonView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private RectTransform _buttonRect;
        [SerializeField] private RectTransform _uiRootRect;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private SkillAimIndicatorView _aimIndicator;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private Text _cooldownText;
        [SerializeField] private SkillButtonConfig _config = default;

        private readonly SkillButtonAimController _aim = new SkillButtonAimController();
        private readonly SkillButtonGestureController _gesture = new SkillButtonGestureController();
        private Camera _uiCamera;
        private SkillButtonCooldownState _cooldownState;

        public SkillButtonConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                NormalizeConfig(ref _config);
                _gesture.Configure(_config);
                ConfigureAim();
            }
        }

        public event Action OnClick;
        public event Action OnLongPress;
        public event Action<Vector2> OnAimStart;
        public event Action<Vector2> OnAimUpdate;
        public event Action<Vector2> OnAimEnd;
        public event Action OnAimCancel;

        public void Initialize(
            RectTransform buttonRect,
            RectTransform uiRootRect,
            Canvas canvas,
            SkillButtonConfig config,
            SkillAimIndicatorView aimIndicator = null,
            Image cooldownOverlay = null,
            Text cooldownText = null)
        {
            _buttonRect = buttonRect;
            _uiRootRect = uiRootRect;
            _canvas = canvas;
            _aimIndicator = aimIndicator;
            _cooldownOverlay = cooldownOverlay;
            _cooldownText = cooldownText;
            _config = config;
            NormalizeConfig(ref _config);
            _gesture.Configure(_config);

            RefreshReferences();
            EnsureCooldownVisualOrder();
            _aim.Hide();
            RefreshCooldownVisuals();
        }

        private void Awake()
        {
            NormalizeConfig(ref _config);
            _gesture.Configure(_config);
            RefreshReferences();

            _aim.Hide();
        }

        private void RefreshReferences()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_buttonRect == null) _buttonRect = transform as RectTransform;
            if (_uiRootRect == null) _uiRootRect = _buttonRect != null ? _buttonRect.root as RectTransform : null;
            _uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            ConfigureAim();
        }

        private void ConfigureAim()
        {
            _aim.Configure(_buttonRect, _uiRootRect, _uiCamera, _aimIndicator, _config);
        }

        private static void NormalizeConfig(ref SkillButtonConfig config)
        {
            var defaults = SkillButtonConfig.Default;
            if (config.LongPressSeconds < 0f) config.LongPressSeconds = defaults.LongPressSeconds;
            if (config.DragThreshold <= 0f) config.DragThreshold = defaults.DragThreshold;
            if (config.AimMaxRadius <= 0f) config.AimMaxRadius = defaults.AimMaxRadius;
            if (config.IndicatorLengthPixels <= 0f) config.IndicatorLengthPixels = defaults.IndicatorLengthPixels;
            if (config.IndicatorWidthPixels <= 0f) config.IndicatorWidthPixels = defaults.IndicatorWidthPixels;
        }

        private void OnDisable()
        {
            CancelAimPreview();
            _gesture.Reset();
            _aim.Hide();
        }

        private void Update()
        {
            RefreshCooldownVisuals();

            if (!CanAcceptInput) return;
            if (!_gesture.Tick(Time.unscaledTime)) return;
            OnLongPress?.Invoke();

            if (_config.EnableAim)
            {
                BeginAim(_gesture.LastScreenPos);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_buttonRect == null) return;
            if (!CanAcceptInput) return;

            _gesture.Begin(eventData.pointerId, eventData.position, Time.unscaledTime);
            if (ShouldPreviewImmediately())
            {
                ShowAimPreview(eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!CanAcceptInput) return;
            if (!_gesture.Drag(eventData.pointerId, eventData.position, out var shouldBeginAim, out var shouldUpdateAim)) return;
            if (shouldBeginAim)
            {
                BeginAim(_gesture.LastScreenPos);
                shouldUpdateAim = true;
            }

            if (shouldUpdateAim)
            {
                var aim = _aim.Calculate(_gesture.LastScreenPos);
                OnAimUpdate?.Invoke(aim);
                _aim.Update(_gesture.LastScreenPos);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!CanAcceptInput)
            {
                _gesture.Reset();
                EndAim();
                return;
            }

            if (!_gesture.End(eventData.pointerId, out var wasAiming, out var longPressFired)) return;

            if (wasAiming)
            {
                var aim = _aim.Calculate(eventData.position);
                OnAimEnd?.Invoke(aim);
                EndAim();
                return;
            }

            if (!longPressFired)
            {
                CancelAimPreview();
                OnClick?.Invoke();
            }

            EndAim();
        }

        public void ApplySkillState(in MobaSkillStateSnapshotEntry entry)
        {
            _cooldownState = SkillButtonCooldownState.FromSnapshot(entry, Time.unscaledTime);
            RefreshCooldownVisuals();
        }

        public void ResetPresentationState()
        {
            CancelAimPreview();
            _gesture.Reset();
            _aim.Hide();
            ClearSkillState();
        }

        public void ClearSkillState()
        {
            _cooldownState = default;
            RefreshCooldownVisuals();
        }

        private bool CanAcceptInput => !_cooldownState.BlocksInput(Time.unscaledTime);

        private void RefreshCooldownVisuals()
        {
            EnsureCooldownVisualOrder();
            var visual = _cooldownState.GetVisualState(Time.unscaledTime);
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.gameObject.SetActive(visual.ShowOverlay);
                _cooldownOverlay.fillAmount = visual.OverlayFillAmount;
                _cooldownOverlay.color = visual.OverlayColor;
            }

            if (_cooldownText != null)
            {
                _cooldownText.gameObject.SetActive(visual.ShowCountdownText);
                _cooldownText.text = visual.ShowCountdownText
                    ? Mathf.CeilToInt(visual.RemainingSeconds).ToString()
                    : string.Empty;
            }
        }

        private void EnsureCooldownVisualOrder()
        {
            var cooldownLayer = _cooldownText != null
                ? _cooldownText.transform.parent
                : _cooldownOverlay != null
                    ? _cooldownOverlay.transform.parent
                    : null;
            if (cooldownLayer != null && cooldownLayer != transform)
            {
                cooldownLayer.SetAsLastSibling();
            }

            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.transform.SetAsLastSibling();
            }

            if (_cooldownText != null)
            {
                _cooldownText.transform.SetAsLastSibling();
            }
        }

        private void BeginAim(Vector2 currentScreen)
        {
            if (!CanAcceptInput) return;
            _gesture.SetAiming(true);
            var aim = _aim.Calculate(currentScreen);
            OnAimStart?.Invoke(aim);
            _aim.Show(currentScreen);
        }

        private void ShowAimPreview(Vector2 currentScreen)
        {
            if (!CanAcceptInput) return;
            _gesture.SetAiming(true);
            var aim = _aim.Calculate(currentScreen);
            OnAimStart?.Invoke(aim);
            OnAimUpdate?.Invoke(aim);
            _aim.Show(currentScreen);
        }

        private bool ShouldPreviewImmediately()
        {
            return _config.EnableAim && _config.IndicatorShape != SkillAimIndicatorShape.Hidden;
        }

        private void EndAim()
        {
            _gesture.SetAiming(false);
            _aim.Hide();
        }

        private void CancelAimPreview()
        {
            if (!_config.EnableAim) return;
            OnAimCancel?.Invoke();
        }

        private readonly struct SkillButtonCooldownVisualState
        {
            public readonly bool ShowOverlay;
            public readonly bool ShowCountdownText;
            public readonly float RemainingSeconds;
            public readonly float OverlayFillAmount;
            public readonly Color OverlayColor;

            public SkillButtonCooldownVisualState(
                bool showOverlay,
                bool showCountdownText,
                float remainingSeconds,
                float overlayFillAmount,
                Color overlayColor)
            {
                ShowOverlay = showOverlay;
                ShowCountdownText = showCountdownText;
                RemainingSeconds = remainingSeconds;
                OverlayFillAmount = overlayFillAmount;
                OverlayColor = overlayColor;
            }
        }

        private struct SkillButtonCooldownState
        {
            private static readonly Color CoolingOverlayColor = new Color(0f, 0f, 0f, 0.58f);
            private static readonly Color DisabledOverlayColor = new Color(0f, 0f, 0f, 0.72f);

            private MobaSkillAvailabilityState _availability;
            private int _disableReason;
            private float _cooldownTotalSeconds;
            private float _cooldownRemainingAtReceiveSeconds;
            private float _localReceiveTimeSeconds;

            public static SkillButtonCooldownState FromSnapshot(in MobaSkillStateSnapshotEntry entry, float localReceiveTimeSeconds)
            {
                var remainingMs = entry.CooldownRemainingMs;
                if (entry.Availability == MobaSkillAvailabilityState.CoolingDown && entry.CooldownEndTimeMs > entry.ServerTimeMs)
                {
                    remainingMs = Math.Max(remainingMs, ClampToInt(entry.CooldownEndTimeMs - entry.ServerTimeMs));
                }

                remainingMs = Math.Max(0, remainingMs);
                var totalMs = Math.Max(Math.Max(0, entry.CooldownTotalMs), remainingMs);
                return new SkillButtonCooldownState
                {
                    _availability = entry.Availability,
                    _disableReason = entry.DisableReason,
                    _cooldownTotalSeconds = totalMs / 1000f,
                    _cooldownRemainingAtReceiveSeconds = remainingMs / 1000f,
                    _localReceiveTimeSeconds = localReceiveTimeSeconds
                };
            }

            public bool BlocksInput(float nowSeconds)
            {
                if (IsDisabled) return true;
                return GetCooldownRemainingSeconds(nowSeconds) > 0f;
            }

            public SkillButtonCooldownVisualState GetVisualState(float nowSeconds)
            {
                if (IsDisabled)
                {
                    return new SkillButtonCooldownVisualState(
                        showOverlay: true,
                        showCountdownText: false,
                        remainingSeconds: 0f,
                        overlayFillAmount: 1f,
                        overlayColor: DisabledOverlayColor);
                }

                var remainingSeconds = GetCooldownRemainingSeconds(nowSeconds);
                if (remainingSeconds <= 0f)
                {
                    return new SkillButtonCooldownVisualState(
                        showOverlay: false,
                        showCountdownText: false,
                        remainingSeconds: 0f,
                        overlayFillAmount: 0f,
                        overlayColor: CoolingOverlayColor);
                }

                var fillAmount = _cooldownTotalSeconds > 0f
                    ? Mathf.Clamp01(remainingSeconds / _cooldownTotalSeconds)
                    : 1f;
                return new SkillButtonCooldownVisualState(
                    showOverlay: true,
                    showCountdownText: true,
                    remainingSeconds: remainingSeconds,
                    overlayFillAmount: fillAmount,
                    overlayColor: CoolingOverlayColor);
            }

            public float GetCooldownRemainingSeconds(float nowSeconds)
            {
                if (_availability != MobaSkillAvailabilityState.CoolingDown) return 0f;
                var elapsed = Mathf.Max(0f, nowSeconds - _localReceiveTimeSeconds);
                return Mathf.Max(0f, _cooldownRemainingAtReceiveSeconds - elapsed);
            }

            private bool IsDisabled => _availability == MobaSkillAvailabilityState.Disabled && _disableReason != 0;

            private static int ClampToInt(long value)
            {
                if (value <= 0L) return 0;
                return value > int.MaxValue ? int.MaxValue : (int)value;
            }
        }
    }
}
