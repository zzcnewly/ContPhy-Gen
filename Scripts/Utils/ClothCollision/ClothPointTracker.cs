using System.Collections;
using System.Collections.Generic;
using ClothCollisionConfig;
using Obi;
using UnityEngine;

public class ClothPointTracker : MonoBehaviour
{
    public ObiSolver solver; //should be assigned outside the script 
    ObiCloth cloth;
    List<Transform> ls_sphere;
    List<List<int>> ls_particle_ids;
    void Start()
    {
        cloth = solver.transform.GetComponentInChildren<ObiCloth>();
        ls_sphere = new List<Transform>();
        ls_sphere.Add(solver.transform.Find("A"));
        ls_sphere.Add(solver.transform.Find("B"));
        ls_sphere.Add(solver.transform.Find("C"));
        ls_sphere.Add(solver.transform.Find("D"));
        ls_sphere.Add(solver.transform.Find("E"));
        ls_particle_ids = new List<List<int>>();
        for (int i = 0 ; i < ls_sphere.Count ; i++){
            ls_particle_ids.Add(new List<int>());
        }
        // A red    corner1 quadrant1 zuoxia 
        // B blue   corner2 quadrant2 youxia | E purple center
        // C yellow corner3 quadrant3 youshang
        // D green  corner4 quadrant4 zuoshang 
        List<string> groupNames = new List<string>(){ "Corner1", "Corner2", "Corner3", "Corner4", "Center" };
        for (int i = 0 ; i < groupNames.Count ; i++){
            var ids = cloth.blueprint.groups.Find(x => x.name == groupNames[i]).particleIndices;
            for (int j = 0 ; j < ids.Count ; j++){
                ls_particle_ids[i].Add(cloth.solverIndices[ids[j]]);
            }
        }
    }

    void Update()
    {
        for (int i = 0 ; i < ls_sphere.Count ; i++){
            Vector3 pos = new Vector3(0,0,0);   
            for (int j = 0 ; j < ls_particle_ids[i].Count ; j++){
                pos += (Vector3)(solver.renderablePositions[ls_particle_ids[i][j]]);
            }
            pos /= ls_particle_ids[i].Count;
            ls_sphere[i].localPosition = pos;
        }
    }
}
