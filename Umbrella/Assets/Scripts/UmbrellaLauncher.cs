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
    public GameObject shortPressLaunchPoint;  // 新增：短按发射起点（可选）
    public float launchSpeed = 50f;           // 发射时的最大速度
    public float accelerationTime = 0.3f;      // 动态加速时间
    public float maxDistance = 25f;           // 最大飞行距离
    private float maxDistanceThisShot = 0f;   // 本次发射的最大距离
    public GameObject umbrellaLaunchPoint;               // 玩家对象
    public GameObject PlayerforTranport; // 传送的目标对象

    [Header("Umbrella Animation")]
    public List<Animator> umbrellaAnimators;    // 需要控制的 Animator 列表

    [Header("Animation Trigger Names")]
    public string launchTriggerName = "open";   // 开启动画的 Trigger 名称
    public string recallTriggerName = "close";    // 关闭动画的 Trigger 名称

        // Tutorial 触发器（首次触发判断）
    private bool firstDropTriggered = false;
    private bool firstLaunchTriggered = false;
    private bool firstRecallTriggered = false;
    private bool firstTeleportTriggered = false;
    [Header("TutorialAnimation")]
    public List<Animator> TutorialAnimator;
    public string Tutorial1 = "click";    // 第一次原地放下伞
    public string Tutorial2 = "charge";   // 第一次发射伞
    public string Tutorial3 = "back";     // 第一次收伞
    public string Tutorial4 = "teleport"; // 第一次传送

    public Image chargeProgressBar;

    [Header("Trajectory Prediction")]
    public GameObject landingMarker;
    public GameObject landingMarkerOriginPoint; // 轨迹预测的发射起点 
    public int predictionSteps = 30;
    public float predictionTimeStep = 0.1f;    // 重命名为专用于预测的时间步长
    public LayerMask predictionCollisionMask;  // 重命名为预测专用碰撞层
    public bool showDebugTrajectory = true;    // 是否显示调试轨迹线
    public Color trajectoryColor = Color.red;  // 轨迹线颜色
    public bool useLinearProjection = true;    // 新增：是否使用直线投影（无重力）
    public float fixedProjectionHeight = 0f;   // 新增：固定投影高度（0表示使用起点高度）

    [Header("Actual Launch Physics")]
    public float actualGravityScale = 1.0f;    // 实际发射时的重力比例调节
    public bool useUnityPhysics = true;        // 是否使用Unity内置物理引擎

    [Header("Collision Settings")]
    public bool stabilizeAfterCollision = true;  // 是否在碰撞后稳定伞的位置
    public float collisionStabilizeDelay = 0.2f; // 碰撞后多久开始稳定化(秒)
    public bool maintainHeight = true;           // 碰撞后是否保持高度不变
    public LayerMask collisionDetectionMask;     // 用于检测碰撞的层

    private bool hasCollided = false;            // 本次发射是否已发生碰撞
    private Coroutine stabilizeCoroutine = null; // 稳定化协程引用

    private UmbrellaState currentState = UmbrellaState.Inactive;
    private Rigidbody umbrellaRb;
    private Vector3 launchOrigin;
    private Coroutine moveCoroutine;

    // 蓄力相关变量
    private float chargeTimer = 0f;
    private bool isCharging = false;
    private readonly float maxChargeTime = 1.5f;  // 最大蓄力时间

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

    [Header("UI Elements")]
    public GameObject crosshair; // 准心对象
    public GameObject chargeBar; // 蓄力条对象

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
            
            // 添加碰撞检测组件
            if (umbrellaRb != null && !umbrellaObject.GetComponent<UmbrellaCollisionHandler>())
            {
                UmbrellaCollisionHandler collisionHandler = umbrellaObject.AddComponent<UmbrellaCollisionHandler>();
                collisionHandler.umbrellaSystem = this;
            }
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
                
                // 获取预测方向和起点
                Vector3 predictionDir = GetPredictionDirection(); // 确保在直线模式下y=0
                Vector3 predictionOrigin = GetPredictionOrigin();
                Vector3 predictionVelocity = predictionDir * launchSpeed * chargeRatio;
                
                // 显示轨迹预测，独立计算
                ShowLandingMarker(predictionOrigin, predictionVelocity, chargeRatio);
                
                // 使用新的稳定方法更新预览伞
                UpdatePreviewUmbrella(predictionOrigin, predictionDir, chargeTimer >= 0.2f && isPreviewing);
            }
            if (Input.GetMouseButtonUp(0) && isCharging)
            {
                
                if (chargeTimer < 0.2f)
                {
                    DropUmbrella();
                }
                else
                {
                    float chargeRatio = Mathf.Clamp01((chargeTimer - 0.2f) / (maxChargeTime - 0.5f));
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
            Vector3 dir = GetPredictionDirection(); // 使用预测方向，而不是发射方向
            Vector3 pos = GetPredictionOrigin();    // 使用预测起点
            UpdatePreviewUmbrella(pos, dir, true);  // 使用更稳定的方法更新
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

        // 根据伞的状态控制准心和蓄力条的显示
        if (crosshair != null && chargeBar != null)
        {
            bool isUmbrellaActive = umbrellaObject.activeSelf;
            crosshair.SetActive(!isUmbrellaActive);
            chargeBar.SetActive(!isUmbrellaActive);
        }
    }

    // 直接放下伞，不施加发射力度
    void DropUmbrella()
    {
        // 确定使用哪个位置放置伞
        Vector3 dropPosition;
        if (shortPressLaunchPoint != null)
        {
            // 使用新的自定义发射点
            dropPosition = shortPressLaunchPoint.transform.position;
        }
        else
        {
            // 使用原来的短按放置点
            dropPosition = shortPressDropPoint.transform.position;
        }
        
        umbrellaObject.transform.position = dropPosition;
        umbrellaObject.SetActive(true);
        currentState = UmbrellaState.Hovering;
        TriggerUmbrellaAnim(launchTriggerName);
        if (!firstDropTriggered)
        {
            TriggerTutorialAnim(Tutorial1);
            firstDropTriggered = true;
        }
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
        // 重置碰撞状态
        hasCollided = false;
        if (stabilizeCoroutine != null)
        {
            StopCoroutine(stabilizeCoroutine);
            stabilizeCoroutine = null;
        }
        
        // 原有代码保持不变
        Fly.Play();
        Open.Play();
        
        // 使用实际发射起点和方向，与预测分离
        Vector3 launchPos = GetLaunchOrigin();
        Vector3 direction = GetLaunchDirection();
        
        umbrellaObject.transform.position = launchPos;
        umbrellaObject.SetActive(true);
        currentState = UmbrellaState.Launched;
        
        if (umbrellaRb == null)
            umbrellaRb = umbrellaObject.GetComponent<Rigidbody>();
            
        umbrellaRb.isKinematic = false;
        
        // 可选:修改实际重力
        if (!useUnityPhysics && actualGravityScale != 1.0f)
        {
            umbrellaRb.useGravity = false; // 停用Unity重力，使用自定义重力
        }
        else
        {
            umbrellaRb.useGravity = true;
        }

        umbrellaObject.transform.rotation = Quaternion.LookRotation(direction);
        launchOrigin = umbrellaObject.transform.position;

        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        Vector3 targetVelocity = direction * launchSpeed * chargeRatio;
        moveCoroutine = StartCoroutine(AccelerateToVelocity(targetVelocity));

        // 根据蓄力时间设置本次飞行距离
        maxDistanceThisShot = maxDistance * chargeRatio;

        TriggerUmbrellaAnim(launchTriggerName);
        // 添加 Tutorial 触发（第一次发射伞）
        if (!firstLaunchTriggered)
        {
            TriggerTutorialAnim(Tutorial2);
            firstLaunchTriggered = true;
        }
        if (landingMarker != null)
            landingMarker.SetActive(false);
        
        // 隐藏所有轨迹预测标记
        HideTrajectoryMarkers();
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

        // 添加 Tutorial 触发（第一次收伞）
        if (!firstRecallTriggered)
        {
            TriggerTutorialAnim(Tutorial3);
            firstRecallTriggered = true;
        }

        TriggerUmbrellaAnim(recallTriggerName);
        yield return new WaitForSeconds(0.3f);
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

    // 延迟0.3秒后传送玩家到伞的位置，传送时停止伞的运动，并播放收伞动画后关闭伞
    IEnumerator TeleportAndCloseUmbrella()
    {
        // 等待0.3秒，让玩家感觉到延迟
        yield return new WaitForSeconds(0.3f);
        if (!firstTeleportTriggered)
        {
            TriggerTutorialAnim(Tutorial4);
            firstTeleportTriggered = true;
        }
        // 触发收伞动画
        TriggerUmbrellaAnim(recallTriggerName);

        // 再等待0.5秒，让动画有点反馈时间
        yield return new WaitForSeconds(0.3f);

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

    // 触发所有 UmbrellaAnimator 的指定 Trigger
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
    // 触发所有 TutorialAnimator 的指定 Trigger
    void TriggerTutorialAnim(string triggerName)
    {
        foreach (Animator anim in TutorialAnimator)
        {
            if (anim != null)
            {
                anim.SetTrigger(triggerName);
                Debug.Log($"Tutorial triggered '{triggerName}' on {anim.gameObject.name}");
            }
        }
    }

    // 为轨迹预测获取发射方向 - 确保与实际发射完全一致
    private Vector3 GetPredictionDirection()
    {
        Vector3 dir = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0)).direction;
        // 在直线投影模式下，保持y=0使其水平方向
        if (useLinearProjection)
            dir.y = 0f;
        dir.Normalize();
        return dir;
    }
    
    // 为轨迹预测获取发射起点
    private Vector3 GetPredictionOrigin()
    {
        // 直接使用实际发射起点，确保一致性
        if (landingMarkerOriginPoint != null)
            return landingMarkerOriginPoint.transform.position;
        return GetLaunchOrigin();
    }
    
    // 为实际发射获取方向
    private Vector3 GetLaunchDirection()
    {
        Vector3 dir = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0)).direction;
        dir.y = 0f;
        dir.Normalize();
        return dir;
    }
    
    // 为实际发射获取起点
    private Vector3 GetLaunchOrigin()
    {
        return umbrellaLaunchPoint.transform.position + Vector3.up * 3;
    }

    // 隐藏所有轨迹标记
    private void HideTrajectoryMarkers()
    {
        if (landingMarker != null)
            landingMarker.SetActive(false);
    }
    
    // 自定义物理更新，可覆盖Unity原生物理
    void FixedUpdate()
    {
        // 如果选择不使用Unity物理，我们可以在这里实现自定义物理
        if (!useUnityPhysics && umbrellaRb != null && !umbrellaRb.isKinematic && umbrellaObject.activeSelf)
        {
            // 应用自定义重力
            umbrellaRb.AddForce(Physics.gravity * actualGravityScale, ForceMode.Acceleration);
        }
    }
    
    // 分离的轨迹预测系统
    void ShowLandingMarker(Vector3 startPos, Vector3 initialVelocity, float chargeRatio)
    {
        if (landingMarker == null && !showDebugTrajectory)
            return;
            
        List<Vector3> trajectoryPoints = PredictTrajectory(startPos, initialVelocity, chargeRatio);
        
        if (trajectoryPoints.Count > 0 && landingMarker != null)
        {
            landingMarker.SetActive(true);
            landingMarker.transform.position = trajectoryPoints[trajectoryPoints.Count - 1];
            
            // 仅在调试模式启用时输出日志
            if (showDebugTrajectory && Time.frameCount % 30 == 0) // 每30帧输出一次
                Debug.Log("📍 Marker位置: " + landingMarker.transform.position);
        }
    }
    
    // 纯粹的轨迹预测逻辑，无临时对象创建
    private List<Vector3> PredictTrajectory(Vector3 startPos, Vector3 initialVelocity, float chargeRatio)
    {
        List<Vector3> points = new List<Vector3>();
        points.Add(startPos);
        
        // 如果使用直线投影，就使用简化的直线计算
        if (useLinearProjection)
        {
            Vector3 direction = initialVelocity.normalized;
            float distance = maxDistance * chargeRatio;  // 直接使用最大距离乘以比率
            
            // 使用更精细的步长绘制轨迹
            int steps = predictionSteps;
            float stepSize = distance / steps;
            
            for (int i = 1; i <= steps; i++)
            {
                float currentDistance = i * stepSize;
                Vector3 nextPoint = startPos + direction * currentDistance;
                
                // 应用固定高度
                if (fixedProjectionHeight != 0)
                {
                    nextPoint.y = fixedProjectionHeight;
                }
                
                // 精确碰撞检测
                RaycastHit hit;
                if (i > 1 && Physics.Linecast(points[points.Count-1], nextPoint, out hit, predictionCollisionMask))
                {
                    points.Add(hit.point);
                    break;
                }
                
                // 绘制调试线
                if (showDebugTrajectory && i > 1)
                {
                    Debug.DrawLine(points[points.Count-1], nextPoint, trajectoryColor, 0.1f);
                }
                
                points.Add(nextPoint);
            }
            
            return points;
        }
        
        // 抛物线投影 - 使用纯数学计算，不创建临时对象
        Vector3 velocity = Vector3.zero;
        Vector3 position = startPos;
        float totalDistance = 0f;
        float maxPredictedDistance = maxDistance * chargeRatio;
        
        // 模拟加速阶段
        float simulationTime = 0f;
        float simulationStep = predictionTimeStep;
        
        // 加速段模拟
        while (simulationTime < accelerationTime && points.Count < predictionSteps)
        {
            float t = simulationTime / accelerationTime;
            velocity = Vector3.Lerp(Vector3.zero, initialVelocity, t);
            Vector3 newPosition = position + velocity * simulationStep;
            
            // 碰撞检测
            RaycastHit hit;
            if (points.Count > 0 && Physics.Linecast(position, newPosition, out hit, predictionCollisionMask))
            {
                points.Add(hit.point);
                if (showDebugTrajectory)
                    Debug.DrawLine(position, hit.point, trajectoryColor, 0.1f);
                break;
            }
            
            // 绘制轨迹
            if (showDebugTrajectory && points.Count > 0)
                Debug.DrawLine(position, newPosition, trajectoryColor, 0.1f);
                
            points.Add(newPosition);
            
            // 检查总距离
            totalDistance += Vector3.Distance(position, newPosition);
            if (totalDistance >= maxPredictedDistance)
                break;
                
            position = newPosition;
            simulationTime += simulationStep;
        }
        
        // 加速结束后的匀速或受重力运动
        if (totalDistance < maxPredictedDistance && points.Count < predictionSteps)
        {
            for (int i = 0; i < predictionSteps - points.Count; i++)
            {
                // 如果使用Unity物理，应用重力
                if (useUnityPhysics)
                    velocity += Physics.gravity * simulationStep * actualGravityScale;
                
                Vector3 newPosition = position + velocity * simulationStep;
                
                // 碰撞检测
                RaycastHit hit;
                if (Physics.Linecast(position, newPosition, out hit, predictionCollisionMask))
                {
                    points.Add(hit.point);
                    if (showDebugTrajectory)
                        Debug.DrawLine(position, hit.point, trajectoryColor, 0.1f);
                    break;
                }
                
                // 绘制轨迹
                if (showDebugTrajectory)
                    Debug.DrawLine(position, newPosition, trajectoryColor, 0.1f);
                    
                points.Add(newPosition);
                
                // 检查总距离
                totalDistance += Vector3.Distance(position, newPosition);
                if (totalDistance >= maxPredictedDistance)
                    break;
                    
                position = newPosition;
            }
        }
        
        return points;
    }
    
    // 改进预览伞的更新，彻底解决抖动问题
    private void UpdatePreviewUmbrella(Vector3 position, Vector3 direction, bool activate)
    {
        if (umbrellaPreviewObject != null)
        {
            umbrellaPreviewObject.SetActive(activate);
            if (activate)
            {
                // 防止位置抖动
                if (Vector3.Distance(umbrellaPreviewObject.transform.position, position) > 0.05f)
                {
                    umbrellaPreviewObject.transform.position = position;
                }
                
                // 防止旋转抖动
                if (Quaternion.Angle(umbrellaPreviewObject.transform.rotation, Quaternion.LookRotation(direction)) > 2.0f)
                {
                    umbrellaPreviewObject.transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }
    }

    // 新增方法：处理伞的碰撞
    public void HandleUmbrellaCollision(Collision collision)
    {
        if (!hasCollided && currentState == UmbrellaState.Launched)
        {
            hasCollided = true;
            
            // 如果启用了碰撞后稳定化
            if (stabilizeAfterCollision)
            {
                // 延迟一小段时间后稳定伞
                if (stabilizeCoroutine != null)
                    StopCoroutine(stabilizeCoroutine);
                    
                stabilizeCoroutine = StartCoroutine(StabilizeAfterCollision());
            }
        }
    }
    
    // 新增协程：碰撞后稳定伞的位置
    private IEnumerator StabilizeAfterCollision()
    {
        // 等待短暂时间让物理系统处理完碰撞反弹
        yield return new WaitForSeconds(collisionStabilizeDelay);
        
        if (umbrellaRb != null && umbrellaObject.activeSelf)
        {
            if (maintainHeight)
            {
                // 保持当前高度，仅修改Y方向速度
                Vector3 velocity = umbrellaRb.linearVelocity;
                velocity.y = 0; // 移除Y方向速度，防止下降
                umbrellaRb.linearVelocity = velocity;
                
                // 可选：若需要完全稳定，可以考虑将伞设为Kinematic
                // umbrellaRb.isKinematic = true;
                // currentState = UmbrellaState.Hovering;
            }
            else
            {
                // 仅减小Y方向下降速度，模拟伞降
                Vector3 velocity = umbrellaRb.linearVelocity;
                if (velocity.y < 0)
                    velocity.y *= 0.3f; // 大幅减小下降速度
                umbrellaRb.linearVelocity = velocity;
            }
        }
    }
}

// 新增类：伞的碰撞处理组件
public class UmbrellaCollisionHandler : MonoBehaviour
{
    [HideInInspector]
    public UmbrellaSystem umbrellaSystem;
    
    private void OnCollisionEnter(Collision collision)
    {
        if (umbrellaSystem != null)
        {
            umbrellaSystem.HandleUmbrellaCollision(collision);
        }
    }
}