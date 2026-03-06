using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class CharacterController : MonoBehaviour
{
    Rigidbody rb; //Rigidbody for the GameObject
    Animator animator; //Animator for the character

    public float moveForce = 10f; //the force we add to a GameObject
    public float turnSpeed = 10f; //how fast the character turns

    public Key keyForward = Key.W;
    public Key keyBackward = Key.S;
    public Key keyLeft = Key.A;
    public Key keyRight = Key.D;

    public GameObject explodePrefab; //Prefab to spawn when the character dies

    Keyboard keyboard = Keyboard.current; //get the keyboard input

    void Start()
    {
        rb = GetComponent<Rigidbody>(); //get Rigidbody
        animator = GetComponent<Animator>(); //get Animator
    }

    void FixedUpdate()
    {
        Vector3 dir = Vector3.zero; //movement direction

        if (keyboard[keyForward].isPressed)
            dir += Vector3.forward;

        if (keyboard[keyBackward].isPressed)
            dir += Vector3.back;

        if (keyboard[keyLeft].isPressed)
            dir += Vector3.left;

        if (keyboard[keyRight].isPressed)
            dir += Vector3.right;

        if (dir != Vector3.zero)
        {
            dir.Normalize();

            //add force to move the character
            rb.AddForce(dir * moveForce);

            //rotate the character to face the movement direction
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            rb.MoveRotation(
                Quaternion.Slerp(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime)
            );
        }

        UpdateAnim();
    }

    void UpdateAnim()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0; //ignore vertical speed

        //use real physical speed to drive animation
        animator.SetFloat("Speed", velocity.magnitude);
    }

    //trigger detection for death
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet")) //check if collided with a bullet
        {
            Die();
        }
    }

    void Die()
    {
        //spawn explosion prefab at the character's position
        if (explodePrefab != null)
            Instantiate(explodePrefab, transform.position, Quaternion.identity);

        //destroy the character GameObject
        Destroy(gameObject);
        GameManager gm = GameManager.FindFirstObjectByType<GameManager>();
        if (gm != null)
            gm.GameOver();
    }
}
