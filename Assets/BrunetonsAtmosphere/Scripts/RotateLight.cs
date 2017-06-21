using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrunetonsAtmosphere
{
    public class RotateLight : MonoBehaviour
    {

        public float speed = 5.0f;

        private Vector3 lastMousePos;

        private bool rotate;

        void Update()
        {
            if (Input.GetMouseButtonDown(0)) rotate = true;
            if (Input.GetMouseButtonUp(0)) rotate = false;

            Vector3 delta = lastMousePos - Input.mousePosition;

            if (rotate)
            {
                transform.Rotate(new Vector3(delta.y * Time.deltaTime * -speed, delta.x * Time.deltaTime * -speed, 0));
            }

            lastMousePos = Input.mousePosition;
        }
    }
}
