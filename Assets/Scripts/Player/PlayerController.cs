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
    [SerializeField, Header("前進力")] private float _moveForced = 5f;
    [SerializeField, Header("水平移動の力")] private float _slideforced = 5f;
    [SerializeField, Header("スロー倍率")] private float _slowDiameter = 0.5f;
    [SerializeField, Header("速度制限")] private float _maxSpeed = 20f;

    [Header("ジャンプ設定")]
    [SerializeField, Header("ジャンプ力")] private float _jumpForced = 5f;
    [SerializeField, Header("接地判定の長さ")] private float _groundCheckDistance = 2f;
    [SerializeField, Header("判断するレイヤー")] private LayerMask _groundLayer;

    [Header("Dash設定")]
    [SerializeField, Header("加速力")] private float _dashAcceretion = 20f;
    [SerializeField, Header(("ダッシュ継続時間"))] private float _dashDuration = 2f;
    [SerializeField] private float _returnSpeed = 5f;

    [Header("壁走り設定")]
    [SerializeField, Header("壁への判定の長さ")] private float _wallCheckDistance = 3f;
    [SerializeField, Header("判断するレイヤー")] private LayerMask _wallLayer;
    [SerializeField, Header("壁への重力")] private float _wallRunGravity = 10f;
    [SerializeField, Header("壁の角度に回転するスピード")] private float _alignSpeed = 1f;
    [SerializeField, Header("重力")] private float _gravityScale = 1f;

    private SkateBoardAction _inputActions;
    private Rigidbody _rb;
    private Vector2 _slideInput;
    private bool _isSlowing;
    private bool _jumpChecked;
    private bool _dashRequested;
    private bool _isWallRunning;
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
            StartCoroutine(DashRoutine());
        }

        if (_state == PlayerState.Dashing)
        {
            velocity.z += _dashAcceretion * Time.fixedDeltaTime;
            velocity.z = Mathf.Min(velocity.z, _maxSpeed);
        }
        else
        {
            float moveSpeed = _isSlowing ? _moveForced * _slowDiameter : _moveForced;
            velocity.z = Mathf.Lerp(velocity.z, moveSpeed, _returnSpeed * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    ///         A,Dキーで水平移動(x方向）
    /// </summary>
    /// <param name="velocity"></param>
    private void SlideMovement(ref Vector3 velocity)
    {
        velocity.x = _slideInput.x * _slideforced;
    }

    /// <summary>
    ///         SpaceでジャンプY方向
    /// </summary>
    /// <param name="velocity"></param>
    private void Jump(ref Vector3 velocity)
    {
        //ジャンプと接地判定のフラグが立っているとき
        if (_jumpChecked && IsGrounded())
        {
            velocity.y = _jumpForced;
            _jumpChecked = false;
        }
    }

    private void WallRun(ref Vector3 velocity)
    {
        if (CheckWall(out _wallNormal) && !IsGrounded())
        {
            _isWallRunning = true;

            // 壁に沿った重力方向（落下方向の修正）
            Vector3 wallDown = Vector3.Cross(transform.forward, _wallNormal);
            wallDown = Vector3.Cross(_wallNormal, wallDown).normalized;
            velocity += wallDown * _wallRunGravity * Time.fixedDeltaTime;

            // 壁の法線方向を正面に向けて回転補正（傾き含む）
            Quaternion targetRotation = Quaternion.LookRotation(-_wallNormal, -wallDown);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, _alignSpeed * Time.fixedDeltaTime);

            // 壁に沿った移動方向を計算（velocityのXZだけ）
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);

            // 壁法線を除いたベクトル（壁に平行）
            Vector3 wallParallel = Vector3.ProjectOnPlane(horizontalVelocity, _wallNormal).normalized;

            // 壁沿いに速度ベクトルを修正（もとの速度の大きさを維持）
            float speed = horizontalVelocity.magnitude;
            Vector3 newHorizontalVelocity = wallParallel * speed;

            velocity.x = newHorizontalVelocity.x;
            velocity.z = newHorizontalVelocity.z;
        }
        else
        {
            _isWallRunning = false;

            // 通常重力
            velocity += Vector3.down * _gravityScale * Time.fixedDeltaTime;

            // 元の姿勢に戻す回転補間
            Quaternion usuallyRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, usuallyRotation, _alignSpeed * Time.fixedDeltaTime);
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
            return true;
        }

        if (Physics.Raycast(this.transform.position, -transform.right, out hit, _wallCheckDistance, _wallLayer))
        {
            wallNomal = hit.normal;
            return true;
        }

        return false;
    }

    /// <summary>
    ///         デバッグ用Gizmos
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(this.transform.position, Vector3.down * _groundCheckDistance);
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
            pc.SlowDown.performed += OnslownDown;
            pc.SlowDown.canceled += OnslownDown;

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

            pc.SlowDown.performed -= OnslownDown;
            pc.SlowDown.canceled -= OnslownDown;

            pc.Jump.performed -= OnJump;

            pc.Dash.performed -= OnDash;
            pc.Dash.canceled -= OnDash;
        }
    }

    private void OnMoveX(InputAction.CallbackContext ctx)
        => _slideInput = ctx.canceled ? Vector2.zero : ctx.ReadValue<Vector2>();

    private void OnslownDown(InputAction.CallbackContext ctx)
        => _isSlowing = !ctx.canceled;

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) _jumpChecked = true;
    }

    private void OnDash(InputAction.CallbackContext ctx)
        => _dashRequested = !ctx.canceled;

    /// <summary>
    ///         いらんけど学び用
    /// </summary>
    private void OnDestroy()
    {
        BindInputAction(false);
    }
    #endregion
}