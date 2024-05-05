using System.Collections;
using ClothCollisionConfig;
using Obi;
using UnityEngine;

public class ClothMotion : MonoBehaviour
{
    public Vector3 pointA;
    public Vector3 pointB;
    public float speed = 3.0f;

    private float startTime;
    private float journeyLength;
    bool inMotion1, inMotion2;
    
    void Start()
    {
        // Record the time we started at
        startTime = Time.time;
        speed += Random.Range(0, cfg.random_speed_additional);
        inMotion1 = true; 
        inMotion2 = false;
        // Determine the distance of the journey
        pointA = transform.position;
        pointB = pointA + new Vector3(0, 0, cfg.move_z_length + Random.Range(cfg.random_z_additional_length_min, cfg.random_z_additional_length_max));
        journeyLength = Vector3.Distance(pointA, pointB);
    }

    void Update()
    {
        if (inMotion1){
            // Determine how far along the journey we are as a proportion of the total distance
            float distCovered = (Time.time - startTime) * speed;
            float fractionOfJourney = distCovered / journeyLength;
            // Move our position a fraction of the distance between the markers.
            transform.position = Vector3.Lerp(pointA, pointB, fractionOfJourney);
            if (fractionOfJourney > 1) { 
                inMotion1 = false;
                inMotion2 = true;
            }
        }
        else {
            
        }
    }
}
