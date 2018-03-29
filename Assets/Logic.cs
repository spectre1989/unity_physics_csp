using UnityEngine;
using System.Collections.Generic;

public class Logic : MonoBehaviour
{
    private struct Inputs
    {
        public bool up;
        public bool down;
        public bool left;
        public bool right;
        public bool jump;
    }

    private struct InputMessage
    {
        public float delivery_time;
        public Inputs inputs;
    }

    public Transform local_player_camera_transform;
    public float player_movement_impulse;
    public float player_jump_y_threshold;
    public GameObject client_player;
    public GameObject server_player;
    public GameObject server_display_player;
    public GameObject proxy_player;
    
    private float timer;

    private Queue<InputMessage> server_inputs;

    private void Start()
    {
        this.client_player.SetActive(true);
        this.server_player.SetActive(false);

        this.timer = 0.0f;

        this.server_inputs = new Queue<InputMessage>();
    }

    private void Update()
    {
        // client update
        this.timer += Time.deltaTime;

        float t = this.timer;
        float dt = Time.fixedDeltaTime;
        while (t >= dt)
        {
            t -= dt;

            Inputs inputs;
            inputs.up = Input.GetKey(KeyCode.W);
            inputs.down = Input.GetKey(KeyCode.S);
            inputs.left = Input.GetKey(KeyCode.A);
            inputs.right = Input.GetKey(KeyCode.D);
            inputs.jump = Input.GetKey(KeyCode.Space);
            this.PrePhysicsStep(this.client_player.GetComponent<Rigidbody>(), inputs);
            Physics.Simulate(dt);

            InputMessage inputMsg;
            inputMsg.delivery_time = Time.time + 0.1f;
            inputMsg.inputs = inputs;
            this.server_inputs.Enqueue(inputMsg);
        }
        this.timer = t;

        while(this.server_inputs.Count > 0 && Time.time >= this.server_inputs.Peek().delivery_time)
        {
            InputMessage inputMsg = this.server_inputs.Dequeue();

            this.client_player.SetActive(false);
            this.server_player.SetActive(true);

            this.PrePhysicsStep(this.server_player.GetComponent<Rigidbody>(), inputMsg.inputs);
            Physics.Simulate(dt);

            this.server_display_player.transform.position = this.server_player.transform.position;
            this.server_display_player.transform.rotation = this.server_player.transform.rotation;

            this.client_player.SetActive(true);
            this.server_player.SetActive(false);
        }


    }

    private void PrePhysicsStep(Rigidbody rigidbody, Inputs inputs)
    {
        if (this.local_player_camera_transform != null)
        {
            if (inputs.up)
            {
                rigidbody.AddForce(this.local_player_camera_transform.forward * this.player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.down)
            {
                rigidbody.AddForce(-this.local_player_camera_transform.forward * this.player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.left)
            {
                rigidbody.AddForce(-this.local_player_camera_transform.right * this.player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.right)
            {
                rigidbody.AddForce(this.local_player_camera_transform.right * this.player_movement_impulse, ForceMode.Impulse);
            }
            if (rigidbody.transform.position.y <= this.player_jump_y_threshold && inputs.jump)
            {
                rigidbody.AddForce(this.local_player_camera_transform.up * this.player_movement_impulse, ForceMode.Impulse);
            }
        }
    }
}
