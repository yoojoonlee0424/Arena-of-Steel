using UnityEngine;

public class RagdollControll : MonoBehaviour
{
    public GameObject[] joints;
    public Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator.enabled = true;
        foreach (GameObject joint in joints)
        {
            joint.GetComponent<Rigidbody>().isKinematic = true;
        }
    }

    // Update is called once per frame
    public void BeRagdoll()
    {
        animator.enabled = false;

        foreach (GameObject joint in joints)
        {
            joint.GetComponent<Rigidbody>().isKinematic = false;
        }

        Invoke("DestroyObj",15);

    }


    void DestroyObj()
    {
        Destroy(transform.parent.gameObject);
    }
}
