using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace NavKeypad
{
    public class Keypad : MonoBehaviour
    {
        [Header("Events")]
        [SerializeField] private UnityEvent onAccessGranted;
        [SerializeField] private UnityEvent onAccessDenied;

        [Header("Combination Code (9 Numbers Max)")]
        [SerializeField] private string keypadCombo = "12345";

        public UnityEvent OnAccessGranted => onAccessGranted;
        public UnityEvent OnAccessDenied => onAccessDenied;

        [Header("Task Lock")]
        [SerializeField] private bool lockUntilTaskStage = true;

        [Tooltip("只有当前任务是这个 Task Id 时，密码键盘才允许输入。")]
        [SerializeField] private string requiredTaskId = "unlock_door";

        [Tooltip("如果当前还没到指定任务阶段，按键时是否显示提示文字。")]
        [SerializeField] private bool showLockedText = true;

        [SerializeField] private string lockedText = "LOCKED";

        [Tooltip("锁定提示显示多久后清空。")]
        [SerializeField] private float lockedTextTime = 1f;

        [Tooltip("未到任务阶段时，锁定音效的冷却时间。连续点击不会重复播放。")]
        [SerializeField] private float lockedSoundCooldown = 2f;

        [Header("Settings")]
        [SerializeField] private string accessGrantedText = "Granted";
        [SerializeField] private string accessDeniedText = "Denied";

        [Header("Visuals")]
        [SerializeField] private float displayResultTime = 1f;

        [Range(0, 5)]
        [SerializeField] private float screenIntensity = 2.5f;

        [Header("Colors")]
        [SerializeField] private Color screenNormalColor = new Color(0.98f, 0.50f, 0.032f, 1f);
        [SerializeField] private Color screenDeniedColor = new Color(1f, 0f, 0f, 1f);
        [SerializeField] private Color screenGrantedColor = new Color(0f, 0.62f, 0.07f);

        [Tooltip("锁定时只改变文字颜色，不改变底色。")]
        [SerializeField] private Color lockedTextColor = Color.red;

        [Header("SoundFx")]
        [SerializeField] private AudioClip buttonClickedSfx;
        [SerializeField] private AudioClip accessDeniedSfx;
        [SerializeField] private AudioClip accessGrantedSfx;

        [Tooltip("还没到指定任务阶段时按键播放的音效，可不填。")]
        [SerializeField] private AudioClip lockedSfx;

        [Header("Component References")]
        [SerializeField] private Renderer panelMesh;
        [SerializeField] private TMP_Text keypadDisplayText;
        [SerializeField] private AudioSource audioSource;

        private string currentInput;
        private bool displayingResult = false;
        private bool accessWasGranted = false;

        private Coroutine lockedRoutine;
        private Color normalTextColor = Color.white;

        private float lastLockedSoundTime = -999f;

        private void Awake()
        {
            if (keypadDisplayText != null)
                normalTextColor = keypadDisplayText.color;

            ClearInput();

            if (panelMesh != null)
                panelMesh.material.SetVector("_EmissionColor", screenNormalColor * screenIntensity);
        }

        public void AddInput(string input)
        {
            if (!CanUseKeypad())
            {
                ShowLockedFeedback();
                return;
            }

            if (audioSource != null && buttonClickedSfx != null)
                audioSource.PlayOneShot(buttonClickedSfx);

            if (displayingResult || accessWasGranted)
                return;

            switch (input)
            {
                case "enter":
                    CheckCombo();
                    break;

                default:
                    if (currentInput != null && currentInput.Length == 9)
                        return;

                    currentInput += input;

                    if (keypadDisplayText != null)
                    {
                        keypadDisplayText.color = normalTextColor;
                        keypadDisplayText.text = currentInput;
                    }

                    break;
            }
        }

        public void CheckCombo()
        {
            if (!CanUseKeypad())
            {
                ShowLockedFeedback();
                return;
            }

            bool granted = string.Equals(currentInput, keypadCombo, StringComparison.Ordinal);

            if (!displayingResult)
                StartCoroutine(DisplayResultRoutine(granted));
        }

        private IEnumerator DisplayResultRoutine(bool granted)
        {
            displayingResult = true;

            if (granted)
                AccessGranted();
            else
                AccessDenied();

            yield return new WaitForSeconds(displayResultTime);

            displayingResult = false;

            if (granted)
                yield break;

            ClearInput();

            if (panelMesh != null)
                panelMesh.material.SetVector("_EmissionColor", screenNormalColor * screenIntensity);
        }

        private bool CanUseKeypad()
        {
            if (!lockUntilTaskStage)
                return true;

            if (TaskChainManager.Instance == null)
            {
                Debug.LogWarning($"{name}: 没有找到 TaskChainManager，密码键盘暂时不可用。");
                return false;
            }

            return TaskChainManager.Instance.IsCurrentTask(requiredTaskId);
        }

        private void ShowLockedFeedback()
        {
            TryPlayLockedSound();

            if (!showLockedText)
                return;

            if (lockedRoutine != null)
                StopCoroutine(lockedRoutine);

            lockedRoutine = StartCoroutine(ShowLockedRoutine());
        }

        private void TryPlayLockedSound()
        {
            if (audioSource == null || lockedSfx == null)
                return;

            if (Time.unscaledTime - lastLockedSoundTime < lockedSoundCooldown)
                return;

            lastLockedSoundTime = Time.unscaledTime;
            audioSource.PlayOneShot(lockedSfx);
        }

        private IEnumerator ShowLockedRoutine()
        {
            displayingResult = true;

            if (keypadDisplayText != null)
            {
                keypadDisplayText.color = lockedTextColor;
                keypadDisplayText.text = lockedText;
            }

            // 锁定时不改变底色，保持原来的黄色 / 橙色
            if (panelMesh != null)
                panelMesh.material.SetVector("_EmissionColor", screenNormalColor * screenIntensity);

            if (lockedTextTime > 0f)
                yield return new WaitForSeconds(lockedTextTime);

            displayingResult = false;

            ClearInput();

            if (panelMesh != null)
                panelMesh.material.SetVector("_EmissionColor", screenNormalColor * screenIntensity);

            lockedRoutine = null;
        }

        private void AccessDenied()
        {
            if (keypadDisplayText != null)
            {
                keypadDisplayText.color = normalTextColor;
                keypadDisplayText.text = accessDeniedText;
            }

            onAccessDenied?.Invoke();

            if (panelMesh != null)
                panelMesh.material.SetVector("_EmissionColor", screenDeniedColor * screenIntensity);

            if (audioSource != null && accessDeniedSfx != null)
                audioSource.PlayOneShot(accessDeniedSfx);
        }

        private void ClearInput()
        {
            currentInput = "";

            if (keypadDisplayText != null)
            {
                keypadDisplayText.color = normalTextColor;
                keypadDisplayText.text = currentInput;
            }
        }

        private void AccessGranted()
        {
            accessWasGranted = true;

            if (keypadDisplayText != null)
            {
                keypadDisplayText.color = normalTextColor;
                keypadDisplayText.text = accessGrantedText;
            }

            onAccessGranted?.Invoke();

            if (panelMesh != null)
                panelMesh.material.SetVector("_EmissionColor", screenGrantedColor * screenIntensity);

            if (audioSource != null && accessGrantedSfx != null)
                audioSource.PlayOneShot(accessGrantedSfx);
        }
    }
}