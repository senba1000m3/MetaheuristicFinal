using UnityEngine;
using UnityEngine.UI;

public class ResultPanelController : MonoBehaviour
{
    public Text beginnerText;
    public Text normalText;
    public Text expertText;
    public Text playerText;
    public Text gaInfoText;

    // 讓你在 Inspector 拖入三個 Text

    public void UpdateResult(int agentIndex, string result)
    {
        switch(agentIndex)
        {
            case 0:
                if (beginnerText != null) beginnerText.text = result;
                break;
            case 1:
                if (normalText != null) normalText.text = result;
                break;
            case 2:
                if (expertText != null) expertText.text = result;
                break;
            case 3:
                if (playerText != null) playerText.text = result;
                break;
        }
    }

    public void UpdateGAInfo(string info)
    {
        if (gaInfoText != null)
        {
            gaInfoText.text = info;
        }
    }
}
