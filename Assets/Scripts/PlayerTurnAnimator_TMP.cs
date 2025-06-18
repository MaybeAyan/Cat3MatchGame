using UnityEngine;
using TMPro; // ���� TextMeshPro �������ռ�
using DG.Tweening;

public class PlayerTurnAnimator_TMP : MonoBehaviour
{
    [Header("������������")]
    public RectTransform mainContainer;
    public RectTransform cloudBoard;
    public TMP_Text notificationText;

    [Header("��������")]
    public float boardEnterDuration = 0.5f;
    public float letterFallDuration = 0.4f;
    public float letterStaggerDelay = 0.1f;
    public float finalPopStrength = 0.1f;
    public float exitDuration = 0.3f;

    private Vector2 boardInitialOffscreenPos;
    private Sequence currentSequence;

    void Awake()
    {
        boardInitialOffscreenPos = new Vector2(0, Screen.height / 2 + cloudBoard.sizeDelta.y);
        cloudBoard.anchoredPosition = boardInitialOffscreenPos;
        mainContainer.gameObject.SetActive(false);
    }

    /// <summary>
    /// ���Ŷ�����������Ҫ��ʾ������
    /// </summary>
    /// <param name="textToShow">Ҫ��ʾ���ı�����</param>
    public void PlayAnimation(string textToShow)
    {
        Debug.Log(textToShow);
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
        }

        // --- ׼������ ---
        mainContainer.gameObject.SetActive(true);
        cloudBoard.anchoredPosition = boardInitialOffscreenPos;
        notificationText.text = textToShow; // �����ı�
        notificationText.alpha = 0; // ��ʼʱ��ȫ͸��

        // ǿ��TextMeshPro�������¼�����Ϣ���������ǲ��ܷ��ʵ�ÿ���ַ���λ��
        notificationText.ForceMeshUpdate();

        // --- ������������ ---
        currentSequence = DOTween.Sequence();

        // 1. ľ���볡
        currentSequence.Append(cloudBoard.DOAnchorPos(Vector2.zero, boardEnterDuration).SetEase(Ease.OutBounce));

        // 2. ���ֶ��� (���ļ���)
        // �������������嵭�룬ͬʱ��ÿ���ַ�һ�����ϵ��µ�λ�ƶ���
        currentSequence.Insert(0.2f, notificationText.DOFade(1, letterFallDuration));

        TMP_TextInfo textInfo = notificationText.textInfo;
        Debug.Log(textInfo);
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue; // �����ո�Ȳ��ɼ��ַ�

            int charIndex = i;
            // ��ȡÿ���ַ��Ķ���
            var verts = textInfo.meshInfo[textInfo.characterInfo[charIndex].materialReferenceIndex].vertices;

            // Ϊÿ���ַ�����һ��������λ�ƶ���
            for (int j = 0; j < 4; j++)
            {
                var origPos = verts[textInfo.characterInfo[charIndex].vertexIndex + j];
                var newPos = origPos + new Vector3(0, 50, 0); // ���Ϸ�50����λ����ʼ

                DOTween.To(() => newPos, x => newPos = x, origPos, letterFallDuration)
                    .SetDelay(0.2f + (charIndex * letterStaggerDelay)) // �����ӳ�
                    .SetEase(Ease.OutBounce)
                    .OnUpdate(() => {
                        // �ڶ���ÿһ֡���¶���λ��
                        var currentVerts = textInfo.meshInfo[textInfo.characterInfo[charIndex].materialReferenceIndex].vertices;
                        currentVerts[textInfo.characterInfo[charIndex].vertexIndex + j] = newPos;
                        notificationText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                    });
            }
        }

        // 3. ����Ŵ�ص�
        currentSequence.Append(mainContainer.DOPunchScale(new Vector3(finalPopStrength, finalPopStrength, 0), 0.4f, 10, 1));
    }

    // �˳��������Լ�Ϊ����ɳ�
    public void PlayExitAnimation()
    {
        // ... (�˳������߼��������ƣ�������mainContainer���߻򵭳�)
    }
}