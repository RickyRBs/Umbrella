using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UmbrellaSystem : MonoBehaviour
{
    public enum UmbrellaState { Inactive, Launched, Hovering, Closing }

    [Header("Umbrella Settings")]
    public GameObject umbrellaObject;       // 场上唯一的伞（初始时应为 Inactive）
    public GameObject umbrellaPreviewObject; // 新增的预览伞对象
    public GameObject shortPressDropPoint;    // 短按落伞位置
    public float launchSpeed = 10f;           // 发射时的最大速度
    public float accelerationTime = 10f;      // 动态加速时间
    public float maxDistance = 25f;           // 最大飞行距离
    private float maxDistanceThisShot = 0f;   // 本次发射的最大距离
    public GameObject umbrellaLaunchPoint;               // 玩家对象
    public GameObject PlayerforTranport; // 传送的目标对象

    [Header("Animation")]
    public List<Animator> umbrellaAnimators;    // 需要控制的 Animator 列表

    [Header("Animation Trigger Names")]
    public string launchTriggerName = "open";   // 开启动画的 Trigger 名称
    public string recallTriggerName = "close";    // 关闭动画的 Trigger 名称

    public Image chargeProgressBar;

    public GameObject landingMarker;
    public int predictionSteps = 30;
    public float timeStep = 0.1f;
    public LayerMask collisionMask;

    private UmbrellaState currentState = UmbrellaState.Inactive;
    private Rigidbody umbrellaRb;
    private Vector3 launchOrigin;
    private Coroutine moveCoroutine;

    // 蓄力相关变量
    private float chargeTimer = 0f;
    private bool isCharging = false;
    private readonly float maxChargeTime = 3f;  // 最大蓄力时间

    // 用于传送的协程引用
    private Coroutine teleportCoroutine = null;

    // 用于检测伞是否移动
    private Vector3 lastPosition;
    private float movementCheckTimer = 0f;
    private bool isUmbrellaMoving = false;
    private const float movementCheckInterval = 0.5f;
    private const float movementThreshold = 0.01f; // 位置变化小于此值视为静止

    //audio 区域
    public AudioSource Charge;
    public AudioSource Fly;
    public AudioSource Close;
    public AudioSource Open;

    private bool isPreviewing = false; // 新增字段
    private float previewChargeRatio = 0f; // 新增字段

    void Start()
    {
        if (umbrellaObject != null)
        {
            umbrellaRb = umbrellaObject.GetComponent<Rigidbody>();
            // 初始时隐藏伞
            umbrellaObject.SetActive(false);
            umbrellaPreviewObject.SetActive(false); // 新增行，隐藏预览伞
            currentState = UmbrellaState.Inactive;
            lastPosition = umbrellaObject.transform.position;
        }
        else
        {
            Debug.LogError("请在 Inspector 中指定 umbrellaObject！");
        }
    }

    void Update()
    {
        // 当伞处于未部署状态时，使用左键进行蓄力
        if (currentState == UmbrellaState.Inactive)
        {
            if (Input.GetMouseButtonDown(0))
            {
                isCharging = true;
                isPreviewing = true; // 进入预览状态
                chargeTimer = 0f;
                Charge.Play();
            }
            
            if (isCharging && Input.GetMouseButton(0))
            {
                chargeTimer += Time.deltaTime;
                chargeTimer = Mathf.Min(chargeTimer, maxChargeTime);

                float chargeRatio = Mathf.Clamp01((chargeTimer - 0.5f) / (maxChargeTime - 0.5f));
                previewChargeRatio = chargeRatio; // 动态赋值
                Vector3 dir = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0)).direction;
                dir.y = 0f;
                dir.Normalize();
                Vector3 velocity = dir * launchSpeed * chargeRatio;


                ShowLandingMarker(umbrellaLaunchPoint.transform.position + Vector3.up * 3, velocity, chargeRatio);
                // if (chargeTimer >= 0.5f) // 新增判断
                // {
                //     ShowLandingMarker(umbrellaLaunchPoint.transform.position + Vector3.up * 3, velocity, chargeRatio);
                // }
            }
            if (Input.GetMouseButtonUp(0) && isCharging)
            {
                
                if (chargeTimer < 0.5f)
                {
                    DropUmbrella();
                }
                else
                {
                    float chargeRatio = Mathf.Clamp01((chargeTimer - 0.5f) / (maxChargeTime - 0.5f));
                    LaunchUmbrella(chargeRatio);
                }
                isCharging = false;
                isPreviewing = false; // 退出预览状态
                umbrellaPreviewObject.SetActive(false); // 隐藏预览伞
                Charge.Stop();
                if (landingMarker != null)
                    landingMarker.SetActive(false);
            }
        }
        else if (isPreviewing) // 如果是预览状态，实时更新伞的位置
        {
            Vector3 dir = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0)).direction;
            dir.y = 0f;
            dir.Normalize();
            umbrellaPreviewObject.transform.position = umbrellaLaunchPoint.transform.position + Vector3.up * 3 + dir * launchSpeed * previewChargeRatio; // 使用预览伞
        }
        else
        {
            // 当伞已存在时，按下左键延迟传送
            if (Input.GetMouseButtonDown(0) && teleportCoroutine == null)
            {
                // 传送前先停止伞的运动
                if (umbrellaRb != null)
                {
                    TriggerUmbrellaAnim(recallTriggerName);
                    umbrellaRb.linearVelocity = Vector3.zero;
                    umbrellaRb.isKinematic = true;
                    Close.Play();
                }
                teleportCoroutine = StartCoroutine(TeleportAndCloseUmbrella());
            }
        }

        // 更新进度条显示
        if (chargeProgressBar != null)
        {
            chargeProgressBar.fillAmount = isCharging ? (chargeTimer / maxChargeTime) : 0f;
        }

        // 右键操作：判断伞是否在移动，若在移动则停止，反之则播放收伞动画
        if (Input.GetMouseButtonDown(1) && umbrellaObject.activeSelf)
        {
            if (isUmbrellaMoving)
            {
                StopUmbrella();
            }
            else
            {
                StartCoroutine(CloseUmbrella());
            }
        }

        // 伞飞行时自动检测距离超过本次飞行距离则停止
        if (currentState == UmbrellaState.Launched)
        {
            float distance = Vector3.Distance(umbrellaObject.transform.position, launchOrigin);
            if (distance >= maxDistanceThisShot)
            {
                StopUmbrella();
            }
        }

        // 每隔 movementCheckInterval 秒检测伞是否在移动
        if (umbrellaObject.activeSelf)
        {
            movementCheckTimer += Time.deltaTime;
            if (movementCheckTimer >= movementCheckInterval)
            {
                float distanceMoved = Vector3.Distance(umbrellaObject.transform.position, lastPosition);
                isUmbrellaMoving = distanceMoved > movementThreshold;
                lastPosition = umbrellaObject.transform.position;
                
                movementCheckTimer = 0f;
            }
        }
        else
        {
            isUmbrellaMoving = false;
        }
    }

    // 直接放下伞，不施加发射力度
    void DropUmbrella()
    {
        umbrellaObject.transform.position = shortPressDropPoint.transform.position;
        umbrellaObject.SetActive(true);
        currentState = UmbrellaState.Hovering;
        TriggerUmbrellaAnim(launchTriggerName);
        if (umbrellaRb != null)
        {
            umbrellaRb.linearVelocity = Vector3.zero;
            umbrellaRb.isKinematic = true;
        }
        if (landingMarker != null)
            landingMarker.SetActive(false);
    }

    // 发射伞：将伞传送到玩家位置后，根据摄像机方向施加发射力度
    void LaunchUmbrella(float chargeRatio)
    {
        Fly.Play();
        Open.Play();
        // 将伞位置设为玩家位置向上偏移 3 个单位
        Vector3 launchPos = umbrellaLaunchPoint.transform.position + Vector3.up * 3;
        umbrellaObject.transform.position = launchPos;
        umbrellaObject.SetActive(true);
        currentState = UmbrellaState.Launched;
        if (umbrellaRb == null)
            umbrellaRb = umbrellaObject.GetComponent<Rigidbody>();
        umbrellaRb.isKinematic = false;

        // 获取摄像机中心的射线方向
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        Vector3 direction = ray.direction;
        direction.y = 0f;
        direction.Normalize();

        umbrellaObject.transform.rotation = Quaternion.LookRotation(direction);
        launchOrigin = umbrellaObject.transform.position;

        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        Vector3 targetVelocity = direction * launchSpeed * chargeRatio;
        moveCoroutine = StartCoroutine(AccelerateToVelocity(targetVelocity));

        // 根据蓄力时间设置本次飞行距离
        maxDistanceThisShot = maxDistance * chargeRatio;

        TriggerUmbrellaAnim(launchTriggerName);
        if (landingMarker != null)
            landingMarker.SetActive(false);
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
        yield return new WaitForSeconds(1.2f);
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

    // 延迟1秒后传送玩家到伞的位置，传送时停止伞的运动，并播放收伞动画后关闭伞
    IEnumerator TeleportAndCloseUmbrella()
    {
        // 等待0.5秒，让玩家感觉到延迟
        yield return new WaitForSeconds(0.5f);

        // 触发收伞动画
        TriggerUmbrellaAnim(recallTriggerName);

        // 再等待0.5秒，让动画有点反馈时间
        yield return new WaitForSeconds(0.5f);

        // 记录伞的位置（传送目标）
        Vector3 teleportPos = umbrellaObject.transform.position;

        // 立即隐藏伞，防止落地时遮挡视野
        umbrellaObject.SetActive(false);
        currentState = UmbrellaState.Inactive;

        // 传送玩家到记录的位置
        CharacterController cc = PlayerforTranport.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        PlayerforTranport.transform.position = teleportPos;
        if (cc != null) cc.enabled = true;

        teleportCoroutine = null;
    }

    // 触发所有 Animator 的指定 Trigger
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

    void ShowLandingMarker(Vector3 startPos, Vector3 initialVelocity, float chargeRatio)
    {
        Debug.Log("⚡ 开始预测落点啦！StartPos: " + startPos + "  Velocity: " + initialVelocity);

        Vector3 currentPos = startPos;
        Vector3 currentVel = initialVelocity;
        float totalDistance = 0f;

        for (int i = 0; i < predictionSteps; i++)
        {
            currentVel += Physics.gravity * timeStep;
            Vector3 nextPos = currentPos + currentVel * timeStep;
            totalDistance += Vector3.Distance(currentPos, nextPos);

            if (totalDistance > maxDistance * chargeRatio)
            {
                break;
            }

            Debug.DrawLine(currentPos, nextPos, Color.red, 1f); // Scene 里可见轨迹线（需开启 Gizmos）

            currentPos = nextPos;
        }

        if (landingMarker != null)
        {
            landingMarker.SetActive(true);
            landingMarker.transform.position = currentPos;
            Debug.Log("📍 设置了 marker 的位置！");
        }
        else
        {
            Debug.LogWarning("⚠️ landingMarker 是空的！");
        }
    }
}