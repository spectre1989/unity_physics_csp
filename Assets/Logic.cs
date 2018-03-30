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
        public uint tick_number;
        public Inputs inputs;
    }

    private struct ClientState
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    private struct StateMessage
    {
        public float delivery_time;
        public uint tick_number;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angular_velocity;
    }

    public Transform local_player_camera_transform;
    public float player_movement_impulse;
    public float player_jump_y_threshold;
    public GameObject client_player;
    public GameObject server_player;
    public GameObject server_display_player;
    public GameObject proxy_player;
    public const float c_latency = 0.1f; // todo(jbr) make a variable to tweak
    
    private float client_timer;
    private uint client_tick_number;
    private const int c_client_buffer_size = 1024;
    private ClientState[] client_state_buffer; // client stores predicted moves here
    private Inputs[] client_input_buffer; // client stores predicted inputs here
    private Queue<StateMessage> client_state_msgs;

    private Queue<InputMessage> server_input_msgs;

    private void Start()
    {
        this.client_timer = 0.0f;
        this.client_tick_number = 0;
        this.client_state_buffer = new ClientState[c_client_buffer_size];
        this.client_input_buffer = new Inputs[c_client_buffer_size];
        this.client_state_msgs = new Queue<StateMessage>();

        this.server_input_msgs = new Queue<InputMessage>();
    }

    private void Update()
    {
        // client update

        // enable client player, disable server player
        this.server_player.SetActive(false);
        this.client_player.SetActive(true);

        float dt = Time.fixedDeltaTime;
        float client_timer = this.client_timer;
        uint client_tick_number = this.client_tick_number;

        client_timer += Time.deltaTime;
        while (client_timer >= dt)
        {
            client_timer -= dt;

            Inputs inputs;
            inputs.up = Input.GetKey(KeyCode.W);
            inputs.down = Input.GetKey(KeyCode.S);
            inputs.left = Input.GetKey(KeyCode.A);
            inputs.right = Input.GetKey(KeyCode.D);
            inputs.jump = Input.GetKey(KeyCode.Space);

            Rigidbody rigidbody = this.client_player.GetComponent<Rigidbody>();

            uint buffer_slot = client_tick_number % c_client_buffer_size;
            this.client_state_buffer[buffer_slot].position = rigidbody.position;
            this.client_state_buffer[buffer_slot].rotation = rigidbody.rotation;
            this.client_input_buffer[buffer_slot] = inputs;

            this.PrePhysicsStep(rigidbody, inputs);
            Physics.Simulate(dt);

            InputMessage input_msg;
            input_msg.delivery_time = Time.time + c_latency;
            input_msg.tick_number = client_tick_number;
            input_msg.inputs = inputs;
            this.server_input_msgs.Enqueue(input_msg);

            ++client_tick_number;
        }

        if (this.client_state_msgs.Count > 0 && Time.time >= this.client_state_msgs.Peek().delivery_time)
        {
            StateMessage state_msg = this.client_state_msgs.Dequeue();
            while (this.client_state_msgs.Count > 0 && Time.time >= this.client_state_msgs.Peek().delivery_time) // todo(jbr) compression
            {
                state_msg = this.client_state_msgs.Dequeue();
            }

            this.proxy_player.transform.position = state_msg.position;
            this.proxy_player.transform.rotation = state_msg.rotation;

            uint buffer_slot = state_msg.tick_number % c_client_buffer_size;
            if ((state_msg.position - this.client_state_buffer[buffer_slot].position).sqrMagnitude > 0.01f ||
                Quaternion.Dot(state_msg.rotation, this.client_state_buffer[buffer_slot].rotation) < 0.99f) // todo(jbr) put these in consts
            {
                Debug.Log("Correction! " + buffer_slot.ToString());
                Rigidbody rigidbody = this.client_player.GetComponent<Rigidbody>();
                rigidbody.position = state_msg.position;
                rigidbody.rotation = state_msg.rotation;
                rigidbody.velocity = state_msg.velocity;
                rigidbody.angularVelocity = state_msg.angular_velocity;

                uint rewind_tick_number = state_msg.tick_number;
                while (rewind_tick_number < client_tick_number)
                {
                    buffer_slot = rewind_tick_number % c_client_buffer_size; // todo(jbr) compression
                    this.client_state_buffer[buffer_slot].position = rigidbody.position;
                    this.client_state_buffer[buffer_slot].rotation = rigidbody.rotation;

                    this.PrePhysicsStep(rigidbody, this.client_input_buffer[buffer_slot]);
                    Physics.Simulate(dt);

                    ++rewind_tick_number;
                }
            }
        }

        this.client_timer = client_timer;
        this.client_tick_number = client_tick_number;

        // server update

        // enable server player, disable client player
        this.client_player.SetActive(false);
        this.server_player.SetActive(true);

        while (this.server_input_msgs.Count > 0 && Time.time >= this.server_input_msgs.Peek().delivery_time)
        {
            InputMessage inputMsg = this.server_input_msgs.Dequeue();

            Rigidbody rigidbody = this.server_player.GetComponent<Rigidbody>();
            this.PrePhysicsStep(rigidbody, inputMsg.inputs);
            Physics.Simulate(dt);

            StateMessage state_msg;
            state_msg.delivery_time = Time.time + c_latency;
            state_msg.tick_number = inputMsg.tick_number + 1;
            state_msg.position = rigidbody.position;
            state_msg.rotation = rigidbody.rotation;
            state_msg.velocity = rigidbody.velocity;
            state_msg.angular_velocity = rigidbody.angularVelocity;
            this.client_state_msgs.Enqueue(state_msg);

            this.server_display_player.transform.position = rigidbody.position;
            this.server_display_player.transform.rotation = rigidbody.rotation;
        }

        // finally, we're viewing the client, so enable the client player, disable server again
        this.server_player.SetActive(false);
        this.client_player.SetActive(true);
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
