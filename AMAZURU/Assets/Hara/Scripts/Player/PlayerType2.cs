﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerType2 : MyAnimation
{
    [SerializeField, Tooltip("PlayerのCharacterController")] private CharacterController character = null;
    [SerializeField, Tooltip("PlayerのAnimator")] private Animator playerAnimator = null;
    [SerializeField, Tooltip("Playerの傘のAnimator")] private Animator umbrellaAnimator = null;
    [SerializeField, Tooltip("地面のLayerMask")] private LayerMask groundLayer;
    [SerializeField, Tooltip("AnimationEventスクリプト")] private PlayerAnimeEvent animeEvent = null;

    // コントローラーの入力
    private float inputX = 0;
    private float inputZ = 0;

    /// <summary>
    /// 入力操作を無効にするフラグ
    /// </summary>
    public bool DontInput { set; private get; } = false;

    [SerializeField, Header("プレイヤーの移動速度"), Range(0, 10)] private float playerSpeed = 5;
    [SerializeField, Header("プレイヤーの水中移動速度"), Range(0, 10)] private float playerWaterSpeed = 2.5f;
    [SerializeField, Header("プレイヤーの加速度グラフ")] private AnimationCurve curve = null;
    [SerializeField, Header("最高速度到達時間"), Range(0.1f, 2.0f)] private float maxSpeedTime = 0.5f;

    /// <summary>
    /// プレイヤーカメラ
    /// </summary>
    public Camera PlayerCamera { set; private get; } = null;

    /// <summary>
    /// Stageの水オブジェクト
    /// </summary>
    public WaterHi StageWater { set; private get; } = null;

    // 透明な壁関連の変数
    private Vector3[] rayPosition = new Vector3[4] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
    private BoxCollider[] hiddenWalls = null;

    // プレイヤーが坂道に立っているときのフラグ
    private bool isOnSlope = false;
    private Vector3 slopeRight = Vector3.zero;

    /// <summary>
    /// プレイヤーの水中フラグ
    /// </summary>
    public bool InWater { private set; get; } = false;

    /// <summary>
    /// プレイヤーが水没したことを検知するフラグ
    /// </summary>
    public bool UnderWater { private set; get; } = false;

    /// <summary>
    /// 一方通行の崖を検知する用のフラグ
    /// </summary>
    public bool CliffFlag { set; private get; } = false;

    /// <summary>
    /// ゲーム停止中のフラグ
    /// </summary>
    public bool IsGameStop { set; private get; } = false;

    /// <summary>
    /// アメフラシの起動フラグ
    /// </summary>
    public bool IsRain { set; private get; } = false;

    /// <summary>
    /// ゲームクリア時のフラグ
    /// </summary>
    public bool IsGameClear { set; private get; } = false;

    /// <summary>
    /// ゲームオーバー時のフラグ
    /// </summary>
    public bool IsGameOver { set; private get; } = false;

    /// <summary>
    /// 水中時のゲームオーバーフラグ
    /// </summary>
    public bool IsGameOverInWater { set; private get; } = false;

    /// <summary>
    /// エネミーとの接触フラグ
    /// </summary>
    public bool IsHitEnemy { set; get; } = false;

    /// <summary>
    /// 感電時のフラグ
    /// </summary>
    public bool IsElectric { set; private get; } = false;

    /// <summary>
    /// CharacterControllerの移動のみを無効にするフラグ(アニメーションは適用されます)
    /// </summary>
    public bool IsDontCharacterMove { set; private get; } = false;

    /// <summary>
    /// 透明壁を無効にする
    /// </summary>
    public bool IsDontShield { set; private get; } = false;

    // 風フラグ
    private bool isWind = false;

    // 風の吹き飛ばし方向
    private Vector3 windMoveDir = Vector3.zero;

    private Coroutine windCoroutine = null;

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
        if (IsGameStop == false)
        {
            // プレイヤーの移動処理
            PlayerMove(true);
        }
        else
        {
            if (playerAnimator != null)
            {
                // ポーズ中のみアニメーションを停止
                playerAnimator.enabled = false;
                if (umbrellaAnimator != null) { umbrellaAnimator.enabled = false; }
            }
        }
    }

    private void Reset()
    {
        character = GetComponent<CharacterController>();
    }

    /// <summary>
    /// 初期化
    /// </summary>
    public void PlayerInit()
    {
        if (PlayerCamera == null) { PlayerCamera = Camera.main; }
        CreateHiddenWall();
    }

    /// <summary>
    /// コントローラ入力を取得
    /// </summary>
    private void GetInputController()
    {
        // キー入力取得
        try
        {
            inputX = ControllerInput.Instance.stick.LStickHorizontal;
            inputZ = ControllerInput.Instance.stick.LStickVertical;
        }
        catch (System.NullReferenceException)
        {
            inputX = Input.GetAxis("Horizontal");
            inputZ = Input.GetAxis("Vertical");
        }
    }

    /// <summary>
    /// プレイヤーの移動処理
    /// </summary>
    private void PlayerMove(bool fixedUpdate)
    {
        // カメラの向いている方向を取得
        Vector3 cameraForward = Vector3.Scale(PlayerCamera.transform.forward == Vector3.up ? -PlayerCamera.transform.up : PlayerCamera.transform.forward == Vector3.down ? PlayerCamera.transform.up : PlayerCamera.transform.forward, new Vector3(1, 0, 1)).normalized;

        // カメラから見た入力方向を取得
        Vector3 direction = cameraForward * inputZ + PlayerCamera.transform.right * inputX;

        float delta = fixedUpdate ? Time.fixedDeltaTime : Time.deltaTime;

        bool input = false;
        float inputSpeed = Mathf.Sqrt((inputX * inputX) + (inputZ * inputZ));

        if (IsDontCharacterMove || CliffFlag)
        {
            DontHiddenWall();
        }
        else
        {
            // 移動方向
            Vector3 moveDirection;

            if (isWind)
            {
                moveDirection = windMoveDir;
            }
            else
            {
                moveDirection = Vector3.zero;
                windMoveDir = Vector3.zero;
            }

            // 入力の最低許容値
            float inputMin = 0.1f;

            // 入力を検知したかチェック
            input = (Mathf.Abs(inputX) > inputMin || Mathf.Abs(inputZ) > inputMin) && DontInput == false && isWind == false;

            if (input)
            {
                // 入力方向を向く処理
                Quaternion rot = Quaternion.LookRotation(direction, Vector3.up);
                rot = Quaternion.Slerp(transform.rotation, rot, 7.5f * delta);
                transform.rotation = rot;

                // 水中かどうかをチェックし、加速度グラフに基づいた移動速度を計算
                float speed = InWater ? playerWaterSpeed : playerSpeed;
                if (speedTime < maxSpeedTime)
                {
                    speedTime += delta;
                }
                else
                {
                    speedTime = maxSpeedTime;
                }

                // 地面にRayを飛ばす
                Ray ground = new Ray(new Vector3(transform.position.x, transform.position.y + character.center.y, transform.position.z), Vector3.down);
                float hitNomalY = 1.0f;
                slopeRight = Vector3.zero;
                if (Physics.Raycast(ground, out RaycastHit hit, character.height, groundLayer))
                {
                    // 地面の傾斜を取得
                    hitNomalY = hit.normal.y;
                    isOnSlope = hitNomalY < 1.0f;

                    if (isOnSlope)
                    {
                        slopeRight = hit.transform.right;
                        slopeRight.x = Mathf.Floor(Mathf.Abs(slopeRight.x) * 10) != 0 ? 1 : 0;
                        slopeRight.y = 0;
                        slopeRight.z = Mathf.Floor(Mathf.Abs(slopeRight.z) * 10) != 0 ? 1 : 0;
                    }
                }
                else
                {
                    isOnSlope = false;
                }

                // 斜め入力時の移動量を修正
                moveDirection = direction.normalized;

                // 坂を移動する際の傾斜を考慮した移動量に修正
                if (hitNomalY != 1.0f)
                {
                    var nomal = hit.normal;
                    Vector3 dir = moveDirection - Vector3.Dot(moveDirection, nomal) * nomal;
                    moveDirection = dir.normalized;
                }

                // 移動量にスピード値を乗算
                moveDirection *= speed * inputSpeed * curve.Evaluate(speedTime / maxSpeedTime);
            }
            else
            {
                speedTime = 0;
            }

            // 重力を反映
            moveDirection.y -= 10.0f;

            // 実際にキャラクターを動かす
            character.Move(moveDirection * delta);

            // 透明な壁の設置
            if(IsDontShield == false)
            {
                SetHiddenWall();
            }
            else
            {
                DontHiddenWall();
            }

            // 水中フラグの設定
            if (StageWater != null)
            {
                InWater = transform.position.y + character.center.y - character.height * 0.25f < StageWater.max;
                UnderWater = transform.position.y + character.center.y + character.height * 0.25f < StageWater.max;
            }
            else
            {
                InWater = false;
                UnderWater = false;
            }

            // AnimationEventの設定
            if (animeEvent != null)
            {
                if (UnderWater)
                {
                    animeEvent.PlayerStepMode = StepMode.UnderWater;
                }
                else if (InWater)
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
            if (umbrellaAnimator != null) { umbrellaAnimator.enabled = true; }

            // 走るアニメーション
            playerAnimator.SetBool("Run", input);
            playerAnimator.SetFloat("Speed", InWater ? (inputSpeed * curve.Evaluate(speedTime / maxSpeedTime)) / (playerSpeed / playerWaterSpeed) : inputSpeed * curve.Evaluate(speedTime / maxSpeedTime));

            // アメフラシを起動するアニメーション
            playerAnimator.SetBool("Switch", IsRain);

            // 崖から降りるアニメーション
            playerAnimator.SetBool("Jump", CliffFlag);

            // ゲームオーバー時のアニメーション
            playerAnimator.SetBool("GameOver", IsGameOver);

            // 水中時のゲームオーバーアニメーション
            playerAnimator.SetBool("GameOverInWater", IsGameOverInWater);

            // クリア時のアニメーションを再生
            if (IsGameClear)
            {
                if (RotateAnimation(transform.gameObject, cameraForward * -1, 360 * delta, true))
                {
                    playerAnimator.SetBool("Run", false);
                    playerAnimator.SetBool("StageClear", true);
                }
            }
        }
    }

    /// <summary>
    /// 透明な壁を生成
    /// </summary>
    private void CreateHiddenWall()
    {
        hiddenWalls = new BoxCollider[rayPosition.Length];
        for(int i = 0; i < hiddenWalls.Length; i++)
        {
            GameObject colObj = new GameObject();
            hiddenWalls[i] = colObj.AddComponent<BoxCollider>();
            hiddenWalls[i].gameObject.name = "WallObject" + i.ToString();
            hiddenWalls[i].size = Vector3.one;
            hiddenWalls[i].enabled = false;
        }
    }

    /// <summary>
    /// 透明な壁を設置
    /// </summary>
    private void SetHiddenWall()
    {
        for (int i = 0; i < hiddenWalls.Length; i++)
        {
            // 床があるかチェック
            Ray mainRay;
            RaycastHit hit;
            bool set;
            float rayRange = isOnSlope && (rayPosition[i] == slopeRight || rayPosition[i] == -slopeRight) ? character.height * 0.6f : character.height;
            Vector3 baseRayPosition = new Vector3(transform.position.x, transform.position.y + character.center.y, transform.position.z) + rayPosition[i] * character.radius;
            mainRay = new Ray(baseRayPosition, Vector3.down);
            if(Physics.Raycast(mainRay, out hit, rayRange, groundLayer) && hit.collider.isTrigger == false)
            {
                float hitDistance = hit.distance;
                // プレイヤーの当たり判定の両端からRayを飛ばして進めるかをチェック
                bool isHitSlope = Mathf.Abs(hit.normal.y) < 1;
                Ray subRay;
                int count = 0;
                bool rayFlag = false;
                for (int j = 0; j < 2; j++)
                {
                    subRay = new Ray(mainRay.origin + Vector3.down * character.height * (isOnSlope ? 0.6f : 0.475f) + rayPosition[i + 1 < rayPosition.Length ? i + 1 : 0] * character.radius * (j == 0 ? 1 : -1), rayPosition[i]);
                    if (Physics.Raycast(subRay, out hit, character.radius * 1.05f, groundLayer) && hit.collider.isTrigger == false)
                    {
                        if (isOnSlope)
                        {
                            if(hit.normal.y == 0)
                            {
                                count++;
                            }
                        }
                        else
                        {
                            if (hit.normal.y != 0)
                            {
                                count++;
                                rayFlag = true;
                                break;
                            }
                        }
                    }
                }

                bool isSetSlopeWall = isOnSlope && count == 2;

                if (rayFlag || isHitSlope)
                {
                    count = 0;
                    for (int j = 0; j < 2; j++)
                    {
                        subRay = new Ray(mainRay.origin + rayPosition[i + 1 < rayPosition.Length ? i + 1 : 0] * character.radius * (j == 0 ? 1 : -1), mainRay.direction);
                        if (Physics.Raycast(subRay, out hit, character.height, groundLayer) && hit.collider.isTrigger == false)
                        {
                            float disA = Mathf.Ceil(Mathf.Floor(hit.distance * 1000) / 10);
                            float disB = Mathf.Ceil(Mathf.Floor(hitDistance * 1000) / 10);
                            
                            if(disA != disB)
                            {
                                count++;
                                break;
                            }
                        }
                    }

                    if (isOnSlope)
                    {
                        set = count > 0 && isSetSlopeWall;
                    }
                    else
                    {
                        set = count > 0;
                    }
                }
                else
                {
                    set = isOnSlope ? isSetSlopeWall : count > 0;
                }
            }
            else
            {
                set = true;
            }

            // 床が無ければ透明な壁を有効化する
            if (set && hiddenWalls[i].enabled == false)
            {
                hiddenWalls[i].transform.position = mainRay.origin + rayPosition[i] * 0.5001f;
                hiddenWalls[i].enabled = true;
            }
            
            if(set && hiddenWalls[i].enabled)
            {
                // 透明な壁をプレイヤーの移動に合わせて移動させる
                if (i % 2 == 0)
                {
                    hiddenWalls[i].transform.position = new Vector3(transform.position.x, transform.position.y + character.center.y, hiddenWalls[i].transform.position.z);
                }
                else
                {
                    hiddenWalls[i].transform.position = new Vector3(hiddenWalls[i].transform.position.x, transform.position.y + character.center.y, transform.position.z);
                }
            }
            else
            {
                // 透明な壁を無効化する
                hiddenWalls[i].enabled = false;
            }
        }
    }

    /// <summary>
    /// 風の効果を適用している間の移動コルーチン
    /// </summary>
    /// <param name="direction">移動方向及び移動速度</param>
    /// <param name="duration">移動時間</param>
    /// <returns></returns>
    private IEnumerator WindActionCoroutine(Vector3 direction, float duration)
    {
        isWind = true;
        windMoveDir = direction;

        float time = 0;
        while (time < duration)
        {
            if(IsGameStop == false)
            {
                if (IsGameStop == false)
                {
                    time += Time.deltaTime;
                }

                if (IsGameClear || IsGameOver || IsHitEnemy)
                {
                    isWind = false;
                    yield break;
                }

                transform.Rotate(new Vector3(0, 15, 0));
            }
            yield return null;
        }
        isWind = false;
        windCoroutine = null;
    }

    /// <summary>
    /// 風の効果を適用している間の移動処理
    /// </summary>
    /// <param name="direction">移動方向及び移動速度</param>
    /// <param name="duration">移動時間</param>
    public void WindAction(Vector3 direction, float duration)
    {
        if(windCoroutine != null)
        {
            StopCoroutine(windCoroutine);
        }
        windCoroutine = StartCoroutine(WindActionCoroutine(direction, duration));
    }

    /// <summary>
    /// 透明壁を無効にする処理
    /// </summary>
    private void DontHiddenWall()
    {
        foreach (var wall in hiddenWalls)
        {
            wall.enabled = false;
        }
    }
}