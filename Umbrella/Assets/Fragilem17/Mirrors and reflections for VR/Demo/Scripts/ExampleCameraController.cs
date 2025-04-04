using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Fragilem17.MirrorsAndPortals
{
    public class ExampleCameraController : MonoBehaviour
    {
        private Vector2 _rotation = Vector2.zero;
        public float Speed = 1;
        public float MouseSpeed = 4;
        public Transform lookTarget;
        public Transform moveTarget;

#if ENABLE_INPUT_SYSTEM
        public InputAction moveAction;
        public InputAction xrControllerAction;
        public InputAction rotateAction;

        [Tooltip("Action to toggle between free look and editor cursor")]
        public InputAction toggleMouseLookAction;


        private void OnEnable()
        {
            moveAction.Enable();
            xrControllerAction.Enable();
            rotateAction.Enable();
            toggleMouseLookAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            xrControllerAction.Disable();
            rotateAction.Disable();
            toggleMouseLookAction.Disable();
        }
#endif

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
        moveTarget.position += lookTarget.forward * Time.deltaTime * Input.GetAxis("Vertical") * Speed;
        moveTarget.position += lookTarget.right * Time.deltaTime * Input.GetAxis("Horizontal") * Speed;

        if (!Cursor.visible && Cursor.lockState == CursorLockMode.Locked)
        {
            _rotation.y = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * MouseSpeed * 0.2f;
            _rotation.x = transform.localEulerAngles.x + -Input.GetAxis("Mouse Y") * MouseSpeed * 0.2f;
            //_rotation.x = Mathf.Clamp(_rotation.x, -40f, 40f);

            // rotate z towards 0
            float deltaZ = 0 - transform.localEulerAngles.z;
            if (transform.localEulerAngles.z > 180f)
            {
                // rotate z towards 360
                deltaZ = 360 - transform.localEulerAngles.z;
            }

            transform.localEulerAngles = new Vector3(_rotation.x, _rotation.y, transform.localEulerAngles.z + (deltaZ * 0.04f));
        }

        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
#endif

#if ENABLE_INPUT_SYSTEM
            moveTarget.position += lookTarget.forward * Time.deltaTime * xrControllerAction.ReadValue<Vector2>().y * Speed;
            moveTarget.position += lookTarget.right * Time.deltaTime * xrControllerAction.ReadValue<Vector2>().x * Speed;

            moveTarget.position += lookTarget.forward * Time.deltaTime * moveAction.ReadValue<Vector3>().z * Speed;
            moveTarget.position += lookTarget.right * Time.deltaTime * moveAction.ReadValue<Vector3>().x * Speed;
            moveTarget.position += lookTarget.up * Time.deltaTime * moveAction.ReadValue<Vector3>().y * Speed;


            if (!Cursor.visible && Cursor.lockState == CursorLockMode.Locked)
            {
                _rotation.y = transform.localEulerAngles.y + rotateAction.ReadValue<Vector2>().x * Time.deltaTime * MouseSpeed;
                _rotation.x = transform.localEulerAngles.x + -rotateAction.ReadValue<Vector2>().y * Time.deltaTime * MouseSpeed;
                //_rotation.x = Mathf.Clamp(_rotation.x, -40f, 40f);

                // rotate z towards 0
                float deltaZ = 0 - transform.localEulerAngles.z;
                if (transform.localEulerAngles.z > 180f)
                {
                    // rotate z towards 360
                    deltaZ = 360 - transform.localEulerAngles.z;
                }

                transform.localEulerAngles = new Vector3(_rotation.x, _rotation.y, transform.localEulerAngles.z + (deltaZ * 0.04f));
            }

            if (toggleMouseLookAction.ReadValue<float>() > 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
#endif
        }
    }
}
