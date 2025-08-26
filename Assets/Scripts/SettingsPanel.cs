using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SettingsPanel : MonoBehaviour
{
    public static SettingsPanel Instance;

    [Header("UI控件引用")]
    public GameObject panelObject;
    public Slider bgmVolumeSlider;
    public Toggle bgmMuteToggle;
    public Slider sfxVolumeSlider;
    public Toggle sfxMuteToggle;
    public Button closeButton;

    private const string BGM_VOLUME_KEY = "BGM_Volume";
    private const string BGM_MUTE_KEY = "BGM_Mute";
    private const string SFX_VOLUME_KEY = "SFX_Volume";
    private const string SFX_MUTE_KEY = "SFX_Mute";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChange);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChange);
        bgmMuteToggle.onValueChanged.AddListener(OnBGMMuteChange);
        sfxMuteToggle.onValueChanged.AddListener(OnSFXMuteChange);
        closeButton.onClick.AddListener(ClosePanel);
    }

    void Start()
    {
        panelObject.SetActive(false);
        LoadSettings();
    }

    private void LoadSettings()
    {
        bgmVolumeSlider.value = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 0.1f); // 与AudioManager保持一致
        sfxVolumeSlider.value = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.4f); // 与AudioManager保持一致

        // 【逻辑反转】加载静音状态。0代表静音(mute=true), 1代表有声(mute=false)。
        // 所以 Toggle.isOn 应该与 mute 的状态相反。
        bool bgmMuted = PlayerPrefs.GetInt(BGM_MUTE_KEY, 0) == 1; // 0=NotMuted, 1=Muted
        bgmMuteToggle.isOn = !bgmMuted; // isON = true when NotMuted

        bool sfxMuted = PlayerPrefs.GetInt(SFX_MUTE_KEY, 0) == 1;
        sfxMuteToggle.isOn = !sfxMuted;

        ApplyAllSettings();
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, bgmVolumeSlider.value);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolumeSlider.value);

        // 【逻辑反转】保存静音状态。当Toggle被勾选(isOn=true)时，代表“不静音”，应保存0。
        PlayerPrefs.SetInt(BGM_MUTE_KEY, !bgmMuteToggle.isOn ? 1 : 0); // Not IsOn -> Mute (1)
        PlayerPrefs.SetInt(SFX_MUTE_KEY, !sfxMuteToggle.isOn ? 1 : 0);

        PlayerPrefs.Save();
    }

    private void OnBGMVolumeChange(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.bgmSource.volume = value;
        SaveSettings();
    }

    private void OnSFXVolumeChange(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.sfxSource.volume = value;
        SaveSettings();
    }

    // 【逻辑反转】isSoundOn 参数代表Toggle的isOn状态
    private void OnBGMMuteChange(bool isSoundOn)
    {
        if (AudioManager.Instance != null)
            // 当 isSoundOn 为 true (勾选) 时，mute 应为 false (不静音)
            AudioManager.Instance.bgmSource.mute = !isSoundOn;
        SaveSettings();
    }

    // 【逻辑反转】isSoundOn 参数代表Toggle的isOn状态
    private void OnSFXMuteChange(bool isSoundOn)
    {
        if (AudioManager.Instance != null)
            // 当 isSoundOn 为 true (勾选) 时，mute 应为 false (不静音)
            AudioManager.Instance.sfxSource.mute = !isSoundOn;
        SaveSettings();
    }

    private void ApplyAllSettings()
    {
        OnBGMVolumeChange(bgmVolumeSlider.value);
        OnBGMMuteChange(bgmMuteToggle.isOn);
        OnSFXVolumeChange(sfxVolumeSlider.value);
        OnSFXMuteChange(sfxMuteToggle.isOn);
    }

    public void OpenPanel()
    {
        panelObject.SetActive(true);
        if (GameBoard.Instance != null)
            GameBoard.Instance.currentState = GameBoard.GameState.wait;

        panelObject.transform.DOScale(1, 0.3f).From(0.5f).SetEase(Ease.OutBack);
    }

    public void ClosePanel()
    {

        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameFlowState.PlayerTurn)
        {
            if (GameBoard.Instance != null)
            {
                GameBoard.Instance.SetBoardState(GameBoard.GameState.move);
            }
        }

        panelObject.transform.DOScale(0, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
        {
            panelObject.SetActive(false);
        });
    }
}