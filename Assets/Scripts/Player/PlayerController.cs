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

    private SkateBoardAction _inputActions;
    private Rigidbody _rb;
    private Vector2 _slideInput;
    private bool _isSlowing;
    private bool _jumpChecked;
    private bool _dashRequested;

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

    /// <summary>
    ///         Stateを戻すためのコルーチン
    /// </summary>
    /// <returns></returns>
    private IEnumerator DashRoutine()
    {
        yield return new WaitForSeconds(_dashDuration);

        _state = PlayerState.Normal;
    }

    #region 接地判定
    /// <summary>
    ///         接地判定をRaycastを使ってチェック
    ///         触れているならtrueで返す
    /// </summary>
    /// <returns></returns>
    private bool IsGrounded()
    {
        return Physics.Raycast
            (this.transform.position, Vector3.down, _groundCheckDistance, _groundLayer);
    }

    /// <summary>
    ///         デバッグ用Gizmos
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, Vector3.down * _groundCheckDistance);
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