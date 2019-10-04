using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
        public uint start_tick_number;
        public List<Inputs> inputs;
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

    // common stuff
    public Transform local_player_camera_transform;
    public float player_movement_impulse;
    public float player_jump_y_threshold;
    public GameObject client_player;
    public GameObject smoothed_client_player;
    public GameObject server_player;
    public GameObject server_display_player;
    public GameObject proxy_player;
    public float latency = 0.1f;
    public float packet_loss_chance = 0.05f;

    // client specific
    public bool client_enable_corrections = true;
    public bool client_correction_smoothing = true;
    public bool client_send_redundant_inputs = true;
    private float client_timer;
    private uint client_tick_number;
    private uint client_last_received_state_tick;
    private const int c_client_buffer_size = 1024;
    private ClientState[] client_state_buffer; // client stores predicted moves here
    private Inputs[] client_input_buffer; // client stores predicted inputs here
    private Queue<StateMessage> client_state_msgs;
    private Vector3 client_pos_error;
    private Quaternion client_rot_error;

    // server specific
    public uint server_snapshot_rate;
    private uint server_tick_number;
    private uint server_tick_accumulator;
    private Queue<InputMessage> server_input_msgs;

    private Scene server_scene, client_scene;
    private PhysicsScene server_physics_scene, client_physics_scene;

    private void Start()
    {
        this.client_timer = 0.0f;
        this.client_tick_number = 0;
        this.client_last_received_state_tick = 0;
        this.client_state_buffer = new ClientState[c_client_buffer_size];
        this.client_input_buffer = new Inputs[c_client_buffer_size];
        this.client_state_msgs = new Queue<StateMessage>();
        this.client_pos_error = Vector3.zero;
        this.client_rot_error = Quaternion.identity;

        this.server_tick_number = 0;
        this.server_tick_accumulator = 0;
        this.server_input_msgs = new Queue<InputMessage>();

        server_scene = SceneManager.LoadScene("physics_scene", new LoadSceneParameters() { loadSceneMode = LoadSceneMode.Additive, localPhysicsMode = LocalPhysicsMode.Physics3D });
        client_scene = SceneManager.LoadScene("physics_scene", new LoadSceneParameters() { loadSceneMode = LoadSceneMode.Additive, localPhysicsMode = LocalPhysicsMode.Physics3D });

        server_physics_scene = server_scene.GetPhysicsScene();
        client_physics_scene = client_scene.GetPhysicsScene();

        SceneManager.MoveGameObjectToScene(client_player, client_scene);
        SceneManager.MoveGameObjectToScene(server_player, server_scene);
    }

    private void Update()
    {
        // client update
        Rigidbody client_rigidbody = this.client_player.GetComponent<Rigidbody>();
        float dt = Time.fixedDeltaTime;
        float client_timer = this.client_timer;
        uint client_tick_number = this.client_tick_number;

        client_timer += Time.deltaTime;
        while (client_timer >= dt)
        {
            client_timer -= dt;

            uint buffer_slot = client_tick_number % c_client_buffer_size;

            // sample and store inputs for this tick
            Inputs inputs;
            inputs.up = Input.GetKey(KeyCode.W);
            inputs.down = Input.GetKey(KeyCode.S);
            inputs.left = Input.GetKey(KeyCode.A);
            inputs.right = Input.GetKey(KeyCode.D);
            inputs.jump = Input.GetKey(KeyCode.Space);
            this.client_input_buffer[buffer_slot] = inputs;

            // store state for this tick, then use current state + input to step simulation
            this.ClientStoreCurrentStateAndStep(
                ref this.client_state_buffer[buffer_slot], 
                client_rigidbody, 
                inputs, 
                dt);

            // send input packet to server
            if (Random.value > this.packet_loss_chance)
            {
                InputMessage input_msg;
                input_msg.delivery_time = Time.time + this.latency;
                input_msg.start_tick_number = this.client_send_redundant_inputs ? this.client_last_received_state_tick : client_tick_number;
                input_msg.inputs = new List<Inputs>();

                for (uint tick = input_msg.start_tick_number; tick <= client_tick_number; ++tick)
                {
                    input_msg.inputs.Add(this.client_input_buffer[tick % c_client_buffer_size]);
                }
                this.server_input_msgs.Enqueue(input_msg);
            }

            ++client_tick_number;
        }
        
        if (this.ClientHasStateMessage())
        {
            StateMessage state_msg = this.client_state_msgs.Dequeue();
            while (this.ClientHasStateMessage()) // make sure if there are any newer state messages available, we use those instead
            {
                state_msg = this.client_state_msgs.Dequeue();
            }

            this.client_last_received_state_tick = state_msg.tick_number;

            this.proxy_player.transform.position = state_msg.position;
            this.proxy_player.transform.rotation = state_msg.rotation;

            if (this.client_enable_corrections)
            {
                uint buffer_slot = state_msg.tick_number % c_client_buffer_size;
                Vector3 position_error = state_msg.position - this.client_state_buffer[buffer_slot].position;
                float rotation_error = 1.0f - Quaternion.Dot(state_msg.rotation, this.client_state_buffer[buffer_slot].rotation);

                if (position_error.sqrMagnitude > 0.0000001f ||
                    rotation_error > 0.00001f)
                {
                    Debug.Log("Correcting for error at tick " + state_msg.tick_number + " (rewinding " + (client_tick_number - state_msg.tick_number) + " ticks)");
                    // capture the current predicted pos for smoothing
                    Vector3 prev_pos = client_rigidbody.position + this.client_pos_error;
                    Quaternion prev_rot = client_rigidbody.rotation * this.client_rot_error;

                    // rewind & replay
                    client_rigidbody.position = state_msg.position;
                    client_rigidbody.rotation = state_msg.rotation;
                    client_rigidbody.velocity = state_msg.velocity;
                    client_rigidbody.angularVelocity = state_msg.angular_velocity;

                    uint rewind_tick_number = state_msg.tick_number;
                    while (rewind_tick_number < client_tick_number)
                    {
                        buffer_slot = rewind_tick_number % c_client_buffer_size;
                        this.ClientStoreCurrentStateAndStep(
                            ref this.client_state_buffer[buffer_slot],
                            client_rigidbody,
                            this.client_input_buffer[buffer_slot],
                            dt);

                        ++rewind_tick_number;
                    }

                    // if more than 2ms apart, just snap
                    if ((prev_pos - client_rigidbody.position).sqrMagnitude >= 4.0f)
                    {
                        this.client_pos_error = Vector3.zero;
                        this.client_rot_error = Quaternion.identity;
                    }
                    else
                    {
                        this.client_pos_error = prev_pos - client_rigidbody.position;
                        this.client_rot_error = Quaternion.Inverse(client_rigidbody.rotation) * prev_rot;
                    }
                }
            }
        }

        this.client_timer = client_timer;
        this.client_tick_number = client_tick_number;

        if (this.client_correction_smoothing)
        {
            this.client_pos_error *= 0.9f;
            this.client_rot_error = Quaternion.Slerp(this.client_rot_error, Quaternion.identity, 0.1f);
        }
        else
        {
            this.client_pos_error = Vector3.zero;
            this.client_rot_error = Quaternion.identity;
        }
        
        this.smoothed_client_player.transform.position = client_rigidbody.position + this.client_pos_error;
        this.smoothed_client_player.transform.rotation = client_rigidbody.rotation * this.client_rot_error;

        // server update   

        uint server_tick_number = this.server_tick_number;
        uint server_tick_accumulator = this.server_tick_accumulator;
        Rigidbody server_rigidbody = this.server_player.GetComponent<Rigidbody>();
        
        while (this.server_input_msgs.Count > 0 && Time.time >= this.server_input_msgs.Peek().delivery_time)
        {
            InputMessage input_msg = this.server_input_msgs.Dequeue();

            // message contains an array of inputs, calculate what tick the final one is
            uint max_tick = input_msg.start_tick_number + (uint)input_msg.inputs.Count - 1;

            // if that tick is greater than or equal to the current tick we're on, then it
            // has inputs which are new
            if (max_tick >= server_tick_number)
            {
                // there may be some inputs in the array that we've already had,
                // so figure out where to start
                uint start_i = server_tick_number > input_msg.start_tick_number ? (server_tick_number - input_msg.start_tick_number) : 0;

                // run through all relevant inputs, and step player forward
                for (int i = (int)start_i; i < input_msg.inputs.Count; ++i)
                {
                    this.PrePhysicsStep(server_rigidbody, input_msg.inputs[i]);
                    server_physics_scene.Simulate(dt);

                    ++server_tick_number;
                    ++server_tick_accumulator;
                    if (server_tick_accumulator >= this.server_snapshot_rate)
                    {
                        server_tick_accumulator = 0;

                        if (Random.value > this.packet_loss_chance)
                        {
                            StateMessage state_msg;
                            state_msg.delivery_time = Time.time + this.latency;
                            state_msg.tick_number = server_tick_number;
                            state_msg.position = server_rigidbody.position;
                            state_msg.rotation = server_rigidbody.rotation;
                            state_msg.velocity = server_rigidbody.velocity;
                            state_msg.angular_velocity = server_rigidbody.angularVelocity;
                            this.client_state_msgs.Enqueue(state_msg);
                        }
                    }
                }
                
                this.server_display_player.transform.position = server_rigidbody.position;
                this.server_display_player.transform.rotation = server_rigidbody.rotation;
            }
        }
        
        this.server_tick_number = server_tick_number;
        this.server_tick_accumulator = server_tick_accumulator;
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

    private bool ClientHasStateMessage()
    {
        return this.client_state_msgs.Count > 0 && Time.time >= this.client_state_msgs.Peek().delivery_time;
    }

    private void ClientStoreCurrentStateAndStep(ref ClientState current_state, Rigidbody rigidbody, Inputs inputs, float dt)
    {
        current_state.position = rigidbody.position;
        current_state.rotation = rigidbody.rotation;

        this.PrePhysicsStep(rigidbody, inputs);
        client_physics_scene.Simulate(dt);
    }     
}
