using UnityEngine;
using UnityEngine.AI;

public class RagdollControll : MonoBehaviour
{
    public GameObject[] joints;
    public Animator animator;

    public NavMeshAgent agent;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator.enabled = true;
        foreach (GameObject joint in joints)
        {
            joint.GetComponent<Rigidbody>().isKinematic = true;
        }
        agent = GetComponentInParent<NavMeshAgent>();   
    }

    // Update is called once per frame
    public void BeRagdoll()
    {
        animator.enabled = false;
        agent.enabled = false;
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
