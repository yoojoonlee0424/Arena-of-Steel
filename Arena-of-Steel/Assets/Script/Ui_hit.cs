using UnityEngine;

public class Ui_hit : MonoBehaviour
{
    public static Ui_hit instance;
    public GameObject hitUi;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        instance = this;
    }

    // Update is called once per frame
    public void InstantiateHitUi()
    {
        Instantiate(hitUi, transform);
    }
}
