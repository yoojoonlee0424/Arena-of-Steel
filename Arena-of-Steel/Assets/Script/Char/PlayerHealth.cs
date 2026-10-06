using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public int health = 100;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Damage")
        {
            DecreaseHealth(10);
        }
    }


    private void DecreaseHealth(int decreaseAmount)
    {
        health -= decreaseAmount;

        Ui_hit.instance.InstantiateHitUi();

        if(health <= 0)
        {
            Die();
        }
    }


    private void Die()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

}
