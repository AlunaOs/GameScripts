using UnityEngine;
using System.Collections;

public class AutomaticMovingWall : MonoBehaviour
{
    public enum MovementType { OneWay, LoopBackAndForth }

    [Header("Movement Behavior")]
    [Tooltip("OneWay will move forward and stop. LoopBackAndForth will ping-pong back and forth forever.")]
    public MovementType movementType = MovementType.LoopBackAndForth;

    [Header("Distance & Speed")]
    [Tooltip("How many meters forward the wall should travel from its starting position.")]
    public float travelDistanceMeters = 5.0f;
    
    [Tooltip("Speed multiplier for the movement execution.")]
    public float moveSpeed = 2.0f;

    [Header("Delays (Seconds)")]
    [Tooltip("How long the wall waits before it starts moving after being activated by the lever.")]
    public float initialStartDelay = 0.0f;

    [Tooltip("How long the wall waits at the destination before moving back (Only applies to Loop mode).")]
    public float edgeWaitDelay = 1.5f;

    private Coroutine movementCoroutine;

    void Start()
    {
        // Wall remains static on Start until activated by LeverInteraction
    }

    /// <summary>
    /// Called by LeverInteraction once the player solves the puzzle.
    /// </summary>
    public void ActivateWallMovement()
    {
        if (movementCoroutine == null)
        {
            movementCoroutine = StartCoroutine(AutomaticMovementRoutine());
        }
    }

    private IEnumerator AutomaticMovementRoutine()
    {
        // Optional delay after lever trigger
        if (initialStartDelay > 0f)
        {
            yield return new WaitForSeconds(initialStartDelay);
        }

        Vector3 startPosition = transform.position;
        // transform.forward uses the local Z-axis (blue arrow in Unity)
        Vector3 forwardTargetPosition = startPosition + (transform.forward * travelDistanceMeters);

        if (movementType == MovementType.OneWay)
        {
            // Just move forward once and stop forever
            yield return StartCoroutine(MoveToPosition(forwardTargetPosition));
        }
        else if (movementType == MovementType.LoopBackAndForth)
        {
            // Ping-pong back and forth infinitely
            while (true)
            {
                // Move to the target forward position
                yield return StartCoroutine(MoveToPosition(forwardTargetPosition));
                yield return new WaitForSeconds(edgeWaitDelay);

                // Move back to the original starting position
                yield return StartCoroutine(MoveToPosition(startPosition));
                yield return new WaitForSeconds(edgeWaitDelay);
            }
        }
    }

    private IEnumerator MoveToPosition(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target; // Ensure perfect snap alignment
    }

    // Draws a line in the Scene View to visually plan your layout grid safely
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * travelDistanceMeters);
        Gizmos.DrawWireCube(transform.position + (transform.forward * travelDistanceMeters), transform.localScale);
    }
}