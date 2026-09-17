using UnityEngine;

public class LeverSpawner : MonoBehaviour
{
    [Header("4 Levers Placed in the Map")]
    public GameObject[] mapLevers = new GameObject[4];

    void Start()
    {
        ShowRandomLever();
    }

    public void ShowRandomLever()
    {
        if (mapLevers.Length == 0) return;

        // Step 1: Hide ALL levers first
        foreach (GameObject lever in mapLevers)
        {
            if (lever != null)
            {
                lever.SetActive(false);
            }
        }

        // Step 2: Pick a random index
        int randomIndex = Random.Range(0, mapLevers.Length);

        // Step 3: Show ONLY the randomly selected lever
        if (mapLevers[randomIndex] != null)
        {
            mapLevers[randomIndex].SetActive(true);
        }

        Debug.Log($"Revealed lever at index: {randomIndex}");
    }
}