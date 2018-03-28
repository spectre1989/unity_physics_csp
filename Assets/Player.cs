using UnityEngine;

public class Player : MonoBehaviour
{
    public Transform camera_transform;
    public float movement_impulse;
    public float jump_y_threshold;

    new private Rigidbody rigidbody;

    private void FixedUpdate()
    {
        if (this.camera_transform != null)
        {
            if (this.rigidbody != null)
            {
                if (Input.GetKey(KeyCode.W))
                {
                    this.rigidbody.AddForce(this.camera_transform.forward * this.movement_impulse, ForceMode.Impulse);
                }
                if (Input.GetKey(KeyCode.S))
                {
                    this.rigidbody.AddForce(-this.camera_transform.forward * this.movement_impulse, ForceMode.Impulse);
                }
                if (Input.GetKey(KeyCode.A))
                {
                    this.rigidbody.AddForce(-this.camera_transform.right * this.movement_impulse, ForceMode.Impulse);
                }
                if (Input.GetKey(KeyCode.D))
                {
                    this.rigidbody.AddForce(this.camera_transform.right * this.movement_impulse, ForceMode.Impulse);
                }
                if (this.transform.position.y <= this.jump_y_threshold && Input.GetKey(KeyCode.Space))
                {
                    this.rigidbody.AddForce(this.camera_transform.up * this.movement_impulse, ForceMode.Impulse);
                }
            }
            else
            {
                this.rigidbody = this.GetComponent<Rigidbody>();
            }
        }
    }
}
