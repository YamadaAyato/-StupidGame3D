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

    [Header("移動設定")] [SerializeField, Header("前進力")]
    private float _moveForced = 5f;

    [SerializeField, Header("水平移動の力")] private float _slideforced = 5f;
    [SerializeField, Header("スロー倍率")] private float _slowDiameter = 0.5f;
    [SerializeField, Header("速度制限")]　private float _maxSpeed = 20f;

    [Header("ジャンプ設定")] [SerializeField, Header("ジャンプ力")]
    private float _jumpForced = 5f;

    [SerializeField, Header("接地判定の長さ")] private float _groundCheckDistance = 2f;
    [SerializeField, Header("判断するレイヤー")] private LayerMask _groundLayer;

    [Header("Dash設定")] [SerializeField, Header("加速力")]
    private float _dashAcceretion = 20f;

    [SerializeField, Header(("ダッシュ継続時間"))]　private float _dashDuration = 2f;
    [SerializeField, Header(("ダッシュ減速率"))]　private float _dashDeceleration = 10f;

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

        //水平移動
        _inputActions.PlayerControls.MoveX.performed += ctx => _slideInput = ctx.ReadValue<Vector2>();
        _inputActions.PlayerControls.MoveX.canceled += ctx => _slideInput = Vector2.zero;

        //スピード減速
        _inputActions.PlayerControls.SlowDown.performed += ctx => _isSlowing = true;
        _inputActions.PlayerControls.SlowDown.canceled += ctx => _isSlowing = false;

        //ジャンプ
        _inputActions.PlayerControls.Jump.performed += ctx => _jumpChecked = true;

        //ダッシュ
        _inputActions.PlayerControls.Dash.performed += ctx => _dashRequested = true;
        _inputActions.PlayerControls.Dash.canceled += ctx => _dashRequested = false;
    }

    private void OnEnable() => _inputActions.Enable();
    private void OnDisable() => _inputActions.Disable();

    private void FixedUpdate()
    {
        //Vector3へ変換
        Vector3 velocity = _rb.linearVelocity;

        //Dash(ref velocity);
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


        //フラグで管理、trueなら倍率を掛けて、falseなら通常移動
        float targetZ = _isSlowing ? _moveForced * _slowDiameter : _moveForced;

        if (_state == PlayerState.Dashing)
        {
            velocity.z += _dashAcceretion * Time.fixedDeltaTime;
            velocity.z = Mathf.Min(velocity.z, _maxSpeed);
        }
        else
        {
            float moveSpeed = _isSlowing ? _moveForced * _slowDiameter : _moveForced;
            velocity.z = moveSpeed;
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

    // /// <summary>
    // ///         LeftShiftで加速(向いている方向)
    // /// </summary>
    // /// <param name="velocity"></param>
    // private void Dash(ref Vector3 velocity)
    // {
    //     if (_dashRequested && _state != PlayerState.Dashing)
    //     {
    //         _state = PlayerState.Dashing;
    //         StartCoroutine(DashRoutine());
    //     }
    //     else if (_state == PlayerState.Dashing)
    //     {
    //         velocity += transform.forward * (_dashAcceretion * Time.fixedDeltaTime);
    //     }
    // }

    /// <summary>
    ///         Stateを戻すためのコルーチン
    /// </summary>
    /// <returns></returns>
    private IEnumerator DashRoutine()
    {
        yield return new WaitForSeconds(_dashDuration);

        _state = PlayerState.Normal;
    }

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
        // レイを描画
        Gizmos.DrawLine(transform.position, Vector3.down * _groundCheckDistance);
    }
}