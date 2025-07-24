using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent (typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    private SkateBoardAction _inputActions;
    private Rigidbody _rb;

    private Vector2 _slideInpt;

    private void Awake()
    {
        _inputActions = new SkateBoardAction();

        _inputActions.PlayerControls.MoveX.performed += ctx => _slideInpt = ctx.ReadValue<Vector2>();
        _inputActions.PlayerControls.MoveX.canceled += ctx => _slideInpt = Vector2.zero;


        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        
    }

}
