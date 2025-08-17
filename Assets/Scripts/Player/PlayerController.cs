using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Normal,
        Dashing
    }

    private PlayerState _state = PlayerState.Normal;
    public PlayerState CurrentState => _state; // 外部からは参照可

    [Header("移動設定")]
    [SerializeField, Header("前進力")] private float _moveForce = 5f;
    [SerializeField, Header("水平移動の力")] private float _slideForce = 5f;
    [SerializeField, Header("スロー倍率")] private float _slowMultiplier = 0.5f;
    [SerializeField, Header("速度制限")] private float _maxSpeed = 20f;

    [Header("ジャンプ設定")]
    [SerializeField, Header("ジャンプ力")] private float _jumpForce = 5f;
    [SerializeField, Header("接地判定の長さ")] private float _groundCheckDistance = 2f;
    [SerializeField, Header("判断するレイヤー")] private LayerMask _groundLayer;

    [Header("Dash設定")]
    [SerializeField, Header("加速力")] private float _dashAcceletion = 20f;
    [SerializeField, Header(("ダッシュ継続時間"))] private float _dashDuration = 2f;
    [SerializeField] private float _returnSpeed = 5f;

    [Header("壁走り設定")]
    [SerializeField, Header("壁への判定の長さ")] private float _wallCheckDistance = 3f;
    [SerializeField, Header("判断するレイヤー")] private LayerMask _wallLayer;
    [SerializeField, Header("壁の角度に回転するスピード")] private float _alignSpeed = 1f;

    private SkateBoardAction _inputActions;
    private Rigidbody _rb;
    private Vector2 _slideInput;
    private bool _isSlowing;
    private bool _jumpRequested;
    private bool _dashRequested;
    private bool _isWallRunning;
    private bool _isRightWall;
    private Vector3 _wallNormal;

    // z方向の現在速度を他スクリプトが参照できるように公開
    public float CurrentZSpeed => _rb != null ? _rb.linearVelocity.z : 0f;


    private void Awake()
    {
        //inputSystemの適用
        _inputActions = new SkateBoardAction();
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        //Vector3へ変換
        Vector3 velocity = _rb.linearVelocity;

        ForwardMovement(ref velocity);
        SlideMovement(ref velocity);
        Jump(ref velocity);
        WallRun(ref velocity);

        velocity = Vector3.ClampMagnitude(velocity, _maxSpeed);

        //最終的な反映して移動
        _rb.linearVelocity = velocity;
    }

    /// <summary>
    ///         z方向への移動
    ///         前進処理や減速処理、ダッシュの処理
    /// </summary>
    /// /// <param name="velocity"></param>
    private void ForwardMovement(ref Vector3 velocity)
    {
        if (_dashRequested && _state != PlayerState.Dashing)
        {
            _state = PlayerState.Dashing;
            _dashRequested = false;
            StartCoroutine(DashRoutine());
        }

        if (_state == PlayerState.Dashing)
        {
            velocity.z += _dashAcceletion * Time.fixedDeltaTime;
        }
        else
        {
            float moveSpeed = _isSlowing ? _moveForce * _slowMultiplier : _moveForce;
            velocity.z = Mathf.Lerp(velocity.z, moveSpeed, _returnSpeed * Time.fixedDeltaTime);
        }

        // Z方向の速度制限
        velocity.z = Mathf.Clamp(velocity.z, 0f, _maxSpeed);
    }

    /// <summary>
    ///         A,Dキーで水平移動(x方向）
    /// </summary>
    /// <param name="velocity"></param>
    private void SlideMovement(ref Vector3 velocity)
    {
        if (_slideInput.magnitude > 0.1f)  // デッドゾーン追加
        {
            Vector3 slideForce = new Vector3(_slideInput.x * _slideForce * Time.fixedDeltaTime, 0f, 0f);

            // 壁との衝突チェック
            if (IsWallInDirection(slideForce.normalized))
            {
                // 壁沿いの移動に補正
                RaycastHit hit;
                Vector3 checkDirection = slideForce.x > 0 ? transform.right : -transform.right;

                if (Physics.Raycast(transform.position, checkDirection, out hit, _wallCheckDistance, _wallLayer))
                {
                    Vector3 wallNormal = hit.normal;
                    slideForce = Vector3.ProjectOnPlane(slideForce, wallNormal);
                }
            }

            velocity += slideForce;
        }
    }

    /// <summary>
    ///         SpaceでジャンプY方向
    /// </summary>
    /// <param name="velocity"></param>
    private void Jump(ref Vector3 velocity)
    {
        //ジャンプと接地判定のフラグが立っているとき
        if (_jumpRequested && IsGrounded())
        {
            velocity.y = _jumpForce;
            _jumpRequested = false;
        }
    }

    private void WallRun(ref Vector3 velocity)
    {
        if (CheckWall(out _wallNormal) && !IsGrounded() && velocity.magnitude > 2f)  // 最低速度条件追加
        {
            if (!_isWallRunning)
            {
                _isWallRunning = true;
                _rb.useGravity = false;
            }

            // 壁沿いの進行方向を計算
            Vector3 wallForward = Vector3.Cross(_wallNormal, Vector3.up).normalized;

            // 右の壁なら進行方向を反転
            if (_isRightWall)
                wallForward *= -1f;

            // 速度を壁面に沿って投影
            velocity = Vector3.ProjectOnPlane(velocity, _wallNormal);

            // 重力の代わりに軽い下向きの力を加える
            velocity.y -= 2f * Time.fixedDeltaTime;

            // プレイヤーの向きを壁沿いに調整
            if (wallForward != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(wallForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _alignSpeed * Time.fixedDeltaTime);
            }
        }
        else
        {
            if (_isWallRunning)
            {
                _isWallRunning = false;
                _rb.useGravity = true;

                // 通常の向き補正
                Vector3 forwardDirection = new Vector3(0, 0, 1);
                Quaternion normalRotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, normalRotation, _alignSpeed * Time.fixedDeltaTime);
            }
        }
    }

    /// <summary>
    ///         Stateを戻すためのコルーチン
    /// </summary>
    /// <returns></returns>
    private IEnumerator DashRoutine()
    {
        yield return new WaitForSeconds(_dashDuration);

        _state = PlayerState.Normal;
    }

    #region 壁や地面の判定
    /// <summary>
    ///         接地判定
    /// </summary>
    /// <returns></returns>
    private bool IsGrounded()
    {
        return Physics.Raycast
            (this.transform.position, Vector3.down, _groundCheckDistance, _groundLayer);
    }

    /// <summary>
    /// 指定方向に壁があるかチェック
    /// </summary>
    private bool IsWallInDirection(Vector3 direction)
    {
        return Physics.Raycast(transform.position, direction, _wallCheckDistance, _wallLayer);
    }

    /// <summary>
    ///         壁の近さの判定
    /// </summary>
    /// <param name="wallNomal">壁の法線ベクトルを返す</param>
    /// <returns></returns>
    private bool CheckWall(out Vector3 wallNomal)
    {
        wallNomal = Vector3.zero;
        RaycastHit hit;

        if (Physics.Raycast(this.transform.position, transform.right, out hit, _wallCheckDistance, _wallLayer))
        {
            wallNomal = hit.normal;
            _isRightWall = true;
            return true;
        }

        if (Physics.Raycast(this.transform.position, -transform.right, out hit, _wallCheckDistance, _wallLayer))
        {
            wallNomal = hit.normal;
            _isRightWall = false;
            return true;
        }

        return false;
    }

    /// <summary>
    ///         デバッグ用Gizmos
    /// </summary>
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            // 接地判定の線
            Gizmos.color = IsGrounded() ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * _groundCheckDistance);

            // 壁判定の線
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.right * _wallCheckDistance);
            Gizmos.DrawLine(transform.position, transform.position - transform.right * _wallCheckDistance);

            // 現在の速度ベクトル
            Gizmos.color = Color.yellow;
            if (_rb != null)
            {
                Gizmos.DrawLine(transform.position, transform.position + _rb.linearVelocity.normalized * 2f);
            }
        }
    }
    #endregion

    #region InputActionのまとめ
    private void OnEnable()
    {
        BindInputAction(true);
        _inputActions.Enable();
    }
    private void OnDisable()
    {
        BindInputAction(false);
        _inputActions.Disable();
    }

    /// <summary>
    ///         InputActionの登録と解除
    /// </summary>
    /// <param name="subscribe"></param>
    private void BindInputAction(bool subscribe)
    {
        var pc = _inputActions.PlayerControls;

        if (subscribe)
        {
            //水平移動
            pc.MoveX.performed += OnMoveX;
            pc.MoveX.canceled += OnMoveX;

            //スピード減速
            pc.SlowDown.performed += OnSlowDown;
            pc.SlowDown.canceled += OnSlowDown;

            //ジャンプ
            pc.Jump.performed += OnJump;

            //ダッシュ
            pc.Dash.performed += OnDash;
            pc.Dash.canceled += OnDash;
        }
        else
        {
            pc.MoveX.performed -= OnMoveX;
            pc.MoveX.canceled -= OnMoveX;

            pc.SlowDown.performed -= OnSlowDown;
            pc.SlowDown.canceled -= OnSlowDown;

            pc.Jump.performed -= OnJump;

            pc.Dash.performed -= OnDash;
            pc.Dash.canceled -= OnDash;
        }
    }

    private void OnMoveX(InputAction.CallbackContext ctx)
    {
        _slideInput = ctx.canceled ? Vector2.zero : ctx.ReadValue<Vector2>();
    }

    private void OnSlowDown(InputAction.CallbackContext ctx)
    {
        _isSlowing = !ctx.canceled;
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            _jumpRequested = true;
    }

    private void OnDash(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            _dashRequested = true;
    }

    private void OnDestroy()
    {
        if (_inputActions != null)
        {
            BindInputAction(false);
            _inputActions.Dispose();
        }
    }
    #endregion
}