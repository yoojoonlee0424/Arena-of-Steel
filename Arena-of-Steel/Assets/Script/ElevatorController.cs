using System.Collections;
using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    public Transform platform;     // 올라갈 플랫폼 오브젝트
    public Transform lid;          // 열릴 뚜껑 오브젝트

    public Vector3 platformTargetOffset = new Vector3(0, 5f, 0); // 플랫폼이 올라갈 거리
    public Vector3 lidOpenRotation = new Vector3(0, 0, 90f);     // 뚜껑이 열릴 때의 각도

    public float moveDuration = 2f; // 움직이는 데 걸리는 시간 (초)

    private Vector3 platformStartPos;
    private Quaternion lidStartRot;
    private bool isActivated = false;

    void Start()
    {
        // 처음 시작했을 때의 위치와 각도를 기억해 둡니다.
        if (platform != null) platformStartPos = platform.position;
        if (lid != null) lidStartRot = lid.rotation;
    }

    // 플레이어가 Trigger 구역에 들어왔을 때 딱 한 번 실행되는 함수
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("충돌 감지됨! 들어온 오브젝트: " + other.name);

        // 플레이어 태그를 가진 오브젝트가 들어왔을 때만 작동
        if (!isActivated && other.CompareTag("Player"))
        {
            isActivated = true;
            StartCoroutine(OperateElevator());
        }
    }

    // 시간에 따라 부드럽게 움직이게 만드는 코루틴 함수
    IEnumerator OperateElevator()
    {
        float elapsedTime = 0f;

        Vector3 platformTargetPos = platformStartPos + platformTargetOffset;
        Quaternion lidTargetRot = lidStartRot * Quaternion.Euler(lidOpenRotation);

        while (elapsedTime < moveDuration)
        {
            float t = elapsedTime / moveDuration; // 0에서 1 사이의 값 (진행률)

            // 1. 플랫폼 위로 이동시키기 (Lerp: 두 값 사이를 부드럽게 채워줌)
            if (platform != null)
            {
                platform.position = Vector3.Lerp(platformStartPos, platformTargetPos, t);
            }

            // 2. 뚜껑 회전시키기
            if (lid != null)
            {
                lid.rotation = Quaternion.Slerp(lidStartRot, lidTargetRot, t);
            }

            elapsedTime += Time.deltaTime; // 흐른 시간 더하기
            yield return null; // 다음 프레임까지 대기
        }

        // 마지막에 위치와 각도를 정확하게 딱 맞춰줌
        if (platform != null) platform.position = platformTargetPos;
        if (lid != null) lid.rotation = lidTargetRot;
    }


}