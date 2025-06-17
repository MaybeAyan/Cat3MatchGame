using System.Collections;
using UnityEngine;
using TMPro; // ����TextMeshPro�������ռ�
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public TextMeshProUGUI turnIndicatorText;

    [Header("��������")]
    public float fadeDuration = 0.5f; // ���뵭��ʱ��
    public float displayDuration = 1.0f; // �ı�ͣ��ʱ��

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // һ��ͨ�õġ�����������ʾ��Ϣ��ʾ����
    public void ShowTurnIndicator(string message, Color color)
    {
        // �����ǰ������ʾ������ֹͣ�ɵĶ���
        turnIndicatorText.DOKill();

        // �����ı�����ɫ
        turnIndicatorText.text = message;
        turnIndicatorText.color = color;

        // �Ƚ�Alpha��Ϊ0��Ϊ������׼��
        turnIndicatorText.alpha = 0f;
        turnIndicatorText.gameObject.SetActive(true);

        // ����һ������->ͣ��->�����Ķ�������
        Sequence sequence = DOTween.Sequence();
        sequence.Append(turnIndicatorText.DOFade(1f, fadeDuration)) // ����
                .AppendInterval(displayDuration) // ͣ��
                .Append(turnIndicatorText.DOFade(0f, fadeDuration)) // ����
                .OnComplete(() =>
                {
                    // ������������ö���
                    turnIndicatorText.gameObject.SetActive(false);
                });
    }
}