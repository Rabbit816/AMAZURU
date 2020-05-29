﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using NavMeshBuilder = UnityEngine.AI.NavMeshBuilder;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerControllerNav : MonoBehaviour
{
    [SerializeField, Tooltip("NavMeshSurfaceのPrefab")] private NavMeshSurface surfacePrefab = null;
    private NavMeshSurface playerNav = null;
    [SerializeField, Tooltip("NavMeshAgent")] private NavMeshAgent playerAgent = null;
    [SerializeField, Tooltip("Rigidbody")] private Rigidbody playerRigid = null;
    [SerializeField, Tooltip("Collider")] private CapsuleCollider playerCollider = null;
    [SerializeField, Tooltip("PlayerのAnimator")] private Animator playerAnimator = null;
    [SerializeField, Tooltip("地面のLayerMask")] private LayerMask layerMask;
    [SerializeField, Tooltip("PlayStateの設定")] private PlayState.GameMode mode = PlayState.GameMode.Play;
    [SerializeField, Tooltip("AnimationEventスクリプト")] private PlayerAnimeEvent animeEvent = null;
    private bool connectPlayState = false;

    // コントローラーの入力
    private float inputX = 0;
    private float inputZ = 0;
    private bool dontInput = false; // 操作入力を無効にするフラグ

    [SerializeField, Header("プレイヤーの移動速度"), Range(0, 10)] private float playerSpeed = 5;
    [SerializeField, Header("プレイヤーの水中移動速度"), Range(0, 10)] private float playerWaterSpeed = 2.5f;
    [SerializeField, Header("プレイヤーの加速度グラフ")] private AnimationCurve curve = null;
    [SerializeField, Header("最高速度到達時間"), Range(0.1f, 2.0f)] private float maxSpeedTime = 0.5f;
    [SerializeField, Header("Rayの長さ"), Range(0, 10)] private float rayLength = 0.5f;

    [SerializeField, Header("NavMesh関連の設定項目"), Tooltip("NavMesh用のLayerMask")] private LayerMask navLayerMask;
    private bool navMeshFlag = false;
    private bool specialMove = false;

    /// <summary>
    /// プレイヤーカメラ
    /// </summary>
    public Camera PlayerCamera { set; private get; } = null;

    /// <summary>
    /// Stageの水オブジェクト
    /// </summary>
    public WaterHi StageWater { set; private get; } = null;

    // プレイヤーの位置(高さ)
    public float PlayerPositionY { private set; get; } = 0;

    // 水が腰の高さになったか
    private bool inWater = false;

    /// <summary>
    /// プレイヤーが水没したことを検知するフラグ
    /// </summary>
    public bool UnderWater { private set; get; } = false;

    /// <summary>
    /// 敵と接触した時のフラグ
    /// </summary>
    public bool ContactEnemy { private set; get; } = false;

    /// <summary>
    /// 一方通行の崖を検知する用のフラグ
    /// </summary>
    public bool CliffFlag { set; private get; } = false;

    // プレイヤーが動き始めてからの経過時間
    private float speedTime = 0;

    // Start is called before the first frame update
    void Start()
    {
        PlayerInit();
    }

    // Update is called once per frame
    void Update()
    {
        GetInputController();
    }

    private void FixedUpdate()
    {
        PlayerMove(true);
    }

    private void Reset()
    {
        playerAgent = GetComponent<NavMeshAgent>();
        playerRigid = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// 初期化
    /// </summary>
    public void PlayerInit()
    {
        if (PlayerCamera == null) { PlayerCamera = Camera.main; }

        connectPlayState = GetPlayState();

        PlayerPositionY = transform.position.y + playerCollider.center.y;

        BakeNavMesh();
    }

    /// <summary>
    /// コントローラ入力を取得
    /// </summary>
    private void GetInputController()
    {
        // キー入力取得
        inputX = Input.GetAxis("Horizontal");
        inputZ = Input.GetAxis("Vertical");
    }

    /// <summary>
    /// プレイヤーの移動処理
    /// </summary>
    private void PlayerMove(bool fixedUpdate)
    {
        if (connectPlayState)
        {
            mode = PlayState.playState.gameMode;
        }
        else
        {
            mode = PlayState.GameMode.Play;
        }

        if (mode == PlayState.GameMode.Play || mode == PlayState.GameMode.Rain)
        {
            bool input;
            float inputSpeed = (Mathf.Abs(inputX) + Mathf.Abs(inputZ)) * 0.5f < 0.5f ? Mathf.Abs(inputX) + Mathf.Abs(inputZ) : 1.0f;

            // NavMeshの更新
            if (mode == PlayState.GameMode.Play)
            {
                if (navMeshFlag)
                {
                    navMeshFlag = false;
                    UpdateNavMesh();
                }

                if (specialMove && CliffFlag == false)
                {
                    specialMove = false;
                    playerAgent.Warp(transform.position);
                    playerAgent.updatePosition = true;
                    playerRigid.isKinematic = true;
                }
            }
            else
            {
                navMeshFlag = true;
                specialMove = true;
                playerAgent.updatePosition = false;
                playerRigid.isKinematic = false;
            }

            // 一方通行の崖を利用する際に実行
            if (CliffFlag)
            {
                specialMove = true;
                playerAgent.updatePosition = false;
                input = false;
            }
            else
            {
                float delta = fixedUpdate ? Time.fixedDeltaTime : Time.deltaTime;

                // 移動方向
                Vector3 moveDirection = Vector3.zero;

                // 入力の最低許容値
                float inputMin = 0.1f;
                input = (Mathf.Abs(inputX) > inputMin || Mathf.Abs(inputZ) > inputMin) && mode == PlayState.GameMode.Play && dontInput == false;

                if (input)
                {
                    // カメラの向いている方向を取得
                    Vector3 cameraForward = Vector3.Scale(PlayerCamera.transform.forward == Vector3.up ? -PlayerCamera.transform.up : PlayerCamera.transform.forward == Vector3.down ? PlayerCamera.transform.up : PlayerCamera.transform.forward, new Vector3(1, 0, 1)).normalized;

                    // プレイヤーカメラ起点の入力方向
                    Vector3 direction = cameraForward * inputZ + PlayerCamera.transform.right * inputX;

                    // 入力方向を向く処理
                    Quaternion rot = Quaternion.LookRotation(direction, Vector3.up);
                    rot = Quaternion.Slerp(transform.rotation, rot, 7.5f * delta);
                    transform.rotation = rot;

                    // 移動方向の決定
                    float vec = Mathf.Abs(inputX) >= Mathf.Abs(inputZ) ? inputZ / inputX : inputX / inputZ;
                    vec = 1.0f / Mathf.Sqrt(1.0f + vec * vec);
                    moveDirection = direction * vec;

                    // 床にRayを飛ばして斜面の角度を取得
                    Ray ground = new Ray(new Vector3(transform.position.x, PlayerPositionY, transform.position.z), Vector3.down);
                    RaycastHit hit;
                    if (Physics.Raycast(ground, out hit, rayLength, layerMask))
                    {
                        var nomal = hit.normal;
                        Vector3 dir = moveDirection - Vector3.Dot(moveDirection, nomal) * nomal;
                        moveDirection = dir.normalized;
                    }

                    // プレイヤーの移動先の算出
                    float speed = inWater ? playerWaterSpeed : playerSpeed;
                    if (speedTime < maxSpeedTime)
                    {
                        speedTime += delta;
                    }
                    else
                    {
                        speedTime = maxSpeedTime;
                    }
                    moveDirection *= speed * delta * inputSpeed * curve.Evaluate(speedTime / maxSpeedTime);
                }
                else
                {
                    speedTime = 0;
                }

                // プレイヤーを移動させる
                if (playerNav != null && playerAgent.updatePosition)
                {
                    playerAgent.Move(moveDirection);
                }

                // プレイヤーのY座標の位置情報を更新
                PlayerPositionY = transform.position.y + playerCollider.center.y;

                // 水中フラグの設定
                if (StageWater != null)
                {
                    inWater = PlayerPositionY < StageWater.max;
                    UnderWater = PlayerPositionY + playerAgent.height * 0.5f < StageWater.max;
                }
                else
                {
                    inWater = false;
                    UnderWater = false;
                }

                // AnimationEventの設定
                if (animeEvent != null)
                {
                    if (UnderWater)
                    {
                        animeEvent.PlayerStepMode = StepMode.UnderWater;
                    }
                    else if (inWater)
                    {
                        animeEvent.PlayerStepMode = StepMode.InWater;
                    }
                    else
                    {
                        animeEvent.PlayerStepMode = StepMode.Nomal;
                    }
                    animeEvent.PlayerPosition = transform.position;
                }
            }

            // アニメーション実行
            if (playerAnimator != null)
            {
                playerAnimator.enabled = true;
                playerAnimator.SetBool("wate", input);
                playerAnimator.SetFloat("speed", inWater ? (inputSpeed * curve.Evaluate(speedTime / maxSpeedTime)) / (playerSpeed / playerWaterSpeed) : inputSpeed * curve.Evaluate(speedTime / maxSpeedTime));
            }
        }
        else
        {
            // アニメーションの停止
            if (playerAnimator != null)
            {
                if (mode == PlayState.GameMode.StartEf || mode == PlayState.GameMode.Stop)
                {
                    playerAnimator.enabled = true;
                }
                else
                {
                    playerAnimator.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// PlayStateを取得できるかチェックする
    /// </summary>
    private bool GetPlayState()
    {
        try
        {
            var state = PlayState.playState.gameMode;
            return true;
        }
        catch (System.NullReferenceException)
        {
            return false;
        }
    }

    /// <summary>
    /// 敵と接触しているときに呼び出す処理
    /// </summary>
    /// <param name="flag">条件式</param>
    public void HitEnemy(bool flag)
    {
        if (connectPlayState == false) { return; }

        if (flag)
        {
            // ゲームオーバー処理
            ContactEnemy = true;
        }
        else
        {
            // 敵と接触中は操作ができないようにする
            dontInput = true;
        }
    }

    /// <summary>
    /// NavMeshSurfaceを取得する関数
    /// </summary>
    private void BakeNavMesh()
    {
        if(playerNav == null)
        {
            if(surfacePrefab == null) 
            { 
                Debug.LogError("NavMeshSurfaceを設定してください");
                return;
            }
            else
            {
                playerNav = Instantiate(surfacePrefab, Vector3.zero, Quaternion.identity);
            }
        }
        playerNav.layerMask = navLayerMask;
        playerNav.BuildNavMesh();
        playerAgent.enabled = true;
        playerAgent.agentTypeID = playerNav.agentTypeID;
        navMeshFlag = true;
    }

    /// <summary>
    /// NavMeshのデータを更新する関数
    /// </summary>
    private void UpdateNavMesh()
    {
        if (playerNav == null) { return; }
        playerNav.layerMask = navLayerMask;
        playerNav.UpdateNavMesh(playerNav.navMeshData);
    }
}