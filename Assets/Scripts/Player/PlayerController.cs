using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField, Header("前進力")] private float _moveForced = 5f;
    [SerializeField, Header("水平移動の力")] private float _slideforced = 5f;
    [SerializeField, Header("スロー倍率")] private float _slowDiameter = 0.5f;
    [SerializeField, Header("ジャンプ力")] private float _jumpForced = 5f;
    [SerializeField, Header("接地判定の長さ")] private float _groundCheckDistance = 2f;
    [SerializeField, Header("判断するレイヤー")] private LayerMask _groundLayer;

    private SkateBoardAction _inputActions;
    private Rigidbody _rb;

    private Vector2 _slideInput;
    private bool _isSlowing;
    private bool _jumpChecked;

    private void Awake()
    {
        _inputActions = new SkateBoardAction();

        _inputActions.PlayerControls.MoveX.performed += ctx => _slideInput = ctx.ReadValue<Vector2>();
        _inputActions.PlayerControls.MoveX.canceled += ctx => _slideInput = Vector2.zero;

        _inputActions.PlayerControls.SlowDown.performed += ctx => _isSlowing = true;
        _inputActions.PlayerControls.SlowDown.canceled += ctx => _isSlowing = false;

        _inputActions.PlayerControls.Jump.performed += ctx => _jumpChecked = true;

        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        //自動で前進(z方向)
        Vector3 velocity = _rb.linearVelocity;
        //フラグで管理、trueなら倍率を掛けて、falseなら通常移動
        float moveSpeed = _isSlowing ? _moveForced * _slowDiameter : _moveForced;
        velocity.z = moveSpeed;

        //A,Dキーで水平移動(x方向）
        velocity.x = _slideInput.x * _slideforced;

        //ジャンプと接地判定のフラグが立っているとき
        if (_jumpChecked && IsGrounded())
        {
            velocity.y = _jumpForced;
            _jumpChecked = false;
        }

        //移動！
        _rb.linearVelocity = velocity;
    }

    /// <summary>
    /// 接地判定をRaycastを使ってチェック
    /// 触れているならtrueで返す
    /// </summary>
    /// <returns></returns>
    private bool IsGrounded()
    {
        return Physics.Raycast
            (this.transform.position, Vector3.down, _groundCheckDistance, _groundLayer);
    }

    /// <summary>
    /// デバッグ用Gizmos
    /// </summary>
    private void OnDrawGizmos()
    {
        // レイの始点
        Vector3 origin = transform.position;
        // レイの方向と長さ
        Vector3 direction = Vector3.down * _groundCheckDistance;

        Gizmos.color = Color.red;
        // レイを描画
        Gizmos.DrawLine(origin, origin + direction);
    }

}
