using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UmbrellaSystem : MonoBehaviour
{
    public enum UmbrellaState { Inactive, Launched, Hovering, Closing }

    [Header("Umbrella Settings")]
    public GameObject umbrellaObject;       // 场上唯一的伞（初始时应为 Inactive）
    public float launchSpeed = 5f;            // 发射时的最大速度
    public float accelerationTime = 0.3f;     // 动态加速时间
    public float maxDistance = 25f;           // 飞行距离超过该值时自动停止
    public GameObject player;               // 玩家对象

    [Header("Animation")]
    public List<Animator> umbrellaAnimators; // 需要控制的 Animator 列表

    [Header("Animation Trigger Names")]
    public string launchTriggerName = "open";   // 开启动画的 Trigger 名称
    public string recallTriggerName = "close";    // 关闭动画的 Trigger 名称

    private UmbrellaState currentState = UmbrellaState.Inactive;
    private Rigidbody umbrellaRb;
    private Vector3 launchOrigin;
    private Coroutine moveCoroutine;

    void Start()
    {
        if (umbrellaObject != null)
        {
            umbrellaRb = umbrellaObject.GetComponent<Rigidbody>();
            // 初始时隐藏伞
            umbrellaObject.SetActive(false);
            currentState = UmbrellaState.Inactive;
        }
        else
        {
            Debug.LogError("请在 Inspector 中指定 umbrellaObject！");
        }
    }

    void Update()
    {
        // 按下 F 键：如果伞处于 Inactive 状态，则启用并发射；否则传送玩家到伞的位置
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (currentState == UmbrellaState.Inactive)
            {
                LaunchUmbrella();
            }
            else
            {
                TeleportToUmbrella();
            }
        }

        // 按下 E 键：
        // 1. 如果伞正在飞行中（Launched），则立即停止，进入悬停状态；
        // 2. 如果伞处于悬停状态，则播放关闭动画，关闭伞
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentState == UmbrellaState.Launched)
            {
                StopUmbrella();
            }
            else if (currentState == UmbrellaState.Hovering)
            {
                StartCoroutine(CloseUmbrella());
            }
        }

        // 若伞正在飞行中，自动检测距离超过设定值则停止
        if (currentState == UmbrellaState.Launched)
        {
            float distance = Vector3.Distance(umbrellaObject.transform.position, launchOrigin);
            if (distance >= maxDistance)
            {
                StopUmbrella();
            }
        }
    }

    // 发射伞：将伞传送到玩家位置，启用后按摄像机方向发射
    void LaunchUmbrella()
    {
        // 将伞瞬间传送到玩家位置并启用
        umbrellaObject.transform.position = player.transform.position;
        umbrellaObject.SetActive(true);

        currentState = UmbrellaState.Launched;
        if (umbrellaRb == null)
            umbrellaRb = umbrellaObject.GetComponent<Rigidbody>();

        umbrellaRb.isKinematic = false;

        // 获取摄像机中心的射线方向，并将 Y 分量置 0
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        Vector3 direction = ray.direction;
        direction.y = 0f;
        direction.Normalize();

        umbrellaObject.transform.rotation = Quaternion.LookRotation(direction);
        launchOrigin = umbrellaObject.transform.position;

        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(AccelerateToVelocity(direction * launchSpeed));

        // 播放开启动画
        TriggerUmbrellaAnim(launchTriggerName);
    }

    // 传送玩家到伞的位置，并关闭伞
    void TeleportToUmbrella()
    {
        if (player != null && umbrellaObject != null && currentState != UmbrellaState.Inactive)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = umbrellaObject.transform.position;
            if (cc != null) cc.enabled = true;

            // 关闭伞
            umbrellaObject.SetActive(false);
            currentState = UmbrellaState.Inactive;
        }
    }

    // 停止伞的飞行，进入悬停状态
    void StopUmbrella()
    {
        if (umbrellaRb != null)
        {
            umbrellaRb.linearVelocity = Vector3.zero;
            umbrellaRb.isKinematic = true;
            currentState = UmbrellaState.Hovering;
        }
    }

    // 播放关闭动画后关闭伞
    IEnumerator CloseUmbrella()
    {
        currentState = UmbrellaState.Closing;
        TriggerUmbrellaAnim(recallTriggerName);
        yield return new WaitForSeconds(0.5f);  // 根据动画时长调整等待时间
        umbrellaObject.SetActive(false);
        currentState = UmbrellaState.Inactive;
    }

    // 动态加速协程：在 accelerationTime 内平滑过渡到目标速度
    IEnumerator AccelerateToVelocity(Vector3 targetVelocity)
    {
        float timer = 0f;
        Vector3 startVel = umbrellaRb.linearVelocity;
        while (timer < accelerationTime)
        {
            float t = timer / accelerationTime;
            umbrellaRb.linearVelocity = Vector3.Lerp(startVel, targetVelocity, t);
            timer += Time.deltaTime;
            yield return null;
        }
        umbrellaRb.linearVelocity = targetVelocity;
    }

    // 触发 Animator 列表中所有 Animator 的指定 Trigger
    void TriggerUmbrellaAnim(string triggerName)
    {
        foreach (Animator anim in umbrellaAnimators)
        {
            if (anim != null)
            {
                anim.SetTrigger(triggerName);
                Debug.Log($"Triggered '{triggerName}' on {anim.gameObject.name}");
            }
        }
    }
}